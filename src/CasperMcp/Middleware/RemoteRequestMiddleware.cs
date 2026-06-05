using System.Net;
using CasperMcp.Configuration;
using CasperMcp.Remote;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

namespace CasperMcp.Middleware;

/// <summary>
/// For remote (http) mode: requires the per-agent CSPR key header and validates the optional
/// network header, before any tool dispatch. Health/readiness probes bypass validation.
/// On the MCP endpoint the rejection is shaped as a JSON-RPC 2.0 error so clients surface the
/// real reason instead of crashing on an unexpected body; other paths keep the plain shape.
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

        // Resolve the configured MCP path (falls back to "/mcp" when no DI container is present,
        // e.g. in direct-construction unit tests) to decide whether to speak JSON-RPC.
        var mcpPath = context.RequestServices?.GetService<ServerConfig>()?.McpPath ?? "/mcp";
        var isMcp = path.StartsWithSegments(mcpPath);

        if (!RemoteHeaders.TryGetCsprKey(context.Request.Headers, out _))
        {
            await Fail(context, isMcp, HttpStatusCode.Unauthorized, JsonRpc.InvalidRequest,
                $"Missing {RemoteHeaders.CsprKeyHeader} header. Set the CSPR_CLOUD_API_KEY environment variable to your CSPR.cloud API key, configure your MCP client to send it as the {RemoteHeaders.CsprKeyHeader} header, then restart the client so it can pick up the new environment variable. See setup instructions: https://docs.cspr.cloud/agentic-tools/mcp-server");
            return;
        }

        // Fallback value is irrelevant here: this call only validates the header when present.
        // The effective per-request network is resolved from ServerConfig.DefaultNetwork in Program.cs.
        if (!RemoteHeaders.TryResolveNetwork(context.Request.Headers, "mainnet", out _))
        {
            await Fail(context, isMcp, HttpStatusCode.BadRequest, JsonRpc.InvalidParams,
                $"Invalid {RemoteHeaders.NetworkHeader}; expected 'mainnet' or 'testnet'.");
            return;
        }

        await _next(context);
    }

    private static async Task Fail(HttpContext context, bool isMcp, HttpStatusCode status, int rpcCode, string message)
    {
        if (isMcp)
        {
            var id = await JsonRpc.TryReadRpcId(context.Request);
            await JsonRpc.WriteError(context, (int)status, rpcCode, message, id);
        }
        else
        {
            await WritePlainError(context, status, message);
        }
    }

    private static async Task WritePlainError(HttpContext context, HttpStatusCode status, string message)
    {
        context.Response.StatusCode = (int)status;
        context.Response.ContentType = "application/json";
        await context.Response.WriteAsync($"{{\"error\":\"{message}\"}}");
    }
}
