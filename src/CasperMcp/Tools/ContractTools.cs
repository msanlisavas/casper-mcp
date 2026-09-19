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
    /// <summary>
    /// The window the contract-package deploy count covers. Unlike the other optional properties
    /// this one is not a bool: CSPR.cloud's <c>deploys_number(N)</c> includer takes N as a number
    /// of PAST DAYS, so the rendered count is meaningless unless the window is printed with it.
    /// </summary>
    private const int DeploysWindowDays = 7;

    [McpServerTool, Description("Get information about a Casper Network smart contract by its hash.")]
    public static async Task<string> GetContract(
        CasperCloudRestClient client,
        CasperMcpOptions options,
        [Description("The contract hash")] string contractHash)
    {
        var endpoint = options.IsTestnet ? (INetworkEndpoint)client.Testnet : client.Mainnet;
        var parameters = new ContractRequestParameters();
        // The whole "Contract Package" section below reads name, description and owner off an
        // OPTIONAL property: without this flag the response never carries it and the section
        // silently never prints at all.
        parameters.OptionalParameters.ContractPackage = true;

        var contract = await endpoint.Contract.GetContractAsync(contractHash, parameters);

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

    [McpServerTool, Description("Get the entry points (callable functions) of a Casper Network smart contract.")]
    public static async Task<string> GetContractEntryPoints(
        CasperCloudRestClient client,
        CasperMcpOptions options,
        [Description("The contract hash")] string contractHash)
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

    [McpServerTool, Description("Get a paginated list of all contracts on the Casper Network.")]
    public static async Task<string> GetContracts(
        CasperCloudRestClient client,
        CasperMcpOptions options,
        [Description("Page number (default: 1)")] int page = 1,
        [Description("Number of results per page (default: 10, max: 250)")] int pageSize = 10)
    {
        var endpoint = options.IsTestnet ? (INetworkEndpoint)client.Testnet : client.Mainnet;
        var parameters = new ContractsRequestParameters
        {
            PageNumber = page,
            PageSize = Math.Min(pageSize, 250)
        };
        // The package name is the only human-readable thing on a contract, and it is an optional
        // property: without this flag every row is a wall of hashes.
        parameters.OptionalParameters.ContractPackage = true;

        var result = await endpoint.Contract.GetContractsAsync(parameters);

        if (result?.Data is null || result.Data.Count == 0)
            return "No contracts found.";

        var sb = new StringBuilder();
        sb.AppendLine($"## Contracts (Page {page}, {result.ItemCount} total)");

        foreach (var contract in result.Data)
        {
            sb.AppendLine($"---");
            sb.AppendLine($"- **Contract Hash:** {FormattingHelpers.FormatHash(contract.ContractHash)}");
            sb.AppendLine($"  Package: {NameHelpers.Labeled(contract.ContractPackage?.Name, contract.ContractPackageHash)}");
            sb.AppendLine($"  Version: {contract.ContractVersion?.ToString() ?? "N/A"} | Disabled: {FormattingHelpers.FormatBool(contract.IsDisabled)}");
            sb.AppendLine($"  Timestamp: {FormattingHelpers.FormatTimestamp(contract.Timestamp)}");
        }

        sb.AppendLine($"---");
        sb.AppendLine($"Page {page} of {result.PageCount}");

        return sb.ToString();
    }

    [McpServerTool, Description("Get the list of contract types on the Casper Network.")]
    public static async Task<string> GetContractTypes(
        CasperCloudRestClient client,
        CasperMcpOptions options)
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

    [McpServerTool, Description("Get cost statistics for a specific contract entry point on the Casper Network.")]
    public static async Task<string> GetContractEntryPointCosts(
        CasperCloudRestClient client,
        CasperMcpOptions options,
        [Description("The contract hash")] string contractHash,
        [Description("The entry point name")] string entryPointName)
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

    [McpServerTool, Description("Get a paginated list of contract packages on the Casper Network.")]
    public static async Task<string> GetContractPackages(
        CasperCloudRestClient client,
        CasperMcpOptions options,
        [Description("Page number (default: 1)")] int page = 1,
        [Description("Number of results per page (default: 10, max: 250)")] int pageSize = 10)
    {
        var endpoint = options.IsTestnet ? (INetworkEndpoint)client.Testnet : client.Mainnet;
        var parameters = new ContractPackageRequestParameters
        {
            PageNumber = page,
            PageSize = Math.Min(pageSize, 250)
        };
        // Both are optional properties: without these the owner is a raw key with no name attached
        // and the deploy count comes back null, so the row can't say whether the package is alive.
        parameters.OptionalParameters.OwnerCsprName = true;
        parameters.OptionalParameters.DeploysNumber = DeploysWindowDays;

        var result = await endpoint.Contract.GetContractPackagesAsync(parameters);

        if (result?.Data is null || result.Data.Count == 0)
            return "No contract packages found.";

        var sb = new StringBuilder();
        sb.AppendLine($"## Contract Packages (Page {page}, {result.ItemCount} total)");

        foreach (var pkg in result.Data)
        {
            sb.AppendLine($"---");
            sb.AppendLine($"- **Package Hash:** {FormattingHelpers.FormatHash(pkg.ContractPackageHash)}");
            sb.AppendLine($"  Name: {pkg.Name ?? "N/A"} | Owner: {NameHelpers.Labeled(pkg.OwnerCsprName, pkg.OwnerPublicKey)}");
            sb.AppendLine($"  Created: {FormattingHelpers.FormatTimestamp(pkg.Timestamp)}");
            sb.AppendLine($"  Deploys (last {DeploysWindowDays} days): {pkg.DeploysNumber?.ToString() ?? "N/A"}");
        }

        sb.AppendLine($"---");
        sb.AppendLine($"Page {page} of {result.PageCount}");

        return sb.ToString();
    }

    [McpServerTool, Description("Get contracts belonging to a specific contract package on the Casper Network.")]
    public static async Task<string> GetContractsByContractPackage(
        CasperCloudRestClient client,
        CasperMcpOptions options,
        [Description("The contract package hash")] string contractPackageHash,
        [Description("Page number (default: 1)")] int page = 1,
        [Description("Number of results per page (default: 10, max: 250)")] int pageSize = 10)
    {
        var endpoint = options.IsTestnet ? (INetworkEndpoint)client.Testnet : client.Mainnet;
        var parameters = new ByContractRequestParameters
        {
            PageNumber = page,
            PageSize = Math.Min(pageSize, 250)
        };
        // Every row here belongs to the one package in the header, whose name is an optional
        // property: without this flag the header can only echo the hash the caller already had.
        parameters.OptionalParameters.ContractPackage = true;

        var result = await endpoint.Contract.GetContractsByContractPackageAsync(contractPackageHash, parameters);

        if (result?.Data is null || result.Data.Count == 0)
            return $"No contracts found for package: {contractPackageHash}";

        // Every contract in this page carries the same package; take the first name that is there.
        var packageName = result.Data
            .Select(c => c.ContractPackage?.Name)
            .FirstOrDefault(n => !string.IsNullOrEmpty(n));

        var sb = new StringBuilder();
        sb.AppendLine($"## Contracts by Package (Page {page}, {result.ItemCount} total)");
        sb.AppendLine($"Package: {NameHelpers.Labeled(packageName, contractPackageHash)}");

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
}
