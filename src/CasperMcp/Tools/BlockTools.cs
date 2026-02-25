using System.ComponentModel;
using System.Text;
using CasperMcp.Configuration;
using CasperMcp.Helpers;
using CSPR.Cloud.Net.Clients;
using CSPR.Cloud.Net.Parameters.Wrapper.Block;
using ModelContextProtocol.Server;

namespace CasperMcp.Tools;

[McpServerToolType]
public static class BlockTools
{
    [McpServerTool, Description("Get detailed information about a specific Casper Network block by its hash.")]
    public static async Task<string> GetBlock(
        CasperCloudRestClient client,
        CasperMcpOptions options,
        [Description("The block hash")] string blockHash)
    {
        try
        {
            var endpoint = options.IsTestnet ? (INetworkEndpoint)client.Testnet : client.Mainnet;
            var block = await endpoint.Block.GetBlockAsync(blockHash);

            if (block is null)
                return $"Block not found: {blockHash}";

            var sb = new StringBuilder();
            sb.AppendLine($"## Block Information");
            sb.AppendLine($"- **Block Height:** {block.BlockHeight?.ToString() ?? "N/A"}");
            sb.AppendLine($"- **Block Hash:** {FormattingHelpers.FormatHash(block.BlockHash)}");
            sb.AppendLine($"- **Parent Hash:** {FormattingHelpers.FormatHash(block.ParentBlockHash)}");
            sb.AppendLine($"- **State Root Hash:** {FormattingHelpers.FormatHash(block.StateRootHash)}");
            sb.AppendLine($"- **Era ID:** {block.EraId?.ToString() ?? "N/A"}");
            sb.AppendLine($"- **Proposer:** {FormattingHelpers.FormatHash(block.ProposerPublicKey)}");
            sb.AppendLine($"- **Native Transfers:** {block.NativeTransfersNumber?.ToString() ?? "0"}");
            sb.AppendLine($"- **Contract Calls:** {block.ContractCallsNumber?.ToString() ?? "0"}");
            sb.AppendLine($"- **Switch Block:** {FormattingHelpers.FormatBool(block.IsSwitchBlock)}");
            sb.AppendLine($"- **Timestamp:** {FormattingHelpers.FormatTimestamp(block.Timestamp)}");

            return sb.ToString();
        }
        catch (Exception ex)
        {
            return $"Error retrieving block: {ex.Message}";
        }
    }

    [McpServerTool, Description("Get the latest blocks from the Casper Network.")]
    public static async Task<string> GetLatestBlocks(
        CasperCloudRestClient client,
        CasperMcpOptions options,
        [Description("Page number (default: 1)")] int page = 1,
        [Description("Number of results per page (default: 10, max: 250)")] int pageSize = 10)
    {
        try
        {
            var endpoint = options.IsTestnet ? (INetworkEndpoint)client.Testnet : client.Mainnet;
            var parameters = new BlockRequestParameters
            {
                PageNumber = page,
                PageSize = Math.Min(pageSize, 250)
            };

            var result = await endpoint.Block.GetBlocksAsync(parameters);

            if (result?.Data is null || result.Data.Count == 0)
                return "No blocks found.";

            var sb = new StringBuilder();
            sb.AppendLine($"## Latest Blocks (Page {page}, {result.ItemCount} total)");

            foreach (var block in result.Data)
            {
                sb.AppendLine($"---");
                sb.AppendLine($"- **Height:** {block.BlockHeight?.ToString() ?? "N/A"} | **Hash:** {block.BlockHash?[..16]}...");
                sb.AppendLine($"  Era: {block.EraId} | Transfers: {block.NativeTransfersNumber ?? 0} | Calls: {block.ContractCallsNumber ?? 0} | {FormattingHelpers.FormatTimestamp(block.Timestamp)}");
            }

            sb.AppendLine($"---");
            sb.AppendLine($"Page {page} of {result.PageCount}");

            return sb.ToString();
        }
        catch (Exception ex)
        {
            return $"Error retrieving latest blocks: {ex.Message}";
        }
    }

    [McpServerTool, Description("Get blocks proposed by a specific validator on the Casper Network.")]
    public static async Task<string> GetValidatorBlocks(
        CasperCloudRestClient client,
        CasperMcpOptions options,
        [Description("The public key of the validator")] string publicKey,
        [Description("Page number (default: 1)")] int page = 1,
        [Description("Number of results per page (default: 10, max: 250)")] int pageSize = 10)
    {
        try
        {
            var endpoint = options.IsTestnet ? (INetworkEndpoint)client.Testnet : client.Mainnet;
            var parameters = new BlockRequestParameters
            {
                PageNumber = page,
                PageSize = Math.Min(pageSize, 250)
            };

            var result = await endpoint.Block.GetValidatorBlocksAsync(publicKey, parameters);

            if (result?.Data is null || result.Data.Count == 0)
                return $"No blocks found for validator: {publicKey}";

            var sb = new StringBuilder();
            sb.AppendLine($"## Validator Blocks (Page {page}, {result.ItemCount} total)");

            foreach (var block in result.Data)
            {
                sb.AppendLine($"---");
                sb.AppendLine($"- **Height:** {block.BlockHeight?.ToString() ?? "N/A"} | **Hash:** {block.BlockHash?[..16]}...");
                sb.AppendLine($"  Era: {block.EraId} | Transfers: {block.NativeTransfersNumber ?? 0} | Calls: {block.ContractCallsNumber ?? 0} | {FormattingHelpers.FormatTimestamp(block.Timestamp)}");
            }

            sb.AppendLine($"---");
            sb.AppendLine($"Page {page} of {result.PageCount}");

            return sb.ToString();
        }
        catch (Exception ex)
        {
            return $"Error retrieving validator blocks: {ex.Message}";
        }
    }
}
