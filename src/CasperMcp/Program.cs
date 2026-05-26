using CasperMcp.Configuration;
using CSPR.Cloud.Net.Clients;
using CSPR.Cloud.Net.Objects.Config;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

var config = new ServerConfig();

for (int i = 0; i < args.Length; i++)
{
    switch (args[i])
    {
        case "--api-key" when i + 1 < args.Length: config.StdioApiKey = args[++i]; break;
        case "--network" when i + 1 < args.Length: config.DefaultNetwork = args[++i]; break;
        case "--transport" when i + 1 < args.Length: config.Transport = args[++i]; break;
        case "--port" when i + 1 < args.Length: if (int.TryParse(args[++i], out var p)) config.Port = p; break;
        case "--mcp-path" when i + 1 < args.Length: config.McpPath = args[++i]; break;
        case "--auth-mode" when i + 1 < args.Length: config.AuthMode = ParseAuthMode(args[++i]); break;
        case "--auth-api-key" when i + 1 < args.Length: config.AuthApiKey = args[++i]; break;
        case "--auth-jwt-authority" when i + 1 < args.Length: config.JwtAuthority = args[++i]; break;
        case "--auth-jwt-audience" when i + 1 < args.Length: config.JwtAudience = args[++i]; break;
    }
}

config.StdioApiKey = Coalesce(config.StdioApiKey, Environment.GetEnvironmentVariable("CSPR_CLOUD_API_KEY"));
config.AuthApiKey = Coalesce(config.AuthApiKey, Environment.GetEnvironmentVariable("CASPER_MCP_AUTH_API_KEY"));
config.JwtAuthority = Coalesce(config.JwtAuthority, Environment.GetEnvironmentVariable("CASPER_MCP_AUTH_JWT_AUTHORITY"));
config.JwtAudience = Coalesce(config.JwtAudience, Environment.GetEnvironmentVariable("CASPER_MCP_AUTH_JWT_AUDIENCE"));
if (config.AuthMode == AuthMode.None && Environment.GetEnvironmentVariable("CASPER_MCP_AUTH_MODE") is { Length: > 0 } envMode)
    config.AuthMode = ParseAuthMode(envMode);

static string Coalesce(string current, string? fallback) =>
    string.IsNullOrEmpty(current) ? (fallback ?? string.Empty) : current;

static AuthMode ParseAuthMode(string value) => value.ToLowerInvariant() switch
{
    "apikey" => AuthMode.ApiKey,
    "jwt" => AuthMode.Jwt,
    _ => AuthMode.None
};

if (config.IsHttp)
{
    Console.Error.WriteLine("http transport not wired yet"); // replaced in Task 11
    return 1;
}

if (string.IsNullOrEmpty(config.StdioApiKey))
{
    Console.Error.WriteLine("Error: API key is required in stdio mode. Provide via --api-key or CSPR_CLOUD_API_KEY.");
    return 1;
}

var casperConfig = new CasperCloudClientConfig(config.StdioApiKey);
var casperClient = new CasperCloudRestClient(casperConfig);

var builder = Host.CreateApplicationBuilder(args);
builder.Logging.ClearProviders();
builder.Logging.AddConsole(opts => opts.LogToStandardErrorThreshold = LogLevel.Trace);
builder.Logging.SetMinimumLevel(LogLevel.Warning);

builder.Services.AddSingleton(casperClient);
builder.Services.AddSingleton(new CasperMcpOptions { Network = config.DefaultNetwork });
builder.Services.AddMcpServer().WithStdioServerTransport().WithToolsFromAssembly();

await builder.Build().RunAsync();
return 0;
