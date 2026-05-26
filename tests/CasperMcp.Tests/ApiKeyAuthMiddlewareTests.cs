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
}
