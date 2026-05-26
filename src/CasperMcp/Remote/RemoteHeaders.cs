using Microsoft.AspNetCore.Http;

namespace CasperMcp.Remote;

public static class RemoteHeaders
{
    public const string CsprKeyHeader = "X-CSPR-Cloud-Api-Key";
    public const string NetworkHeader = "X-Casper-Network";

    public static bool TryGetCsprKey(IHeaderDictionary headers, out string key)
    {
        var value = headers[CsprKeyHeader].ToString();
        if (string.IsNullOrWhiteSpace(value))
        {
            key = string.Empty;
            return false;
        }
        key = value.Trim();
        return true;
    }

    /// <summary>
    /// Resolves the effective network. Returns false only when the header is present but invalid.
    /// </summary>
    public static bool TryResolveNetwork(IHeaderDictionary headers, string fallback, out string network)
    {
        var value = headers[NetworkHeader].ToString();
        if (string.IsNullOrWhiteSpace(value))
        {
            network = fallback;
            return true;
        }

        switch (value.Trim().ToLowerInvariant())
        {
            case "mainnet": network = "mainnet"; return true;
            case "testnet": network = "testnet"; return true;
            default: network = string.Empty; return false;
        }
    }
}
