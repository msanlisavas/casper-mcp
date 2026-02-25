using System.ComponentModel;
using System.Text;
using CasperMcp.Configuration;
using CasperMcp.Helpers;
using CSPR.Cloud.Net.Clients;
using CSPR.Cloud.Net.Parameters.Wrapper.CentralizedAccountInfo;
using ModelContextProtocol.Server;

namespace CasperMcp.Tools;

[McpServerToolType]
public static class CentralizedAccountTools
{
    [McpServerTool, Description("Get centralized account information for a Casper Network account by account hash.")]
    public static async Task<string> GetCentralizedAccountInfo(
        CasperCloudRestClient client,
        CasperMcpOptions options,
        [Description("The account hash")] string accountHash)
    {
        try
        {
            var endpoint = options.IsTestnet ? (INetworkEndpoint)client.Testnet : client.Mainnet;
            var info = await endpoint.CentralizedAccount.GetCentralizedAccountInfoAsync(accountHash);

            if (info is null)
                return $"Centralized account info not found: {accountHash}";

            var sb = new StringBuilder();
            sb.AppendLine($"## Centralized Account Information");
            sb.AppendLine($"- **Account Hash:** {FormattingHelpers.FormatHash(info.AccountHash)}");
            sb.AppendLine($"- **Name:** {info.Name ?? "N/A"}");
            sb.AppendLine($"- **URL:** {info.Url ?? "N/A"}");
            sb.AppendLine($"- **Avatar URL:** {info.AvatarUrl ?? "N/A"}");

            return sb.ToString();
        }
        catch (Exception ex)
        {
            return $"Error retrieving centralized account info: {ex.Message}";
        }
    }

    [McpServerTool, Description("Get a list of centralized account information entries on the Casper Network.")]
    public static async Task<string> GetCentralizedAccounts(
        CasperCloudRestClient client,
        CasperMcpOptions options,
        [Description("Page number (default: 1)")] int page = 1,
        [Description("Number of results per page (default: 10, max: 250)")] int pageSize = 10)
    {
        try
        {
            var endpoint = options.IsTestnet ? (INetworkEndpoint)client.Testnet : client.Mainnet;
            var parameters = new CentralizedAccountInfoRequestParameters
            {
                PageNumber = page,
                PageSize = Math.Min(pageSize, 250)
            };

            var result = await endpoint.CentralizedAccount.GetCentralizedAccountInfosAsync(parameters);

            if (result?.Data is null || result.Data.Count == 0)
                return "No centralized accounts found.";

            var sb = new StringBuilder();
            sb.AppendLine($"## Centralized Accounts (Page {page}, {result.ItemCount} total)");

            foreach (var info in result.Data)
            {
                sb.AppendLine($"---");
                sb.AppendLine($"- **Name:** {info.Name ?? "N/A"}");
                sb.AppendLine($"  Account Hash: {FormattingHelpers.FormatHash(info.AccountHash)}");
                sb.AppendLine($"  URL: {info.Url ?? "N/A"}");
            }

            sb.AppendLine($"---");
            sb.AppendLine($"Page {page} of {result.PageCount}");

            return sb.ToString();
        }
        catch (Exception ex)
        {
            return $"Error retrieving centralized accounts: {ex.Message}";
        }
    }
}
