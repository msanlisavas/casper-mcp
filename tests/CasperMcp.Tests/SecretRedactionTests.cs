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
