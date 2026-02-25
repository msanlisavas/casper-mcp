using System.ComponentModel;
using System.Text;
using CasperMcp.Configuration;
using CasperMcp.Helpers;
using CSPR.Cloud.Net.Clients;
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
}
