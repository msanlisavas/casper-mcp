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
        Assert.Equal(100m, p.TransferPerTxCspr);
        Assert.Equal(500m, p.TransferPerDayCspr);
        Assert.Equal(100m, p.StakePerTxCspr);
    }

    [Fact]
    public void New_Env_Overrides_Transfer_And_Stake_Independently()
    {
        var env = new Dictionary<string, string?>
        {
            ["CASPER_MCP_MAINNET_ENABLED"] = "true",
            ["CASPER_MCP_TRANSFER_PER_TX_CSPR"] = "10",
            ["CASPER_MCP_TRANSFER_PER_DAY_CSPR"] = "25",
            ["CASPER_MCP_STAKE_PER_TX_CSPR"] = "5000000",   // big stake, tight transfer — the whole point
            ["CASPER_MCP_ALLOW_RECIPIENTS"] = "01aa,account-hash-bb",
            ["CASPER_MCP_ALLOW_VALIDATORS"] = "01cc, 01dd ",
        };
        var p = WritePolicy.Load(null, k => env.GetValueOrDefault(k));
        Assert.True(p.MainnetEnabled);
        Assert.Equal(10m, p.TransferPerTxCspr);
        Assert.Equal(25m, p.TransferPerDayCspr);
        Assert.Equal(5000000m, p.StakePerTxCspr);          // decoupled from the transfer caps
        Assert.Equal(2, p.AllowRecipients.Count);
        Assert.Contains("01cc", p.AllowValidators);
        Assert.Contains("01dd", p.AllowValidators);         // trimmed
    }

    [Fact]
    public void Legacy_Env_Vars_Still_Set_Transfer_Caps()
    {
        var env = new Dictionary<string, string?>
        {
            ["CASPER_MCP_PER_TX_CSPR"] = "12",
            ["CASPER_MCP_PER_DAY_CSPR"] = "34",
        };
        var p = WritePolicy.Load(null, k => env.GetValueOrDefault(k));
        Assert.Equal(12m, p.TransferPerTxCspr);
        Assert.Equal(34m, p.TransferPerDayCspr);
    }

    [Fact]
    public void New_File_Format_Sets_Transfer_And_Stake()
    {
        var path = Path.GetTempFileName();
        File.WriteAllText(path, """
        { "mainnet_enabled": true,
          "transfer": { "per_tx_cspr": 5, "per_day_cspr": 9 },
          "stake": { "per_tx_cspr": 1000000 },
          "allowlist": { "recipients": ["01ff"], "validators": ["02ee"] } }
        """);
        var p = WritePolicy.Load(path, _ => null);
        Assert.True(p.MainnetEnabled);
        Assert.Equal(5m, p.TransferPerTxCspr);
        Assert.Equal(9m, p.TransferPerDayCspr);
        Assert.Equal(1000000m, p.StakePerTxCspr);
        Assert.Contains("01ff", p.AllowRecipients);
        Assert.Contains("02ee", p.AllowValidators);
        File.Delete(path);
    }

    [Fact]
    public void Legacy_Caps_Block_Still_Read_For_Transfer()
    {
        var path = Path.GetTempFileName();
        File.WriteAllText(path, """
        { "caps": { "per_tx_cspr": 5, "per_day_cspr": 9 },
          "allowlist": { "recipients": ["01ff"], "validators": [] } }
        """);
        var p = WritePolicy.Load(path, _ => null);
        Assert.Equal(5m, p.TransferPerTxCspr);
        Assert.Equal(9m, p.TransferPerDayCspr);
        File.Delete(path);
    }

    [Fact]
    public void Env_Wins_Over_File()
    {
        var path = Path.GetTempFileName();
        File.WriteAllText(path, """{ "transfer": { "per_tx_cspr": 5, "per_day_cspr": 9 } }""");
        var env = new Dictionary<string, string?> { ["CASPER_MCP_TRANSFER_PER_TX_CSPR"] = "7" };
        var p = WritePolicy.Load(path, k => env.GetValueOrDefault(k));
        Assert.Equal(7m, p.TransferPerTxCspr);   // env beats file
        Assert.Equal(9m, p.TransferPerDayCspr);  // file value kept
        File.Delete(path);
    }

    [Fact]
    public void Invalid_File_Is_Fail_Closed_To_Strict_Default()
    {
        var path = Path.GetTempFileName();
        File.WriteAllText(path, "{ this is not valid json ");
        var p = WritePolicy.Load(path, _ => null);
        Assert.False(p.MainnetEnabled);          // collapses to strict default, never permissive
        Assert.Empty(p.AllowRecipients);
        Assert.Equal(100m, p.StakePerTxCspr);
        File.Delete(path);
    }
}
