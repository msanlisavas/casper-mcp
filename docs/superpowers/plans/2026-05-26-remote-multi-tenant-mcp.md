# Remote Multi-Tenant MCP Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Turn casper-mcp into a stateless, multi-tenant remote MCP service where each request carries its own CSPR.Cloud API key, with pluggable standards-based auth and proper connection pooling, while keeping stdio mode for local use.

**Architecture:** Two transport profiles share ~70 request/response tools. `stdio` keeps singleton clients built from a startup key. `http` runs stateless Streamable HTTP at `/mcp`: per request the server validates headers, resolves the agent's CSPR key and network, and injects a fresh `CasperCloudRestClient` (over a pooled `IHttpClientFactory` handler) plus a per-request `CasperMcpOptions` via request-scoped DI — so tool method signatures never change. A central `CallTool` filter maps upstream errors and logs with a redacted correlation id. The websocket `Watch*` tools are removed.

**Tech Stack:** .NET 10, ASP.NET Core minimal hosting, `ModelContextProtocol` + `ModelContextProtocol.AspNetCore` SDK, `CSPR.Cloud.Net`, `Microsoft.AspNetCore.Authentication.JwtBearer`, xUnit.

---

## File Structure

**New files:**
- `src/CasperMcp/Configuration/ServerConfig.cs` — server-level startup config + `AuthMode` enum.
- `src/CasperMcp/Remote/RemoteHeaders.cs` — header names + pure header parsing (CSPR key, network).
- `src/CasperMcp/Remote/CasperClientFactory.cs` — builds a `CasperCloudRestClient` from a key + pooled `HttpClient`.
- `src/CasperMcp/Remote/UpstreamErrorMapper.cs` — maps exceptions to friendly, secret-free strings.
- `src/CasperMcp/Remote/ToolErrorFilter.cs` — `CallTool` request filter (typed errors + correlation-id logging).
- `src/CasperMcp/Security/SecretRedaction.cs` — sensitive-header detection + value redaction.
- `src/CasperMcp/Middleware/RemoteRequestMiddleware.cs` — validates CSPR key presence (401) + network value (400).
- `src/CasperMcp/Remote/ProtectedResourceMetadata.cs` — OAuth protected-resource metadata payload (jwt mode).
- Tests: `RemoteHeadersTests.cs`, `CasperClientFactoryTests.cs`, `UpstreamErrorMapperTests.cs`, `SecretRedactionTests.cs`, `RemoteRequestMiddlewareTests.cs`, `ApiKeyAuthMiddlewareTests.cs` under `tests/CasperMcp.Tests/`.

**Modified files:**
- `src/CasperMcp/Configuration/CasperMcpOptions.cs` — slim to tool-facing `Network` + `IsTestnet`.
- `src/CasperMcp/Middleware/ApiKeyAuthMiddleware.cs` — constant-time compare; used by `apikey` auth mode.
- `src/CasperMcp/Program.cs` — full rebuild of both transport branches.
- `src/CasperMcp/CasperMcp.csproj` — add JwtBearer package.
- `tests/CasperMcp.Tests/CasperMcp.Tests.csproj` — add `Microsoft.AspNetCore.App` framework reference.
- `tests/CasperMcp.Tests/UnitTest1.cs` — update `CasperMcpOptionsTests` for the slimmed type.
- `src/CasperMcp/Tools/*.cs` — replace inline error formatting with `UpstreamErrorMapper.Describe` (Task 12).
- `README.md`, `mcp.json`, `docker-compose.yml`, `Dockerfile` — docs/ops for the http profile (Task 14).

**Deleted files:**
- `src/CasperMcp/Tools/StreamingTools.cs`.

---

## Task 1: Add dependencies and test framework reference

**Files:**
- Modify: `src/CasperMcp/CasperMcp.csproj`
- Modify: `tests/CasperMcp.Tests/CasperMcp.Tests.csproj`

- [ ] **Step 1: Add the JwtBearer package to the app project**

In `src/CasperMcp/CasperMcp.csproj`, inside the existing `<ItemGroup>` that has the package references, add:

```xml
    <PackageReference Include="Microsoft.AspNetCore.Authentication.JwtBearer" Version="10.0.0" />
```

- [ ] **Step 2: Add the ASP.NET Core framework reference to the test project**

In `tests/CasperMcp.Tests/CasperMcp.Tests.csproj`, add a new item group (so tests can use `DefaultHttpContext` / `Microsoft.AspNetCore.Http`):

```xml
  <ItemGroup>
    <FrameworkReference Include="Microsoft.AspNetCore.App" />
  </ItemGroup>
```

- [ ] **Step 3: Restore and build**

Run: `dotnet build casper-mcp.slnx`
Expected: Build succeeds (JwtBearer restores). If `10.0.0` is unavailable, run `dotnet add src/CasperMcp package Microsoft.AspNetCore.Authentication.JwtBearer` to pin the matching net10 version, then rebuild.

- [ ] **Step 4: Commit**

```bash
git add src/CasperMcp/CasperMcp.csproj tests/CasperMcp.Tests/CasperMcp.Tests.csproj
git commit -m "build: add JwtBearer package and AspNetCore test framework reference"
```

---

## Task 2: Introduce ServerConfig and slim CasperMcpOptions

This separates server-level startup config from the per-request, tool-facing options. Tools only ever read `Network`/`IsTestnet`.

**Files:**
- Create: `src/CasperMcp/Configuration/ServerConfig.cs`
- Modify: `src/CasperMcp/Configuration/CasperMcpOptions.cs`
- Modify: `tests/CasperMcp.Tests/UnitTest1.cs` (the `CasperMcpOptionsTests` class)

- [ ] **Step 1: Update the CasperMcpOptions unit tests (failing)**

Replace the entire `CasperMcpOptionsTests` class in `tests/CasperMcp.Tests/UnitTest1.cs` with:

```csharp
public class CasperMcpOptionsTests
{
    [Fact]
    public void IsTestnet_DefaultNetwork_ReturnsFalse()
    {
        var options = new CasperMcpOptions();
        Assert.False(options.IsTestnet);
    }

    [Fact]
    public void IsTestnet_MainnetNetwork_ReturnsFalse()
    {
        var options = new CasperMcpOptions { Network = "mainnet" };
        Assert.False(options.IsTestnet);
    }

    [Fact]
    public void IsTestnet_TestnetNetwork_ReturnsTrue()
    {
        var options = new CasperMcpOptions { Network = "testnet" };
        Assert.True(options.IsTestnet);
    }

    [Fact]
    public void IsTestnet_CaseInsensitive()
    {
        var options = new CasperMcpOptions { Network = "TESTNET" };
        Assert.True(options.IsTestnet);
    }

    [Fact]
    public void DefaultNetwork_IsMainnet()
    {
        var options = new CasperMcpOptions();
        Assert.Equal("mainnet", options.Network);
    }
}

public class ServerConfigTests
{
    [Fact]
    public void Defaults_AreSane()
    {
        var cfg = new ServerConfig();
        Assert.Equal("stdio", cfg.Transport);
        Assert.False(cfg.IsHttp);
        Assert.Equal(3001, cfg.Port);
        Assert.Equal("/mcp", cfg.McpPath);
        Assert.Equal("mainnet", cfg.DefaultNetwork);
        Assert.Equal(AuthMode.None, cfg.AuthMode);
    }

    [Fact]
    public void IsHttp_WhenTransportHttp_ReturnsTrue()
    {
        var cfg = new ServerConfig { Transport = "http" };
        Assert.True(cfg.IsHttp);
    }

    [Fact]
    public void IsHttp_CaseInsensitive()
    {
        var cfg = new ServerConfig { Transport = "HTTP" };
        Assert.True(cfg.IsHttp);
    }
}
```

- [ ] **Step 2: Run the tests to verify they fail**

Run: `dotnet test tests/CasperMcp.Tests --filter "FullyQualifiedName~CasperMcpOptionsTests|FullyQualifiedName~ServerConfigTests"`
Expected: Build/compile FAILS (`ServerConfig` and `AuthMode` do not exist; removed properties referenced).

- [ ] **Step 3: Slim CasperMcpOptions**

Replace the entire contents of `src/CasperMcp/Configuration/CasperMcpOptions.cs` with:

```csharp
namespace CasperMcp.Configuration;

/// <summary>
/// Per-request, tool-facing options. In http mode this is registered scoped and the
/// network is resolved from the request; in stdio mode it is a singleton from startup config.
/// </summary>
public class CasperMcpOptions
{
    public string Network { get; set; } = "mainnet";

    public bool IsTestnet => Network.Equals("testnet", StringComparison.OrdinalIgnoreCase);
}
```

- [ ] **Step 4: Create ServerConfig**

Create `src/CasperMcp/Configuration/ServerConfig.cs`:

```csharp
namespace CasperMcp.Configuration;

public enum AuthMode
{
    None,
    ApiKey,
    Jwt
}

/// <summary>
/// Server-level configuration resolved once at startup from CLI args / environment.
/// </summary>
public class ServerConfig
{
    public string Transport { get; set; } = "stdio";
    public int Port { get; set; } = 3001;
    public string McpPath { get; set; } = "/mcp";
    public string DefaultNetwork { get; set; } = "mainnet";

    /// <summary>CSPR.Cloud key used only in stdio mode (required there).</summary>
    public string StdioApiKey { get; set; } = string.Empty;

    public AuthMode AuthMode { get; set; } = AuthMode.None;
    public string AuthApiKey { get; set; } = string.Empty;
    public string JwtAuthority { get; set; } = string.Empty;
    public string JwtAudience { get; set; } = string.Empty;

    public bool IsHttp => Transport.Equals("http", StringComparison.OrdinalIgnoreCase);
}
```

- [ ] **Step 5: Make Program.cs compile against the new types (temporary stdio-only)**

`Program.cs` still references removed members. To keep the build green until the full rebuild (Task 11/13), replace the argument-parsing + bootstrap section at the top of `src/CasperMcp/Program.cs` (the block from `var options = new CasperMcpOptions();` through the `if (string.IsNullOrEmpty(options.ApiKey))` error check) with:

```csharp
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
```

Then, for now, replace the rest of the file (the `if (options.IsSseTransport)` block onward) with a minimal stdio-only bootstrap so it compiles — this is fully replaced in Tasks 11/13:

```csharp
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
```

Remove the now-unused `using` for `CasperCloudSocketClient` only if present; leave other usings.

- [ ] **Step 6: Run the tests to verify they pass**

Run: `dotnet test tests/CasperMcp.Tests --filter "FullyQualifiedName~CasperMcpOptionsTests|FullyQualifiedName~ServerConfigTests"`
Expected: PASS (build succeeds; the listed tests pass).

> Note: `StreamingTools.cs` still references `CasperCloudSocketClient` and will be deleted in Task 3; the build stays green because that file is untouched here.

- [ ] **Step 7: Commit**

```bash
git add src/CasperMcp/Configuration/ServerConfig.cs src/CasperMcp/Configuration/CasperMcpOptions.cs src/CasperMcp/Program.cs tests/CasperMcp.Tests/UnitTest1.cs
git commit -m "refactor: split ServerConfig from tool-facing CasperMcpOptions"
```

---

## Task 3: Remove the websocket streaming tools and socket client

**Files:**
- Delete: `src/CasperMcp/Tools/StreamingTools.cs`

- [ ] **Step 1: Delete the file**

```bash
git rm src/CasperMcp/Tools/StreamingTools.cs
```

- [ ] **Step 2: Confirm no remaining references to the socket client**

Run: `grep -rn "CasperCloudSocketClient\|StreamingTools\|WatchBlocks\|WatchDeploys" src tests`
Expected: No matches (the stdio bootstrap from Task 2 does not register the socket client). If any match appears in `Program.cs`, remove that line.

- [ ] **Step 3: Build**

Run: `dotnet build casper-mcp.slnx`
Expected: Build succeeds.

- [ ] **Step 4: Commit**

```bash
git add -A
git commit -m "feat!: remove websocket Watch* tools and socket client"
```

---

## Task 4: RemoteHeaders — pure header parsing (TDD)

**Files:**
- Create: `src/CasperMcp/Remote/RemoteHeaders.cs`
- Test: `tests/CasperMcp.Tests/RemoteHeadersTests.cs`

- [ ] **Step 1: Write the failing test**

Create `tests/CasperMcp.Tests/RemoteHeadersTests.cs`:

```csharp
using CasperMcp.Remote;
using Microsoft.AspNetCore.Http;

namespace CasperMcp.Tests;

public class RemoteHeadersTests
{
    private static IHeaderDictionary Headers(params (string, string)[] pairs)
    {
        var h = new HeaderDictionary();
        foreach (var (k, v) in pairs) h[k] = v;
        return h;
    }

    [Fact]
    public void TryGetCsprKey_Present_ReturnsTrueAndKey()
    {
        var ok = RemoteHeaders.TryGetCsprKey(Headers(("X-CSPR-Cloud-Api-Key", "abc")), out var key);
        Assert.True(ok);
        Assert.Equal("abc", key);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void TryGetCsprKey_MissingOrBlank_ReturnsFalse(string value)
    {
        var ok = RemoteHeaders.TryGetCsprKey(Headers(("X-CSPR-Cloud-Api-Key", value)), out var key);
        Assert.False(ok);
        Assert.Equal(string.Empty, key);
    }

    [Fact]
    public void TryGetCsprKey_Absent_ReturnsFalse()
    {
        var ok = RemoteHeaders.TryGetCsprKey(Headers(), out _);
        Assert.False(ok);
    }

    [Fact]
    public void TryResolveNetwork_Absent_UsesFallback()
    {
        var ok = RemoteHeaders.TryResolveNetwork(Headers(), "mainnet", out var net);
        Assert.True(ok);
        Assert.Equal("mainnet", net);
    }

    [Theory]
    [InlineData("testnet", "testnet")]
    [InlineData("MAINNET", "mainnet")]
    public void TryResolveNetwork_ValidHeader_Normalizes(string header, string expected)
    {
        var ok = RemoteHeaders.TryResolveNetwork(Headers(("X-Casper-Network", header)), "mainnet", out var net);
        Assert.True(ok);
        Assert.Equal(expected, net);
    }

    [Fact]
    public void TryResolveNetwork_InvalidHeader_ReturnsFalse()
    {
        var ok = RemoteHeaders.TryResolveNetwork(Headers(("X-Casper-Network", "devnet")), "mainnet", out _);
        Assert.False(ok);
    }
}
```

- [ ] **Step 2: Run the test to verify it fails**

Run: `dotnet test tests/CasperMcp.Tests --filter "FullyQualifiedName~RemoteHeadersTests"`
Expected: FAIL (compile error — `RemoteHeaders` does not exist).

- [ ] **Step 3: Implement RemoteHeaders**

Create `src/CasperMcp/Remote/RemoteHeaders.cs`:

```csharp
using Microsoft.AspNetCore.Http;

namespace CasperMcp.Remote;

public static class RemoteHeaders
{
    public const string CsprKeyHeader = "X-CSPR-Cloud-Api-Key";
    public const string NetworkHeader = "X-Casper-Network";

    public static bool TryGetCsprKey(IHeaderDictionary headers, out string key)
    {
        var value = headers[CsprKeyHeader].ToString();
        if (string.IsNullOrWhiteSpace(value))
        {
            key = string.Empty;
            return false;
        }
        key = value.Trim();
        return true;
    }

    /// <summary>
    /// Resolves the effective network. Returns false only when the header is present but invalid.
    /// </summary>
    public static bool TryResolveNetwork(IHeaderDictionary headers, string fallback, out string network)
    {
        var value = headers[NetworkHeader].ToString();
        if (string.IsNullOrWhiteSpace(value))
        {
            network = fallback;
            return true;
        }

        switch (value.Trim().ToLowerInvariant())
        {
            case "mainnet": network = "mainnet"; return true;
            case "testnet": network = "testnet"; return true;
            default: network = string.Empty; return false;
        }
    }
}
```

- [ ] **Step 4: Run the test to verify it passes**

Run: `dotnet test tests/CasperMcp.Tests --filter "FullyQualifiedName~RemoteHeadersTests"`
Expected: PASS.

- [ ] **Step 5: Commit**

```bash
git add src/CasperMcp/Remote/RemoteHeaders.cs tests/CasperMcp.Tests/RemoteHeadersTests.cs
git commit -m "feat: add RemoteHeaders parsing for per-request key and network"
```

---

## Task 5: CasperClientFactory — per-request client over a pooled HttpClient (TDD)

**Files:**
- Create: `src/CasperMcp/Remote/CasperClientFactory.cs`
- Test: `tests/CasperMcp.Tests/CasperClientFactoryTests.cs`

- [ ] **Step 1: Write the failing test**

Create `tests/CasperMcp.Tests/CasperClientFactoryTests.cs`:

```csharp
using CasperMcp.Remote;
using CSPR.Cloud.Net.Clients;

namespace CasperMcp.Tests;

public class CasperClientFactoryTests
{
    [Fact]
    public void Create_WithKeyAndHttpClient_ReturnsUsableClient()
    {
        using var http = new HttpClient();
        var client = CasperClientFactory.Create("test-key", http, loggerFactory: null);

        Assert.NotNull(client);
        Assert.NotNull(client.Mainnet);
        Assert.NotNull(client.Testnet);
    }

    [Fact]
    public void Create_DistinctKeys_ReturnDistinctInstances()
    {
        using var http = new HttpClient();
        var a = CasperClientFactory.Create("key-a", http, null);
        var b = CasperClientFactory.Create("key-b", http, null);

        Assert.NotSame(a, b);
    }
}
```

- [ ] **Step 2: Run the test to verify it fails**

Run: `dotnet test tests/CasperMcp.Tests --filter "FullyQualifiedName~CasperClientFactoryTests"`
Expected: FAIL (compile error — `CasperClientFactory` does not exist).

- [ ] **Step 3: Implement CasperClientFactory**

Create `src/CasperMcp/Remote/CasperClientFactory.cs`:

```csharp
using CSPR.Cloud.Net.Clients;
using CSPR.Cloud.Net.Objects.Config;
using Microsoft.Extensions.Logging;

namespace CasperMcp.Remote;

/// <summary>
/// Builds a lightweight, per-request CasperCloudRestClient bound to a single agent's API key.
/// The HttpClient is supplied by IHttpClientFactory so the underlying connection pool is shared
/// across all tenants; a fresh client per request keeps the api key isolated between tenants.
/// </summary>
public static class CasperClientFactory
{
    public static CasperCloudRestClient Create(string apiKey, HttpClient httpClient, ILoggerFactory? loggerFactory)
    {
        var config = new CasperCloudClientConfig(apiKey);
        return new CasperCloudRestClient(config, httpClient, loggerFactory!);
    }
}
```

- [ ] **Step 4: Run the test to verify it passes**

Run: `dotnet test tests/CasperMcp.Tests --filter "FullyQualifiedName~CasperClientFactoryTests"`
Expected: PASS.

- [ ] **Step 5: Commit**

```bash
git add src/CasperMcp/Remote/CasperClientFactory.cs tests/CasperMcp.Tests/CasperClientFactoryTests.cs
git commit -m "feat: add per-request CasperClientFactory over pooled HttpClient"
```

---

## Task 6: SecretRedaction (TDD)

**Files:**
- Create: `src/CasperMcp/Security/SecretRedaction.cs`
- Test: `tests/CasperMcp.Tests/SecretRedactionTests.cs`

- [ ] **Step 1: Write the failing test**

Create `tests/CasperMcp.Tests/SecretRedactionTests.cs`:

```csharp
using CasperMcp.Security;

namespace CasperMcp.Tests;

public class SecretRedactionTests
{
    [Theory]
    [InlineData("X-CSPR-Cloud-Api-Key")]
    [InlineData("authorization")]
    [InlineData("X-API-Key")]
    public void IsSensitive_KnownSecretHeaders_True(string name)
    {
        Assert.True(SecretRedaction.IsSensitiveHeader(name));
    }

    [Theory]
    [InlineData("X-Casper-Network")]
    [InlineData("Content-Type")]
    public void IsSensitive_NonSecretHeaders_False(string name)
    {
        Assert.False(SecretRedaction.IsSensitiveHeader(name));
    }

    [Fact]
    public void Redact_NonEmpty_ReturnsMask()
    {
        Assert.Equal("***", SecretRedaction.Redact("anything"));
    }

    [Fact]
    public void Redact_Empty_ReturnsEmpty()
    {
        Assert.Equal(string.Empty, SecretRedaction.Redact(""));
    }
}
```

- [ ] **Step 2: Run the test to verify it fails**

Run: `dotnet test tests/CasperMcp.Tests --filter "FullyQualifiedName~SecretRedactionTests"`
Expected: FAIL (compile error — `SecretRedaction` does not exist).

- [ ] **Step 3: Implement SecretRedaction**

Create `src/CasperMcp/Security/SecretRedaction.cs`:

```csharp
namespace CasperMcp.Security;

public static class SecretRedaction
{
    private static readonly HashSet<string> Sensitive = new(StringComparer.OrdinalIgnoreCase)
    {
        "X-CSPR-Cloud-Api-Key",
        "Authorization",
        "X-API-Key"
    };

    public static bool IsSensitiveHeader(string name) => Sensitive.Contains(name);

    public static string Redact(string value) => string.IsNullOrEmpty(value) ? string.Empty : "***";
}
```

- [ ] **Step 4: Run the test to verify it passes**

Run: `dotnet test tests/CasperMcp.Tests --filter "FullyQualifiedName~SecretRedactionTests"`
Expected: PASS.

- [ ] **Step 5: Commit**

```bash
git add src/CasperMcp/Security/SecretRedaction.cs tests/CasperMcp.Tests/SecretRedactionTests.cs
git commit -m "feat: add secret-header redaction helper"
```

---

## Task 7: UpstreamErrorMapper (TDD)

**Files:**
- Create: `src/CasperMcp/Remote/UpstreamErrorMapper.cs`
- Test: `tests/CasperMcp.Tests/UpstreamErrorMapperTests.cs`

- [ ] **Step 1: Write the failing test**

Create `tests/CasperMcp.Tests/UpstreamErrorMapperTests.cs`:

```csharp
using System.Net;
using CasperMcp.Remote;

namespace CasperMcp.Tests;

public class UpstreamErrorMapperTests
{
    [Theory]
    [InlineData(HttpStatusCode.Unauthorized)]
    [InlineData(HttpStatusCode.Forbidden)]
    public void Describe_AuthStatuses_MentionsCredential(HttpStatusCode code)
    {
        var ex = new HttpRequestException("boom", null, code);
        var msg = UpstreamErrorMapper.Describe(ex);
        Assert.Contains("CSPR.Cloud", msg);
        Assert.DoesNotContain("boom", msg); // raw upstream text not leaked
    }

    [Fact]
    public void Describe_TooManyRequests_MentionsRateLimit()
    {
        var ex = new HttpRequestException("x", null, HttpStatusCode.TooManyRequests);
        Assert.Contains("rate limit", UpstreamErrorMapper.Describe(ex), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Describe_ServerError_MentionsUnavailable()
    {
        var ex = new HttpRequestException("x", null, HttpStatusCode.BadGateway);
        Assert.Contains("unavailable", UpstreamErrorMapper.Describe(ex), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Describe_Timeout_MentionsTimedOut()
    {
        var ex = new TaskCanceledException("timeout");
        Assert.Contains("timed out", UpstreamErrorMapper.Describe(ex), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Describe_Generic_ReturnsGenericMessage()
    {
        var ex = new InvalidOperationException("internal detail with secret 12345");
        var msg = UpstreamErrorMapper.Describe(ex);
        Assert.DoesNotContain("12345", msg);
        Assert.Contains("request failed", msg, StringComparison.OrdinalIgnoreCase);
    }
}
```

- [ ] **Step 2: Run the test to verify it fails**

Run: `dotnet test tests/CasperMcp.Tests --filter "FullyQualifiedName~UpstreamErrorMapperTests"`
Expected: FAIL (compile error — `UpstreamErrorMapper` does not exist).

- [ ] **Step 3: Implement UpstreamErrorMapper**

Create `src/CasperMcp/Remote/UpstreamErrorMapper.cs`:

```csharp
using System.Net;

namespace CasperMcp.Remote;

/// <summary>
/// Maps exceptions raised while calling CSPR.Cloud into safe, agent-facing messages.
/// Never includes raw upstream text, stack traces, or secrets.
/// </summary>
public static class UpstreamErrorMapper
{
    public static string Describe(Exception ex)
    {
        switch (ex)
        {
            case OperationCanceledException:
            case TaskCanceledException:
                return "Upstream request timed out or was cancelled. Try again.";
            case HttpRequestException http when http.StatusCode is { } code:
                return DescribeStatus(code);
            default:
                return "The request failed due to an unexpected error.";
        }
    }

    private static string DescribeStatus(HttpStatusCode code) => code switch
    {
        HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden =>
            "Upstream authentication failed. Check your CSPR.Cloud API key and plan.",
        HttpStatusCode.TooManyRequests =>
            "Rate limited by CSPR.Cloud. Slow down and retry shortly.",
        >= (HttpStatusCode)500 =>
            "CSPR.Cloud is temporarily unavailable. Try again later.",
        HttpStatusCode.NotFound =>
            "The requested resource was not found on CSPR.Cloud.",
        _ => $"The request failed (upstream status {(int)code})."
    };
}
```

- [ ] **Step 4: Run the test to verify it passes**

Run: `dotnet test tests/CasperMcp.Tests --filter "FullyQualifiedName~UpstreamErrorMapperTests"`
Expected: PASS.

- [ ] **Step 5: Commit**

```bash
git add src/CasperMcp/Remote/UpstreamErrorMapper.cs tests/CasperMcp.Tests/UpstreamErrorMapperTests.cs
git commit -m "feat: add typed upstream error mapper"
```

---

## Task 8: RemoteRequestMiddleware — validate key presence + network (TDD)

**Files:**
- Create: `src/CasperMcp/Middleware/RemoteRequestMiddleware.cs`
- Test: `tests/CasperMcp.Tests/RemoteRequestMiddlewareTests.cs`

- [ ] **Step 1: Write the failing test**

Create `tests/CasperMcp.Tests/RemoteRequestMiddlewareTests.cs`:

```csharp
using CasperMcp.Middleware;
using Microsoft.AspNetCore.Http;

namespace CasperMcp.Tests;

public class RemoteRequestMiddlewareTests
{
    private static (RemoteRequestMiddleware mw, Func<bool> nextCalled) Build()
    {
        var called = false;
        var mw = new RemoteRequestMiddleware(_ => { called = true; return Task.CompletedTask; });
        return (mw, () => called);
    }

    private static DefaultHttpContext Ctx(string path, params (string, string)[] headers)
    {
        var ctx = new DefaultHttpContext();
        ctx.Request.Path = path;
        foreach (var (k, v) in headers) ctx.Request.Headers[k] = v;
        return ctx;
    }

    [Fact]
    public async Task MissingCsprKey_Returns401_DoesNotCallNext()
    {
        var (mw, nextCalled) = Build();
        var ctx = Ctx("/mcp");
        await mw.InvokeAsync(ctx);
        Assert.Equal(401, ctx.Response.StatusCode);
        Assert.False(nextCalled());
    }

    [Fact]
    public async Task InvalidNetwork_Returns400_DoesNotCallNext()
    {
        var (mw, nextCalled) = Build();
        var ctx = Ctx("/mcp", ("X-CSPR-Cloud-Api-Key", "k"), ("X-Casper-Network", "devnet"));
        await mw.InvokeAsync(ctx);
        Assert.Equal(400, ctx.Response.StatusCode);
        Assert.False(nextCalled());
    }

    [Fact]
    public async Task ValidRequest_CallsNext()
    {
        var (mw, nextCalled) = Build();
        var ctx = Ctx("/mcp", ("X-CSPR-Cloud-Api-Key", "k"), ("X-Casper-Network", "testnet"));
        await mw.InvokeAsync(ctx);
        Assert.True(nextCalled());
    }

    [Fact]
    public async Task HealthPath_BypassesValidation()
    {
        var (mw, nextCalled) = Build();
        var ctx = Ctx("/health");
        await mw.InvokeAsync(ctx);
        Assert.True(nextCalled());
    }

    [Fact]
    public async Task ReadyPath_BypassesValidation()
    {
        var (mw, nextCalled) = Build();
        var ctx = Ctx("/ready");
        await mw.InvokeAsync(ctx);
        Assert.True(nextCalled());
    }
}
```

- [ ] **Step 2: Run the test to verify it fails**

Run: `dotnet test tests/CasperMcp.Tests --filter "FullyQualifiedName~RemoteRequestMiddlewareTests"`
Expected: FAIL (compile error — `RemoteRequestMiddleware` does not exist).

- [ ] **Step 3: Implement the middleware**

Create `src/CasperMcp/Middleware/RemoteRequestMiddleware.cs`:

```csharp
using System.Net;
using CasperMcp.Remote;
using Microsoft.AspNetCore.Http;

namespace CasperMcp.Middleware;

/// <summary>
/// For remote (http) mode: requires the per-agent CSPR key header and validates the optional
/// network header, before any tool dispatch. Health/readiness probes bypass validation.
/// </summary>
public class RemoteRequestMiddleware
{
    private readonly RequestDelegate _next;

    public RemoteRequestMiddleware(RequestDelegate next) => _next = next;

    public async Task InvokeAsync(HttpContext context)
    {
        var path = context.Request.Path;
        if (path.StartsWithSegments("/health") || path.StartsWithSegments("/ready"))
        {
            await _next(context);
            return;
        }

        if (!RemoteHeaders.TryGetCsprKey(context.Request.Headers, out _))
        {
            await WriteError(context, HttpStatusCode.Unauthorized,
                $"Missing required {RemoteHeaders.CsprKeyHeader} header.");
            return;
        }

        if (!RemoteHeaders.TryResolveNetwork(context.Request.Headers, "mainnet", out _))
        {
            await WriteError(context, HttpStatusCode.BadRequest,
                $"Invalid {RemoteHeaders.NetworkHeader}; expected 'mainnet' or 'testnet'.");
            return;
        }

        await _next(context);
    }

    private static async Task WriteError(HttpContext context, HttpStatusCode status, string message)
    {
        context.Response.StatusCode = (int)status;
        context.Response.ContentType = "application/json";
        await context.Response.WriteAsync($"{{\"error\":\"{message}\"}}");
    }
}
```

- [ ] **Step 4: Run the test to verify it passes**

Run: `dotnet test tests/CasperMcp.Tests --filter "FullyQualifiedName~RemoteRequestMiddlewareTests"`
Expected: PASS.

- [ ] **Step 5: Commit**

```bash
git add src/CasperMcp/Middleware/RemoteRequestMiddleware.cs tests/CasperMcp.Tests/RemoteRequestMiddlewareTests.cs
git commit -m "feat: add remote request validation middleware"
```

---

## Task 9: ApiKeyAuthMiddleware — constant-time apikey auth (TDD)

**Files:**
- Modify: `src/CasperMcp/Middleware/ApiKeyAuthMiddleware.cs`
- Test: `tests/CasperMcp.Tests/ApiKeyAuthMiddlewareTests.cs`

- [ ] **Step 1: Write the failing test**

Create `tests/CasperMcp.Tests/ApiKeyAuthMiddlewareTests.cs`:

```csharp
using CasperMcp.Middleware;
using Microsoft.AspNetCore.Http;

namespace CasperMcp.Tests;

public class ApiKeyAuthMiddlewareTests
{
    private static (ApiKeyAuthMiddleware mw, Func<bool> nextCalled) Build(string expected)
    {
        var called = false;
        var mw = new ApiKeyAuthMiddleware(_ => { called = true; return Task.CompletedTask; }, expected);
        return (mw, () => called);
    }

    private static DefaultHttpContext Ctx(string path, params (string, string)[] headers)
    {
        var ctx = new DefaultHttpContext();
        ctx.Request.Path = path;
        foreach (var (k, v) in headers) ctx.Request.Headers[k] = v;
        return ctx;
    }

    [Fact]
    public async Task ValidBearer_CallsNext()
    {
        var (mw, nextCalled) = Build("secret");
        var ctx = Ctx("/mcp", ("Authorization", "Bearer secret"));
        await mw.InvokeAsync(ctx);
        Assert.True(nextCalled());
    }

    [Fact]
    public async Task ValidXApiKey_CallsNext()
    {
        var (mw, nextCalled) = Build("secret");
        var ctx = Ctx("/mcp", ("X-API-Key", "secret"));
        await mw.InvokeAsync(ctx);
        Assert.True(nextCalled());
    }

    [Fact]
    public async Task WrongSecret_Returns401()
    {
        var (mw, nextCalled) = Build("secret");
        var ctx = Ctx("/mcp", ("X-API-Key", "nope"));
        await mw.InvokeAsync(ctx);
        Assert.Equal(401, ctx.Response.StatusCode);
        Assert.False(nextCalled());
    }

    [Fact]
    public async Task HealthPath_BypassesAuth()
    {
        var (mw, nextCalled) = Build("secret");
        var ctx = Ctx("/health");
        await mw.InvokeAsync(ctx);
        Assert.True(nextCalled());
    }
}
```

- [ ] **Step 2: Run the test to verify it fails**

Run: `dotnet test tests/CasperMcp.Tests --filter "FullyQualifiedName~ApiKeyAuthMiddlewareTests"`
Expected: FAIL — current middleware does not accept `Authorization: Bearer` and uses `api_key` query, not the asserted behavior.

- [ ] **Step 3: Replace the middleware implementation**

Replace the entire contents of `src/CasperMcp/Middleware/ApiKeyAuthMiddleware.cs` with:

```csharp
using System.Net;
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Http;

namespace CasperMcp.Middleware;

/// <summary>
/// Shared-secret auth for `--auth-mode apikey`. Accepts the secret via
/// `Authorization: Bearer &lt;secret&gt;` or `X-API-Key`. Distinct from the per-agent CSPR key.
/// </summary>
public class ApiKeyAuthMiddleware
{
    private const string ApiKeyHeaderName = "X-API-Key";

    private readonly RequestDelegate _next;
    private readonly byte[] _expected;

    public ApiKeyAuthMiddleware(RequestDelegate next, string expectedApiKey)
    {
        _next = next;
        _expected = Encoding.UTF8.GetBytes(expectedApiKey);
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var path = context.Request.Path;
        if (path.StartsWithSegments("/health") || path.StartsWithSegments("/ready"))
        {
            await _next(context);
            return;
        }

        var presented = ExtractKey(context.Request);
        if (presented is null || !FixedTimeEquals(presented))
        {
            context.Response.StatusCode = (int)HttpStatusCode.Unauthorized;
            context.Response.ContentType = "application/json";
            await context.Response.WriteAsync("""{"error":"Unauthorized."}""");
            return;
        }

        await _next(context);
    }

    private static string? ExtractKey(HttpRequest request)
    {
        var auth = request.Headers.Authorization.ToString();
        if (auth.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
            return auth["Bearer ".Length..].Trim();

        var header = request.Headers[ApiKeyHeaderName].ToString();
        return string.IsNullOrEmpty(header) ? null : header;
    }

    private bool FixedTimeEquals(string presented) =>
        CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(presented), _expected);
}
```

- [ ] **Step 4: Run the test to verify it passes**

Run: `dotnet test tests/CasperMcp.Tests --filter "FullyQualifiedName~ApiKeyAuthMiddlewareTests"`
Expected: PASS.

- [ ] **Step 5: Commit**

```bash
git add src/CasperMcp/Middleware/ApiKeyAuthMiddleware.cs tests/CasperMcp.Tests/ApiKeyAuthMiddlewareTests.cs
git commit -m "feat: constant-time bearer/X-API-Key auth middleware"
```

---

## Task 10: ToolErrorFilter and protected-resource metadata

These have no pure-unit surface beyond what Task 7 covers; they are wired and verified in the host tasks. Create them now so Task 11 can reference them.

**Files:**
- Create: `src/CasperMcp/Remote/ToolErrorFilter.cs`
- Create: `src/CasperMcp/Remote/ProtectedResourceMetadata.cs`

- [ ] **Step 1: Implement the CallTool filter**

Create `src/CasperMcp/Remote/ToolErrorFilter.cs`:

```csharp
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
                    logger?.LogError(ex, "Tool call failed. CorrelationId={CorrelationId}", correlationId);

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
```

> If the SDK's filter context member names differ (`context.Services`, `next(context, cancellationToken)`), adjust to match the installed `ModelContextProtocol` version — the shape is per the SDK's `AddCallToolFilter` docs. Verify against the package by inspecting `IMcpServerBuilder.WithRequestFilters`.

- [ ] **Step 2: Implement protected-resource metadata**

Create `src/CasperMcp/Remote/ProtectedResourceMetadata.cs`:

```csharp
namespace CasperMcp.Remote;

/// <summary>
/// Minimal OAuth 2.0 Protected Resource Metadata (RFC 9728) served at
/// /.well-known/oauth-protected-resource in jwt auth mode.
/// </summary>
public static class ProtectedResourceMetadata
{
    public static object Build(string resource, string authority) => new
    {
        resource,
        authorization_servers = new[] { authority },
        bearer_methods_supported = new[] { "header" }
    };
}
```

- [ ] **Step 3: Build**

Run: `dotnet build casper-mcp.slnx`
Expected: Build succeeds.

- [ ] **Step 4: Commit**

```bash
git add src/CasperMcp/Remote/ToolErrorFilter.cs src/CasperMcp/Remote/ProtectedResourceMetadata.cs
git commit -m "feat: add CallTool error filter and protected-resource metadata"
```

---

## Task 11: Rebuild Program.cs — http profile host

This is the integration of all prior units. There is no unit test; verification is build + manual smoke (Task 13 covers a scripted smoke run).

**Files:**
- Modify: `src/CasperMcp/Program.cs`

- [ ] **Step 1: Replace the post-config section with both transport branches**

Replace everything in `src/CasperMcp/Program.cs` AFTER the argument/env parsing block (i.e. replace the temporary `if (config.IsHttp) { ... } else stdio ...` from Task 2) with the following. Keep the `using` directives at the top; ensure these are present: `CasperMcp.Configuration`, `CasperMcp.Middleware`, `CasperMcp.Remote`, `CSPR.Cloud.Net.Clients`, `CSPR.Cloud.Net.Objects.Config`, `Microsoft.AspNetCore.Authentication.JwtBearer`, `Microsoft.AspNetCore.Builder`, `Microsoft.AspNetCore.Http`, `Microsoft.Extensions.DependencyInjection`, `Microsoft.Extensions.Hosting`, `Microsoft.Extensions.Logging`, `System.Net.Http`.

```csharp
if (config.IsHttp)
{
    var builder = WebApplication.CreateBuilder(args);

    builder.Logging.ClearProviders();
    builder.Logging.AddJsonConsole();
    builder.Logging.SetMinimumLevel(LogLevel.Information);

    builder.Services.AddSingleton(config);
    builder.Services.AddHttpContextAccessor();

    // Shared, pooled connection handler for all tenants.
    builder.Services.AddHttpClient("cspr")
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
        var loggerFactory = sp.GetRequiredService<ILoggerFactory>();
        return CasperClientFactory.Create(key, factory.CreateClient("cspr"), loggerFactory);
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
    ToolErrorFilter.Add(mcpBuilder);

    builder.WebHost.UseUrls($"http://0.0.0.0:{config.Port}");

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

    Console.WriteLine($"Casper MCP (http) on http://0.0.0.0:{config.Port}{config.McpPath} | auth={config.AuthMode} | default-network={config.DefaultNetwork}");
    await app.RunAsync();
    return 0;
}

// ---- stdio profile ----
if (string.IsNullOrEmpty(config.StdioApiKey))
{
    Console.Error.WriteLine("Error: API key is required in stdio mode. Provide via --api-key or CSPR_CLOUD_API_KEY.");
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
hostBuilder.Services.AddMcpServer().WithStdioServerTransport().WithToolsFromAssembly();

await hostBuilder.Build().RunAsync();
return 0;
```

- [ ] **Step 2: Build**

Run: `dotnet build casper-mcp.slnx`
Expected: Build succeeds. If `AddJsonConsole`, `MapMcp(string)`, `WithRequestFilters`, or `o.Stateless` are not found, reconcile against the installed `ModelContextProtocol(.AspNetCore)` version (these are the documented APIs; method names may need the correct namespace `using`).

- [ ] **Step 3: Manual smoke — health, readiness, and 401 without key**

Run (PowerShell), in one terminal:
`dotnet run --project src/CasperMcp -- --transport http --port 3001`

In another terminal:
```powershell
curl.exe -s http://localhost:3001/health
curl.exe -s http://localhost:3001/ready
curl.exe -s -o NUL -w "%{http_code}" -X POST http://localhost:3001/mcp
```
Expected: `/health` → `{"status":"healthy",...}`; `/ready` → `{"status":"ready"}`; the keyless `POST /mcp` → `401`. Stop the server (Ctrl+C).

- [ ] **Step 4: Commit**

```bash
git add src/CasperMcp/Program.cs
git commit -m "feat: stateless http profile with per-agent key, pooling, pluggable auth"
```

---

## Task 12: Route tool error formatting through UpstreamErrorMapper

Tools currently swallow exceptions and return `"Error ...: {ex.Message}"`, which would otherwise leak raw text and bypass the central mapper. Replace those inline messages with `UpstreamErrorMapper.Describe(ex)` so both stdio (direct return) and http (string is returned, not thrown) paths produce safe, typed text. (The `ToolErrorFilter` remains the outer net for anything uncaught.)

**Files:**
- Modify: all `src/CasperMcp/Tools/*.cs` that contain `catch (Exception ex)`.
- Possibly modify: `tests/CasperMcp.Tests/IntegrationTests.cs` (see Step 4).

- [ ] **Step 1: Inventory the catch sites**

Run: `grep -rn "catch (Exception ex)" src/CasperMcp/Tools`
Expected: a list across ~16 tool files. Note the count.

- [ ] **Step 2: Replace each catch body**

In every `src/CasperMcp/Tools/*.cs`, change each occurrence of the pattern:

```csharp
        catch (Exception ex)
        {
            return $"Error <something>: {ex.Message}";
        }
```

to:

```csharp
        catch (Exception ex)
        {
            return CasperMcp.Remote.UpstreamErrorMapper.Describe(ex);
        }
```

(The exact text after `Error` varies per method; replace the whole `return $"...{ex.Message}";` line with the `Describe` call. Leave the `try` body and all non-exception `return "... not found ..."` strings unchanged.)

- [ ] **Step 3: Verify no raw exception text remains**

Run: `grep -rn "ex.Message" src/CasperMcp/Tools`
Expected: No matches.

- [ ] **Step 4: Reconcile the one error-asserting integration test**

`IntegrationTests.GetBlock_WithInvalidHash_ReturnsError` asserts the result contains `"not found"` (case-insensitive) OR `"Error"` (case-insensitive). `UpstreamErrorMapper.Describe` does not contain the word "error" for a 404 ("The requested resource was not found on CSPR.Cloud." — contains "not found"). No change needed if the upstream returns 404/empty. If, when run live, the call instead throws a non-404 mapped message, update that test's assertion to also accept `result.Contains("failed", StringComparison.OrdinalIgnoreCase)`. Document the change in the commit if made.

- [ ] **Step 5: Build and run offline unit tests**

Run: `dotnet test tests/CasperMcp.Tests --filter "FullyQualifiedName!~IntegrationTests"`
Expected: Build succeeds; all non-integration tests PASS.

- [ ] **Step 6: Commit**

```bash
git add src/CasperMcp/Tools tests/CasperMcp.Tests/IntegrationTests.cs
git commit -m "refactor: centralize tool error formatting via UpstreamErrorMapper"
```

---

## Task 13: End-to-end smoke for both profiles with a real key

Validates the full request path (per-agent key resolution, network header, a real tool call) and that stdio still works.

**Files:** none (manual verification + recorded result).

- [ ] **Step 1: stdio still serves tools**

Run: `dotnet run --project src/CasperMcp -- --api-key $env:CSPR_CLOUD_TESTNET_API_KEY --network testnet`
Then send an MCP `initialize`+`tools/list` via your MCP client (or stop after confirming it starts and logs no errors). Expected: server starts, `tools/list` returns ~70 tools, no `Watch*` tools present. Stop it.

- [ ] **Step 2: http profile end-to-end tool call**

Start: `dotnet run --project src/CasperMcp -- --transport http --port 3001`
Issue a Streamable HTTP `tools/call` for `GetNetworkStatus` to `POST http://localhost:3001/mcp` with headers `X-CSPR-Cloud-Api-Key: <testnet key>` and `X-Casper-Network: testnet` (use your MCP client or the SDK test client). Expected: a successful result containing "Casper Network Status". Repeat without the key header → `401`. Repeat with `X-Casper-Network: devnet` → `400`.

- [ ] **Step 3: Record outcome**

Note pass/fail for each check in the task tracker. If any fail, debug before proceeding (do not mark complete on unverified claims).

---

## Task 14: Update docs and ops artifacts

**Files:**
- Modify: `README.md`, `mcp.json`, `docker-compose.yml`, `Dockerfile`

- [ ] **Step 1: README — transport, endpoints, headers, auth, removed features**

Update `README.md`:
- Replace all `--transport sse` with `--transport http`; replace the `/sse` endpoint and SSE client config snippets with Streamable HTTP at `/mcp`.
- Document remote headers: `X-CSPR-Cloud-Api-Key` (required), `X-Casper-Network` (optional).
- Document `--auth-mode none|apikey|jwt` and the `--auth-*` flags / `CASPER_MCP_AUTH_*` envs; explain `none` trusts a fronting WAF.
- Document `/health` (liveness) and `/ready` (readiness), and (jwt) `/.well-known/oauth-protected-resource`.
- Remove the `Watch*` tools from any tool listing; update the tool count to the new number from Task 13 Step 1.
- Note v3.0.0 breaking changes (no `/sse`, per-request key required, `Watch*` removed).

- [ ] **Step 2: docker-compose — http command + env**

Replace `command:` and `environment:` in `docker-compose.yml` with:

```yaml
    environment:
      - CASPER_MCP_AUTH_MODE=${CASPER_MCP_AUTH_MODE:-none}
      - CASPER_MCP_AUTH_API_KEY=${CASPER_MCP_AUTH_API_KEY:-}
      - CASPER_MCP_AUTH_JWT_AUTHORITY=${CASPER_MCP_AUTH_JWT_AUTHORITY:-}
      - CASPER_MCP_AUTH_JWT_AUDIENCE=${CASPER_MCP_AUTH_JWT_AUDIENCE:-}
    command: ["--transport", "http", "--port", "3001"]
```

(Remove the global `CSPR_CLOUD_API_KEY` — it is not used in http mode.)

- [ ] **Step 3: Dockerfile/healthcheck — point at /health**

Confirm any healthcheck uses `http://localhost:3001/health` (already correct in compose). No image change needed unless the Dockerfile hardcodes `--transport sse`; if so, update to `http`.

- [ ] **Step 4: mcp.json — refresh tool list/version**

Regenerate or hand-edit `mcp.json` to drop the `Watch*` tools and reflect any version bump.

- [ ] **Step 5: Bump version to 3.0.0**

In `src/CasperMcp/CasperMcp.csproj`, set `<Version>3.0.0</Version>`. Add a `CHANGELOG.md` 3.0.0 entry summarizing the remote redesign and breaking changes.

- [ ] **Step 6: Commit**

```bash
git add README.md mcp.json docker-compose.yml Dockerfile src/CasperMcp/CasperMcp.csproj CHANGELOG.md
git commit -m "docs: document remote http profile, auth modes, and v3.0.0 breaking changes"
```

---

## Task 15: Full verification pass

**Files:** none.

- [ ] **Step 1: Clean build**

Run: `dotnet build casper-mcp.slnx -c Release`
Expected: Build succeeds with no warnings about unused removed symbols.

- [ ] **Step 2: Offline test suite**

Run: `dotnet test tests/CasperMcp.Tests --filter "FullyQualifiedName!~IntegrationTests"`
Expected: All unit/middleware tests PASS.

- [ ] **Step 3: Integration suite (live testnet)**

Run: `dotnet test tests/CasperMcp.Tests --filter "FullyQualifiedName~IntegrationTests"` with `CSPR_CLOUD_TESTNET_API_KEY` set.
Expected: PASS (tools still work via direct calls; the throttle remains in place).

- [ ] **Step 4: Confirm the Watch* tools are gone end-to-end**

Run: `grep -rn "Watch" src/CasperMcp/Tools`
Expected: No matches.

- [ ] **Step 5: Final commit / branch ready for PR**

```bash
git status
git log --oneline -15
```
Expected: clean tree; the branch `feat/remote-multi-tenant-mcp` contains the full series. Open a PR when ready.

---

## Self-Review

**Spec coverage:**
- Stateless Streamable HTTP at `/mcp` — Task 11.
- Per-agent `X-CSPR-Cloud-Api-Key`, 401 if missing — Tasks 4, 8, 11.
- Request-scoped client + options, no tool signature changes — Task 11 (scoped registrations); tools untouched except catch bodies (Task 12).
- `IHttpClientFactory` pooling, fresh client per request, no cache — Tasks 5, 11.
- Auth `none`/`apikey`/`jwt` + protected-resource metadata — Tasks 9, 10, 11.
- Per-request network via `X-Casper-Network` — Tasks 4, 8, 11.
- Typed upstream error mapping — Tasks 7, 10, 12.
- Structured logging + secret redaction — Tasks 6, 10, 11 (`AddJsonConsole` + redaction helper + correlation id).
- Liveness/readiness split — Task 11.
- Streaming tools removed — Task 3.
- stdio preserved — Tasks 2, 11.
- Breaking changes / 3.0.0 — Task 14.

**Placeholder scan:** No "TBD"/"handle errors"/"similar to". Each code step shows full code. The one mechanical multi-file change (Task 12) gives the exact before/after pattern plus a `grep` verification rather than 16 duplicated blocks.

**Type consistency:** `ServerConfig`, `AuthMode`, `CasperMcpOptions { Network, IsTestnet }`, `RemoteHeaders.{CsprKeyHeader, NetworkHeader, TryGetCsprKey, TryResolveNetwork}`, `CasperClientFactory.Create`, `UpstreamErrorMapper.Describe`, `SecretRedaction.{IsSensitiveHeader, Redact}`, `RemoteRequestMiddleware`, `ApiKeyAuthMiddleware(next, expectedApiKey)`, `ToolErrorFilter.Add`, `ProtectedResourceMetadata.Build` are used consistently across tasks.

**Known reconciliation points (flagged inline for the implementer):** SDK method names (`WithRequestFilters`/`AddCallToolFilter` context shape, `MapMcp(path)`, `WithHttpTransport(o => o.Stateless)`), the JwtBearer package version for net10, and the `AddJsonConsole` namespace — all standard APIs, verified against docs, but pinned to whatever the installed package versions expose.
