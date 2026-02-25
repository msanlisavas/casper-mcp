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
    [McpServerTool, Description("Get information about a specific bidder on the Casper Network by public key.")]
    public static async Task<string> GetBidder(
        CasperCloudRestClient client,
        CasperMcpOptions options,
        [Description("The public key of the bidder")] string publicKey)
    {
        try
        {
            var endpoint = options.IsTestnet ? (INetworkEndpoint)client.Testnet : client.Mainnet;
            var parameters = new BidderRequestParameters();
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
        catch (Exception ex)
        {
            return $"Error retrieving bidder: {ex.Message}";
        }
    }

    [McpServerTool, Description("Get a list of bidders on the Casper Network.")]
    public static async Task<string> GetBidders(
        CasperCloudRestClient client,
        CasperMcpOptions options,
        [Description("Page number (default: 1)")] int page = 1,
        [Description("Number of results per page (default: 10, max: 250)")] int pageSize = 10)
    {
        try
        {
            var endpoint = options.IsTestnet ? (INetworkEndpoint)client.Testnet : client.Mainnet;
            var parameters = new BiddersRequestParameters
            {
                PageNumber = page,
                PageSize = Math.Min(pageSize, 250)
            };

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
        catch (Exception ex)
        {
            return $"Error retrieving bidders: {ex.Message}";
        }
    }
}
