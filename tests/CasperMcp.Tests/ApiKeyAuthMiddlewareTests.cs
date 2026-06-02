using System.Text.Json;
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

    [Fact]
    public async Task ReadyPath_BypassesAuth()
    {
        var (mw, nextCalled) = Build("secret");
        var ctx = Ctx("/ready");
        await mw.InvokeAsync(ctx);
        Assert.True(nextCalled());
    }

    // ---- JSON-RPC error envelope on the MCP path (same fix as the per-agent key gate) ----

    [Fact]
    public async Task WrongSecret_OnMcpPath_WritesJsonRpcError_WithEchoedId()
    {
        var (mw, _) = Build("secret");
        var ctx = MiddlewareTestContext.WithBody(
            "/mcp", "POST", """{"jsonrpc":"2.0","id":9,"method":"initialize"}""",
            ("X-API-Key", "nope"));

        await mw.InvokeAsync(ctx);

        Assert.Equal(401, ctx.Response.StatusCode);
        using var doc = await MiddlewareTestContext.ReadJson(ctx);
        Assert.Equal("2.0", doc.RootElement.GetProperty("jsonrpc").GetString());
        Assert.Equal(-32600, doc.RootElement.GetProperty("error").GetProperty("code").GetInt32());
        Assert.Equal("Unauthorized.", doc.RootElement.GetProperty("error").GetProperty("message").GetString());
        Assert.Equal(9, doc.RootElement.GetProperty("id").GetInt32());
    }

    [Fact]
    public async Task WrongSecret_OnNonMcpPath_WritesPlainError()
    {
        var (mw, _) = Build("secret");
        var ctx = MiddlewareTestContext.WithBody("/foo", "POST", null, ("X-API-Key", "nope"));

        await mw.InvokeAsync(ctx);

        Assert.Equal(401, ctx.Response.StatusCode);
        using var doc = await MiddlewareTestContext.ReadJson(ctx);
        Assert.Equal(JsonValueKind.String, doc.RootElement.GetProperty("error").ValueKind);
    }
}
