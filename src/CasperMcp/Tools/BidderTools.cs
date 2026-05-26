using System.ComponentModel;
using System.Text;
using CasperMcp.Configuration;
using CasperMcp.Helpers;
using CSPR.Cloud.Net.Clients;
using CSPR.Cloud.Net.Parameters.Wrapper.Bidder;
using ModelContextProtocol.Server;

namespace CasperMcp.Tools;

[McpServerToolType]
public static class BidderTools
{
    // Current era is network-global and changes ~every 2 hours; cache briefly to avoid an
    // extra auction-metrics call on every bidder request. Safe across tenants (not key-specific).
    private static readonly System.Collections.Concurrent.ConcurrentDictionary<string, (string Era, DateTime FetchedAt)> _eraCache = new();
    private static readonly TimeSpan _eraTtl = TimeSpan.FromSeconds(60);

    private static async Task<string?> ResolveCurrentEraAsync(INetworkEndpoint endpoint, bool isTestnet)
    {
        var net = isTestnet ? "testnet" : "mainnet";
        if (_eraCache.TryGetValue(net, out var cached) && DateTime.UtcNow - cached.FetchedAt < _eraTtl)
            return cached.Era;
        var auctionMetrics = await endpoint.Auction.GetAuctionMetricsAsync();
        var era = auctionMetrics?.Data?.CurrentEraId?.ToString();
        if (!string.IsNullOrEmpty(era))
            _eraCache[net] = (era, DateTime.UtcNow);
        return era;
    }

    [McpServerTool, Description("Get information about a specific bidder on the Casper Network by public key.")]
    public static async Task<string> GetBidder(
        CasperCloudRestClient client,
        CasperMcpOptions options,
        [Description("The public key of the bidder")] string publicKey,
        [Description("Era ID (defaults to the current era)")] string? eraId = null)
    {
        var endpoint = options.IsTestnet ? (INetworkEndpoint)client.Testnet : client.Mainnet;

        // Fetch current era ID if not provided (required by the API)
        var resolvedEra = !string.IsNullOrEmpty(eraId) ? eraId : await ResolveCurrentEraAsync(endpoint, options.IsTestnet);
        if (string.IsNullOrEmpty(resolvedEra))
            return "Unable to determine current era. Cannot fetch bidder.";

        var parameters = new BidderRequestParameters();
        parameters.FilterParameters.EraId = resolvedEra;
        var bidder = await endpoint.Bidder.GetBidderAsync(publicKey, parameters);

        if (bidder is null)
            return $"Bidder not found: {publicKey}";

        var sb = new StringBuilder();
        sb.AppendLine($"## Bidder Information");
        sb.AppendLine($"- **Public Key:** {FormattingHelpers.FormatHash(bidder.PublicKey)}");
        sb.AppendLine($"- **Rank:** #{bidder.Rank?.ToString() ?? "N/A"}");
        sb.AppendLine($"- **Active:** {FormattingHelpers.FormatBool(bidder.IsActive)}");
        sb.AppendLine($"- **Fee:** {bidder.Fee?.ToString() ?? "N/A"}%");
        sb.AppendLine($"- **Self Stake:** {FormattingHelpers.MotesToCspr(bidder.SelfStake)}");
        sb.AppendLine($"- **Total Stake:** {FormattingHelpers.MotesToCspr(bidder.TotalStake)}");
        sb.AppendLine($"- **Self Share:** {bidder.SelfShare ?? "N/A"}");
        sb.AppendLine($"- **Network Share:** {bidder.NetworkShare ?? "N/A"}");

        return sb.ToString();
    }

    [McpServerTool, Description("Get a list of bidders on the Casper Network.")]
    public static async Task<string> GetBidders(
        CasperCloudRestClient client,
        CasperMcpOptions options,
        [Description("Era ID (defaults to the current era)")] string? eraId = null,
        [Description("Page number (default: 1)")] int page = 1,
        [Description("Number of results per page (default: 10, max: 250)")] int pageSize = 10)
    {
        var endpoint = options.IsTestnet ? (INetworkEndpoint)client.Testnet : client.Mainnet;

        // Fetch current era ID if not provided (required by the API)
        var resolvedEra = !string.IsNullOrEmpty(eraId) ? eraId : await ResolveCurrentEraAsync(endpoint, options.IsTestnet);
        if (string.IsNullOrEmpty(resolvedEra))
            return "Unable to determine current era. Cannot fetch bidders.";

        var parameters = new BiddersRequestParameters
        {
            PageNumber = page,
            PageSize = Math.Min(pageSize, 250)
        };
        parameters.FilterParameters.EraId = resolvedEra;

        var result = await endpoint.Bidder.GetBiddersAsync(parameters);

        if (result?.Data is null || result.Data.Count == 0)
            return "No bidders found.";

        var sb = new StringBuilder();
        sb.AppendLine($"## Bidders (Page {page}, {result.ItemCount} total)");

        foreach (var b in result.Data)
        {
            sb.AppendLine($"---");
            sb.AppendLine($"- **Rank #{b.Rank}** | **Active:** {FormattingHelpers.FormatBool(b.IsActive)}");
            sb.AppendLine($"  Public Key: {FormattingHelpers.FormatHash(b.PublicKey)}");
            sb.AppendLine($"  Fee: {b.Fee?.ToString() ?? "N/A"}% | Self Stake: {FormattingHelpers.MotesToCspr(b.SelfStake)}");
            sb.AppendLine($"  Total Stake: {FormattingHelpers.MotesToCspr(b.TotalStake)} | Network Share: {b.NetworkShare ?? "N/A"}");
        }

        sb.AppendLine($"---");
        sb.AppendLine($"Page {page} of {result.PageCount}");

        return sb.ToString();
    }
}
