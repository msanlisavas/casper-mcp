using System.Text.Json;
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

    // ---- JSON-RPC error envelope on the MCP path (so clients surface a real message) ----

    [Fact]
    public async Task MissingCsprKey_OnMcpPath_WritesJsonRpcError_WithEchoedId()
    {
        var (mw, _) = Build();
        var ctx = MiddlewareTestContext.WithBody(
            "/mcp", "POST", """{"jsonrpc":"2.0","id":42,"method":"initialize"}""");

        await mw.InvokeAsync(ctx);

        Assert.Equal(401, ctx.Response.StatusCode);
        using var doc = await MiddlewareTestContext.ReadJson(ctx);
        var root = doc.RootElement;
        Assert.Equal("2.0", root.GetProperty("jsonrpc").GetString());
        Assert.Equal(-32600, root.GetProperty("error").GetProperty("code").GetInt32());
        // The message must be self-explanatory enough for an agent to guide the user:
        // the header that's missing, the env var to map from, and that a restart is needed.
        var message = root.GetProperty("error").GetProperty("message").GetString();
        Assert.Contains("X-CSPR-Cloud-Api-Key", message);
        Assert.Contains("CSPR_CLOUD_API_KEY", message);
        Assert.Contains("environment variable", message, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(42, root.GetProperty("id").GetInt32());
    }

    [Fact]
    public async Task InvalidNetwork_OnMcpPath_WritesJsonRpcError_InvalidParams()
    {
        var (mw, _) = Build();
        var ctx = MiddlewareTestContext.WithBody(
            "/mcp", "POST", """{"jsonrpc":"2.0","id":7,"method":"tools/list"}""",
            ("X-CSPR-Cloud-Api-Key", "k"), ("X-Casper-Network", "devnet"));

        await mw.InvokeAsync(ctx);

        Assert.Equal(400, ctx.Response.StatusCode);
        using var doc = await MiddlewareTestContext.ReadJson(ctx);
        Assert.Equal(-32602, doc.RootElement.GetProperty("error").GetProperty("code").GetInt32());
        Assert.Equal(7, doc.RootElement.GetProperty("id").GetInt32());
    }

    [Fact]
    public async Task MissingCsprKey_OnMcpPath_GetWithoutBody_WritesNullId()
    {
        var (mw, _) = Build();
        var ctx = MiddlewareTestContext.WithBody("/mcp", "GET", null);

        await mw.InvokeAsync(ctx);

        Assert.Equal(401, ctx.Response.StatusCode);
        using var doc = await MiddlewareTestContext.ReadJson(ctx);
        Assert.Equal("2.0", doc.RootElement.GetProperty("jsonrpc").GetString());
        Assert.Equal(JsonValueKind.Null, doc.RootElement.GetProperty("id").ValueKind);
    }

    [Fact]
    public async Task MissingCsprKey_OnNonMcpPath_WritesPlainError()
    {
        var (mw, _) = Build();
        var ctx = MiddlewareTestContext.WithBody(
            "/foo", "POST", """{"jsonrpc":"2.0","id":1,"method":"x"}""");

        await mw.InvokeAsync(ctx);

        Assert.Equal(401, ctx.Response.StatusCode);
        using var doc = await MiddlewareTestContext.ReadJson(ctx);
        Assert.False(doc.RootElement.TryGetProperty("jsonrpc", out _));
        Assert.Equal(JsonValueKind.String, doc.RootElement.GetProperty("error").ValueKind);
    }
}
