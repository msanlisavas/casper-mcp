using System.ComponentModel;
using System.Text;
using CasperMcp.Configuration;
using CasperMcp.Helpers;
using CSPR.Cloud.Net.Clients;
using CSPR.Cloud.Net.Parameters.Wrapper.Contract;
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

    [McpServerTool, Description("Get a paginated list of all contracts on the Casper Network.")]
    public static async Task<string> GetContracts(
        CasperCloudRestClient client,
        CasperMcpOptions options,
        [Description("Page number (default: 1)")] int page = 1,
        [Description("Number of results per page (default: 10, max: 250)")] int pageSize = 10)
    {
        try
        {
            var endpoint = options.IsTestnet ? (INetworkEndpoint)client.Testnet : client.Mainnet;
            var parameters = new ContractsRequestParameters
            {
                PageNumber = page,
                PageSize = Math.Min(pageSize, 250)
            };

            var result = await endpoint.Contract.GetContractsAsync(parameters);

            if (result?.Data is null || result.Data.Count == 0)
                return "No contracts found.";

            var sb = new StringBuilder();
            sb.AppendLine($"## Contracts (Page {page}, {result.ItemCount} total)");

            foreach (var contract in result.Data)
            {
                sb.AppendLine($"---");
                sb.AppendLine($"- **Contract Hash:** {FormattingHelpers.FormatHash(contract.ContractHash)}");
                sb.AppendLine($"  Package: {FormattingHelpers.FormatHash(contract.ContractPackageHash)}");
                sb.AppendLine($"  Version: {contract.ContractVersion?.ToString() ?? "N/A"} | Disabled: {FormattingHelpers.FormatBool(contract.IsDisabled)}");
                sb.AppendLine($"  Timestamp: {FormattingHelpers.FormatTimestamp(contract.Timestamp)}");
            }

            sb.AppendLine($"---");
            sb.AppendLine($"Page {page} of {result.PageCount}");

            return sb.ToString();
        }
        catch (Exception ex)
        {
            return $"Error retrieving contracts: {ex.Message}";
        }
    }

    [McpServerTool, Description("Get the list of contract types on the Casper Network.")]
    public static async Task<string> GetContractTypes(
        CasperCloudRestClient client,
        CasperMcpOptions options)
    {
        try
        {
            var endpoint = options.IsTestnet ? (INetworkEndpoint)client.Testnet : client.Mainnet;
            var result = await endpoint.Contract.GetContractTypesAsync();

            if (result is null || result.Count == 0)
                return "No contract types found.";

            var sb = new StringBuilder();
            sb.AppendLine($"## Contract Types ({result.Count} total)");

            foreach (var ct in result)
            {
                sb.AppendLine($"- **ID:** {ct.Id?.ToString() ?? "N/A"} | **Name:** {ct.Name ?? "N/A"}");
            }

            return sb.ToString();
        }
        catch (Exception ex)
        {
            return $"Error retrieving contract types: {ex.Message}";
        }
    }

    [McpServerTool, Description("Get cost statistics for a specific contract entry point on the Casper Network.")]
    public static async Task<string> GetContractEntryPointCosts(
        CasperCloudRestClient client,
        CasperMcpOptions options,
        [Description("The contract hash")] string contractHash,
        [Description("The entry point name")] string entryPointName)
    {
        try
        {
            var endpoint = options.IsTestnet ? (INetworkEndpoint)client.Testnet : client.Mainnet;
            var result = await endpoint.Contract.GetContractEntryPointCostsAsync(contractHash, entryPointName);

            if (result?.Data is null)
                return $"No cost data found for entry point '{entryPointName}' on contract: {contractHash}";

            var cost = result.Data;
            var sb = new StringBuilder();
            sb.AppendLine($"## Entry Point Cost Statistics");
            sb.AppendLine($"- **Contract:** {contractHash}");
            sb.AppendLine($"- **Entry Point:** {entryPointName}");
            sb.AppendLine($"- **Deploys:** {cost.DeploysNum?.ToString() ?? "N/A"}");
            sb.AppendLine($"- **Since:** {FormattingHelpers.FormatTimestamp(cost.Since)}");
            sb.AppendLine($"- **Average Cost:** {FormattingHelpers.FormatDecimal(cost.AvgCost)}");
            sb.AppendLine($"- **Min Cost:** {FormattingHelpers.FormatDecimal(cost.MinCost)}");
            sb.AppendLine($"- **Max Cost:** {FormattingHelpers.FormatDecimal(cost.MaxCost)}");
            sb.AppendLine($"- **Average Payment:** {FormattingHelpers.FormatDecimal(cost.AvgPaymentAmount)}");
            sb.AppendLine($"- **Min Payment:** {FormattingHelpers.FormatDecimal(cost.MinPaymentAmount)}");
            sb.AppendLine($"- **Max Payment:** {FormattingHelpers.FormatDecimal(cost.MaxPaymentAmount)}");

            return sb.ToString();
        }
        catch (Exception ex)
        {
            return $"Error retrieving entry point costs: {ex.Message}";
        }
    }

    [McpServerTool, Description("Get a paginated list of contract packages on the Casper Network.")]
    public static async Task<string> GetContractPackages(
        CasperCloudRestClient client,
        CasperMcpOptions options,
        [Description("Page number (default: 1)")] int page = 1,
        [Description("Number of results per page (default: 10, max: 250)")] int pageSize = 10)
    {
        try
        {
            var endpoint = options.IsTestnet ? (INetworkEndpoint)client.Testnet : client.Mainnet;
            var parameters = new ContractPackageRequestParameters
            {
                PageNumber = page,
                PageSize = Math.Min(pageSize, 250)
            };

            var result = await endpoint.Contract.GetContractPackagesAsync(parameters);

            if (result?.Data is null || result.Data.Count == 0)
                return "No contract packages found.";

            var sb = new StringBuilder();
            sb.AppendLine($"## Contract Packages (Page {page}, {result.ItemCount} total)");

            foreach (var pkg in result.Data)
            {
                sb.AppendLine($"---");
                sb.AppendLine($"- **Package Hash:** {FormattingHelpers.FormatHash(pkg.ContractPackageHash)}");
                sb.AppendLine($"  Name: {pkg.Name ?? "N/A"} | Owner: {FormattingHelpers.FormatHash(pkg.OwnerPublicKey)}");
                sb.AppendLine($"  Created: {FormattingHelpers.FormatTimestamp(pkg.Timestamp)}");
            }

            sb.AppendLine($"---");
            sb.AppendLine($"Page {page} of {result.PageCount}");

            return sb.ToString();
        }
        catch (Exception ex)
        {
            return $"Error retrieving contract packages: {ex.Message}";
        }
    }

    [McpServerTool, Description("Get contracts belonging to a specific contract package on the Casper Network.")]
    public static async Task<string> GetContractsByContractPackage(
        CasperCloudRestClient client,
        CasperMcpOptions options,
        [Description("The contract package hash")] string contractPackageHash,
        [Description("Page number (default: 1)")] int page = 1,
        [Description("Number of results per page (default: 10, max: 250)")] int pageSize = 10)
    {
        try
        {
            var endpoint = options.IsTestnet ? (INetworkEndpoint)client.Testnet : client.Mainnet;
            var parameters = new ByContractRequestParameters
            {
                PageNumber = page,
                PageSize = Math.Min(pageSize, 250)
            };

            var result = await endpoint.Contract.GetContractsByContractPackageAsync(contractPackageHash, parameters);

            if (result?.Data is null || result.Data.Count == 0)
                return $"No contracts found for package: {contractPackageHash}";

            var sb = new StringBuilder();
            sb.AppendLine($"## Contracts by Package (Page {page}, {result.ItemCount} total)");
            sb.AppendLine($"Package: {contractPackageHash}");

            foreach (var contract in result.Data)
            {
                sb.AppendLine($"---");
                sb.AppendLine($"- **Contract Hash:** {FormattingHelpers.FormatHash(contract.ContractHash)}");
                sb.AppendLine($"  Version: {contract.ContractVersion?.ToString() ?? "N/A"} | Disabled: {FormattingHelpers.FormatBool(contract.IsDisabled)}");
                sb.AppendLine($"  Block Height: {contract.BlockHeight} | Timestamp: {FormattingHelpers.FormatTimestamp(contract.Timestamp)}");
            }

            sb.AppendLine($"---");
            sb.AppendLine($"Page {page} of {result.PageCount}");

            return sb.ToString();
        }
        catch (Exception ex)
        {
            return $"Error retrieving contracts by package: {ex.Message}";
        }
    }
}
