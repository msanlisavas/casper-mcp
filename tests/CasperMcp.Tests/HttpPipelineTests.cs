using System.Collections.Concurrent;
using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace CasperMcp.Tests;

// ---------------------------------------------------------------------------
// Collection definition: run all tests in this class sequentially on one
// thread — they share process-wide env vars and a test server.
// ---------------------------------------------------------------------------
[CollectionDefinition("HttpPipeline", DisableParallelization = true)]
public class HttpPipelineCollection { }

[Collection("HttpPipeline")]
public class HttpPipelineTests : IDisposable
{
    // Env-var names we set — cleared in Dispose so nothing leaks into other tests.
    private static readonly string[] ManagedEnvVars =
    [
        "CASPER_MCP_TRANSPORT",
        "CASPER_MCP_AUTH_MODE",
        "CASPER_MCP_AUTH_API_KEY",
        "CASPER_MCP_AUTH_JWT_AUTHORITY",
        "CASPER_MCP_AUTH_JWT_AUDIENCE",
        "CASPER_MCP_NETWORK",
        "CASPER_MCP_PATH",
    ];

    public void Dispose()
    {
        foreach (var v in ManagedEnvVars)
            Environment.SetEnvironmentVariable(v, null);
    }

    // -----------------------------------------------------------------------
    // Factory
    // -----------------------------------------------------------------------

    private static CasperAppFactory CreateFactory(
        IDictionary<string, string?> env,
        Action<IServiceCollection>? configure = null)
        => new(env, configure);

    private static Dictionary<string, string?> BaseEnv() =>
        new() { ["CASPER_MCP_TRANSPORT"] = "http" };

    // -----------------------------------------------------------------------
    // Recording fake handler
    // -----------------------------------------------------------------------

    /// <summary>
    /// Returns 200 OK with body {} for every request and records the raw
    /// Authorization header value from each outbound call.
    /// </summary>
    private sealed class RecordingHandler : HttpMessageHandler
    {
        public ConcurrentBag<string> AuthValues { get; } = new();

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            // Capture the Authorization header as sent by the SDK.
            string auth;
            if (request.Headers.Authorization is not null)
                auth = request.Headers.Authorization.ToString();
            else if (request.Headers.TryGetValues("Authorization", out var vals))
                auth = string.Join(",", vals);
            else
                auth = string.Empty;
            AuthValues.Add(auth);

            var response = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("{}", Encoding.UTF8, "application/json")
            };
            return Task.FromResult(response);
        }
    }

    // -----------------------------------------------------------------------
    // Helper: build a JSON-RPC request content
    // -----------------------------------------------------------------------
    private static StringContent JsonRpc(string method, object? @params = null, int id = 1)
    {
        var body = JsonSerializer.Serialize(new
        {
            jsonrpc = "2.0",
            id,
            method,
            @params = @params ?? new { }
        });
        return new StringContent(body, Encoding.UTF8, "application/json");
    }

    // -----------------------------------------------------------------------
    // Helper: apply standard Streamable-HTTP headers to a request message
    // -----------------------------------------------------------------------
    private static HttpRequestMessage McpRequest(
        StringContent body,
        string? csprKey,
        string? network = null)
    {
        var msg = new HttpRequestMessage(HttpMethod.Post, "/mcp") { Content = body };
        msg.Headers.Accept.Clear();
        msg.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        msg.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("text/event-stream"));
        if (csprKey is not null)
            msg.Headers.Add("X-CSPR-Cloud-Api-Key", csprKey);
        if (network is not null)
            msg.Headers.Add("X-Casper-Network", network);
        return msg;
    }

    // -----------------------------------------------------------------------
    // Test 1 — Health / Ready endpoints
    // -----------------------------------------------------------------------
    [Fact]
    public async Task HealthAndReady_Return200()
    {
        using var factory = CreateFactory(BaseEnv());
        var client = factory.CreateClient();

        var health = await client.GetAsync("/health");
        var ready  = await client.GetAsync("/ready");

        Assert.Equal(HttpStatusCode.OK, health.StatusCode);
        Assert.Equal(HttpStatusCode.OK, ready.StatusCode);
    }

    // -----------------------------------------------------------------------
    // Test 2 — Missing CSPR key → 401
    // -----------------------------------------------------------------------
    [Fact]
    public async Task MissingCsprKey_Returns401()
    {
        using var factory = CreateFactory(BaseEnv());
        var client = factory.CreateClient();

        var content = JsonRpc("tools/list");
        var msg = new HttpRequestMessage(HttpMethod.Post, "/mcp") { Content = content };
        msg.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        msg.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("text/event-stream"));
        // deliberately no X-CSPR-Cloud-Api-Key

        var response = await client.SendAsync(msg);
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    // -----------------------------------------------------------------------
    // Test 2b — Missing CSPR key on /mcp → body is a parseable JSON-RPC 2.0
    // error (not a bare {"error":...}) so MCP clients surface the real message
    // instead of crashing on deserialization. Echoes the request id.
    // -----------------------------------------------------------------------
    [Fact]
    public async Task MissingCsprKey_OnMcp_ReturnsParseableJsonRpcError_WithEchoedId()
    {
        using var factory = CreateFactory(BaseEnv());
        var client = factory.CreateClient();

        var content = JsonRpc("initialize", id: 99);
        var msg = new HttpRequestMessage(HttpMethod.Post, "/mcp") { Content = content };
        msg.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        msg.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("text/event-stream"));
        // deliberately no X-CSPR-Cloud-Api-Key

        var response = await client.SendAsync(msg);
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);

        var body = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(body);
        var root = doc.RootElement;
        Assert.Equal("2.0", root.GetProperty("jsonrpc").GetString());
        Assert.Equal(-32600, root.GetProperty("error").GetProperty("code").GetInt32());
        Assert.Equal(99, root.GetProperty("id").GetInt32());
        var message = root.GetProperty("error").GetProperty("message").GetString();
        Assert.Contains("X-CSPR-Cloud-Api-Key", message);
        Assert.Contains("CSPR_CLOUD_API_KEY", message);
    }

    // -----------------------------------------------------------------------
    // Test 3 — Invalid network → 400
    // -----------------------------------------------------------------------
    [Fact]
    public async Task InvalidNetwork_Returns400()
    {
        using var factory = CreateFactory(BaseEnv());
        var client = factory.CreateClient();

        var msg = McpRequest(JsonRpc("tools/list"), csprKey: "k", network: "devnet");
        var response = await client.SendAsync(msg);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    // -----------------------------------------------------------------------
    // Test 4 — Valid tools/list returns tool names (no upstream call)
    // -----------------------------------------------------------------------
    [Fact]
    public async Task ToolsList_ValidRequest_Returns200WithToolNames()
    {
        using var factory = CreateFactory(BaseEnv());
        var client = factory.CreateClient();

        var msg = McpRequest(JsonRpc("tools/list"), csprKey: "any-key", network: "testnet");
        var response = await client.SendAsync(msg);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("get_network_status", body);
    }

    // -----------------------------------------------------------------------
    // Test 5 — Tenant isolation under concurrency (THE KEY TEST)
    // Each of N=20 concurrent requests carries a distinct X-CSPR-Cloud-Api-Key.
    // The recording handler captures the outbound Authorization value.
    // Assert: the multiset of captured values equals exactly the set of 20
    // distinct keys → no key bleeds across tenants.
    // -----------------------------------------------------------------------
    [Fact]
    public async Task TenantIsolation_ConcurrentRequests_NoKeyBleed()
    {
        const int N = 20;

        var handler = new RecordingHandler();
        var env = BaseEnv();

        using var factory = CreateFactory(env, services =>
        {
            // Override the "cspr" named client with our recording handler.
            // Last-registration wins in IHttpClientFactory.
            services.AddHttpClient("cspr")
                    .ConfigurePrimaryHttpMessageHandler(() => handler);
        });

        var client = factory.CreateClient();

        // Fire N concurrent requests, each with a unique key.
        var tasks = Enumerable.Range(0, N).Select(async i =>
        {
            var key = $"key-{i}";
            var body = JsonRpc(
                "tools/call",
                new { name = "get_deploys", arguments = new { pageSize = 1 } },
                id: i + 1);
            var msg = McpRequest(body, csprKey: key, network: "testnet");
            var resp = await client.SendAsync(msg);
            return (key, resp.StatusCode);
        }).ToList();

        var results = await Task.WhenAll(tasks);

        // All requests must have reached the MCP layer (not rejected by middleware).
        foreach (var (key, status) in results)
            Assert.True(
                status == HttpStatusCode.OK || status == HttpStatusCode.Accepted,
                $"Unexpected status {status} for {key}");

        // The recording handler should have seen exactly N outbound requests.
        Assert.Equal(N, handler.AuthValues.Count);

        // Build the expected multiset of Authorization values.
        // The CSPR SDK may send the key verbatim or as "Bearer <key>".
        // We normalise by stripping a possible "Bearer " prefix for comparison.
        static string Normalize(string v) =>
            v.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase)
                ? v["Bearer ".Length..].Trim()
                : v.Trim();

        var captured = handler.AuthValues
            .Select(Normalize)
            .OrderBy(x => x)
            .ToList();

        var expected = Enumerable.Range(0, N)
            .Select(i => $"key-{i}")
            .OrderBy(x => x)
            .ToList();

        Assert.Equal(expected, captured);
    }

    // -----------------------------------------------------------------------
    // Test 6 — JWT mode: metadata endpoint + unauthenticated /mcp → 401
    // -----------------------------------------------------------------------
    [Fact]
    public async Task JwtMode_MetadataAndUnauthenticatedMcp()
    {
        var env = new Dictionary<string, string?>
        {
            ["CASPER_MCP_TRANSPORT"]           = "http",
            ["CASPER_MCP_AUTH_MODE"]            = "jwt",
            ["CASPER_MCP_AUTH_JWT_AUTHORITY"]   = "https://example.test/",
            ["CASPER_MCP_AUTH_JWT_AUDIENCE"]    = "casper-mcp",
        };

        using var factory = CreateFactory(env);
        var client = factory.CreateClient();

        // (a) /.well-known/oauth-protected-resource → 200 with authority + resource
        var meta = await client.GetAsync("/.well-known/oauth-protected-resource");
        Assert.Equal(HttpStatusCode.OK, meta.StatusCode);

        var metaBody = await meta.Content.ReadAsStringAsync();
        Assert.Contains("example.test", metaBody);
        Assert.Contains("resource", metaBody);

        // (b) POST /mcp with CSPR key but NO Authorization: Bearer → 401
        // (JWT middleware rejects unauthenticated requests to the MCP endpoint.)
        var msg = McpRequest(JsonRpc("tools/list"), csprKey: "some-key", network: "testnet");
        var resp = await client.SendAsync(msg);
        Assert.Equal(HttpStatusCode.Unauthorized, resp.StatusCode);
    }
}

// ---------------------------------------------------------------------------
// Custom WebApplicationFactory
// ---------------------------------------------------------------------------

public class CasperAppFactory : WebApplicationFactory<Program>
{
    private readonly Action<IServiceCollection>? _configure;

    public CasperAppFactory(IDictionary<string, string?> env, Action<IServiceCollection>? configure = null)
    {
        // Set env vars BEFORE the host is built (factory builds lazily on first CreateClient()).
        foreach (var kv in env)
            Environment.SetEnvironmentVariable(kv.Key, kv.Value);

        _configure = configure;
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        if (_configure != null)
            builder.ConfigureServices(_configure);
    }
}
