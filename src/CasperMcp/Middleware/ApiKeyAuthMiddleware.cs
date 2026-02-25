using System.Net;
using Microsoft.AspNetCore.Http;

namespace CasperMcp.Middleware;

public class ApiKeyAuthMiddleware
{
    private const string ApiKeyHeaderName = "X-API-Key";
    private const string ApiKeyQueryParam = "api_key";

    private readonly RequestDelegate _next;
    private readonly string _expectedApiKey;

    public ApiKeyAuthMiddleware(RequestDelegate next, string expectedApiKey)
    {
        _next = next;
        _expectedApiKey = expectedApiKey;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        if (context.Request.Path.StartsWithSegments("/health"))
        {
            await _next(context);
            return;
        }

        string? apiKey = context.Request.Headers[ApiKeyHeaderName].FirstOrDefault()
                         ?? context.Request.Query[ApiKeyQueryParam].FirstOrDefault();

        if (string.IsNullOrEmpty(apiKey) || apiKey != _expectedApiKey)
        {
            context.Response.StatusCode = (int)HttpStatusCode.Unauthorized;
            context.Response.ContentType = "application/json";
            await context.Response.WriteAsync("""{"error":"Unauthorized. Provide a valid API key via X-API-Key header or api_key query parameter."}""");
            return;
        }

        await _next(context);
    }
}
