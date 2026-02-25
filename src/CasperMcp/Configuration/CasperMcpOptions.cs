namespace CasperMcp.Configuration;

public class CasperMcpOptions
{
    public string ApiKey { get; set; } = string.Empty;
    public string Network { get; set; } = "mainnet";
    public string Transport { get; set; } = "stdio";
    public int Port { get; set; } = 3001;
    public string ServerApiKey { get; set; } = string.Empty;

    public bool IsTestnet => Network.Equals("testnet", StringComparison.OrdinalIgnoreCase);
    public bool IsSseTransport => Transport.Equals("sse", StringComparison.OrdinalIgnoreCase);
}
