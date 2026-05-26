using System.Net;
using CasperMcp.Remote;
using Microsoft.AspNetCore.Http;

namespace CasperMcp.Middleware;

/// <summary>
/// For remote (http) mode: requires the per-agent CSPR key header and validates the optional
/// network header, before any tool dispatch. Health/readiness probes bypass validation.
/// </summary>
public class RemoteRequestMiddleware
{
    private readonly RequestDelegate _next;

    public RemoteRequestMiddleware(RequestDelegate next) => _next = next;

    public async Task InvokeAsync(HttpContext context)
    {
        var path = context.Request.Path;
        // Probes and unauthenticated OAuth discovery must bypass the per-agent key requirement.
        if (path.StartsWithSegments("/health")
            || path.StartsWithSegments("/ready")
            || path.StartsWithSegments("/.well-known"))
        {
            await _next(context);
            return;
        }

        if (!RemoteHeaders.TryGetCsprKey(context.Request.Headers, out _))
        {
            await WriteError(context, HttpStatusCode.Unauthorized,
                $"Missing required {RemoteHeaders.CsprKeyHeader} header.");
            return;
        }

        // Fallback value is irrelevant here: this call only validates the header when present.
        // The effective per-request network is resolved from ServerConfig.DefaultNetwork in Program.cs.
        if (!RemoteHeaders.TryResolveNetwork(context.Request.Headers, "mainnet", out _))
        {
            await WriteError(context, HttpStatusCode.BadRequest,
                $"Invalid {RemoteHeaders.NetworkHeader}; expected 'mainnet' or 'testnet'.");
            return;
        }

        await _next(context);
    }

    private static async Task WriteError(HttpContext context, HttpStatusCode status, string message)
    {
        context.Response.StatusCode = (int)status;
        context.Response.ContentType = "application/json";
        await context.Response.WriteAsync($"{{\"error\":\"{message}\"}}");
    }
}
