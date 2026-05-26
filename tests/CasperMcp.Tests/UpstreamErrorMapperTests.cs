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
