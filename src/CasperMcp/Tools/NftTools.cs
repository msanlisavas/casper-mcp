using System.ComponentModel;
using System.Text;
using CasperMcp.Configuration;
using CasperMcp.Helpers;
using CSPR.Cloud.Net.Clients;
using CSPR.Cloud.Net.Parameters.Wrapper.Nft;
using ModelContextProtocol.Server;

namespace CasperMcp.Tools;

[McpServerToolType]
public static class NftTools
{
    [McpServerTool, Description("Get information about an NFT collection (contract package) on the Casper Network.")]
    public static async Task<string> GetNftCollection(
        CasperCloudRestClient client,
        CasperMcpOptions options,
        [Description("The contract package hash of the NFT collection")] string contractPackageHash,
        [Description("Page number (default: 1)")] int page = 1,
        [Description("Number of results per page (default: 10, max: 250)")] int pageSize = 10)
    {
        try
        {
            var endpoint = options.IsTestnet ? (INetworkEndpoint)client.Testnet : client.Mainnet;
            var parameters = new NFTContractPackageRequestParameters
            {
                PageNumber = page,
                PageSize = Math.Min(pageSize, 250)
            };

            var result = await endpoint.NFT.GetContractPackageNFTsAsync(contractPackageHash, parameters);

            if (result?.Data is null || result.Data.Count == 0)
                return $"No NFTs found in collection: {contractPackageHash}";

            var sb = new StringBuilder();
            sb.AppendLine($"## NFT Collection (Page {page}, {result.ItemCount} total)");
            sb.AppendLine($"Contract Package: {contractPackageHash}");

            foreach (var nft in result.Data)
            {
                sb.AppendLine($"---");
                sb.AppendLine($"- **Token ID:** {nft.TokenId ?? "N/A"}");
                sb.AppendLine($"  Owner: {FormattingHelpers.FormatHash(nft.OwnerPublicKey ?? nft.OwnerHash)}");
                sb.AppendLine($"  Burned: {FormattingHelpers.FormatBool(nft.IsBurned)} | Minted at Block: {nft.BlockHeight}");
                sb.AppendLine($"  Created: {FormattingHelpers.FormatTimestamp(nft.Timestamp)}");
            }

            sb.AppendLine($"---");
            sb.AppendLine($"Page {page} of {result.PageCount}");

            return sb.ToString();
        }
        catch (Exception ex)
        {
            return $"Error retrieving NFT collection: {ex.Message}";
        }
    }

    [McpServerTool, Description("Get NFTs owned by a Casper Network account.")]
    public static async Task<string> GetAccountNfts(
        CasperCloudRestClient client,
        CasperMcpOptions options,
        [Description("The public key or account hash")] string accountIdentifier,
        [Description("Page number (default: 1)")] int page = 1,
        [Description("Number of results per page (default: 10, max: 250)")] int pageSize = 10)
    {
        try
        {
            var endpoint = options.IsTestnet ? (INetworkEndpoint)client.Testnet : client.Mainnet;
            var parameters = new NFTAccountRequestParameters
            {
                PageNumber = page,
                PageSize = Math.Min(pageSize, 250)
            };

            var result = await endpoint.NFT.GetAccountNFTsAsync(accountIdentifier, parameters);

            if (result?.Data is null || result.Data.Count == 0)
                return $"No NFTs found for account: {accountIdentifier}";

            var sb = new StringBuilder();
            sb.AppendLine($"## Account NFTs (Page {page}, {result.ItemCount} total)");

            foreach (var nft in result.Data)
            {
                sb.AppendLine($"---");
                sb.AppendLine($"- **Token ID:** {nft.TokenId ?? "N/A"}");
                sb.AppendLine($"  Collection: {FormattingHelpers.FormatHash(nft.ContractPackageHash)}");
                if (nft.ContractPackage is not null)
                    sb.AppendLine($"  Name: {nft.ContractPackage.Name ?? "N/A"}");
                sb.AppendLine($"  Burned: {FormattingHelpers.FormatBool(nft.IsBurned)}");
                sb.AppendLine($"  Created: {FormattingHelpers.FormatTimestamp(nft.Timestamp)}");
            }

            sb.AppendLine($"---");
            sb.AppendLine($"Page {page} of {result.PageCount}");

            return sb.ToString();
        }
        catch (Exception ex)
        {
            return $"Error retrieving account NFTs: {ex.Message}";
        }
    }
}
