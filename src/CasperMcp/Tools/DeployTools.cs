using System.ComponentModel;
using System.Text;
using CasperMcp.Configuration;
using CasperMcp.Helpers;
using CSPR.Cloud.Net.Clients;
using CSPR.Cloud.Net.Parameters.Wrapper.Deploy;
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

    [McpServerTool, Description("Get a paginated list of all deploys (transactions) on the Casper Network.")]
    public static async Task<string> GetDeploys(
        CasperCloudRestClient client,
        CasperMcpOptions options,
        [Description("Page number (default: 1)")] int page = 1,
        [Description("Number of results per page (default: 10, max: 250)")] int pageSize = 10)
    {
        try
        {
            var endpoint = options.IsTestnet ? (INetworkEndpoint)client.Testnet : client.Mainnet;
            var parameters = new DeploysRequestParameters
            {
                PageNumber = page,
                PageSize = Math.Min(pageSize, 250)
            };

            var result = await endpoint.Deploy.GetDeploysAsync(parameters);

            if (result?.Data is null || result.Data.Count == 0)
                return "No deploys found.";

            var sb = new StringBuilder();
            sb.AppendLine($"## Deploys (Page {page}, {result.ItemCount} total)");

            foreach (var deploy in result.Data)
            {
                sb.AppendLine($"---");
                sb.AppendLine($"- **Deploy Hash:** {FormattingHelpers.FormatHash(deploy.DeployHash)}");
                sb.AppendLine($"  Caller: {FormattingHelpers.FormatHash(deploy.CallerPublicKey)}");
                sb.AppendLine($"  Status: {deploy.Status ?? "N/A"} | Cost: {FormattingHelpers.MotesToCspr(deploy.Cost)}");
                sb.AppendLine($"  Block Height: {deploy.BlockHeight?.ToString() ?? "N/A"} | {FormattingHelpers.FormatTimestamp(deploy.Timestamp)}");
            }

            sb.AppendLine($"---");
            sb.AppendLine($"Page {page} of {result.PageCount}");

            return sb.ToString();
        }
        catch (Exception ex)
        {
            return $"Error retrieving deploys: {ex.Message}";
        }
    }

    [McpServerTool, Description("Get deploys (transactions) included in a specific block on the Casper Network.")]
    public static async Task<string> GetBlockDeploys(
        CasperCloudRestClient client,
        CasperMcpOptions options,
        [Description("The block hash")] string blockHash,
        [Description("Page number (default: 1)")] int page = 1,
        [Description("Number of results per page (default: 10, max: 250)")] int pageSize = 10)
    {
        try
        {
            var endpoint = options.IsTestnet ? (INetworkEndpoint)client.Testnet : client.Mainnet;
            var parameters = new BlockDeploysRequestParameters
            {
                PageNumber = page,
                PageSize = Math.Min(pageSize, 250)
            };

            var result = await endpoint.Deploy.GetBlockDeploysAsync(blockHash, parameters);

            if (result?.Data is null || result.Data.Count == 0)
                return $"No deploys found for block: {blockHash}";

            var sb = new StringBuilder();
            sb.AppendLine($"## Block Deploys (Page {page}, {result.ItemCount} total)");
            sb.AppendLine($"Block: {blockHash}");

            foreach (var deploy in result.Data)
            {
                sb.AppendLine($"---");
                sb.AppendLine($"- **Deploy Hash:** {FormattingHelpers.FormatHash(deploy.DeployHash)}");
                sb.AppendLine($"  Caller: {FormattingHelpers.FormatHash(deploy.CallerPublicKey)}");
                sb.AppendLine($"  Status: {deploy.Status ?? "N/A"} | Cost: {FormattingHelpers.MotesToCspr(deploy.Cost)}");
                sb.AppendLine($"  Timestamp: {FormattingHelpers.FormatTimestamp(deploy.Timestamp)}");
            }

            sb.AppendLine($"---");
            sb.AppendLine($"Page {page} of {result.PageCount}");

            return sb.ToString();
        }
        catch (Exception ex)
        {
            return $"Error retrieving block deploys: {ex.Message}";
        }
    }

    [McpServerTool, Description("Get the list of deploy execution types on the Casper Network.")]
    public static async Task<string> GetDeployExecutionTypes(
        CasperCloudRestClient client,
        CasperMcpOptions options)
    {
        try
        {
            var endpoint = options.IsTestnet ? (INetworkEndpoint)client.Testnet : client.Mainnet;
            var result = await endpoint.Deploy.GetDeployExecutionTypesAsync();

            if (result?.Data is null || result.Data.Count == 0)
                return "No deploy execution types found.";

            var sb = new StringBuilder();
            sb.AppendLine($"## Deploy Execution Types ({result.Data.Count} total)");

            foreach (var et in result.Data)
            {
                sb.AppendLine($"- **ID:** {et.Id?.ToString() ?? "N/A"} | **Name:** {et.Name ?? "N/A"}");
            }

            return sb.ToString();
        }
        catch (Exception ex)
        {
            return $"Error retrieving deploy execution types: {ex.Message}";
        }
    }
}
