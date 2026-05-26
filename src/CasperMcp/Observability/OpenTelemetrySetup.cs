using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

namespace CasperMcp.Observability;

public static class OpenTelemetrySetup
{
    /// <summary>
    /// Wires OpenTelemetry tracing + metrics with an OTLP exporter when <c>OTEL_EXPORTER_OTLP_ENDPOINT</c>
    /// is set (the OTLP exporter reads that and the other standard <c>OTEL_*</c> env vars automatically).
    /// No-op when unset, so the server runs without requiring an observability backend.
    /// Captures ASP.NET Core (inbound), HttpClient (outbound CSPR.Cloud), .NET runtime, and the
    /// custom <c>casper-mcp</c> tool source/meter. Returns whether telemetry was enabled.
    /// </summary>
    public static bool AddCasperTelemetry(this WebApplicationBuilder builder)
    {
        if (string.IsNullOrEmpty(Environment.GetEnvironmentVariable("OTEL_EXPORTER_OTLP_ENDPOINT")))
            return false;

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

        return true;
    }
}
