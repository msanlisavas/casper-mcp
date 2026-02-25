using System.ComponentModel;
using System.Text;
using CasperMcp.Configuration;
using CasperMcp.Helpers;
using CSPR.Cloud.Net.Clients;
using ModelContextProtocol.Server;

namespace CasperMcp.Tools;

[McpServerToolType]
public static class NetworkTools
{
    [McpServerTool, Description("Get current Casper Network status including active validators, era info, and total stake.")]
    public static async Task<string> GetNetworkStatus(
        CasperCloudRestClient client,
        CasperMcpOptions options)
    {
        try
        {
            var endpoint = options.IsTestnet ? (INetworkEndpoint)client.Testnet : client.Mainnet;
            var result = await endpoint.Auction.GetAuctionMetricsAsync();

            if (result?.Data is null)
                return "Unable to retrieve network status.";

            var metrics = result.Data;
            var sb = new StringBuilder();
            sb.AppendLine($"## Casper Network Status ({(options.IsTestnet ? "Testnet" : "Mainnet")})");
            sb.AppendLine($"- **Current Era:** {metrics.CurrentEraId?.ToString() ?? "N/A"}");
            sb.AppendLine($"- **Active Validators:** {FormattingHelpers.FormatNumber(metrics.ActiveValidatorNumber)}");
            sb.AppendLine($"- **Total Bids:** {FormattingHelpers.FormatNumber(metrics.TotalBidsNumber)}");
            sb.AppendLine($"- **Active Bids:** {FormattingHelpers.FormatNumber(metrics.ActiveBidsNumber)}");
            sb.AppendLine($"- **Total Active Era Stake:** {FormattingHelpers.MotesToCspr(metrics.TotalActiveEraStake)}");

            return sb.ToString();
        }
        catch (Exception ex)
        {
            return $"Error retrieving network status: {ex.Message}";
        }
    }

    [McpServerTool, Description("Get current Casper Network era information from auction metrics.")]
    public static async Task<string> GetEraInfo(
        CasperCloudRestClient client,
        CasperMcpOptions options)
    {
        try
        {
            var endpoint = options.IsTestnet ? (INetworkEndpoint)client.Testnet : client.Mainnet;
            var result = await endpoint.Auction.GetAuctionMetricsAsync();

            if (result?.Data is null)
                return "Unable to retrieve era info.";

            var metrics = result.Data;
            var sb = new StringBuilder();
            sb.AppendLine($"## Era Information ({(options.IsTestnet ? "Testnet" : "Mainnet")})");
            sb.AppendLine($"- **Current Era ID:** {metrics.CurrentEraId?.ToString() ?? "N/A"}");
            sb.AppendLine($"- **Active Validators:** {FormattingHelpers.FormatNumber(metrics.ActiveValidatorNumber)}");
            sb.AppendLine($"- **Total Active Era Stake:** {FormattingHelpers.MotesToCspr(metrics.TotalActiveEraStake)}");

            return sb.ToString();
        }
        catch (Exception ex)
        {
            return $"Error retrieving era info: {ex.Message}";
        }
    }

    [McpServerTool, Description("Get CSPR token supply information including total and circulating supply.")]
    public static async Task<string> GetSupplyInfo(
        CasperCloudRestClient client,
        CasperMcpOptions options)
    {
        try
        {
            var endpoint = options.IsTestnet ? (INetworkEndpoint)client.Testnet : client.Mainnet;
            var result = await endpoint.Supply.GetSupplyAsync();

            if (result?.Data is null)
                return "Unable to retrieve supply info.";

            var supply = result.Data;
            var sb = new StringBuilder();
            sb.AppendLine($"## CSPR Supply Information");
            sb.AppendLine($"- **Token:** {supply.Token ?? "CSPR"}");
            sb.AppendLine($"- **Total Supply:** {FormattingHelpers.MotesToCspr(supply.Total)}");
            sb.AppendLine($"- **Circulating Supply:** {FormattingHelpers.MotesToCspr(supply.Circulating)}");
            sb.AppendLine($"- **Last Updated:** {FormattingHelpers.FormatTimestamp(supply.Timestamp)}");

            return sb.ToString();
        }
        catch (Exception ex)
        {
            return $"Error retrieving supply info: {ex.Message}";
        }
    }
}
