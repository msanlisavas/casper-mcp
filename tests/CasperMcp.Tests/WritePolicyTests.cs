using CasperMcp.Writes;

namespace CasperMcp.Tests;

public class WritePolicyTests
{
    [Fact]
    public void Default_Is_Strict_Fail_Closed()
    {
        var p = WritePolicy.StrictDefault();
        Assert.False(p.MainnetEnabled);
        Assert.Empty(p.AllowRecipients);
        Assert.Empty(p.AllowValidators);
        Assert.Equal(100m, p.PerTxCspr);
        Assert.Equal(500m, p.PerDayCspr);
    }

    [Fact]
    public void Env_Overrides_Are_Applied()
    {
        var env = new Dictionary<string, string?>
        {
            ["CASPER_MCP_MAINNET_ENABLED"] = "true",
            ["CASPER_MCP_PER_TX_CSPR"] = "10",
            ["CASPER_MCP_PER_DAY_CSPR"] = "25",
            ["CASPER_MCP_ALLOW_RECIPIENTS"] = "01aa,account-hash-bb",
            ["CASPER_MCP_ALLOW_VALIDATORS"] = "01cc, 01dd ",
        };
        var p = WritePolicy.Load(policyFilePath: null, getEnv: k => env.GetValueOrDefault(k));
        Assert.True(p.MainnetEnabled);
        Assert.Equal(10m, p.PerTxCspr);
        Assert.Equal(25m, p.PerDayCspr);
        Assert.Equal(2, p.AllowRecipients.Count);
        Assert.Contains("01cc", p.AllowValidators);
        Assert.Contains("01dd", p.AllowValidators); // trimmed
    }

    [Fact]
    public void File_Is_Read_When_Present_And_Env_Wins_Over_File()
    {
        var path = Path.GetTempFileName();
        File.WriteAllText(path, """
        { "mainnet_enabled": true, "caps": { "per_tx_cspr": 5, "per_day_cspr": 9 },
          "allowlist": { "recipients": ["01ff"], "validators": [] } }
        """);
        var env = new Dictionary<string, string?> { ["CASPER_MCP_PER_TX_CSPR"] = "7" };
        var p = WritePolicy.Load(path, k => env.GetValueOrDefault(k));
        Assert.True(p.MainnetEnabled);
        Assert.Equal(7m, p.PerTxCspr);      // env beats file
        Assert.Equal(9m, p.PerDayCspr);     // file value kept
        Assert.Contains("01ff", p.AllowRecipients);
        File.Delete(path);
    }

    [Fact]
    public void Invalid_File_Is_Fail_Closed_To_Strict_Default()
    {
        var path = Path.GetTempFileName();
        File.WriteAllText(path, "{ this is not valid json ");
        var p = WritePolicy.Load(path, _ => null);
        Assert.False(p.MainnetEnabled);    // collapses to strict default, never permissive
        Assert.Empty(p.AllowRecipients);
        File.Delete(path);
    }
}
