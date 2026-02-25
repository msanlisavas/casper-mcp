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
}
