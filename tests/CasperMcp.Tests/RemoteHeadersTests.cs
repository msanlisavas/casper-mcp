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
