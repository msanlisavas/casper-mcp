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
