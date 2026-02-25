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

    [McpServerTool, Description("Get a specific NFT by contract package hash and token ID on the Casper Network.")]
    public static async Task<string> GetNft(
        CasperCloudRestClient client,
        CasperMcpOptions options,
        [Description("The contract package hash of the NFT collection")] string contractPackageHash,
        [Description("The token ID")] string tokenId)
    {
        try
        {
            var endpoint = options.IsTestnet ? (INetworkEndpoint)client.Testnet : client.Mainnet;
            var result = await endpoint.NFT.GetNFTAsync(contractPackageHash, tokenId);

            if (result?.Data is null)
                return $"NFT not found: {contractPackageHash} / {tokenId}";

            var nft = result.Data;
            var sb = new StringBuilder();
            sb.AppendLine($"## NFT Details");
            sb.AppendLine($"- **Contract Package:** {FormattingHelpers.FormatHash(nft.ContractPackageHash)}");
            sb.AppendLine($"- **Token ID:** {nft.TokenId ?? "N/A"}");
            sb.AppendLine($"- **Owner:** {FormattingHelpers.FormatHash(nft.OwnerPublicKey ?? nft.OwnerHash)}");
            sb.AppendLine($"- **Burned:** {FormattingHelpers.FormatBool(nft.IsBurned)}");
            sb.AppendLine($"- **Block Height:** {nft.BlockHeight}");
            sb.AppendLine($"- **Timestamp:** {FormattingHelpers.FormatTimestamp(nft.Timestamp)}");
            sb.AppendLine($"- **Token Standard ID:** {nft.TokenStandardId}");

            return sb.ToString();
        }
        catch (Exception ex)
        {
            return $"Error retrieving NFT: {ex.Message}";
        }
    }

    [McpServerTool, Description("Get the list of NFT standards supported on the Casper Network.")]
    public static async Task<string> GetNftStandards(
        CasperCloudRestClient client,
        CasperMcpOptions options)
    {
        try
        {
            var endpoint = options.IsTestnet ? (INetworkEndpoint)client.Testnet : client.Mainnet;
            var result = await endpoint.NFT.GetNFTStandardsAsync();

            if (result?.Data is null || result.Data.Count == 0)
                return "No NFT standards found.";

            var sb = new StringBuilder();
            sb.AppendLine($"## NFT Standards ({result.Data.Count} total)");

            foreach (var standard in result.Data)
            {
                sb.AppendLine($"- **ID:** {standard.Id} | **Name:** {standard.Name ?? "N/A"}");
            }

            return sb.ToString();
        }
        catch (Exception ex)
        {
            return $"Error retrieving NFT standards: {ex.Message}";
        }
    }

    [McpServerTool, Description("Get the list of offchain NFT metadata statuses on the Casper Network.")]
    public static async Task<string> GetNftMetadataStatuses(
        CasperCloudRestClient client,
        CasperMcpOptions options)
    {
        try
        {
            var endpoint = options.IsTestnet ? (INetworkEndpoint)client.Testnet : client.Mainnet;
            var result = await endpoint.NFT.GetOffchainNFTMetadataStatusesAsync();

            if (result?.Data is null || result.Data.Count == 0)
                return "No NFT metadata statuses found.";

            var sb = new StringBuilder();
            sb.AppendLine($"## NFT Metadata Statuses ({result.Data.Count} total)");

            foreach (var status in result.Data)
            {
                sb.AppendLine($"- **ID:** {status.Id} | **Name:** {status.Name ?? "N/A"}");
            }

            return sb.ToString();
        }
        catch (Exception ex)
        {
            return $"Error retrieving NFT metadata statuses: {ex.Message}";
        }
    }

    [McpServerTool, Description("Get NFT actions for a specific token in a collection on the Casper Network.")]
    public static async Task<string> GetNftActionsForToken(
        CasperCloudRestClient client,
        CasperMcpOptions options,
        [Description("The contract package hash of the NFT collection")] string contractPackageHash,
        [Description("The token ID")] string tokenId,
        [Description("Page number (default: 1)")] int page = 1,
        [Description("Number of results per page (default: 10, max: 250)")] int pageSize = 10)
    {
        try
        {
            var endpoint = options.IsTestnet ? (INetworkEndpoint)client.Testnet : client.Mainnet;
            var parameters = new NFTContractPackageTokenActionsRequestParameters
            {
                PageNumber = page,
                PageSize = Math.Min(pageSize, 250)
            };

            var result = await endpoint.NFT.GetContractPackageNFTActionsForATokenAsync(contractPackageHash, tokenId, parameters);

            if (result?.Data is null || result.Data.Count == 0)
                return $"No actions found for token {tokenId} in collection: {contractPackageHash}";

            var sb = new StringBuilder();
            sb.AppendLine($"## NFT Token Actions (Page {page}, {result.ItemCount} total)");
            sb.AppendLine($"Collection: {contractPackageHash} | Token: {tokenId}");

            foreach (var action in result.Data)
            {
                sb.AppendLine($"---");
                sb.AppendLine($"- **Deploy:** {FormattingHelpers.FormatHash(action.DeployHash)}");
                sb.AppendLine($"  From: {FormattingHelpers.FormatHash(action.FromPublicKey ?? action.FromHash)}");
                sb.AppendLine($"  To: {FormattingHelpers.FormatHash(action.ToPublicKey ?? action.ToHash)}");
                sb.AppendLine($"  Action ID: {action.NftActionId} | {FormattingHelpers.FormatTimestamp(action.Timestamp)}");
            }

            sb.AppendLine($"---");
            sb.AppendLine($"Page {page} of {result.PageCount}");

            return sb.ToString();
        }
        catch (Exception ex)
        {
            return $"Error retrieving NFT token actions: {ex.Message}";
        }
    }

    [McpServerTool, Description("Get NFT actions for a specific account on the Casper Network.")]
    public static async Task<string> GetAccountNftActions(
        CasperCloudRestClient client,
        CasperMcpOptions options,
        [Description("The public key or account hash")] string accountIdentifier,
        [Description("Page number (default: 1)")] int page = 1,
        [Description("Number of results per page (default: 10, max: 250)")] int pageSize = 10)
    {
        try
        {
            var endpoint = options.IsTestnet ? (INetworkEndpoint)client.Testnet : client.Mainnet;
            var parameters = new NFTAccountActionsRequestParameters
            {
                PageNumber = page,
                PageSize = Math.Min(pageSize, 250)
            };

            var result = await endpoint.NFT.GetAccountNFTActionsAsync(accountIdentifier, parameters);

            if (result?.Data is null || result.Data.Count == 0)
                return $"No NFT actions found for account: {accountIdentifier}";

            var sb = new StringBuilder();
            sb.AppendLine($"## Account NFT Actions (Page {page}, {result.ItemCount} total)");

            foreach (var action in result.Data)
            {
                sb.AppendLine($"---");
                sb.AppendLine($"- **Deploy:** {FormattingHelpers.FormatHash(action.DeployHash)}");
                sb.AppendLine($"  Token: {action.TokenId ?? "N/A"} | Collection: {FormattingHelpers.FormatHash(action.ContractPackageHash)}");
                sb.AppendLine($"  From: {FormattingHelpers.FormatHash(action.FromPublicKey ?? action.FromHash)}");
                sb.AppendLine($"  To: {FormattingHelpers.FormatHash(action.ToPublicKey ?? action.ToHash)}");
                sb.AppendLine($"  Action ID: {action.NftActionId} | {FormattingHelpers.FormatTimestamp(action.Timestamp)}");
            }

            sb.AppendLine($"---");
            sb.AppendLine($"Page {page} of {result.PageCount}");

            return sb.ToString();
        }
        catch (Exception ex)
        {
            return $"Error retrieving account NFT actions: {ex.Message}";
        }
    }

    [McpServerTool, Description("Get NFT actions for a specific contract package on the Casper Network.")]
    public static async Task<string> GetContractPackageNftActions(
        CasperCloudRestClient client,
        CasperMcpOptions options,
        [Description("The contract package hash")] string contractPackageHash,
        [Description("Page number (default: 1)")] int page = 1,
        [Description("Number of results per page (default: 10, max: 250)")] int pageSize = 10)
    {
        try
        {
            var endpoint = options.IsTestnet ? (INetworkEndpoint)client.Testnet : client.Mainnet;
            var parameters = new NFTContractPackageActionsRequestParameters
            {
                PageNumber = page,
                PageSize = Math.Min(pageSize, 250)
            };

            var result = await endpoint.NFT.GetContractPackageNFTActionsAsync(contractPackageHash, parameters);

            if (result?.Data is null || result.Data.Count == 0)
                return $"No NFT actions found for contract package: {contractPackageHash}";

            var sb = new StringBuilder();
            sb.AppendLine($"## Contract Package NFT Actions (Page {page}, {result.ItemCount} total)");

            foreach (var action in result.Data)
            {
                sb.AppendLine($"---");
                sb.AppendLine($"- **Deploy:** {FormattingHelpers.FormatHash(action.DeployHash)}");
                sb.AppendLine($"  Token: {action.TokenId ?? "N/A"}");
                sb.AppendLine($"  From: {FormattingHelpers.FormatHash(action.FromPublicKey ?? action.FromHash)}");
                sb.AppendLine($"  To: {FormattingHelpers.FormatHash(action.ToPublicKey ?? action.ToHash)}");
                sb.AppendLine($"  Action ID: {action.NftActionId} | {FormattingHelpers.FormatTimestamp(action.Timestamp)}");
            }

            sb.AppendLine($"---");
            sb.AppendLine($"Page {page} of {result.PageCount}");

            return sb.ToString();
        }
        catch (Exception ex)
        {
            return $"Error retrieving contract package NFT actions: {ex.Message}";
        }
    }

    [McpServerTool, Description("Get the list of NFT action types on the Casper Network.")]
    public static async Task<string> GetNftActionTypes(
        CasperCloudRestClient client,
        CasperMcpOptions options)
    {
        try
        {
            var endpoint = options.IsTestnet ? (INetworkEndpoint)client.Testnet : client.Mainnet;
            var result = await endpoint.NFT.GetNFTActionTypesAsync();

            if (result?.Data is null || result.Data.Count == 0)
                return "No NFT action types found.";

            var sb = new StringBuilder();
            sb.AppendLine($"## NFT Action Types ({result.Data.Count} total)");

            foreach (var actionType in result.Data)
            {
                sb.AppendLine($"- **ID:** {actionType.Id?.ToString() ?? "N/A"} | **Name:** {actionType.Name ?? "N/A"}");
            }

            return sb.ToString();
        }
        catch (Exception ex)
        {
            return $"Error retrieving NFT action types: {ex.Message}";
        }
    }

    [McpServerTool, Description("Get NFT ownership distribution for a specific contract package on the Casper Network.")]
    public static async Task<string> GetContractPackageNftOwnership(
        CasperCloudRestClient client,
        CasperMcpOptions options,
        [Description("The contract package hash")] string contractPackageHash,
        [Description("Page number (default: 1)")] int page = 1,
        [Description("Number of results per page (default: 10, max: 250)")] int pageSize = 10)
    {
        try
        {
            var endpoint = options.IsTestnet ? (INetworkEndpoint)client.Testnet : client.Mainnet;
            var parameters = new NFTContractPackageOwnershipRequestParameters
            {
                PageNumber = page,
                PageSize = Math.Min(pageSize, 250)
            };

            var result = await endpoint.NFT.GetContractPackageNFTOwnershipAsync(contractPackageHash, parameters);

            if (result?.Data is null || result.Data.Count == 0)
                return $"No NFT ownership data found for contract package: {contractPackageHash}";

            var sb = new StringBuilder();
            sb.AppendLine($"## NFT Ownership by Contract Package (Page {page}, {result.ItemCount} total)");

            foreach (var owner in result.Data)
            {
                sb.AppendLine($"---");
                sb.AppendLine($"- **Owner:** {FormattingHelpers.FormatHash(owner.OwnerPublicKey ?? owner.OwnerHash)}");
                sb.AppendLine($"  Tokens Owned: {FormattingHelpers.FormatNumber(owner.TokensNumber)}");
            }

            sb.AppendLine($"---");
            sb.AppendLine($"Page {page} of {result.PageCount}");

            return sb.ToString();
        }
        catch (Exception ex)
        {
            return $"Error retrieving NFT ownership: {ex.Message}";
        }
    }

    [McpServerTool, Description("Get NFT ownership summary for a specific account on the Casper Network.")]
    public static async Task<string> GetAccountNftOwnership(
        CasperCloudRestClient client,
        CasperMcpOptions options,
        [Description("The public key or account hash")] string accountIdentifier,
        [Description("Page number (default: 1)")] int page = 1,
        [Description("Number of results per page (default: 10, max: 250)")] int pageSize = 10)
    {
        try
        {
            var endpoint = options.IsTestnet ? (INetworkEndpoint)client.Testnet : client.Mainnet;
            var parameters = new NFTAccountOwnershipRequestParameters
            {
                PageNumber = page,
                PageSize = Math.Min(pageSize, 250)
            };

            var result = await endpoint.NFT.GetAccountNFTOwnershipAsync(accountIdentifier, parameters);

            if (result?.Data is null || result.Data.Count == 0)
                return $"No NFT ownership data found for account: {accountIdentifier}";

            var sb = new StringBuilder();
            sb.AppendLine($"## Account NFT Ownership (Page {page}, {result.ItemCount} total)");

            foreach (var ownership in result.Data)
            {
                sb.AppendLine($"---");
                sb.AppendLine($"- **Collection:** {FormattingHelpers.FormatHash(ownership.ContractPackageHash)}");
                if (ownership.ContractPackage is not null)
                    sb.AppendLine($"  Name: {ownership.ContractPackage.Name ?? "N/A"}");
                sb.AppendLine($"  Tokens Owned: {FormattingHelpers.FormatNumber(ownership.TokensNumber)}");
            }

            sb.AppendLine($"---");
            sb.AppendLine($"Page {page} of {result.PageCount}");

            return sb.ToString();
        }
        catch (Exception ex)
        {
            return $"Error retrieving account NFT ownership: {ex.Message}";
        }
    }
}
