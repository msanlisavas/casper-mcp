using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OpenTelemetry.Logs;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

namespace CasperMcp.Observability;

public static class OpenTelemetrySetup
{
    /// <summary>
    /// Wires OpenTelemetry traces, metrics, AND logs with an OTLP exporter when
    /// <c>OTEL_EXPORTER_OTLP_ENDPOINT</c> is set (the OTLP exporter reads that and the other standard
    /// <c>OTEL_*</c> env vars automatically). No-op when unset, so the server runs without requiring
    /// an observability backend. Captures ASP.NET Core (inbound), HttpClient (outbound CSPR.Cloud),
    /// the .NET runtime, and the custom <c>casper-mcp</c> tool source/meter. Logs continue to also
    /// go to stdout as JSON. Returns whether telemetry was enabled.
    /// </summary>
    public static bool AddCasperTelemetry(this WebApplicationBuilder builder)
    {
        if (string.IsNullOrEmpty(Environment.GetEnvironmentVariable("OTEL_EXPORTER_OTLP_ENDPOINT")))
            return false;

        var resource = ResourceBuilder.CreateDefault().AddService(serviceName: Telemetry.ServiceName, serviceVersion: "3.0.0");

        builder.Services.AddOpenTelemetry()
            .ConfigureResource(r => r.AddService(serviceName: Telemetry.ServiceName, serviceVersion: "3.0.0"))
            .WithTracing(t => t
                .AddAspNetCoreInstrumentation()
                .AddHttpClientInstrumentation()
                .AddSource(Telemetry.ServiceName)
                .AddOtlpExporter())
            .WithMetrics(m => m
                .AddAspNetCoreInstrumentation()
                .AddHttpClientInstrumentation()
                .AddRuntimeInstrumentation()
                .AddMeter(Telemetry.ServiceName)
                .AddOtlpExporter());

        // Ship logs over OTLP too, so the per-tool structured log lines land in the same backend
        // as the traces/metrics and correlate by trace id. (They also remain on stdout as JSON.)
        builder.Logging.AddOpenTelemetry(o =>
        {
            o.SetResourceBuilder(resource);
            o.IncludeFormattedMessage = true;
            o.IncludeScopes = true;
            o.AddOtlpExporter();
        });

        return true;
    }
}
