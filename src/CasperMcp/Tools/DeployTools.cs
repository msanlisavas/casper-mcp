using System.ComponentModel;
using System.Text;
using CasperMcp.Configuration;
using CasperMcp.Helpers;
using CSPR.Cloud.Net.Clients;
using ModelContextProtocol.Server;

namespace CasperMcp.Tools;

[McpServerToolType]
public static class DeployTools
{
    [McpServerTool, Description("Get detailed information about a specific Casper Network deploy (transaction) by its hash.")]
    public static async Task<string> GetDeploy(
        CasperCloudRestClient client,
        CasperMcpOptions options,
        [Description("The deploy hash")] string deployHash)
    {
        try
        {
            var endpoint = options.IsTestnet ? (INetworkEndpoint)client.Testnet : client.Mainnet;
            var result = await endpoint.Deploy.GetDeployAsync(deployHash);

            if (result?.Data is null)
                return $"Deploy not found: {deployHash}";

            var deploy = result.Data;
            var sb = new StringBuilder();
            sb.AppendLine($"## Deploy Information");
            sb.AppendLine($"- **Deploy Hash:** {FormattingHelpers.FormatHash(deploy.DeployHash)}");
            sb.AppendLine($"- **Block Hash:** {FormattingHelpers.FormatHash(deploy.BlockHash)}");
            sb.AppendLine($"- **Block Height:** {deploy.BlockHeight?.ToString() ?? "N/A"}");
            sb.AppendLine($"- **Caller:** {FormattingHelpers.FormatHash(deploy.CallerPublicKey)}");
            sb.AppendLine($"- **Status:** {deploy.Status ?? "N/A"}");
            sb.AppendLine($"- **Cost:** {FormattingHelpers.MotesToCspr(deploy.Cost)}");
            sb.AppendLine($"- **Payment Amount:** {FormattingHelpers.MotesToCspr(deploy.PaymentAmount)}");
            sb.AppendLine($"- **Timestamp:** {FormattingHelpers.FormatTimestamp(deploy.Timestamp)}");

            if (!string.IsNullOrEmpty(deploy.ContractHash))
                sb.AppendLine($"- **Contract Hash:** {FormattingHelpers.FormatHash(deploy.ContractHash)}");

            if (!string.IsNullOrEmpty(deploy.ContractPackageHash))
                sb.AppendLine($"- **Contract Package:** {FormattingHelpers.FormatHash(deploy.ContractPackageHash)}");

            if (!string.IsNullOrEmpty(deploy.ErrorMessage))
                sb.AppendLine($"- **Error:** {deploy.ErrorMessage}");

            if (deploy.Transfers is { Count: > 0 })
            {
                sb.AppendLine();
                sb.AppendLine($"### Transfers ({deploy.Transfers.Count})");
                foreach (var transfer in deploy.Transfers)
                {
                    sb.AppendLine($"- From: {FormattingHelpers.FormatHash(transfer.FromPursePublicKey)} → To: {FormattingHelpers.FormatHash(transfer.ToPublicKey)} | Amount: {FormattingHelpers.MotesToCspr(transfer.Amount)}");
                }
            }

            return sb.ToString();
        }
        catch (Exception ex)
        {
            return $"Error retrieving deploy: {ex.Message}";
        }
    }
}
