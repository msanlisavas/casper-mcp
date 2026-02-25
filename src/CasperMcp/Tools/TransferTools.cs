using System.ComponentModel;
using System.Text;
using CasperMcp.Configuration;
using CasperMcp.Helpers;
using CSPR.Cloud.Net.Clients;
using CSPR.Cloud.Net.Parameters.Wrapper.Transfer;
using ModelContextProtocol.Server;

namespace CasperMcp.Tools;

[McpServerToolType]
public static class TransferTools
{
    [McpServerTool, Description("Get native CSPR transfer history for a Casper Network account.")]
    public static async Task<string> GetTransfers(
        CasperCloudRestClient client,
        CasperMcpOptions options,
        [Description("The public key or account hash")] string accountIdentifier,
        [Description("Page number (default: 1)")] int page = 1,
        [Description("Number of results per page (default: 10, max: 250)")] int pageSize = 10)
    {
        try
        {
            var endpoint = options.IsTestnet ? (INetworkEndpoint)client.Testnet : client.Mainnet;
            var parameters = new TransferAccountRequestParameters
            {
                PageNumber = page,
                PageSize = Math.Min(pageSize, 250)
            };

            var result = await endpoint.Transfer.GetAccountTransfersAsync(accountIdentifier, parameters);

            if (result?.Data is null || result.Data.Count == 0)
                return $"No transfers found for account: {accountIdentifier}";

            var sb = new StringBuilder();
            sb.AppendLine($"## Account Transfers (Page {page}, {result.ItemCount} total)");

            foreach (var t in result.Data)
            {
                sb.AppendLine($"---");
                sb.AppendLine($"- **Deploy:** {FormattingHelpers.FormatHash(t.DeployHash)}");
                sb.AppendLine($"  From: {FormattingHelpers.FormatHash(t.FromPursePublicKey ?? t.InitiatorAccountHash)}");
                sb.AppendLine($"  To: {FormattingHelpers.FormatHash(t.ToPublicKey ?? t.ToAccountHash)}");
                sb.AppendLine($"  Amount: {FormattingHelpers.MotesToCspr(t.Amount)}");
                sb.AppendLine($"  Block: {t.BlockHeight?.ToString() ?? "N/A"} | {FormattingHelpers.FormatTimestamp(t.Timestamp)}");
            }

            sb.AppendLine($"---");
            sb.AppendLine($"Page {page} of {result.PageCount}");

            return sb.ToString();
        }
        catch (Exception ex)
        {
            return $"Error retrieving transfers: {ex.Message}";
        }
    }

    [McpServerTool, Description("Get native CSPR transfers for a specific deploy on the Casper Network.")]
    public static async Task<string> GetDeployTransfers(
        CasperCloudRestClient client,
        CasperMcpOptions options,
        [Description("The deploy hash")] string deployHash,
        [Description("Page number (default: 1)")] int page = 1,
        [Description("Number of results per page (default: 10, max: 250)")] int pageSize = 10)
    {
        try
        {
            var endpoint = options.IsTestnet ? (INetworkEndpoint)client.Testnet : client.Mainnet;
            var parameters = new TransferDeployRequestParameters
            {
                PageNumber = page,
                PageSize = Math.Min(pageSize, 250)
            };

            var result = await endpoint.Transfer.GetDeployTransfersAsync(deployHash, parameters);

            if (result?.Data is null || result.Data.Count == 0)
                return $"No transfers found for deploy: {deployHash}";

            var sb = new StringBuilder();
            sb.AppendLine($"## Deploy Transfers (Page {page}, {result.ItemCount} total)");
            sb.AppendLine($"Deploy: {deployHash}");

            foreach (var t in result.Data)
            {
                sb.AppendLine($"---");
                sb.AppendLine($"- From: {FormattingHelpers.FormatHash(t.FromPursePublicKey ?? t.InitiatorAccountHash)}");
                sb.AppendLine($"  To: {FormattingHelpers.FormatHash(t.ToPublicKey ?? t.ToAccountHash)}");
                sb.AppendLine($"  Amount: {FormattingHelpers.MotesToCspr(t.Amount)}");
                sb.AppendLine($"  Block: {t.BlockHeight?.ToString() ?? "N/A"} | {FormattingHelpers.FormatTimestamp(t.Timestamp)}");
            }

            sb.AppendLine($"---");
            sb.AppendLine($"Page {page} of {result.PageCount}");

            return sb.ToString();
        }
        catch (Exception ex)
        {
            return $"Error retrieving deploy transfers: {ex.Message}";
        }
    }
}
