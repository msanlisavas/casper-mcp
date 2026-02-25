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
        try
        {
            var endpoint = options.IsTestnet ? (INetworkEndpoint)client.Testnet : client.Mainnet;
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
            sb.AppendLine($"- **Deploys:** {pkg.DeploysNumber?.ToString() ?? "N/A"}");
            sb.AppendLine($"- **Created:** {FormattingHelpers.FormatTimestamp(pkg.Timestamp)}");

            return sb.ToString();
        }
        catch (Exception ex)
        {
            return $"Error retrieving token info: {ex.Message}";
        }
    }

    [McpServerTool, Description("Get the holders (ownership list) of a fungible token on the Casper Network.")]
    public static async Task<string> GetFtTokenHolders(
        CasperCloudRestClient client,
        CasperMcpOptions options,
        [Description("The contract package hash of the fungible token")] string contractPackageHash,
        [Description("Page number (default: 1)")] int page = 1,
        [Description("Number of results per page (default: 10, max: 250)")] int pageSize = 10)
    {
        try
        {
            var endpoint = options.IsTestnet ? (INetworkEndpoint)client.Testnet : client.Mainnet;
            var parameters = new FTContractPackageOwnershipRequestParameters
            {
                PageNumber = page,
                PageSize = Math.Min(pageSize, 250)
            };

            var result = await endpoint.FT.GetContractPackageFTOwnershipAsync(contractPackageHash, parameters);

            if (result?.Data is null || result.Data.Count == 0)
                return $"No holders found for token: {contractPackageHash}";

            var sb = new StringBuilder();
            sb.AppendLine($"## Token Holders (Page {page}, {result.ItemCount} total)");

            foreach (var holder in result.Data)
            {
                sb.AppendLine($"---");
                sb.AppendLine($"- **Owner:** {FormattingHelpers.FormatHash(holder.OwnerHash)}");
                sb.AppendLine($"  Balance: {holder.Balance ?? "N/A"}");
            }

            sb.AppendLine($"---");
            sb.AppendLine($"Page {page} of {result.PageCount}");

            return sb.ToString();
        }
        catch (Exception ex)
        {
            return $"Error retrieving token holders: {ex.Message}";
        }
    }

    [McpServerTool, Description("Get fungible token balances for a Casper Network account.")]
    public static async Task<string> GetAccountFtBalances(
        CasperCloudRestClient client,
        CasperMcpOptions options,
        [Description("The public key or account hash")] string accountIdentifier,
        [Description("Page number (default: 1)")] int page = 1,
        [Description("Number of results per page (default: 10, max: 250)")] int pageSize = 10)
    {
        try
        {
            var endpoint = options.IsTestnet ? (INetworkEndpoint)client.Testnet : client.Mainnet;
            var parameters = new FTAccountOwnershipRequestParameters
            {
                PageNumber = page,
                PageSize = Math.Min(pageSize, 250)
            };

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
        catch (Exception ex)
        {
            return $"Error retrieving account FT balances: {ex.Message}";
        }
    }

    [McpServerTool, Description("Get fungible token actions (transfers, mints, burns) on the Casper Network.")]
    public static async Task<string> GetFungibleTokenActions(
        CasperCloudRestClient client,
        CasperMcpOptions options,
        [Description("Page number (default: 1)")] int page = 1,
        [Description("Number of results per page (default: 10, max: 250)")] int pageSize = 10)
    {
        try
        {
            var endpoint = options.IsTestnet ? (INetworkEndpoint)client.Testnet : client.Mainnet;
            var parameters = new FTActionRequestParameters
            {
                PageNumber = page,
                PageSize = Math.Min(pageSize, 250)
            };

            var result = await endpoint.FT.GetFTActionsAsync(parameters);

            if (result?.Data is null || result.Data.Count == 0)
                return "No fungible token actions found.";

            var sb = new StringBuilder();
            sb.AppendLine($"## Fungible Token Actions (Page {page}, {result.ItemCount} total)");

            foreach (var action in result.Data)
            {
                sb.AppendLine($"---");
                sb.AppendLine($"- **Deploy:** {FormattingHelpers.FormatHash(action.DeployHash)}");
                sb.AppendLine($"  Token: {FormattingHelpers.FormatHash(action.ContractPackageHash)}");
                sb.AppendLine($"  From: {FormattingHelpers.FormatHash(action.FromPublicKey ?? action.FromHash)}");
                sb.AppendLine($"  To: {FormattingHelpers.FormatHash(action.ToPublicKey ?? action.ToHash)}");
                sb.AppendLine($"  Amount: {action.Amount ?? "N/A"} | {FormattingHelpers.FormatTimestamp(action.Timestamp)}");
            }

            sb.AppendLine($"---");
            sb.AppendLine($"Page {page} of {result.PageCount}");

            return sb.ToString();
        }
        catch (Exception ex)
        {
            return $"Error retrieving fungible token actions: {ex.Message}";
        }
    }

    [McpServerTool, Description("Get fungible token actions for a specific account on the Casper Network.")]
    public static async Task<string> GetAccountFungibleTokenActions(
        CasperCloudRestClient client,
        CasperMcpOptions options,
        [Description("The public key or account hash")] string accountIdentifier,
        [Description("Page number (default: 1)")] int page = 1,
        [Description("Number of results per page (default: 10, max: 250)")] int pageSize = 10)
    {
        try
        {
            var endpoint = options.IsTestnet ? (INetworkEndpoint)client.Testnet : client.Mainnet;
            var parameters = new FTAccountActionRequestParameters
            {
                PageNumber = page,
                PageSize = Math.Min(pageSize, 250)
            };

            var result = await endpoint.FT.GetAccountFTActionsAsync(accountIdentifier, parameters);

            if (result?.Data is null || result.Data.Count == 0)
                return $"No fungible token actions found for account: {accountIdentifier}";

            var sb = new StringBuilder();
            sb.AppendLine($"## Account Fungible Token Actions (Page {page}, {result.ItemCount} total)");

            foreach (var action in result.Data)
            {
                sb.AppendLine($"---");
                sb.AppendLine($"- **Deploy:** {FormattingHelpers.FormatHash(action.DeployHash)}");
                sb.AppendLine($"  Token: {FormattingHelpers.FormatHash(action.ContractPackageHash)}");
                sb.AppendLine($"  From: {FormattingHelpers.FormatHash(action.FromPublicKey ?? action.FromHash)}");
                sb.AppendLine($"  To: {FormattingHelpers.FormatHash(action.ToPublicKey ?? action.ToHash)}");
                sb.AppendLine($"  Amount: {action.Amount ?? "N/A"} | {FormattingHelpers.FormatTimestamp(action.Timestamp)}");
            }

            sb.AppendLine($"---");
            sb.AppendLine($"Page {page} of {result.PageCount}");

            return sb.ToString();
        }
        catch (Exception ex)
        {
            return $"Error retrieving account fungible token actions: {ex.Message}";
        }
    }

    [McpServerTool, Description("Get fungible token actions for a specific contract package on the Casper Network.")]
    public static async Task<string> GetContractPackageFungibleTokenActions(
        CasperCloudRestClient client,
        CasperMcpOptions options,
        [Description("The contract package hash")] string contractPackageHash,
        [Description("Page number (default: 1)")] int page = 1,
        [Description("Number of results per page (default: 10, max: 250)")] int pageSize = 10)
    {
        try
        {
            var endpoint = options.IsTestnet ? (INetworkEndpoint)client.Testnet : client.Mainnet;
            var parameters = new FTContractPackageActionRequestParameters
            {
                PageNumber = page,
                PageSize = Math.Min(pageSize, 250)
            };

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
        catch (Exception ex)
        {
            return $"Error retrieving contract package FT actions: {ex.Message}";
        }
    }
}
