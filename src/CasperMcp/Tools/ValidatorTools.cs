using System.ComponentModel;
using System.Text;
using CasperMcp.Configuration;
using CasperMcp.Helpers;
using CSPR.Cloud.Net.Clients;
using CSPR.Cloud.Net.Parameters.Wrapper.Block;
using CSPR.Cloud.Net.Parameters.Wrapper.Delegate;
using CSPR.Cloud.Net.Parameters.Wrapper.Validator;
using ModelContextProtocol.Server;

namespace CasperMcp.Tools;

[McpServerToolType]
public static class ValidatorTools
{
    [McpServerTool, Description("Get a list of validators on the Casper Network with their stake, fee, and performance info.")]
    public static async Task<string> GetValidators(
        CasperCloudRestClient client,
        CasperMcpOptions options,
        [Description("Page number (default: 1)")] int page = 1,
        [Description("Number of results per page (default: 10, max: 250)")] int pageSize = 10)
    {
        try
        {
            var endpoint = options.IsTestnet ? (INetworkEndpoint)client.Testnet : client.Mainnet;

            // Fetch current era ID (required by the API)
            var auctionMetrics = await endpoint.Auction.GetAuctionMetricsAsync();
            var currentEraId = auctionMetrics?.Data?.CurrentEraId?.ToString();
            if (string.IsNullOrEmpty(currentEraId))
                return "Unable to determine current era. Cannot fetch validators.";

            var parameters = new ValidatorsRequestParameters
            {
                PageNumber = page,
                PageSize = Math.Min(pageSize, 250)
            };
            parameters.FilterParameters.EraId = currentEraId;

            var result = await endpoint.Validator.GetValidatorsAsync(parameters);

            if (result?.Data is null || result.Data.Count == 0)
                return "No validators found.";

            var sb = new StringBuilder();
            sb.AppendLine($"## Validators (Page {page}, {result.ItemCount} total)");

            foreach (var v in result.Data)
            {
                sb.AppendLine($"---");
                sb.AppendLine($"- **Rank #{v.Rank}** | **Active:** {FormattingHelpers.FormatBool(v.IsActive)}");
                sb.AppendLine($"  Public Key: {FormattingHelpers.FormatHash(v.PublicKey)}");
                sb.AppendLine($"  Fee: {FormattingHelpers.FormatPercentage(v.Fee)} | Delegators: {FormattingHelpers.FormatNumber(v.DelegatorsNumber)}");
                sb.AppendLine($"  Self Stake: {FormattingHelpers.MotesToCspr(v.SelfStake)} | Delegators Stake: {FormattingHelpers.MotesToCspr(v.DelegatorsStake)}");
                sb.AppendLine($"  Total Stake: {FormattingHelpers.MotesToCspr(v.TotalStake)}");
                sb.AppendLine($"  Network Share: {FormattingHelpers.FormatPercentage(v.NetworkShare)}");
            }

            sb.AppendLine($"---");
            sb.AppendLine($"Page {page} of {result.PageCount}");

            return sb.ToString();
        }
        catch (Exception ex)
        {
            return $"Error retrieving validators: {ex.Message}";
        }
    }

    [McpServerTool, Description("Get detailed information about a specific Casper Network validator by public key.")]
    public static async Task<string> GetValidatorInfo(
        CasperCloudRestClient client,
        CasperMcpOptions options,
        [Description("The validator's public key")] string publicKey)
    {
        try
        {
            var endpoint = options.IsTestnet ? (INetworkEndpoint)client.Testnet : client.Mainnet;

            // Fetch current era ID (required by the API)
            var auctionMetrics = await endpoint.Auction.GetAuctionMetricsAsync();
            var currentEraId = auctionMetrics?.Data?.CurrentEraId?.ToString();
            if (string.IsNullOrEmpty(currentEraId))
                return "Unable to determine current era. Cannot fetch validator info.";

            var parameters = new ValidatorRequestParameters();
            parameters.FilterParameters.EraId = currentEraId;
            var result = await endpoint.Validator.GetValidatorAsync(publicKey, parameters);

            if (result?.Data is null)
                return $"Validator not found: {publicKey}";

            var v = result.Data;
            var sb = new StringBuilder();
            sb.AppendLine($"## Validator Information");
            sb.AppendLine($"- **Rank:** #{v.Rank}");
            sb.AppendLine($"- **Public Key:** {FormattingHelpers.FormatHash(v.PublicKey)}");
            sb.AppendLine($"- **Active:** {FormattingHelpers.FormatBool(v.IsActive)}");
            sb.AppendLine($"- **Era ID:** {v.EraId?.ToString() ?? "N/A"}");
            sb.AppendLine($"- **Fee:** {FormattingHelpers.FormatPercentage(v.Fee)}");
            sb.AppendLine($"- **Delegators:** {FormattingHelpers.FormatNumber(v.DelegatorsNumber)}");
            sb.AppendLine($"- **Self Stake:** {FormattingHelpers.MotesToCspr(v.SelfStake)}");
            sb.AppendLine($"- **Delegators Stake:** {FormattingHelpers.MotesToCspr(v.DelegatorsStake)}");
            sb.AppendLine($"- **Total Stake:** {FormattingHelpers.MotesToCspr(v.TotalStake)}");
            sb.AppendLine($"- **Self Share:** {FormattingHelpers.FormatPercentage(v.SelfShare)}");
            sb.AppendLine($"- **Network Share:** {FormattingHelpers.FormatPercentage(v.NetworkShare)}");

            return sb.ToString();
        }
        catch (Exception ex)
        {
            return $"Error retrieving validator info: {ex.Message}";
        }
    }

    [McpServerTool, Description("Get delegations to a specific validator on the Casper Network.")]
    public static async Task<string> GetValidatorDelegations(
        CasperCloudRestClient client,
        CasperMcpOptions options,
        [Description("The public key of the validator")] string publicKey,
        [Description("Page number (default: 1)")] int page = 1,
        [Description("Number of results per page (default: 10, max: 250)")] int pageSize = 10)
    {
        try
        {
            var endpoint = options.IsTestnet ? (INetworkEndpoint)client.Testnet : client.Mainnet;
            var parameters = new DelegationRequestParameters
            {
                PageNumber = page,
                PageSize = Math.Min(pageSize, 250)
            };

            var result = await endpoint.Delegate.GetValidatorDelegationsAsync(publicKey, parameters);

            if (result?.Data is null || result.Data.Count == 0)
                return $"No delegations found for validator: {publicKey}";

            var sb = new StringBuilder();
            sb.AppendLine($"## Validator Delegations (Page {page}, {result.ItemCount} total)");

            foreach (var delegation in result.Data)
            {
                sb.AppendLine($"---");
                sb.AppendLine($"- **Delegator:** {FormattingHelpers.FormatHash(delegation.PublicKey)}");
                sb.AppendLine($"  Staked Amount: {FormattingHelpers.MotesToCspr(delegation.Stake)}");
            }

            sb.AppendLine($"---");
            sb.AppendLine($"Page {page} of {result.PageCount}");

            return sb.ToString();
        }
        catch (Exception ex)
        {
            return $"Error retrieving validator delegations: {ex.Message}";
        }
    }

    [McpServerTool, Description("Get rewards earned by a specific validator on the Casper Network.")]
    public static async Task<string> GetValidatorRewards(
        CasperCloudRestClient client,
        CasperMcpOptions options,
        [Description("The public key of the validator")] string publicKey,
        [Description("Page number (default: 1)")] int page = 1,
        [Description("Number of results per page (default: 10, max: 250)")] int pageSize = 10)
    {
        try
        {
            var endpoint = options.IsTestnet ? (INetworkEndpoint)client.Testnet : client.Mainnet;
            var parameters = new ValidatorRewardsRequestParameters
            {
                PageNumber = page,
                PageSize = Math.Min(pageSize, 250)
            };

            var result = await endpoint.Validator.GetValidatorRewardsAsync(publicKey, parameters);

            if (result?.Data is null || result.Data.Count == 0)
                return $"No rewards found for validator: {publicKey}";

            var sb = new StringBuilder();
            sb.AppendLine($"## Validator Rewards (Page {page}, {result.ItemCount} total)");

            foreach (var reward in result.Data)
            {
                sb.AppendLine($"---");
                sb.AppendLine($"- **Era:** {reward.EraId?.ToString() ?? "N/A"}");
                sb.AppendLine($"  Amount: {FormattingHelpers.MotesToCspr(reward.Amount)}");
                sb.AppendLine($"  Timestamp: {FormattingHelpers.FormatTimestamp(reward.Timestamp)}");
            }

            sb.AppendLine($"---");
            sb.AppendLine($"Page {page} of {result.PageCount}");

            return sb.ToString();
        }
        catch (Exception ex)
        {
            return $"Error retrieving validator rewards: {ex.Message}";
        }
    }

    [McpServerTool, Description("Get the total rewards earned by a validator on the Casper Network.")]
    public static async Task<string> GetValidatorTotalRewards(
        CasperCloudRestClient client,
        CasperMcpOptions options,
        [Description("The public key of the validator")] string publicKey)
    {
        try
        {
            var endpoint = options.IsTestnet ? (INetworkEndpoint)client.Testnet : client.Mainnet;
            var result = await endpoint.Validator.GetValidatorTotalRewardsAsync(publicKey);

            var sb = new StringBuilder();
            sb.AppendLine($"## Validator Total Rewards");
            sb.AppendLine($"- **Public Key:** {FormattingHelpers.FormatHash(publicKey)}");
            sb.AppendLine($"- **Total Rewards:** {FormattingHelpers.MotesToCspr(result?.Data)}");

            return sb.ToString();
        }
        catch (Exception ex)
        {
            return $"Error retrieving validator total rewards: {ex.Message}";
        }
    }

    [McpServerTool, Description("Get historical performance scores for a specific validator on the Casper Network.")]
    public static async Task<string> GetHistoricalValidatorPerformance(
        CasperCloudRestClient client,
        CasperMcpOptions options,
        [Description("The public key of the validator")] string publicKey,
        [Description("Page number (default: 1)")] int page = 1,
        [Description("Number of results per page (default: 10, max: 250)")] int pageSize = 10)
    {
        try
        {
            var endpoint = options.IsTestnet ? (INetworkEndpoint)client.Testnet : client.Mainnet;
            var parameters = new ValidatorHistoricalPerformanceRequestParameters
            {
                PageNumber = page,
                PageSize = Math.Min(pageSize, 250)
            };

            var result = await endpoint.Validator.GetHistoricalValidatorPerformanceAsync(publicKey, parameters);

            if (result?.Data is null || result.Data.Count == 0)
                return $"No performance data found for validator: {publicKey}";

            var sb = new StringBuilder();
            sb.AppendLine($"## Validator Historical Performance (Page {page}, {result.ItemCount} total)");

            foreach (var perf in result.Data)
            {
                sb.AppendLine($"- **Era {perf.EraId?.ToString() ?? "N/A"}:** Score: {FormattingHelpers.FormatDouble(perf.Score)}");
            }

            sb.AppendLine($"---");
            sb.AppendLine($"Page {page} of {result.PageCount}");

            return sb.ToString();
        }
        catch (Exception ex)
        {
            return $"Error retrieving validator performance: {ex.Message}";
        }
    }

    [McpServerTool, Description("Get historical average performance for a specific validator on the Casper Network.")]
    public static async Task<string> GetHistoricalValidatorAveragePerformance(
        CasperCloudRestClient client,
        CasperMcpOptions options,
        [Description("The public key of the validator")] string publicKey,
        [Description("Page number (default: 1)")] int page = 1,
        [Description("Number of results per page (default: 10, max: 250)")] int pageSize = 10)
    {
        try
        {
            var endpoint = options.IsTestnet ? (INetworkEndpoint)client.Testnet : client.Mainnet;
            var parameters = new ValidatorHistoricalAveragePerformanceRequestParameters
            {
                PageNumber = page,
                PageSize = Math.Min(pageSize, 250)
            };

            var result = await endpoint.Validator.GetHistoricalValidatorAveragePerformanceAsync(publicKey, parameters);

            if (result?.Data is null || result.Data.Count == 0)
                return $"No average performance data found for validator: {publicKey}";

            var sb = new StringBuilder();
            sb.AppendLine($"## Validator Historical Average Performance (Page {page}, {result.ItemCount} total)");

            foreach (var perf in result.Data)
            {
                sb.AppendLine($"- **Era {perf.EraId?.ToString() ?? "N/A"}:** Average Score: {FormattingHelpers.FormatDouble(perf.AverageScore)}");
            }

            sb.AppendLine($"---");
            sb.AppendLine($"Page {page} of {result.PageCount}");

            return sb.ToString();
        }
        catch (Exception ex)
        {
            return $"Error retrieving validator average performance: {ex.Message}";
        }
    }

    [McpServerTool, Description("Get historical average performance for all validators on the Casper Network.")]
    public static async Task<string> GetHistoricalValidatorsAveragePerformance(
        CasperCloudRestClient client,
        CasperMcpOptions options,
        [Description("Page number (default: 1)")] int page = 1,
        [Description("Number of results per page (default: 10, max: 250)")] int pageSize = 10)
    {
        try
        {
            var endpoint = options.IsTestnet ? (INetworkEndpoint)client.Testnet : client.Mainnet;
            var parameters = new ValidatorsHistoricalAveragePerformanceRequestParameters
            {
                PageNumber = page,
                PageSize = Math.Min(pageSize, 250)
            };

            var result = await endpoint.Validator.GetHistoricalValidatorsAveragePerformanceAsync(parameters);

            if (result?.Data is null || result.Data.Count == 0)
                return "No validators average performance data found.";

            var sb = new StringBuilder();
            sb.AppendLine($"## Validators Historical Average Performance (Page {page}, {result.ItemCount} total)");

            foreach (var perf in result.Data)
            {
                sb.AppendLine($"- **Era {perf.EraId?.ToString() ?? "N/A"}:** {FormattingHelpers.FormatHash(perf.PublicKey)} | Score: {FormattingHelpers.FormatDouble(perf.Score)}");
            }

            sb.AppendLine($"---");
            sb.AppendLine($"Page {page} of {result.PageCount}");

            return sb.ToString();
        }
        catch (Exception ex)
        {
            return $"Error retrieving validators average performance: {ex.Message}";
        }
    }

    [McpServerTool, Description("Get validator rewards aggregated by era on the Casper Network.")]
    public static async Task<string> GetValidatorEraRewards(
        CasperCloudRestClient client,
        CasperMcpOptions options,
        [Description("The public key of the validator")] string publicKey,
        [Description("Page number (default: 1)")] int page = 1,
        [Description("Number of results per page (default: 10, max: 250)")] int pageSize = 10)
    {
        try
        {
            var endpoint = options.IsTestnet ? (INetworkEndpoint)client.Testnet : client.Mainnet;
            var parameters = new ValidatorEraRewardsRequestParameters
            {
                PageNumber = page,
                PageSize = Math.Min(pageSize, 250)
            };

            var result = await endpoint.Validator.GetValidatorEraRewardsAsync(publicKey, parameters);

            if (result?.Data is null || result.Data.Count == 0)
                return $"No era rewards found for validator: {publicKey}";

            var sb = new StringBuilder();
            sb.AppendLine($"## Validator Era Rewards (Page {page}, {result.ItemCount} total)");

            foreach (var reward in result.Data)
            {
                sb.AppendLine($"---");
                sb.AppendLine($"- **Era:** {reward.EraId?.ToString() ?? "N/A"}");
                sb.AppendLine($"  Amount: {FormattingHelpers.MotesToCspr(reward.Amount)}");
                sb.AppendLine($"  Timestamp: {FormattingHelpers.FormatTimestamp(reward.Timestamp)}");
            }

            sb.AppendLine($"---");
            sb.AppendLine($"Page {page} of {result.PageCount}");

            return sb.ToString();
        }
        catch (Exception ex)
        {
            return $"Error retrieving validator era rewards: {ex.Message}";
        }
    }
}
