using System.ComponentModel;
using System.Text;
using CasperMcp.Configuration;
using CasperMcp.Helpers;
using CSPR.Cloud.Net.Clients;
using CSPR.Cloud.Net.Parameters.Wrapper.Delegate;
using CSPR.Cloud.Net.Parameters.Wrapper.Deploy;
using ModelContextProtocol.Server;

namespace CasperMcp.Tools;

[McpServerToolType]
public static class AccountTools
{
    [McpServerTool, Description("Get detailed information about a Casper Network account by public key or account hash, including balance, staking info, and delegation status.")]
    public static async Task<string> GetAccountInfo(
        CasperCloudRestClient client,
        CasperMcpOptions options,
        [Description("The public key or account hash of the account")] string accountIdentifier)
    {
        try
        {
            var endpoint = options.IsTestnet ? (INetworkEndpoint)client.Testnet : client.Mainnet;
            var account = await endpoint.Account.GetAccountAsync(accountIdentifier);

            if (account is null)
                return $"Account not found: {accountIdentifier}";

            var sb = new StringBuilder();
            sb.AppendLine($"## Account Information");
            sb.AppendLine($"- **Public Key:** {FormattingHelpers.FormatHash(account.PublicKey)}");
            sb.AppendLine($"- **Account Hash:** {FormattingHelpers.FormatHash(account.AccountHash)}");
            sb.AppendLine($"- **Balance:** {FormattingHelpers.MotesToCspr(account.Balance)}");
            sb.AppendLine($"- **Staked Balance:** {FormattingHelpers.MotesToCspr(account.StakedBalance)}");
            sb.AppendLine($"- **Delegated Balance:** {FormattingHelpers.MotesToCspr(account.DelegatedBalance)}");
            sb.AppendLine($"- **Undelegated Balance:** {FormattingHelpers.MotesToCspr(account.UndelegatedBalance)}");
            sb.AppendLine($"- **Auction Status:** {account.AuctionStatus ?? "N/A"}");
            sb.AppendLine($"- **Main Purse:** {FormattingHelpers.FormatHash(account.MainPurseUref)}");

            return sb.ToString();
        }
        catch (Exception ex)
        {
            return $"Error retrieving account info: {ex.Message}";
        }
    }

    [McpServerTool, Description("Get the CSPR balance of a Casper Network account.")]
    public static async Task<string> GetAccountBalance(
        CasperCloudRestClient client,
        CasperMcpOptions options,
        [Description("The public key or account hash of the account")] string accountIdentifier)
    {
        try
        {
            var endpoint = options.IsTestnet ? (INetworkEndpoint)client.Testnet : client.Mainnet;
            var account = await endpoint.Account.GetAccountAsync(accountIdentifier);

            if (account is null)
                return $"Account not found: {accountIdentifier}";

            var sb = new StringBuilder();
            sb.AppendLine($"## Account Balance");
            sb.AppendLine($"- **Public Key:** {FormattingHelpers.FormatHash(account.PublicKey)}");
            sb.AppendLine($"- **Liquid Balance:** {FormattingHelpers.MotesToCspr(account.Balance)}");
            sb.AppendLine($"- **Staked Balance:** {FormattingHelpers.MotesToCspr(account.StakedBalance)}");
            sb.AppendLine($"- **Delegated Balance:** {FormattingHelpers.MotesToCspr(account.DelegatedBalance)}");

            var totalBalance = (account.Balance ?? 0) + (account.StakedBalance ?? 0) + (account.DelegatedBalance ?? 0);
            sb.AppendLine($"- **Total (liquid + staked + delegated):** {FormattingHelpers.MotesToCspr(totalBalance)}");

            return sb.ToString();
        }
        catch (Exception ex)
        {
            return $"Error retrieving account balance: {ex.Message}";
        }
    }

    [McpServerTool, Description("Get recent deploys (transactions) for a Casper Network account.")]
    public static async Task<string> GetAccountDeploys(
        CasperCloudRestClient client,
        CasperMcpOptions options,
        [Description("The public key of the account")] string publicKey,
        [Description("Page number (default: 1)")] int page = 1,
        [Description("Number of results per page (default: 10, max: 250)")] int pageSize = 10)
    {
        try
        {
            var endpoint = options.IsTestnet ? (INetworkEndpoint)client.Testnet : client.Mainnet;
            var parameters = new AccountDeploysRequestParameters
            {
                PageNumber = page,
                PageSize = Math.Min(pageSize, 250)
            };

            var result = await endpoint.Deploy.GetAccountDeploysAsync(publicKey, parameters);

            if (result?.Data is null || result.Data.Count == 0)
                return $"No deploys found for account: {publicKey}";

            var sb = new StringBuilder();
            sb.AppendLine($"## Account Deploys (Page {page}, {result.ItemCount} total)");

            foreach (var deploy in result.Data)
            {
                sb.AppendLine($"---");
                sb.AppendLine($"- **Deploy Hash:** {FormattingHelpers.FormatHash(deploy.DeployHash)}");
                sb.AppendLine($"- **Status:** {deploy.Status ?? "N/A"}");
                sb.AppendLine($"- **Cost:** {FormattingHelpers.MotesToCspr(deploy.Cost)}");
                sb.AppendLine($"- **Block Height:** {deploy.BlockHeight?.ToString() ?? "N/A"}");
                sb.AppendLine($"- **Timestamp:** {FormattingHelpers.FormatTimestamp(deploy.Timestamp)}");
                if (!string.IsNullOrEmpty(deploy.ErrorMessage))
                    sb.AppendLine($"- **Error:** {deploy.ErrorMessage}");
            }

            sb.AppendLine($"---");
            sb.AppendLine($"Page {page} of {result.PageCount}");

            return sb.ToString();
        }
        catch (Exception ex)
        {
            return $"Error retrieving account deploys: {ex.Message}";
        }
    }

    [McpServerTool, Description("Get delegation information for a Casper Network account, showing which validators the account has delegated to.")]
    public static async Task<string> GetAccountDelegations(
        CasperCloudRestClient client,
        CasperMcpOptions options,
        [Description("The public key of the account")] string publicKey,
        [Description("Page number (default: 1)")] int page = 1,
        [Description("Number of results per page (default: 10, max: 250)")] int pageSize = 10)
    {
        try
        {
            var endpoint = options.IsTestnet ? (INetworkEndpoint)client.Testnet : client.Mainnet;
            var parameters = new DelegationRequestParameters
            {
                PageNumber = page,
                PageSize = Math.Min(pageSize, 250)
            };

            var result = await endpoint.Delegate.GetAccountDelegationsAsync(publicKey, parameters);

            if (result?.Data is null || result.Data.Count == 0)
                return $"No delegations found for account: {publicKey}";

            var sb = new StringBuilder();
            sb.AppendLine($"## Account Delegations (Page {page}, {result.ItemCount} total)");

            foreach (var delegation in result.Data)
            {
                sb.AppendLine($"---");
                sb.AppendLine($"- **Validator:** {FormattingHelpers.FormatHash(delegation.ValidatorPublicKey)}");
                sb.AppendLine($"- **Staked Amount:** {FormattingHelpers.MotesToCspr(delegation.Stake)}");
            }

            sb.AppendLine($"---");
            sb.AppendLine($"Page {page} of {result.PageCount}");

            return sb.ToString();
        }
        catch (Exception ex)
        {
            return $"Error retrieving account delegations: {ex.Message}";
        }
    }
}
