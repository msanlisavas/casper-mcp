using System.ComponentModel;
using System.Text;
using CasperMcp.Configuration;
using CasperMcp.Helpers;
using CSPR.Cloud.Net.Clients;
using CSPR.Cloud.Net.Parameters.Wrapper.Block;
using CSPR.Cloud.Net.Parameters.Wrapper.Deploy;
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

    [McpServerTool, Description("Get current Casper Network era information from auction metrics.")]
    public static async Task<string> GetEraInfo(
        CasperCloudRestClient client,
        CasperMcpOptions options)
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

    [McpServerTool, Description("Get network activity over the last N hours (1 to 24, default 24), counted back from the latest block: blocks and average block time, transactions with their categories (native transfers, auction, install/upgrade, small, medium, large), contract calls, failed transactions, fees paid (cost minus refund) and distinct senders. Covers at most the last 24 hours.")]
    public static async Task<string> GetNetworkActivity(
        CasperCloudRestClient client,
        CasperMcpOptions options,
        [Description("Hours to count back from the latest block, 1 to 24 (default: 24)")] int hours = 24,
        CancellationToken cancellationToken = default)
    {
        var endpoint = options.IsTestnet ? (INetworkEndpoint)client.Testnet : client.Mainnet;
        var limits = new NetworkActivityLimits();

        // No optional properties on either list: the counters, costs and callers are all base fields.
        var report = await NetworkActivity.ComputeAsync(
            (page, ct) => endpoint.Block.GetBlocksAsync(
                new BlockRequestParameters { PageNumber = page, PageSize = limits.PageSize }, ct),
            (page, ct) => endpoint.Deploy.GetDeploysAsync(
                new DeploysRequestParameters { PageNumber = page, PageSize = limits.PageSize }, ct),
            hours,
            cancellationToken,
            limits);

        return report is null ? "No blocks found." : NetworkActivity.Format(report, options.IsTestnet);
    }

    [McpServerTool, Description("Get CSPR token supply information including total and circulating supply.")]
    public static async Task<string> GetSupplyInfo(
        CasperCloudRestClient client,
        CasperMcpOptions options)
    {
        var endpoint = options.IsTestnet ? (INetworkEndpoint)client.Testnet : client.Mainnet;
        var result = await endpoint.Supply.GetSupplyAsync();

        if (result?.Data is null)
            return "Unable to retrieve supply info.";

        var supply = result.Data;
        var sb = new StringBuilder();
        sb.AppendLine($"## CSPR Supply Information");
        sb.AppendLine($"- **Token:** {supply.Token ?? "CSPR"}");
        // /supply reports whole CSPR, not motes (live 2026-11-12: total 20182293198); through
        // MotesToCspr these printed as "20.182293250 CSPR".
        sb.AppendLine($"- **Total Supply:** {FormattingHelpers.FormatWholeCspr(supply.Total)}");
        sb.AppendLine($"- **Circulating Supply:** {FormattingHelpers.FormatWholeCspr(supply.Circulating)}");

        if (supply.TotalAnnualIssuance is not null)
            sb.AppendLine($"- **Total Annual Issuance:** {supply.TotalAnnualIssuance.Value:P4}");
        if (supply.AnnualStakingRewardsIssuance is not null)
            sb.AppendLine($"- **Annual Staking Rewards Issuance:** {supply.AnnualStakingRewardsIssuance.Value:P4}");
        if (supply.AnnualEcosystemSustainIssuance is not null)
            sb.AppendLine($"- **Annual Ecosystem Sustain Issuance:** {supply.AnnualEcosystemSustainIssuance.Value:P4}");

        sb.AppendLine($"- **Last Updated:** {FormattingHelpers.FormatUnixSeconds(supply.Timestamp)}");

        return sb.ToString();
    }
}
