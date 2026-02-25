using System.ComponentModel;
using System.Text;
using CasperMcp.Configuration;
using CasperMcp.Helpers;
using CSPR.Cloud.Net.Clients;
using CSPR.Cloud.Net.Parameters.Filtering.Ft;
using CSPR.Cloud.Net.Parameters.Wrapper.Ft;
using ModelContextProtocol.Server;

namespace CasperMcp.Tools;

[McpServerToolType]
public static class FtRateTools
{
    [McpServerTool, Description("Get the latest fungible token rate for a contract package on the Casper Network.")]
    public static async Task<string> GetFtRateLatest(
        CasperCloudRestClient client,
        CasperMcpOptions options,
        [Description("The contract package hash of the fungible token")] string contractPackageHash,
        [Description("Optional currency ID to filter by")] string? currencyId = null)
    {
        try
        {
            var endpoint = options.IsTestnet ? (INetworkEndpoint)client.Testnet : client.Mainnet;
            var filterParams = new FTRateFilterParameters();
            if (!string.IsNullOrEmpty(currencyId))
                filterParams.CurrencyId = currencyId;

            var result = await endpoint.FT.GetFTRateLatestAsync(contractPackageHash, filterParams);

            if (result is null)
                return $"No rate data found for token: {contractPackageHash}";

            var sb = new StringBuilder();
            sb.AppendLine($"## Latest FT Rate");
            sb.AppendLine($"- **Token:** {FormattingHelpers.FormatHash(result.TokenContractPackageHash)}");
            sb.AppendLine($"- **Currency ID:** {result.CurrencyId?.ToString() ?? "N/A"}");
            sb.AppendLine($"- **Amount:** {FormattingHelpers.FormatDouble(result.Amount)}");
            sb.AppendLine($"- **Volume:** {result.Volume ?? "N/A"}");
            sb.AppendLine($"- **DEX ID:** {result.DexId?.ToString() ?? "N/A"}");
            sb.AppendLine($"- **Transaction:** {FormattingHelpers.FormatHash(result.TransactionHash)}");
            sb.AppendLine($"- **Timestamp:** {result.Timestamp ?? "N/A"}");

            return sb.ToString();
        }
        catch (Exception ex)
        {
            return $"Error retrieving FT rate: {ex.Message}";
        }
    }

    [McpServerTool, Description("Get historical fungible token rates for a contract package on the Casper Network.")]
    public static async Task<string> GetFtRates(
        CasperCloudRestClient client,
        CasperMcpOptions options,
        [Description("The contract package hash of the fungible token")] string contractPackageHash,
        [Description("Page number (default: 1)")] int page = 1,
        [Description("Number of results per page (default: 10, max: 250)")] int pageSize = 10)
    {
        try
        {
            var endpoint = options.IsTestnet ? (INetworkEndpoint)client.Testnet : client.Mainnet;
            var parameters = new FTRateRequestParameters
            {
                PageNumber = page,
                PageSize = Math.Min(pageSize, 250)
            };

            var result = await endpoint.FT.GetFTRatesAsync(contractPackageHash, parameters);

            if (result?.Data is null || result.Data.Count == 0)
                return $"No rate history found for token: {contractPackageHash}";

            var sb = new StringBuilder();
            sb.AppendLine($"## FT Rate History (Page {page}, {result.ItemCount} total)");

            foreach (var rate in result.Data)
            {
                sb.AppendLine($"---");
                sb.AppendLine($"- **Amount:** {FormattingHelpers.FormatDouble(rate.Amount)} | Currency: {rate.CurrencyId?.ToString() ?? "N/A"}");
                sb.AppendLine($"  Volume: {rate.Volume ?? "N/A"} | DEX: {rate.DexId?.ToString() ?? "N/A"}");
                sb.AppendLine($"  Timestamp: {rate.Timestamp ?? "N/A"}");
            }

            sb.AppendLine($"---");
            sb.AppendLine($"Page {page} of {result.PageCount}");

            return sb.ToString();
        }
        catch (Exception ex)
        {
            return $"Error retrieving FT rates: {ex.Message}";
        }
    }

    [McpServerTool, Description("Get the latest daily aggregated fungible token rate on the Casper Network.")]
    public static async Task<string> GetFtDailyRateLatest(
        CasperCloudRestClient client,
        CasperMcpOptions options,
        [Description("The contract package hash of the fungible token")] string contractPackageHash,
        [Description("Optional currency ID to filter by")] string? currencyId = null)
    {
        try
        {
            var endpoint = options.IsTestnet ? (INetworkEndpoint)client.Testnet : client.Mainnet;
            var filterParams = new FTRateFilterParameters();
            if (!string.IsNullOrEmpty(currencyId))
                filterParams.CurrencyId = currencyId;

            var result = await endpoint.FT.GetFTDailyRateLatestAsync(contractPackageHash, filterParams);

            if (result is null)
                return $"No daily rate data found for token: {contractPackageHash}";

            var sb = new StringBuilder();
            sb.AppendLine($"## Latest Daily FT Rate");
            sb.AppendLine($"- **Token:** {FormattingHelpers.FormatHash(result.TokenContractPackageHash)}");
            sb.AppendLine($"- **Currency ID:** {result.CurrencyId?.ToString() ?? "N/A"}");
            sb.AppendLine($"- **Amount:** {FormattingHelpers.FormatDouble(result.Amount)}");
            sb.AppendLine($"- **Volume:** {result.Volume ?? "N/A"}");
            sb.AppendLine($"- **Date:** {result.Date ?? "N/A"}");

            return sb.ToString();
        }
        catch (Exception ex)
        {
            return $"Error retrieving daily FT rate: {ex.Message}";
        }
    }

    [McpServerTool, Description("Get historical daily aggregated fungible token rates on the Casper Network.")]
    public static async Task<string> GetFtDailyRates(
        CasperCloudRestClient client,
        CasperMcpOptions options,
        [Description("The contract package hash of the fungible token")] string contractPackageHash,
        [Description("Page number (default: 1)")] int page = 1,
        [Description("Number of results per page (default: 10, max: 250)")] int pageSize = 10)
    {
        try
        {
            var endpoint = options.IsTestnet ? (INetworkEndpoint)client.Testnet : client.Mainnet;
            var parameters = new FTDailyRateRequestParameters
            {
                PageNumber = page,
                PageSize = Math.Min(pageSize, 250)
            };

            var result = await endpoint.FT.GetFTDailyRatesAsync(contractPackageHash, parameters);

            if (result?.Data is null || result.Data.Count == 0)
                return $"No daily rate history found for token: {contractPackageHash}";

            var sb = new StringBuilder();
            sb.AppendLine($"## Daily FT Rate History (Page {page}, {result.ItemCount} total)");

            foreach (var rate in result.Data)
            {
                sb.AppendLine($"---");
                sb.AppendLine($"- **Amount:** {FormattingHelpers.FormatDouble(rate.Amount)} | Currency: {rate.CurrencyId?.ToString() ?? "N/A"}");
                sb.AppendLine($"  Volume: {rate.Volume ?? "N/A"} | Date: {rate.Date ?? "N/A"}");
            }

            sb.AppendLine($"---");
            sb.AppendLine($"Page {page} of {result.PageCount}");

            return sb.ToString();
        }
        catch (Exception ex)
        {
            return $"Error retrieving daily FT rates: {ex.Message}";
        }
    }

    [McpServerTool, Description("Get the latest token-to-token DEX rate for a fungible token on the Casper Network.")]
    public static async Task<string> GetFtDexRateLatest(
        CasperCloudRestClient client,
        CasperMcpOptions options,
        [Description("The contract package hash of the fungible token")] string contractPackageHash,
        [Description("Optional target token contract package hash")] string? targetContractPackageHash = null)
    {
        try
        {
            var endpoint = options.IsTestnet ? (INetworkEndpoint)client.Testnet : client.Mainnet;
            var filterParams = new FTDexRateFilterParameters();
            if (!string.IsNullOrEmpty(targetContractPackageHash))
                filterParams.TargetContractPackageHash = targetContractPackageHash;

            var result = await endpoint.FT.GetFTDexRateLatestAsync(contractPackageHash, filterParams);

            if (result is null)
                return $"No DEX rate data found for token: {contractPackageHash}";

            var sb = new StringBuilder();
            sb.AppendLine($"## Latest FT DEX Rate");
            sb.AppendLine($"- **Token:** {FormattingHelpers.FormatHash(result.TokenContractPackageHash)}");
            sb.AppendLine($"- **Target Token:** {FormattingHelpers.FormatHash(result.TargetTokenContractPackageHash)}");
            sb.AppendLine($"- **Amount:** {FormattingHelpers.FormatDouble(result.Amount)}");
            sb.AppendLine($"- **Volume:** {result.Volume ?? "N/A"}");
            sb.AppendLine($"- **DEX ID:** {result.DexId?.ToString() ?? "N/A"}");
            sb.AppendLine($"- **Transaction:** {FormattingHelpers.FormatHash(result.TransactionHash)}");
            sb.AppendLine($"- **Timestamp:** {result.Timestamp ?? "N/A"}");

            return sb.ToString();
        }
        catch (Exception ex)
        {
            return $"Error retrieving FT DEX rate: {ex.Message}";
        }
    }

    [McpServerTool, Description("Get historical token-to-token DEX rates for a fungible token on the Casper Network.")]
    public static async Task<string> GetFtDexRates(
        CasperCloudRestClient client,
        CasperMcpOptions options,
        [Description("The contract package hash of the fungible token")] string contractPackageHash,
        [Description("Page number (default: 1)")] int page = 1,
        [Description("Number of results per page (default: 10, max: 250)")] int pageSize = 10)
    {
        try
        {
            var endpoint = options.IsTestnet ? (INetworkEndpoint)client.Testnet : client.Mainnet;
            var parameters = new FTDexRateRequestParameters
            {
                PageNumber = page,
                PageSize = Math.Min(pageSize, 250)
            };

            var result = await endpoint.FT.GetFTDexRatesAsync(contractPackageHash, parameters);

            if (result?.Data is null || result.Data.Count == 0)
                return $"No DEX rate history found for token: {contractPackageHash}";

            var sb = new StringBuilder();
            sb.AppendLine($"## FT DEX Rate History (Page {page}, {result.ItemCount} total)");

            foreach (var rate in result.Data)
            {
                sb.AppendLine($"---");
                sb.AppendLine($"- **Amount:** {FormattingHelpers.FormatDouble(rate.Amount)}");
                sb.AppendLine($"  Target: {FormattingHelpers.FormatHash(rate.TargetTokenContractPackageHash)}");
                sb.AppendLine($"  Volume: {rate.Volume ?? "N/A"} | DEX: {rate.DexId?.ToString() ?? "N/A"}");
                sb.AppendLine($"  Timestamp: {rate.Timestamp ?? "N/A"}");
            }

            sb.AppendLine($"---");
            sb.AppendLine($"Page {page} of {result.PageCount}");

            return sb.ToString();
        }
        catch (Exception ex)
        {
            return $"Error retrieving FT DEX rates: {ex.Message}";
        }
    }

    [McpServerTool, Description("Get the latest daily token-to-token DEX rate for a fungible token on the Casper Network.")]
    public static async Task<string> GetFtDailyDexRateLatest(
        CasperCloudRestClient client,
        CasperMcpOptions options,
        [Description("The contract package hash of the fungible token")] string contractPackageHash,
        [Description("Optional target token contract package hash")] string? targetContractPackageHash = null)
    {
        try
        {
            var endpoint = options.IsTestnet ? (INetworkEndpoint)client.Testnet : client.Mainnet;
            var filterParams = new FTDexRateFilterParameters();
            if (!string.IsNullOrEmpty(targetContractPackageHash))
                filterParams.TargetContractPackageHash = targetContractPackageHash;

            var result = await endpoint.FT.GetFTDailyDexRateLatestAsync(contractPackageHash, filterParams);

            if (result is null)
                return $"No daily DEX rate data found for token: {contractPackageHash}";

            var sb = new StringBuilder();
            sb.AppendLine($"## Latest Daily FT DEX Rate");
            sb.AppendLine($"- **Token:** {FormattingHelpers.FormatHash(result.TokenContractPackageHash)}");
            sb.AppendLine($"- **Target Token:** {FormattingHelpers.FormatHash(result.TargetTokenContractPackageHash)}");
            sb.AppendLine($"- **Amount:** {FormattingHelpers.FormatDouble(result.Amount)}");
            sb.AppendLine($"- **Volume:** {result.Volume ?? "N/A"}");
            sb.AppendLine($"- **DEX ID:** {result.DexId?.ToString() ?? "N/A"}");
            sb.AppendLine($"- **Date:** {result.Date ?? "N/A"}");

            return sb.ToString();
        }
        catch (Exception ex)
        {
            return $"Error retrieving daily FT DEX rate: {ex.Message}";
        }
    }

    [McpServerTool, Description("Get historical daily token-to-token DEX rates for a fungible token on the Casper Network.")]
    public static async Task<string> GetFtDailyDexRates(
        CasperCloudRestClient client,
        CasperMcpOptions options,
        [Description("The contract package hash of the fungible token")] string contractPackageHash,
        [Description("Page number (default: 1)")] int page = 1,
        [Description("Number of results per page (default: 10, max: 250)")] int pageSize = 10)
    {
        try
        {
            var endpoint = options.IsTestnet ? (INetworkEndpoint)client.Testnet : client.Mainnet;
            var parameters = new FTDailyDexRateRequestParameters
            {
                PageNumber = page,
                PageSize = Math.Min(pageSize, 250)
            };

            var result = await endpoint.FT.GetFTDailyDexRatesAsync(contractPackageHash, parameters);

            if (result?.Data is null || result.Data.Count == 0)
                return $"No daily DEX rate history found for token: {contractPackageHash}";

            var sb = new StringBuilder();
            sb.AppendLine($"## Daily FT DEX Rate History (Page {page}, {result.ItemCount} total)");

            foreach (var rate in result.Data)
            {
                sb.AppendLine($"---");
                sb.AppendLine($"- **Amount:** {FormattingHelpers.FormatDouble(rate.Amount)}");
                sb.AppendLine($"  Target: {FormattingHelpers.FormatHash(rate.TargetTokenContractPackageHash)}");
                sb.AppendLine($"  Volume: {rate.Volume ?? "N/A"} | DEX: {rate.DexId?.ToString() ?? "N/A"}");
                sb.AppendLine($"  Date: {rate.Date ?? "N/A"}");
            }

            sb.AppendLine($"---");
            sb.AppendLine($"Page {page} of {result.PageCount}");

            return sb.ToString();
        }
        catch (Exception ex)
        {
            return $"Error retrieving daily FT DEX rates: {ex.Message}";
        }
    }
}
