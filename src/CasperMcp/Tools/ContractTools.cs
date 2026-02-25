using System.ComponentModel;
using System.Text;
using CasperMcp.Configuration;
using CasperMcp.Helpers;
using CSPR.Cloud.Net.Clients;
using ModelContextProtocol.Server;

namespace CasperMcp.Tools;

[McpServerToolType]
public static class ContractTools
{
    [McpServerTool, Description("Get information about a Casper Network smart contract by its hash.")]
    public static async Task<string> GetContract(
        CasperCloudRestClient client,
        CasperMcpOptions options,
        [Description("The contract hash")] string contractHash)
    {
        try
        {
            var endpoint = options.IsTestnet ? (INetworkEndpoint)client.Testnet : client.Mainnet;
            var contract = await endpoint.Contract.GetContractAsync(contractHash);

            if (contract is null)
                return $"Contract not found: {contractHash}";

            var sb = new StringBuilder();
            sb.AppendLine($"## Contract Information");
            sb.AppendLine($"- **Contract Hash:** {FormattingHelpers.FormatHash(contract.ContractHash)}");
            sb.AppendLine($"- **Package Hash:** {FormattingHelpers.FormatHash(contract.ContractPackageHash)}");
            sb.AppendLine($"- **Deploy Hash:** {FormattingHelpers.FormatHash(contract.DeployHash)}");
            sb.AppendLine($"- **Block Height:** {contract.BlockHeight}");
            sb.AppendLine($"- **Contract Type ID:** {contract.ContractTypeId?.ToString() ?? "N/A"}");
            sb.AppendLine($"- **Version:** {contract.ContractVersion?.ToString() ?? "N/A"}");
            sb.AppendLine($"- **Disabled:** {FormattingHelpers.FormatBool(contract.IsDisabled)}");
            sb.AppendLine($"- **Timestamp:** {FormattingHelpers.FormatTimestamp(contract.Timestamp)}");

            if (contract.ContractPackage is not null)
            {
                sb.AppendLine();
                sb.AppendLine($"### Contract Package");
                sb.AppendLine($"- **Name:** {contract.ContractPackage.Name ?? "N/A"}");
                sb.AppendLine($"- **Description:** {contract.ContractPackage.Description ?? "N/A"}");
                sb.AppendLine($"- **Owner:** {FormattingHelpers.FormatHash(contract.ContractPackage.OwnerPublicKey)}");
            }

            return sb.ToString();
        }
        catch (Exception ex)
        {
            return $"Error retrieving contract: {ex.Message}";
        }
    }

    [McpServerTool, Description("Get the entry points (callable functions) of a Casper Network smart contract.")]
    public static async Task<string> GetContractEntryPoints(
        CasperCloudRestClient client,
        CasperMcpOptions options,
        [Description("The contract hash")] string contractHash)
    {
        try
        {
            var endpoint = options.IsTestnet ? (INetworkEndpoint)client.Testnet : client.Mainnet;
            var result = await endpoint.Contract.GetContractEntryPointsAsync(contractHash);

            if (result?.Data is null || result.Data.Count == 0)
                return $"No entry points found for contract: {contractHash}";

            var sb = new StringBuilder();
            sb.AppendLine($"## Contract Entry Points ({result.ItemCount} total)");
            sb.AppendLine($"Contract: {contractHash}");
            sb.AppendLine();

            foreach (var ep in result.Data)
            {
                sb.AppendLine($"- **{ep.Name ?? "unnamed"}** (ID: {ep.Id})");
            }

            return sb.ToString();
        }
        catch (Exception ex)
        {
            return $"Error retrieving contract entry points: {ex.Message}";
        }
    }
}
