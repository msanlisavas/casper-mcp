using System.Diagnostics;
using CasperMcp.Observability;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;

namespace CasperMcp.Remote;

/// <summary>
/// Central filter around every MCP tool call. It is the single place that:
/// <list type="bullet">
/// <item>maps exceptions to safe, agent-facing messages and returns an <c>IsError</c> result;</item>
/// <item>emits a per-call span + metrics (duration, count by tool and status);</item>
/// <item>logs a structured, redacted line (tool, status, duration, tenant fingerprint, correlation id).</item>
/// </list>
/// Tools no longer catch their own exceptions — they propagate here, so this filter sees every
/// failure. <see cref="OperationCanceledException"/> is re-thrown so a client/WAF disconnect
/// cancels in-flight work. Secrets are never logged: only the non-reversible tenant fingerprint is.
/// </summary>
public static class ToolInvocationFilter
{
    public static void Add(IMcpServerBuilder builder)
    {
        builder.WithRequestFilters(filters =>
        {
            filters.AddCallToolFilter(next => async (context, cancellationToken) =>
            {
                var tool = context.Params?.Name ?? "unknown";
                var tenant = ResolveTenant(context);
                var logger = context.Services?.GetService<ILoggerFactory>()?.CreateLogger("ToolCall");

                using var activity = Telemetry.ActivitySource.StartActivity($"tool/{tool}", ActivityKind.Server);
                activity?.SetTag("mcp.tool", tool);
                activity?.SetTag("casper.tenant", tenant);
                var correlationId = activity?.TraceId.ToString() ?? Guid.NewGuid().ToString("n");

                var sw = Stopwatch.StartNew();
                try
                {
                    var result = await next(context, cancellationToken);
                    sw.Stop();

                    var status = result.IsError == true ? "error" : "ok";
                    Telemetry.RecordToolCall(tool, status, sw.Elapsed.TotalMilliseconds);
                    activity?.SetStatus(status == "error" ? ActivityStatusCode.Error : ActivityStatusCode.Ok);
                    logger?.LogInformation(
                        "tool={Tool} status={Status} duration_ms={DurationMs} tenant={Tenant} correlation_id={CorrelationId}",
                        tool, status, sw.Elapsed.TotalMilliseconds, tenant, correlationId);
                    return result;
                }
                catch (OperationCanceledException)
                {
                    sw.Stop();
                    Telemetry.RecordToolCall(tool, "cancelled", sw.Elapsed.TotalMilliseconds);
                    activity?.SetStatus(ActivityStatusCode.Error, "cancelled");
                    throw; // let cancellation propagate so the request is actually abandoned
                }
                catch (Exception ex)
                {
                    sw.Stop();
                    Telemetry.RecordToolCall(tool, "error", sw.Elapsed.TotalMilliseconds);
                    activity?.SetStatus(ActivityStatusCode.Error, ex.GetType().Name);
                    // Log the exception TYPE + correlation id (never the message body, which may carry
                    // upstream text) at Error; the full exception only at Debug for opt-in deep diagnosis.
                    logger?.LogError(
                        "tool={Tool} status=error duration_ms={DurationMs} tenant={Tenant} correlation_id={CorrelationId} exception={ExceptionType}",
                        tool, sw.Elapsed.TotalMilliseconds, tenant, correlationId, ex.GetType().FullName);
                    logger?.LogDebug(ex, "Tool call exception detail. correlation_id={CorrelationId}", correlationId);

                    return new CallToolResult
                    {
                        IsError = true,
                        Content = [new TextContentBlock { Text = $"{UpstreamErrorMapper.Describe(ex)} (ref: {correlationId})" }]
                    };
                }
            });
        });
    }

    private static string ResolveTenant(RequestContext<CallToolRequestParams> context)
    {
        var http = context.Services?.GetService<IHttpContextAccessor>()?.HttpContext;
        if (http is null) return "local"; // stdio mode
        RemoteHeaders.TryGetCsprKey(http.Request.Headers, out var key);
        return KeyFingerprint.Of(key);
    }
}
