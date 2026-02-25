using System.ComponentModel;
using System.Text;
using CasperMcp.Configuration;
using CasperMcp.Helpers;
using CSPR.Cloud.Net.Clients;
using CSPR.Cloud.Net.Parameters.Wrapper.Rate;
using ModelContextProtocol.Server;

namespace CasperMcp.Tools;

[McpServerToolType]
public static class CurrencyTools
{
    [McpServerTool, Description("Get the current CSPR exchange rate for a specific currency.")]
    public static async Task<string> GetCurrentCurrencyRate(
        CasperCloudRestClient client,
        CasperMcpOptions options,
        [Description("The currency ID (e.g., 1 for USD)")] string currencyId)
    {
        try
        {
            var endpoint = options.IsTestnet ? (INetworkEndpoint)client.Testnet : client.Mainnet;
            var result = await endpoint.Rate.GetCurrentCurrencyRateAsync(currencyId);

            if (result?.Data is null)
                return $"Currency rate not found for currency ID: {currencyId}";

            var rate = result.Data;
            var sb = new StringBuilder();
            sb.AppendLine($"## Current Currency Rate");
            sb.AppendLine($"- **Currency ID:** {rate.CurrencyId}");
            sb.AppendLine($"- **Rate:** {rate.Amount?.ToString() ?? "N/A"}");
            sb.AppendLine($"- **Timestamp:** {FormattingHelpers.FormatTimestamp(rate.Created)}");

            return sb.ToString();
        }
        catch (Exception ex)
        {
            return $"Error retrieving currency rate: {ex.Message}";
        }
    }

    [McpServerTool, Description("Get historical CSPR exchange rates for a specific currency.")]
    public static async Task<string> GetHistoricalCurrencyRates(
        CasperCloudRestClient client,
        CasperMcpOptions options,
        [Description("The currency ID (e.g., 1 for USD)")] string currencyId,
        [Description("Page number (default: 1)")] int page = 1,
        [Description("Number of results per page (default: 10, max: 250)")] int pageSize = 10)
    {
        try
        {
            var endpoint = options.IsTestnet ? (INetworkEndpoint)client.Testnet : client.Mainnet;
            var parameters = new RateHistoricalRequestParameters
            {
                PageNumber = page,
                PageSize = Math.Min(pageSize, 250)
            };

            var result = await endpoint.Rate.GetHistoricalCurrencyRatesAsync(currencyId, parameters);

            if (result?.Data is null || result.Data.Count == 0)
                return $"No historical rates found for currency ID: {currencyId}";

            var sb = new StringBuilder();
            sb.AppendLine($"## Historical Currency Rates (Page {page}, {result.ItemCount} total)");

            foreach (var rate in result.Data)
            {
                sb.AppendLine($"---");
                sb.AppendLine($"- **Rate:** {rate.Amount?.ToString() ?? "N/A"} | **Timestamp:** {FormattingHelpers.FormatTimestamp(rate.Created)}");
            }

            sb.AppendLine($"---");
            sb.AppendLine($"Page {page} of {result.PageCount}");

            return sb.ToString();
        }
        catch (Exception ex)
        {
            return $"Error retrieving historical currency rates: {ex.Message}";
        }
    }

    [McpServerTool, Description("Get a list of supported currencies for CSPR exchange rates.")]
    public static async Task<string> GetCurrencies(
        CasperCloudRestClient client,
        CasperMcpOptions options,
        [Description("Page number (default: 1)")] int page = 1,
        [Description("Number of results per page (default: 10, max: 250)")] int pageSize = 10)
    {
        try
        {
            var endpoint = options.IsTestnet ? (INetworkEndpoint)client.Testnet : client.Mainnet;
            var parameters = new RateCurrenciesRequestParameters
            {
                PageNumber = page,
                PageSize = Math.Min(pageSize, 250)
            };

            var result = await endpoint.Rate.GetCurrenciesAsync(parameters);

            if (result?.Data is null || result.Data.Count == 0)
                return "No currencies found.";

            var sb = new StringBuilder();
            sb.AppendLine($"## Supported Currencies (Page {page}, {result.ItemCount} total)");

            foreach (var currency in result.Data)
            {
                sb.AppendLine($"- **ID:** {currency.Id?.ToString() ?? "N/A"} | **Code:** {currency.Code ?? "N/A"} | **Type ID:** {currency.TypeId?.ToString() ?? "N/A"}");
            }

            sb.AppendLine($"---");
            sb.AppendLine($"Page {page} of {result.PageCount}");

            return sb.ToString();
        }
        catch (Exception ex)
        {
            return $"Error retrieving currencies: {ex.Message}";
        }
    }
}
