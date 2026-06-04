using CasperMcp.Configuration;
using CasperMcp.Middleware;
using CasperMcp.Observability;
using CasperMcp.Remote;
using CasperMcp.Writes;
using CSPR.Cloud.Net.Clients;
using CSPR.Cloud.Net.Objects.Config;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System.Net.Http;

var config = new ServerConfig();

// Environment defaults (container-friendly). CLI args below override these.
if (Environment.GetEnvironmentVariable("CASPER_MCP_TRANSPORT") is { Length: > 0 } envTransport) config.Transport = envTransport;
if (Environment.GetEnvironmentVariable("CASPER_MCP_NETWORK") is { Length: > 0 } envNetwork) config.DefaultNetwork = envNetwork;
if (Environment.GetEnvironmentVariable("CASPER_MCP_PATH") is { Length: > 0 } envPath) config.McpPath = envPath;
if (int.TryParse(Environment.GetEnvironmentVariable("CASPER_MCP_PORT"), out var envPort)) config.Port = envPort;
if (Environment.GetEnvironmentVariable("CASPER_MCP_ENABLE_WRITES") is "1" or "true") config.WritesEnabled = true;
if (Environment.GetEnvironmentVariable("CASPER_MCP_KEY_PATH") is { Length: > 0 } envKeyPath) config.KeyPath = envKeyPath;
if (Environment.GetEnvironmentVariable("CASPER_MCP_KEY_ALGO") is { Length: > 0 } envKeyAlgo) config.KeyAlgo = envKeyAlgo;
if (Environment.GetEnvironmentVariable("CASPER_MCP_POLICY_PATH") is { Length: > 0 } envPolicyPath) config.PolicyPath = envPolicyPath;
if (Environment.GetEnvironmentVariable("CASPER_MCP_NODE_RPC_URL") is { Length: > 0 } envNodeUrl) config.NodeRpcUrl = envNodeUrl;

for (int i = 0; i < args.Length; i++)
{
    switch (args[i])
    {
        case "--api-key" when i + 1 < args.Length: config.StdioApiKey = args[++i]; break;
        case "--network" when i + 1 < args.Length: config.DefaultNetwork = args[++i]; break;
        case "--transport" when i + 1 < args.Length: config.Transport = args[++i]; break;
        case "--port" when i + 1 < args.Length: if (int.TryParse(args[++i], out var p)) config.Port = p; break;
        case "--mcp-path" when i + 1 < args.Length: config.McpPath = args[++i]; break;
        case "--enable-writes": config.WritesEnabled = true; break;
        case "--key-path" when i + 1 < args.Length: config.KeyPath = args[++i]; break;
        case "--key-algo" when i + 1 < args.Length: config.KeyAlgo = args[++i]; break;
        case "--policy-path" when i + 1 < args.Length: config.PolicyPath = args[++i]; break;
        case "--node-rpc-url" when i + 1 < args.Length: config.NodeRpcUrl = args[++i]; break;
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
    if (config.WritesEnabled)
    {
        Console.Error.WriteLine("Error: --enable-writes is not allowed with http transport. Run the signer locally over stdio.");
        return 1;
    }
    if (config.AuthMode == AuthMode.ApiKey && string.IsNullOrEmpty(config.AuthApiKey))
    {
        Console.Error.WriteLine("Error: --auth-api-key (or CASPER_MCP_AUTH_API_KEY) is required when --auth-mode=apikey.");
        return 1;
    }
    if (config.AuthMode == AuthMode.Jwt && string.IsNullOrEmpty(config.JwtAuthority))
    {
        Console.Error.WriteLine("Error: --auth-jwt-authority (or CASPER_MCP_AUTH_JWT_AUTHORITY) is required when --auth-mode=jwt.");
        return 1;
    }

    var builder = WebApplication.CreateBuilder(args);

    builder.Logging.ClearProviders();
    builder.Logging.AddJsonConsole();
    builder.Logging.SetMinimumLevel(LogLevel.Information);

    // Opt-in OpenTelemetry (traces + metrics) — enabled when OTEL_EXPORTER_OTLP_ENDPOINT is set.
    var telemetryEnabled = builder.AddCasperTelemetry();

    builder.Services.AddSingleton(config);
    builder.Services.AddHttpContextAccessor();

    // Shared, pooled connection handler for all tenants. 30s request timeout so a hung upstream
    // never holds a request long enough to trip WAF/proxy idle limits.
    builder.Services.AddHttpClient("cspr", c => c.Timeout = TimeSpan.FromSeconds(30))
        .ConfigurePrimaryHttpMessageHandler(() => new SocketsHttpHandler
        {
            PooledConnectionLifetime = TimeSpan.FromMinutes(2)
        });

    // Per-request, per-agent REST client built from the request's CSPR key.
    builder.Services.AddScoped(sp =>
    {
        var http = sp.GetRequiredService<IHttpContextAccessor>().HttpContext!;
        RemoteHeaders.TryGetCsprKey(http.Request.Headers, out var key); // presence enforced by middleware
        var factory = sp.GetRequiredService<IHttpClientFactory>();
        // Do NOT pass the app logger into the SDK: its exception constructors log the raw upstream
        // response body, which would bypass our redaction. Errors are surfaced via ToolInvocationFilter.
        return CasperClientFactory.Create(key, factory.CreateClient("cspr"), loggerFactory: null);
    });

    // Per-request tool-facing options with network resolved from the request.
    builder.Services.AddScoped(sp =>
    {
        var http = sp.GetRequiredService<IHttpContextAccessor>().HttpContext!;
        RemoteHeaders.TryResolveNetwork(http.Request.Headers, config.DefaultNetwork, out var network);
        return new CasperMcpOptions { Network = network };
    });

    if (config.AuthMode == AuthMode.Jwt)
    {
        builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(options =>
            {
                options.Authority = config.JwtAuthority;
                options.Audience = config.JwtAudience;
            });
        builder.Services.AddAuthorization();
    }

    var mcpBuilder = builder.Services
        .AddMcpServer()
        .WithHttpTransport(o => o.Stateless = true)
        .WithToolsFromAssembly();
    ToolInvocationFilter.Add(mcpBuilder);

    builder.Configuration["urls"] = $"http://0.0.0.0:{config.Port}";

    var app = builder.Build();

    // Auth (mode-dependent) runs first.
    if (config.AuthMode == AuthMode.ApiKey)
        app.UseMiddleware<ApiKeyAuthMiddleware>(config.AuthApiKey);
    if (config.AuthMode == AuthMode.Jwt)
    {
        app.UseAuthentication();
        app.UseAuthorization();
    }

    // Then enforce the per-agent CSPR key + network validity.
    app.UseMiddleware<RemoteRequestMiddleware>();

    var mcp = app.MapMcp(config.McpPath);
    if (config.AuthMode == AuthMode.Jwt)
        mcp.RequireAuthorization();

    app.MapGet("/health", () => Results.Ok(new { status = "healthy", server = "casper-mcp", transport = "http" }));
    app.MapGet("/ready", () =>
    {
        var ready = config.AuthMode != AuthMode.Jwt || !string.IsNullOrEmpty(config.JwtAuthority);
        return ready ? Results.Ok(new { status = "ready" }) : Results.StatusCode(503);
    });

    if (config.AuthMode == AuthMode.Jwt)
    {
        app.MapGet("/.well-known/oauth-protected-resource", (HttpContext ctx) =>
            Results.Ok(ProtectedResourceMetadata.Build(
                $"{ctx.Request.Scheme}://{ctx.Request.Host}{config.McpPath}", config.JwtAuthority)));
    }

    Console.WriteLine($"Casper MCP (http) on http://0.0.0.0:{config.Port}{config.McpPath} | auth={config.AuthMode} | default-network={config.DefaultNetwork} | telemetry={(telemetryEnabled ? "otlp" : "off")}");
    await app.RunAsync();
    return 0;
}

// ---- stdio profile ----
if (string.IsNullOrEmpty(config.StdioApiKey))
{
    Console.Error.WriteLine("Error: API key is required in stdio mode. Provide via --api-key or CSPR_CLOUD_API_KEY.");
    return 1;
}

var (writesOk, writesError) = ServerConfig.ValidateWriteConfig(config);
if (!writesOk)
{
    Console.Error.WriteLine($"Error: {writesError}");
    return 1;
}

var stdioConfig = new CasperCloudClientConfig(config.StdioApiKey);
var stdioClient = new CasperCloudRestClient(stdioConfig);

var hostBuilder = Host.CreateApplicationBuilder(args);
hostBuilder.Logging.ClearProviders();
hostBuilder.Logging.AddConsole(opts => opts.LogToStandardErrorThreshold = LogLevel.Trace);
hostBuilder.Logging.SetMinimumLevel(LogLevel.Warning);

hostBuilder.Services.AddSingleton(stdioClient);
hostBuilder.Services.AddSingleton(new CasperMcpOptions { Network = config.DefaultNetwork });

var mcpStdio = hostBuilder.Services.AddMcpServer().WithStdioServerTransport().WithToolsFromAssembly();

// Write tools live ONLY on the stdio surface and ONLY when writes are enabled. They are not
// [McpServerToolType]-annotated, so WithToolsFromAssembly never picks them up — the remote http
// surface stays read-only. Here we register them explicitly alongside the signer they depend on.
if (config.WritesEnabled)
{
    var signer = SignerFactory.Create(config);
    hostBuilder.Services.AddSingleton(signer);
    // The tool classes are `static` (only static tool methods), so the generic WithTools<T>()
    // overload can't be used (CS0718: static types are not valid type arguments). Register them via
    // the IEnumerable<Type> overload, which accepts typeof(...) of a static class.
    mcpStdio.WithTools([typeof(CasperMcp.Tools.TransactionBuildTools),
                        typeof(CasperMcp.Tools.TransactionSignTools)]);
    Console.Error.WriteLine($"casper-mcp signer ENABLED (stdio) | network={config.DefaultNetwork} | key={signer.SignerPublicKeyShort} | writes=transfer,delegate,undelegate,redelegate");
}

await hostBuilder.Build().RunAsync();
return 0;

// Exposed so the test project can host the app via WebApplicationFactory<Program>.
public partial class Program { }
