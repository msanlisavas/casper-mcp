using System.ComponentModel;
using System.Text;
using CasperMcp.Configuration;
using CasperMcp.Helpers;
using CSPR.Cloud.Net.Clients;
using CSPR.Cloud.Net.Objects.Transfer;
using CSPR.Cloud.Net.Parameters.OptionalParameters.Transfer;
using CSPR.Cloud.Net.Parameters.Wrapper.Transfer;
using ModelContextProtocol.Server;

namespace CasperMcp.Tools;

[McpServerToolType]
public static class TransferTools
{
    /// <summary>
    /// Both ends of a transfer are OPTIONAL properties. Without these flags CSPR.cloud returns only
    /// the initiator's and the recipient's account hashes, so the public-key branch of every
    /// "From"/"To" line below is dead code and no caller can tell who actually moved the CSPR.
    /// </summary>
    private static TransferAccountOptionalParameters FullTransferIdentity() => new()
    {
        InitiatorPublicKey = true,
        FromPursePublicKey = true,
        ToPublicKey = true,
        FromPurseAccountInfo = true,
        FromPurseCentralizedAccountInfo = true,
        ToAccountInfo = true,
        ToCentralizedAccountInfo = true,
        InitiatorCsprName = true,
        FromPurseCsprName = true,
        ToCsprName = true,
    };

    /// <summary>
    /// The same identity set for the deploy-scoped endpoint, which takes its own parameters type.
    /// </summary>
    private static TransferDeployOptionalParameters FullDeployTransferIdentity() => new()
    {
        InitiatorPublicKey = true,
        FromPursePublicKey = true,
        ToPublicKey = true,
        FromPurseAccountInfo = true,
        FromPurseCentralizedAccountInfo = true,
        ToAccountInfo = true,
        ToCentralizedAccountInfo = true,
        InitiatorCsprName = true,
        FromPurseCsprName = true,
        ToCsprName = true,
    };

    /// <summary>
    /// The sending party exactly as the render picks it - the from-purse's public key when
    /// CSPR.cloud returned one, the initiator's account hash otherwise - labeled with the name that
    /// belongs to whichever of the two is actually printed. The initiator carries no account-info
    /// of its own in this response; a CSPR.name is the only name the API offers for it.
    /// </summary>
    private static string FromParty(TransferData t) =>
        t.FromPursePublicKey is not null
            ? NameHelpers.Labeled(
                NameHelpers.DisplayName(t.FromPurseAccountInfo, t.FromPurseCentralizedAccountInfo, t.FromPurseCsprName),
                t.FromPursePublicKey)
            : NameHelpers.Labeled(
                NameHelpers.DisplayName(null, csprName: t.InitiatorCsprName),
                t.InitiatorAccountHash);

    /// <summary>
    /// The receiving party. It is the same party whether the public key or the account-hash
    /// fallback prints, so one name labels both.
    /// </summary>
    private static string ToParty(TransferData t) =>
        NameHelpers.Labeled(
            NameHelpers.DisplayName(t.ToAccountInfo, t.ToCentralizedAccountInfo, t.ToCsprName),
            t.ToPublicKey ?? t.ToAccountHash);

    [McpServerTool, Description("Get native CSPR transfer history for a Casper Network account.")]
    public static async Task<string> GetTransfers(
        CasperCloudRestClient client,
        CasperMcpOptions options,
        [Description("The public key or account hash")] string accountIdentifier,
        [Description("Page number (default: 1)")] int page = 1,
        [Description("Number of results per page (default: 10, max: 250)")] int pageSize = 10)
    {
        var endpoint = options.IsTestnet ? (INetworkEndpoint)client.Testnet : client.Mainnet;
        var parameters = new TransferAccountRequestParameters
        {
            PageNumber = page,
            PageSize = Math.Min(pageSize, 250),
            OptionalParameters = FullTransferIdentity()
        };

        var result = await endpoint.Transfer.GetAccountTransfersAsync(accountIdentifier, parameters);

        if (result?.Data is null || result.Data.Count == 0)
            return $"No transfers found for account: {accountIdentifier}";

        var sb = new StringBuilder();
        sb.AppendLine($"## Account Transfers (Page {page}, {result.ItemCount} total)");

        foreach (var t in result.Data)
        {
            sb.AppendLine($"---");
            sb.AppendLine($"- **Deploy:** {FormattingHelpers.FormatHash(t.DeployHash)}");
            sb.AppendLine($"  From: {FromParty(t)}");
            sb.AppendLine($"  To: {ToParty(t)}");
            sb.AppendLine($"  Amount: {FormattingHelpers.MotesToCspr(t.Amount)}");
            sb.AppendLine($"  Block: {t.BlockHeight?.ToString() ?? "N/A"} | {FormattingHelpers.FormatTimestamp(t.Timestamp)}");
        }

        sb.AppendLine($"---");
        sb.AppendLine($"Page {page} of {result.PageCount}");

        return sb.ToString();
    }

    [McpServerTool, Description("Get native CSPR transfers for a specific deploy on the Casper Network.")]
    public static async Task<string> GetDeployTransfers(
        CasperCloudRestClient client,
        CasperMcpOptions options,
        [Description("The deploy hash")] string deployHash,
        [Description("Page number (default: 1)")] int page = 1,
        [Description("Number of results per page (default: 10, max: 250)")] int pageSize = 10)
    {
        var endpoint = options.IsTestnet ? (INetworkEndpoint)client.Testnet : client.Mainnet;
        var parameters = new TransferDeployRequestParameters
        {
            PageNumber = page,
            PageSize = Math.Min(pageSize, 250),
            OptionalParameters = FullDeployTransferIdentity()
        };

        var result = await endpoint.Transfer.GetDeployTransfersAsync(deployHash, parameters);

        if (result?.Data is null || result.Data.Count == 0)
            return $"No transfers found for deploy: {deployHash}";

        var sb = new StringBuilder();
        sb.AppendLine($"## Deploy Transfers (Page {page}, {result.ItemCount} total)");
        sb.AppendLine($"Deploy: {deployHash}");

        foreach (var t in result.Data)
        {
            sb.AppendLine($"---");
            sb.AppendLine($"- From: {FromParty(t)}");
            sb.AppendLine($"  To: {ToParty(t)}");
            sb.AppendLine($"  Amount: {FormattingHelpers.MotesToCspr(t.Amount)}");
            sb.AppendLine($"  Block: {t.BlockHeight?.ToString() ?? "N/A"} | {FormattingHelpers.FormatTimestamp(t.Timestamp)}");
        }

        sb.AppendLine($"---");
        sb.AppendLine($"Page {page} of {result.PageCount}");

        return sb.ToString();
    }

    [McpServerTool, Description("Get native CSPR transfers for a specific purse on the Casper Network.")]
    public static async Task<string> GetPurseTransfers(
        CasperCloudRestClient client,
        CasperMcpOptions options,
        [Description("The purse URef")] string purseUref,
        [Description("Page number (default: 1)")] int page = 1,
        [Description("Number of results per page (default: 10, max: 250)")] int pageSize = 10)
    {
        var endpoint = options.IsTestnet ? (INetworkEndpoint)client.Testnet : client.Mainnet;
        var parameters = new TransferAccountRequestParameters
        {
            PageNumber = page,
            PageSize = Math.Min(pageSize, 250),
            OptionalParameters = FullTransferIdentity()
        };

        var result = await endpoint.Transfer.GetPurseTransfersAsync(purseUref, parameters);

        if (result?.Data is null || result.Data.Count == 0)
            return $"No transfers found for purse: {purseUref}";

        var sb = new StringBuilder();
        sb.AppendLine($"## Purse Transfers (Page {page}, {result.ItemCount} total)");

        foreach (var t in result.Data)
        {
            sb.AppendLine($"---");
            sb.AppendLine($"- **Deploy:** {FormattingHelpers.FormatHash(t.DeployHash)}");
            sb.AppendLine($"  From: {FromParty(t)}");
            sb.AppendLine($"  To: {ToParty(t)}");
            sb.AppendLine($"  Amount: {FormattingHelpers.MotesToCspr(t.Amount)}");
            sb.AppendLine($"  Block: {t.BlockHeight?.ToString() ?? "N/A"} | {FormattingHelpers.FormatTimestamp(t.Timestamp)}");
        }

        sb.AppendLine($"---");
        sb.AppendLine($"Page {page} of {result.PageCount}");

        return sb.ToString();
    }
}
