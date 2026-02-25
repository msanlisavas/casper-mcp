using CasperMcp.Configuration;
using CasperMcp.Middleware;
using CSPR.Cloud.Net.Clients;
using CSPR.Cloud.Net.Objects.Config;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

var options = new CasperMcpOptions();

// Parse command-line arguments
for (int i = 0; i < args.Length; i++)
{
    switch (args[i])
    {
        case "--api-key" when i + 1 < args.Length:
            options.ApiKey = args[++i];
            break;
        case "--network" when i + 1 < args.Length:
            options.Network = args[++i];
            break;
        case "--transport" when i + 1 < args.Length:
            options.Transport = args[++i];
            break;
        case "--port" when i + 1 < args.Length:
            if (int.TryParse(args[++i], out var port))
                options.Port = port;
            break;
        case "--server-api-key" when i + 1 < args.Length:
            options.ServerApiKey = args[++i];
            break;
    }
}

// Fall back to environment variables
if (string.IsNullOrEmpty(options.ApiKey))
    options.ApiKey = Environment.GetEnvironmentVariable("CSPR_CLOUD_API_KEY") ?? string.Empty;

if (string.IsNullOrEmpty(options.ServerApiKey))
    options.ServerApiKey = Environment.GetEnvironmentVariable("CASPER_MCP_SERVER_API_KEY") ?? string.Empty;

if (string.IsNullOrEmpty(options.ApiKey))
{
    Console.Error.WriteLine("Error: API key is required. Provide via --api-key argument or CSPR_CLOUD_API_KEY environment variable.");
    return 1;
}

var casperConfig = new CasperCloudClientConfig(options.ApiKey);
var casperClient = new CasperCloudRestClient(casperConfig);

if (options.IsSseTransport)
{
    // SSE / HTTP transport mode
    var builder = WebApplication.CreateBuilder(args);

    builder.Logging.ClearProviders();
    builder.Logging.AddConsole();
    builder.Logging.SetMinimumLevel(LogLevel.Information);

    builder.Services.AddSingleton(casperClient);
    builder.Services.AddSingleton(options);

    builder.Services
        .AddMcpServer()
        .WithHttpTransport()
        .WithToolsFromAssembly();

    builder.WebHost.UseUrls($"http://0.0.0.0:{options.Port}");

    var app = builder.Build();

    if (!string.IsNullOrEmpty(options.ServerApiKey))
    {
        app.UseMiddleware<ApiKeyAuthMiddleware>(options.ServerApiKey);
    }

    app.MapMcp();

    app.MapGet("/health", () => Results.Ok(new
    {
        status = "healthy",
        server = "casper-mcp",
        transport = "sse",
        network = options.Network
    }));

    Console.WriteLine($"Casper MCP server starting on http://0.0.0.0:{options.Port}");
    Console.WriteLine($"  Transport: SSE");
    Console.WriteLine($"  Network:   {options.Network}");
    Console.WriteLine($"  Auth:      {(string.IsNullOrEmpty(options.ServerApiKey) ? "disabled" : "enabled (X-API-Key)")}");
    Console.WriteLine($"  Health:    http://localhost:{options.Port}/health");
    Console.WriteLine($"  MCP:       http://localhost:{options.Port}/sse");

    await app.RunAsync();
}
else
{
    // stdio transport mode (default)
    var builder = Host.CreateApplicationBuilder(args);

    builder.Logging.ClearProviders();
    builder.Logging.AddConsole(opts => opts.LogToStandardErrorThreshold = LogLevel.Trace);
    builder.Logging.SetMinimumLevel(LogLevel.Warning);

    builder.Services.AddSingleton(casperClient);
    builder.Services.AddSingleton(options);

    builder.Services
        .AddMcpServer()
        .WithStdioServerTransport()
        .WithToolsFromAssembly();

    await builder.Build().RunAsync();
}

return 0;
