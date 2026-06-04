using CasperMcp.Configuration;

namespace CasperMcp.Tests;

public class SignerStartupTests
{
    [Fact]
    public void Writes_With_Http_Transport_Is_Rejected()
    {
        var cfg = new ServerConfig { WritesEnabled = true, Transport = "http", KeyPath = "k.pem" };
        var (ok, error) = ServerConfig.ValidateWriteConfig(cfg);
        Assert.False(ok);
        Assert.Contains("http", error, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Writes_Without_Key_Is_Rejected()
    {
        var cfg = new ServerConfig { WritesEnabled = true, Transport = "stdio", KeyPath = "" };
        var (ok, error) = ServerConfig.ValidateWriteConfig(cfg);
        Assert.False(ok);
        Assert.Contains("key", error, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Writes_With_Missing_Key_File_Is_Rejected()
    {
        var cfg = new ServerConfig { WritesEnabled = true, Transport = "stdio", KeyPath = "does-not-exist.pem" };
        var (ok, error) = ServerConfig.ValidateWriteConfig(cfg);
        Assert.False(ok);
        Assert.Contains("not found", error, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Reads_Only_Config_Is_Valid()
    {
        var (ok, _) = ServerConfig.ValidateWriteConfig(new ServerConfig { WritesEnabled = false, Transport = "http" });
        Assert.True(ok);
    }

    [Fact]
    public void WriteMode_Without_Explicit_Network_Defaults_To_Testnet()
    {
        var cfg = new ServerConfig { WritesEnabled = true, DefaultNetwork = "mainnet", NetworkExplicitlySet = false };
        ServerConfig.ApplyWriteModeNetworkDefault(cfg);
        Assert.Equal("testnet", cfg.DefaultNetwork);
    }

    [Fact]
    public void WriteMode_With_Explicit_Mainnet_Stays_Mainnet()
    {
        var cfg = new ServerConfig { WritesEnabled = true, DefaultNetwork = "mainnet", NetworkExplicitlySet = true };
        ServerConfig.ApplyWriteModeNetworkDefault(cfg);
        Assert.Equal("mainnet", cfg.DefaultNetwork);
    }

    [Fact]
    public void ReadOnly_Mode_Network_Unchanged()
    {
        var cfg = new ServerConfig { WritesEnabled = false, DefaultNetwork = "mainnet", NetworkExplicitlySet = false };
        ServerConfig.ApplyWriteModeNetworkDefault(cfg);
        Assert.Equal("mainnet", cfg.DefaultNetwork);
    }
}
