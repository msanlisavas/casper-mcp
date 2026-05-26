using System.Net;
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Http;

namespace CasperMcp.Middleware;

/// <summary>
/// Shared-secret auth for `--auth-mode apikey`. Accepts the secret via
/// `Authorization: Bearer &lt;secret&gt;` or `X-API-Key`. Distinct from the per-agent CSPR key.
/// </summary>
public class ApiKeyAuthMiddleware
{
    private const string ApiKeyHeaderName = "X-API-Key";

    private readonly RequestDelegate _next;
    private readonly byte[] _expected;

    public ApiKeyAuthMiddleware(RequestDelegate next, string expectedApiKey)
    {
        _next = next;
        _expected = Encoding.UTF8.GetBytes(expectedApiKey);
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var path = context.Request.Path;
        if (path.StartsWithSegments("/health") || path.StartsWithSegments("/ready"))
        {
            await _next(context);
            return;
        }

        var presented = ExtractKey(context.Request);
        if (presented is null || !FixedTimeEquals(presented))
        {
            context.Response.StatusCode = (int)HttpStatusCode.Unauthorized;
            context.Response.ContentType = "application/json";
            await context.Response.WriteAsync("""{"error":"Unauthorized."}""");
            return;
        }

        await _next(context);
    }

    private static string? ExtractKey(HttpRequest request)
    {
        var auth = request.Headers.Authorization.ToString();
        if (auth.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
            return auth["Bearer ".Length..].Trim();

        var header = request.Headers[ApiKeyHeaderName].ToString();
        return string.IsNullOrEmpty(header) ? null : header;
    }

    private bool FixedTimeEquals(string presented) =>
        CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(presented), _expected);
}
