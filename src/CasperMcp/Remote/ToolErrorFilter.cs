using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;

namespace CasperMcp.Remote;

/// <summary>
/// Central CallTool filter: catches exceptions escaping tools, maps them to safe messages,
/// logs once with a correlation id, and returns an IsError result. OperationCanceledException
/// is allowed to propagate so client/WAF disconnects cancel in-flight work.
/// </summary>
public static class ToolErrorFilter
{
    public static void Add(IMcpServerBuilder builder)
    {
        builder.WithRequestFilters(filters =>
        {
            filters.AddCallToolFilter(next => async (context, cancellationToken) =>
            {
                try
                {
                    return await next(context, cancellationToken);
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    var logger = context.Services?.GetService<ILoggerFactory>()?.CreateLogger("ToolCall");
                    var correlationId = Guid.NewGuid().ToString("n");
                    // Log a sanitized summary by default; full exception only at Debug to avoid
                    // any chance of upstream messages carrying secret material into aggregated logs.
                    logger?.LogError(
                        "Tool call failed. CorrelationId={CorrelationId} ExceptionType={ExceptionType}",
                        correlationId, ex.GetType().FullName);
                    logger?.LogDebug(ex, "Tool call exception detail. CorrelationId={CorrelationId}", correlationId);

                    return new CallToolResult
                    {
                        IsError = true,
                        Content = [new TextContentBlock { Text = $"{UpstreamErrorMapper.Describe(ex)} (ref: {correlationId})" }]
                    };
                }
            });
        });
    }
}
