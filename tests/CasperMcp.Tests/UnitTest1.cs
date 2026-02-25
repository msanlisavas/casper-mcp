using CasperMcp.Configuration;
using CasperMcp.Helpers;

namespace CasperMcp.Tests;

public class FormattingHelpersTests
{
    [Theory]
    [InlineData(1_000_000_000UL, "1.000000000 CSPR")]
    [InlineData(0UL, "0.000000000 CSPR")]
    [InlineData(500_000_000UL, "0.500000000 CSPR")]
    [InlineData(1_500_000_000_000UL, "1,500.000000000 CSPR")]
    public void MotesToCspr_UlongValue_FormatsCorrectly(ulong motes, string expected)
    {
        var result = FormattingHelpers.MotesToCspr(motes);
        Assert.Equal(expected, result);
    }

    [Fact]
    public void MotesToCspr_NullUlong_ReturnsNA()
    {
        var result = FormattingHelpers.MotesToCspr((ulong?)null);
        Assert.Equal("N/A", result);
    }

    [Theory]
    [InlineData("1000000000", "1.000000000 CSPR")]
    [InlineData("0", "0.000000000 CSPR")]
    [InlineData("500000000", "0.500000000 CSPR")]
    public void MotesToCspr_StringValue_FormatsCorrectly(string motes, string expected)
    {
        var result = FormattingHelpers.MotesToCspr(motes);
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("not_a_number")]
    public void MotesToCspr_InvalidString_ReturnsNA(string? motes)
    {
        var result = FormattingHelpers.MotesToCspr(motes);
        Assert.Equal("N/A", result);
    }

    [Fact]
    public void FormatTimestamp_ValidDate_FormatsCorrectly()
    {
        var date = new DateTime(2024, 1, 15, 10, 30, 45, DateTimeKind.Utc);
        var result = FormattingHelpers.FormatTimestamp(date);
        Assert.Equal("2024-01-15 10:30:45 UTC", result);
    }

    [Fact]
    public void FormatTimestamp_Null_ReturnsNA()
    {
        var result = FormattingHelpers.FormatTimestamp(null);
        Assert.Equal("N/A", result);
    }

    [Theory]
    [InlineData(5.55f, "5.55%")]
    [InlineData(100f, "100.00%")]
    [InlineData(0f, "0.00%")]
    public void FormatPercentage_ValidValue_FormatsCorrectly(float value, string expected)
    {
        var result = FormattingHelpers.FormatPercentage(value);
        Assert.Equal(expected, result);
    }

    [Fact]
    public void FormatPercentage_Null_ReturnsNA()
    {
        var result = FormattingHelpers.FormatPercentage(null);
        Assert.Equal("N/A", result);
    }

    [Fact]
    public void FormatHash_ValidHash_ReturnsSame()
    {
        var hash = "01abc123def456";
        Assert.Equal(hash, FormattingHelpers.FormatHash(hash));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void FormatHash_NullOrEmpty_ReturnsNA(string? hash)
    {
        Assert.Equal("N/A", FormattingHelpers.FormatHash(hash));
    }

    [Fact]
    public void FormatBool_True_ReturnsYes()
    {
        Assert.Equal("Yes", FormattingHelpers.FormatBool(true));
    }

    [Fact]
    public void FormatBool_False_ReturnsNo()
    {
        Assert.Equal("No", FormattingHelpers.FormatBool(false));
    }

    [Fact]
    public void FormatNumber_UlongValue_FormatsWithCommas()
    {
        var result = FormattingHelpers.FormatNumber((ulong?)1000000);
        Assert.Equal("1,000,000", result);
    }

    [Fact]
    public void FormatNumber_NullUlong_ReturnsNA()
    {
        var result = FormattingHelpers.FormatNumber((ulong?)null);
        Assert.Equal("N/A", result);
    }
}

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
    public void DefaultApiKey_IsEmpty()
    {
        var options = new CasperMcpOptions();
        Assert.Equal(string.Empty, options.ApiKey);
    }

    [Fact]
    public void DefaultTransport_IsStdio()
    {
        var options = new CasperMcpOptions();
        Assert.Equal("stdio", options.Transport);
        Assert.False(options.IsSseTransport);
    }

    [Fact]
    public void IsSseTransport_SseValue_ReturnsTrue()
    {
        var options = new CasperMcpOptions { Transport = "sse" };
        Assert.True(options.IsSseTransport);
    }

    [Fact]
    public void IsSseTransport_CaseInsensitive()
    {
        var options = new CasperMcpOptions { Transport = "SSE" };
        Assert.True(options.IsSseTransport);
    }

    [Fact]
    public void DefaultPort_Is3001()
    {
        var options = new CasperMcpOptions();
        Assert.Equal(3001, options.Port);
    }

    [Fact]
    public void DefaultServerApiKey_IsEmpty()
    {
        var options = new CasperMcpOptions();
        Assert.Equal(string.Empty, options.ServerApiKey);
    }
}
