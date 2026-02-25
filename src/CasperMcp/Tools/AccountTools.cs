using System.ComponentModel;
using System.Text;
using CasperMcp.Configuration;
using CasperMcp.Helpers;
using CSPR.Cloud.Net.Clients;
using CSPR.Cloud.Net.Parameters.Wrapper.Accounts;
using CSPR.Cloud.Net.Parameters.Wrapper.Contract;
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

    [McpServerTool, Description("Get a paginated list of all accounts on the Casper Network.")]
    public static async Task<string> GetAccounts(
        CasperCloudRestClient client,
        CasperMcpOptions options,
        [Description("Page number (default: 1)")] int page = 1,
        [Description("Number of results per page (default: 10, max: 250)")] int pageSize = 10)
    {
        try
        {
            var endpoint = options.IsTestnet ? (INetworkEndpoint)client.Testnet : client.Mainnet;
            var parameters = new AccountsRequestParameters
            {
                PageNumber = page,
                PageSize = Math.Min(pageSize, 250)
            };

            var result = await endpoint.Account.GetAccountsAsync(parameters);

            if (result?.Data is null || result.Data.Count == 0)
                return "No accounts found.";

            var sb = new StringBuilder();
            sb.AppendLine($"## Accounts (Page {page}, {result.ItemCount} total)");

            foreach (var account in result.Data)
            {
                sb.AppendLine($"---");
                sb.AppendLine($"- **Public Key:** {FormattingHelpers.FormatHash(account.PublicKey)}");
                sb.AppendLine($"  Account Hash: {FormattingHelpers.FormatHash(account.AccountHash)}");
                sb.AppendLine($"  Balance: {FormattingHelpers.MotesToCspr(account.Balance)}");
            }

            sb.AppendLine($"---");
            sb.AppendLine($"Page {page} of {result.PageCount}");

            return sb.ToString();
        }
        catch (Exception ex)
        {
            return $"Error retrieving accounts: {ex.Message}";
        }
    }

    [McpServerTool, Description("Get contract packages deployed by a Casper Network account.")]
    public static async Task<string> GetAccountContractPackages(
        CasperCloudRestClient client,
        CasperMcpOptions options,
        [Description("The public key of the account")] string publicKey,
        [Description("Page number (default: 1)")] int page = 1,
        [Description("Number of results per page (default: 10, max: 250)")] int pageSize = 10)
    {
        try
        {
            var endpoint = options.IsTestnet ? (INetworkEndpoint)client.Testnet : client.Mainnet;
            var parameters = new AccountContractPackageRequestParameters
            {
                PageNumber = page,
                PageSize = Math.Min(pageSize, 250)
            };

            var result = await endpoint.Contract.GetAccountContractPackagesAsync(publicKey, parameters);

            if (result?.Data is null || result.Data.Count == 0)
                return $"No contract packages found for account: {publicKey}";

            var sb = new StringBuilder();
            sb.AppendLine($"## Account Contract Packages (Page {page}, {result.ItemCount} total)");

            foreach (var pkg in result.Data)
            {
                sb.AppendLine($"---");
                sb.AppendLine($"- **Package Hash:** {FormattingHelpers.FormatHash(pkg.ContractPackageHash)}");
                sb.AppendLine($"  Name: {pkg.Name ?? "N/A"}");
                sb.AppendLine($"  Description: {pkg.Description ?? "N/A"}");
                sb.AppendLine($"  Owner: {FormattingHelpers.FormatHash(pkg.OwnerPublicKey)}");
                sb.AppendLine($"  Created: {FormattingHelpers.FormatTimestamp(pkg.Timestamp)}");
            }

            sb.AppendLine($"---");
            sb.AppendLine($"Page {page} of {result.PageCount}");

            return sb.ToString();
        }
        catch (Exception ex)
        {
            return $"Error retrieving account contract packages: {ex.Message}";
        }
    }

    [McpServerTool, Description("Get delegation rewards for a Casper Network account.")]
    public static async Task<string> GetAccountDelegationRewards(
        CasperCloudRestClient client,
        CasperMcpOptions options,
        [Description("The public key of the account")] string publicKey,
        [Description("Page number (default: 1)")] int page = 1,
        [Description("Number of results per page (default: 10, max: 250)")] int pageSize = 10)
    {
        try
        {
            var endpoint = options.IsTestnet ? (INetworkEndpoint)client.Testnet : client.Mainnet;
            var parameters = new AccountDelegatorRewardRequestParameters
            {
                PageNumber = page,
                PageSize = Math.Min(pageSize, 250)
            };

            var result = await endpoint.Delegate.GetAccountDelegatorRewardsAsync(publicKey, parameters);

            if (result?.Data is null || result.Data.Count == 0)
                return $"No delegation rewards found for account: {publicKey}";

            var sb = new StringBuilder();
            sb.AppendLine($"## Account Delegation Rewards (Page {page}, {result.ItemCount} total)");

            foreach (var reward in result.Data)
            {
                sb.AppendLine($"---");
                sb.AppendLine($"- **Era:** {reward.EraId?.ToString() ?? "N/A"}");
                sb.AppendLine($"  Validator: {FormattingHelpers.FormatHash(reward.ValidatorPublicKey)}");
                sb.AppendLine($"  Amount: {FormattingHelpers.MotesToCspr(reward.Amount)}");
                sb.AppendLine($"  Timestamp: {FormattingHelpers.FormatTimestamp(reward.Timestamp)}");
            }

            sb.AppendLine($"---");
            sb.AppendLine($"Page {page} of {result.PageCount}");

            return sb.ToString();
        }
        catch (Exception ex)
        {
            return $"Error retrieving account delegation rewards: {ex.Message}";
        }
    }

    [McpServerTool, Description("Get the total delegation rewards for a Casper Network account.")]
    public static async Task<string> GetTotalAccountDelegationRewards(
        CasperCloudRestClient client,
        CasperMcpOptions options,
        [Description("The public key of the account")] string publicKey)
    {
        try
        {
            var endpoint = options.IsTestnet ? (INetworkEndpoint)client.Testnet : client.Mainnet;
            var total = await endpoint.Delegate.GetTotalAccountDelegationRewards(publicKey);

            var sb = new StringBuilder();
            sb.AppendLine($"## Total Account Delegation Rewards");
            sb.AppendLine($"- **Public Key:** {FormattingHelpers.FormatHash(publicKey)}");
            sb.AppendLine($"- **Total Rewards:** {FormattingHelpers.MotesToCspr(total)}");

            return sb.ToString();
        }
        catch (Exception ex)
        {
            return $"Error retrieving total account delegation rewards: {ex.Message}";
        }
    }

    [McpServerTool, Description("Get the total delegation rewards paid out by a validator to its delegators.")]
    public static async Task<string> GetTotalValidatorDelegatorRewards(
        CasperCloudRestClient client,
        CasperMcpOptions options,
        [Description("The public key of the validator")] string publicKey)
    {
        try
        {
            var endpoint = options.IsTestnet ? (INetworkEndpoint)client.Testnet : client.Mainnet;
            var total = await endpoint.Delegate.GetTotalValidatorDelegationRewards(publicKey);

            var sb = new StringBuilder();
            sb.AppendLine($"## Total Validator Delegator Rewards");
            sb.AppendLine($"- **Validator Public Key:** {FormattingHelpers.FormatHash(publicKey)}");
            sb.AppendLine($"- **Total Rewards:** {FormattingHelpers.MotesToCspr(total)}");

            return sb.ToString();
        }
        catch (Exception ex)
        {
            return $"Error retrieving total validator delegator rewards: {ex.Message}";
        }
    }
}
