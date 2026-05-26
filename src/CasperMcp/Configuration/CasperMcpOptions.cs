namespace CasperMcp.Configuration;

/// <summary>
/// Per-request, tool-facing options. In http mode this is registered scoped and the
/// network is resolved from the request; in stdio mode it is a singleton from startup config.
/// </summary>
public class CasperMcpOptions
{
    public string Network { get; set; } = "mainnet";

    public bool IsTestnet => Network.Equals("testnet", StringComparison.OrdinalIgnoreCase);
}
