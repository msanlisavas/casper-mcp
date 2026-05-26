using System.Net;
using CasperMcp.Remote;
using CSPR.Cloud.Net.Errors;

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

    // --- CSPR.Cloud.Net typed exceptions ---

    [Fact]
    public void Describe_NotFoundException_MentionsNotFound()
    {
        var ex = new NotFoundException("Not Found Error: no such block", null);
        Assert.Contains("not found", UpstreamErrorMapper.Describe(ex), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Describe_UnauthorizedException_MentionsAuth_DoesNotEchoBody()
    {
        var ex = new UnauthorizedException("Unauthorized Error: bad-key-detail", null);
        var msg = UpstreamErrorMapper.Describe(ex);
        Assert.Contains("authentication failed", msg, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("bad-key-detail", msg);
    }

    [Fact]
    public void Describe_AccessDeniedException_MentionsAuth()
    {
        var ex = new AccessDeniedException("Access Denied Error: plan", null);
        Assert.Contains("authentication failed", UpstreamErrorMapper.Describe(ex), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Describe_InvalidParamException_SurfacesDetail()
    {
        var ex = new InvalidParamException("Invalid Param Error: public_key is required", null);
        var msg = UpstreamErrorMapper.Describe(ex);
        Assert.Contains("parameters", msg, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("public_key is required", msg);
    }

    [Fact]
    public void Describe_InternalServerErrorException_MentionsUnavailable()
    {
        var ex = new InternalServerErrorException("Internal Server Error: boom", null);
        Assert.Contains("unavailable", UpstreamErrorMapper.Describe(ex), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Describe_DuplicateEntryException_MentionsConflict()
    {
        var ex = new DuplicateEntryException("Duplicate Entry Error: dup", null);
        var msg = UpstreamErrorMapper.Describe(ex);
        Assert.True(msg.Contains("duplicate", StringComparison.OrdinalIgnoreCase)
                    || msg.Contains("conflict", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Describe_RateLimitException_MentionsRateLimit()
    {
        // The SDK throws RateLimitException on HTTP 429 (not HttpRequestException), so the mapper
        // must handle the real type, not just the status-code fallback.
        var ex = new RateLimitException("Rate Limit Error: slow down", null);
        Assert.Contains("rate limit", UpstreamErrorMapper.Describe(ex), StringComparison.OrdinalIgnoreCase);
    }
}
