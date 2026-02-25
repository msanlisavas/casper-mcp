using System.ComponentModel;
using System.Text;
using CasperMcp.Configuration;
using CasperMcp.Helpers;
using CSPR.Cloud.Net.Clients;
using CSPR.Cloud.Net.Parameters.Wrapper.Swap;
using ModelContextProtocol.Server;

namespace CasperMcp.Tools;

[McpServerToolType]
public static class DexTools
{
    [McpServerTool, Description("Get a list of all decentralized exchanges (DEXes) on the Casper Network.")]
    public static async Task<string> GetDexes(
        CasperCloudRestClient client,
        CasperMcpOptions options)
    {
        try
        {
            var endpoint = options.IsTestnet ? (INetworkEndpoint)client.Testnet : client.Mainnet;
            var result = await endpoint.Dex.GetDexesAsync();

            if (result?.Data is null || result.Data.Count == 0)
                return "No DEXes found.";

            var sb = new StringBuilder();
            sb.AppendLine($"## Decentralized Exchanges");

            foreach (var dex in result.Data)
            {
                sb.AppendLine($"- **ID:** {dex.Id?.ToString() ?? "N/A"} | **Name:** {dex.Name ?? "N/A"}");
            }

            return sb.ToString();
        }
        catch (Exception ex)
        {
            return $"Error retrieving DEXes: {ex.Message}";
        }
    }

    [McpServerTool, Description("Get a paginated list of token swaps on the Casper Network DEXes.")]
    public static async Task<string> GetSwaps(
        CasperCloudRestClient client,
        CasperMcpOptions options,
        [Description("Page number (default: 1)")] int page = 1,
        [Description("Number of results per page (default: 10, max: 250)")] int pageSize = 10)
    {
        try
        {
            var endpoint = options.IsTestnet ? (INetworkEndpoint)client.Testnet : client.Mainnet;
            var parameters = new SwapRequestParameters
            {
                PageNumber = page,
                PageSize = Math.Min(pageSize, 250)
            };

            var result = await endpoint.Swap.GetSwapsAsync(parameters);

            if (result?.Data is null || result.Data.Count == 0)
                return "No swaps found.";

            var sb = new StringBuilder();
            sb.AppendLine($"## Token Swaps (Page {page}, {result.ItemCount} total)");

            foreach (var swap in result.Data)
            {
                sb.AppendLine($"---");
                sb.AppendLine($"- **Transaction:** {FormattingHelpers.FormatHash(swap.TransactionHash)}");
                sb.AppendLine($"  Sender: {FormattingHelpers.FormatHash(swap.SenderPublicKey ?? swap.SenderHash)}");
                sb.AppendLine($"  Token0: {FormattingHelpers.FormatHash(swap.Token0ContractPackageHash)} | Token1: {FormattingHelpers.FormatHash(swap.Token1ContractPackageHash)}");
                sb.AppendLine($"  Amount0 In: {swap.Amount0In ?? "0"} | Amount1 In: {swap.Amount1In ?? "0"}");
                sb.AppendLine($"  Amount0 Out: {swap.Amount0Out ?? "0"} | Amount1 Out: {swap.Amount1Out ?? "0"}");
                sb.AppendLine($"  DEX ID: {swap.DexId?.ToString() ?? "N/A"} | Block: {swap.BlockHeight?.ToString() ?? "N/A"}");
                sb.AppendLine($"  Timestamp: {swap.Timestamp ?? "N/A"}");
            }

            sb.AppendLine($"---");
            sb.AppendLine($"Page {page} of {result.PageCount}");

            return sb.ToString();
        }
        catch (Exception ex)
        {
            return $"Error retrieving swaps: {ex.Message}";
        }
    }
}
