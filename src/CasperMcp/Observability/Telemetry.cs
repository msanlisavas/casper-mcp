using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace CasperMcp.Observability;

/// <summary>
/// Central OpenTelemetry instrumentation for casper-mcp. The <see cref="ActivitySource"/> and
/// <see cref="Meter"/> names ("casper-mcp") are what operators register with their OTLP pipeline
/// to observe tool traffic (request volume, latency, error rate) per tool and per tenant.
/// </summary>
public static class Telemetry
{
    public const string ServiceName = "casper-mcp";

    public static readonly ActivitySource ActivitySource = new(ServiceName);

    private static readonly Meter Meter = new(ServiceName);

    private static readonly Counter<long> ToolCalls = Meter.CreateCounter<long>(
        "casper_mcp.tool.calls", unit: "{call}", description: "Number of MCP tool calls, tagged by tool and status.");

    private static readonly Histogram<double> ToolDuration = Meter.CreateHistogram<double>(
        "casper_mcp.tool.duration", unit: "ms", description: "MCP tool call duration in milliseconds, tagged by tool and status.");

    /// <summary>Records one tool invocation. <paramref name="status"/> is "ok", "error", or "cancelled".</summary>
    public static void RecordToolCall(string tool, string status, double durationMs)
    {
        var tags = new TagList { { "tool", tool }, { "status", status } };
        ToolCalls.Add(1, tags);
        ToolDuration.Record(durationMs, tags);
    }
}
