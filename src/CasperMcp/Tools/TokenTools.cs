using System.ComponentModel;
using System.Text;
using CasperMcp.Configuration;
using CasperMcp.Helpers;
using CSPR.Cloud.Net.Clients;
using CSPR.Cloud.Net.Parameters.Wrapper.Ft;
using ModelContextProtocol.Server;

namespace CasperMcp.Tools;

[McpServerToolType]
public static class TokenTools
{
    [McpServerTool, Description("Get information about a fungible token (CEP-18) contract package on the Casper Network.")]
    public static async Task<string> GetFtTokenInfo(
        CasperCloudRestClient client,
        CasperMcpOptions options,
        [Description("The contract package hash of the fungible token")] string contractPackageHash)
    {
        var endpoint = options.IsTestnet ? (INetworkEndpoint)client.Testnet : client.Mainnet;
        // DeploysNumber below is an optional property, but the single-package endpoint takes no
        // parameters object in CSPR.Cloud.Net 3.0.0 — there is no flag to set, so "Deploys" stays
        // "N/A" until the SDK exposes one. Do not "fix" it by inventing a flag.
        var result = await endpoint.Contract.GetContractPackageAsync(contractPackageHash);

        if (result?.Data is null)
            return $"Token contract package not found: {contractPackageHash}";

        var pkg = result.Data;
        var sb = new StringBuilder();
        sb.AppendLine($"## Fungible Token Information");
        sb.AppendLine($"- **Contract Package:** {FormattingHelpers.FormatHash(pkg.ContractPackageHash)}");
        sb.AppendLine($"- **Name:** {pkg.Name ?? "N/A"}");
        sb.AppendLine($"- **Description:** {pkg.Description ?? "N/A"}");
        sb.AppendLine($"- **Owner:** {FormattingHelpers.FormatHash(pkg.OwnerPublicKey)}");
        if (!string.IsNullOrEmpty(pkg.IconUrl))
            sb.AppendLine($"- **Icon URL:** {pkg.IconUrl}");
        if (!string.IsNullOrEmpty(pkg.WebsiteUrl))
            sb.AppendLine($"- **Website URL:** {pkg.WebsiteUrl}");
        sb.AppendLine($"- **Deploys:** {pkg.DeploysNumber?.ToString() ?? "N/A"}");
        sb.AppendLine($"- **Created:** {FormattingHelpers.FormatTimestamp(pkg.Timestamp)}");

        return sb.ToString();
    }

    [McpServerTool, Description("Get the holders (ownership list) of a fungible token on the Casper Network.")]
    public static async Task<string> GetFtTokenHolders(
        CasperCloudRestClient client,
        CasperMcpOptions options,
        [Description("The contract package hash of the fungible token")] string contractPackageHash,
        [Description("Page number (default: 1)")] int page = 1,
        [Description("Number of results per page (default: 10, max: 250)")] int pageSize = 10)
    {
        var endpoint = options.IsTestnet ? (INetworkEndpoint)client.Testnet : client.Mainnet;
        var parameters = new FTContractPackageOwnershipRequestParameters
        {
            PageNumber = page,
            PageSize = Math.Min(pageSize, 250)
        };
        // Holder names are optional properties: without these the response carries account hashes
        // only, and "who are this token's biggest holders?" can be answered in hex alone.
        parameters.OptionalParameters.AccountInfo = true;
        parameters.OptionalParameters.CentralizedAccountInfo = true;
        parameters.OptionalParameters.OwnerCsprName = true;

        var result = await endpoint.FT.GetContractPackageFTOwnershipAsync(contractPackageHash, parameters);

        if (result?.Data is null || result.Data.Count == 0)
            return $"No holders found for token: {contractPackageHash}";

        var sb = new StringBuilder();
        sb.AppendLine($"## Token Holders (Page {page}, {result.ItemCount} total)");

        foreach (var holder in result.Data)
        {
            sb.AppendLine($"---");
            sb.AppendLine($"- **Owner:** {NameHelpers.Labeled(NameHelpers.DisplayName(holder.AccountInfo, holder.CentralizedAccountInfo, holder.OwnerCsprName), holder.OwnerHash)}");
            sb.AppendLine($"  Balance: {holder.Balance ?? "N/A"}");
        }

        sb.AppendLine($"---");
        sb.AppendLine($"Page {page} of {result.PageCount}");

        return sb.ToString();
    }

    [McpServerTool, Description("Get fungible token balances for a Casper Network account.")]
    public static async Task<string> GetAccountFtBalances(
        CasperCloudRestClient client,
        CasperMcpOptions options,
        [Description("The public key or account hash")] string accountIdentifier,
        [Description("Page number (default: 1)")] int page = 1,
        [Description("Number of results per page (default: 10, max: 250)")] int pageSize = 10)
    {
        var endpoint = options.IsTestnet ? (INetworkEndpoint)client.Testnet : client.Mainnet;
        var parameters = new FTAccountOwnershipRequestParameters
        {
            PageNumber = page,
            PageSize = Math.Min(pageSize, 250)
        };
        // ContractPackage is an optional property, and the renderer below prints its Name: without
        // this flag it is never sent, so the "Name:" line has never appeared for any token.
        parameters.OptionalParameters.ContractPackage = true;

        var result = await endpoint.FT.GetAccountFTOwnershipAsync(accountIdentifier, parameters);

        if (result?.Data is null || result.Data.Count == 0)
            return $"No fungible token balances found for account: {accountIdentifier}";

        var sb = new StringBuilder();
        sb.AppendLine($"## Account Fungible Token Balances (Page {page}, {result.ItemCount} total)");

        foreach (var token in result.Data)
        {
            sb.AppendLine($"---");
            sb.AppendLine($"- **Token:** {FormattingHelpers.FormatHash(token.ContractPackageHash)}");
            if (token.ContractPackage is not null)
                sb.AppendLine($"  Name: {token.ContractPackage.Name ?? "N/A"}");
            sb.AppendLine($"  Balance: {token.Balance ?? "N/A"}");
        }

        sb.AppendLine($"---");
        sb.AppendLine($"Page {page} of {result.PageCount}");

        return sb.ToString();
    }

    [McpServerTool, Description("Get fungible token actions (transfers, mints, burns) on the Casper Network.")]
    public static async Task<string> GetFungibleTokenActions(
        CasperCloudRestClient client,
        CasperMcpOptions options,
        [Description("Page number (default: 1)")] int page = 1,
        [Description("Number of results per page (default: 10, max: 250)")] int pageSize = 10)
    {
        var endpoint = options.IsTestnet ? (INetworkEndpoint)client.Testnet : client.Mainnet;
        var parameters = new FTActionRequestParameters
        {
            PageNumber = page,
            PageSize = Math.Min(pageSize, 250)
        };
        // The public keys and the token's package are optional properties: without these flags
        // From/To always fall back to a raw account hash and the token is a bare package hash.
        parameters.OptionalParameters.FromPublicKey = true;
        parameters.OptionalParameters.ToPublicKey = true;
        parameters.OptionalParameters.ContractPackage = true;

        var result = await endpoint.FT.GetFTActionsAsync(parameters);

        if (result?.Data is null || result.Data.Count == 0)
            return "No fungible token actions found.";

        var sb = new StringBuilder();
        sb.AppendLine($"## Fungible Token Actions (Page {page}, {result.ItemCount} total)");

        foreach (var action in result.Data)
        {
            sb.AppendLine($"---");
            sb.AppendLine($"- **Deploy:** {FormattingHelpers.FormatHash(action.DeployHash)}");
            sb.AppendLine($"  Token: {NameHelpers.Labeled(action.ContractPackage?.Name, action.ContractPackageHash)}");
            sb.AppendLine($"  From: {FormattingHelpers.FormatHash(action.FromPublicKey ?? action.FromHash)}");
            sb.AppendLine($"  To: {FormattingHelpers.FormatHash(action.ToPublicKey ?? action.ToHash)}");
            sb.AppendLine($"  Amount: {action.Amount ?? "N/A"} | {FormattingHelpers.FormatTimestamp(action.Timestamp)}");
        }

        sb.AppendLine($"---");
        sb.AppendLine($"Page {page} of {result.PageCount}");

        return sb.ToString();
    }

    [McpServerTool, Description("Get fungible token actions for a specific account on the Casper Network.")]
    public static async Task<string> GetAccountFungibleTokenActions(
        CasperCloudRestClient client,
        CasperMcpOptions options,
        [Description("The public key or account hash")] string accountIdentifier,
        [Description("Page number (default: 1)")] int page = 1,
        [Description("Number of results per page (default: 10, max: 250)")] int pageSize = 10)
    {
        var endpoint = options.IsTestnet ? (INetworkEndpoint)client.Testnet : client.Mainnet;
        var parameters = new FTAccountActionRequestParameters
        {
            PageNumber = page,
            PageSize = Math.Min(pageSize, 250)
        };
        // The public keys and the token's package are optional properties: without these flags
        // From/To always fall back to a raw account hash and the token is a bare package hash.
        parameters.OptionalParameters.FromPublicKey = true;
        parameters.OptionalParameters.ToPublicKey = true;
        parameters.OptionalParameters.ContractPackage = true;

        var result = await endpoint.FT.GetAccountFTActionsAsync(accountIdentifier, parameters);

        if (result?.Data is null || result.Data.Count == 0)
            return $"No fungible token actions found for account: {accountIdentifier}";

        var sb = new StringBuilder();
        sb.AppendLine($"## Account Fungible Token Actions (Page {page}, {result.ItemCount} total)");

        foreach (var action in result.Data)
        {
            sb.AppendLine($"---");
            sb.AppendLine($"- **Deploy:** {FormattingHelpers.FormatHash(action.DeployHash)}");
            sb.AppendLine($"  Token: {NameHelpers.Labeled(action.ContractPackage?.Name, action.ContractPackageHash)}");
            sb.AppendLine($"  From: {FormattingHelpers.FormatHash(action.FromPublicKey ?? action.FromHash)}");
            sb.AppendLine($"  To: {FormattingHelpers.FormatHash(action.ToPublicKey ?? action.ToHash)}");
            sb.AppendLine($"  Amount: {action.Amount ?? "N/A"} | {FormattingHelpers.FormatTimestamp(action.Timestamp)}");
        }

        sb.AppendLine($"---");
        sb.AppendLine($"Page {page} of {result.PageCount}");

        return sb.ToString();
    }

    [McpServerTool, Description("Get fungible token actions for a specific contract package on the Casper Network.")]
    public static async Task<string> GetContractPackageFungibleTokenActions(
        CasperCloudRestClient client,
        CasperMcpOptions options,
        [Description("The contract package hash")] string contractPackageHash,
        [Description("Page number (default: 1)")] int page = 1,
        [Description("Number of results per page (default: 10, max: 250)")] int pageSize = 10)
    {
        var endpoint = options.IsTestnet ? (INetworkEndpoint)client.Testnet : client.Mainnet;
        var parameters = new FTContractPackageActionRequestParameters
        {
            PageNumber = page,
            PageSize = Math.Min(pageSize, 250)
        };
        // The public keys are optional properties: without these flags From/To always fall back to
        // a raw account hash. ContractPackage is left off on purpose here — every row is the package
        // the caller named, and nothing in this view renders it.
        parameters.OptionalParameters.FromPublicKey = true;
        parameters.OptionalParameters.ToPublicKey = true;

        var result = await endpoint.FT.GetContractPackageFTActionsAsync(contractPackageHash, parameters);

        if (result?.Data is null || result.Data.Count == 0)
            return $"No fungible token actions found for contract package: {contractPackageHash}";

        var sb = new StringBuilder();
        sb.AppendLine($"## Contract Package FT Actions (Page {page}, {result.ItemCount} total)");

        foreach (var action in result.Data)
        {
            sb.AppendLine($"---");
            sb.AppendLine($"- **Deploy:** {FormattingHelpers.FormatHash(action.DeployHash)}");
            sb.AppendLine($"  From: {FormattingHelpers.FormatHash(action.FromPublicKey ?? action.FromHash)}");
            sb.AppendLine($"  To: {FormattingHelpers.FormatHash(action.ToPublicKey ?? action.ToHash)}");
            sb.AppendLine($"  Amount: {action.Amount ?? "N/A"} | {FormattingHelpers.FormatTimestamp(action.Timestamp)}");
        }

        sb.AppendLine($"---");
        sb.AppendLine($"Page {page} of {result.PageCount}");

        return sb.ToString();
    }

    [McpServerTool, Description("Get the list of fungible token action types on the Casper Network (e.g., transfer, mint, burn).")]
    public static async Task<string> GetFtActionTypes(
        CasperCloudRestClient client,
        CasperMcpOptions options)
    {
        var endpoint = options.IsTestnet ? (INetworkEndpoint)client.Testnet : client.Mainnet;
        var result = await endpoint.FT.GetFTTokenActionTypesAsync();

        if (result?.Data is null || result.Data.Count == 0)
            return "No FT action types found.";

        var sb = new StringBuilder();
        sb.AppendLine($"## Fungible Token Action Types");

        foreach (var actionType in result.Data)
        {
            sb.AppendLine($"- **ID:** {actionType.Id?.ToString() ?? "N/A"} | **Name:** {actionType.Name ?? "N/A"}");
        }

        return sb.ToString();
    }
}
