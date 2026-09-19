using System.ComponentModel;
using System.Text;
using CasperMcp.Configuration;
using CasperMcp.Helpers;
using CSPR.Cloud.Net.Clients;
using CSPR.Cloud.Net.Parameters.OptionalParameters.Account;
using CSPR.Cloud.Net.Parameters.Wrapper.Accounts;
using CSPR.Cloud.Net.Parameters.Wrapper.Contract;
using CSPR.Cloud.Net.Parameters.Wrapper.Delegate;
using CSPR.Cloud.Net.Parameters.Wrapper.Deploy;
using ModelContextProtocol.Server;

namespace CasperMcp.Tools;

[McpServerToolType]
public static class AccountTools
{

    /// <summary>
    /// Everything an account's identity and staking picture needs. Each of these is an OPTIONAL
    /// property: omit the flag and CSPR.cloud omits the field, which the renderer then prints as
    /// "N/A" — indistinguishable from a genuine zero. That understated one mainnet account's
    /// reported total by 904,257,784 CSPR.
    /// </summary>
    private static AccountsOptionalParameters FullAccountDetail() => new()
    {
        StakedBalance = true,
        DelegatedBalance = true,
        UndelegatingBalance = true,
        AuctionStatus = true,
        AccountInfo = true,
        CentralizedAccountInfo = true,
        CsprName = true,
    };

    [McpServerTool, Description("Get detailed information about a Casper Network account by public key or account hash, including balance, staking info, and delegation status.")]
    public static async Task<string> GetAccountInfo(
        CasperCloudRestClient client,
        CasperMcpOptions options,
        [Description("The public key or account hash of the account")] string accountIdentifier)
    {
        var endpoint = options.IsTestnet ? (INetworkEndpoint)client.Testnet : client.Mainnet;
        var account = await endpoint.Account.GetAccountAsync(accountIdentifier, FullAccountDetail());

        if (account is null)
            return $"Account not found: {accountIdentifier}";

        var sb = new StringBuilder();
        var accountName = NameHelpers.DisplayName(account.AccountInfo, account.CentralizedAccountInfo, account.CsprName);
        sb.AppendLine($"## Account Information");
        if (accountName is not null)
            sb.AppendLine($"- **Name:** {accountName}");
        sb.AppendLine($"- **Public Key:** {FormattingHelpers.FormatHash(account.PublicKey)}");
        sb.AppendLine($"- **Account Hash:** {FormattingHelpers.FormatHash(account.AccountHash)}");
        sb.AppendLine($"- **Balance:** {FormattingHelpers.MotesToCspr(account.Balance)}");
        sb.AppendLine($"- **Staked Balance:** {FormattingHelpers.MotesToCspr(account.StakedBalance)}");
        sb.AppendLine($"- **Delegated Balance:** {FormattingHelpers.MotesToCspr(account.DelegatedBalance)}");
        sb.AppendLine($"- **Undelegating Balance:** {FormattingHelpers.MotesToCspr(account.UndelegatingBalance)}");
        sb.AppendLine($"- **Auction Status:** {account.AuctionStatus ?? "N/A"}");
        sb.AppendLine($"- **Main Purse:** {FormattingHelpers.FormatHash(account.MainPurseUref)}");
        if (!string.IsNullOrEmpty(account.CsprName))
            sb.AppendLine($"- **CSPR.name:** {account.CsprName}");

        return sb.ToString();
    }

    [McpServerTool, Description("Get the CSPR balance of a Casper Network account.")]
    public static async Task<string> GetAccountBalance(
        CasperCloudRestClient client,
        CasperMcpOptions options,
        [Description("The public key or account hash of the account")] string accountIdentifier)
    {
        var endpoint = options.IsTestnet ? (INetworkEndpoint)client.Testnet : client.Mainnet;
        var account = await endpoint.Account.GetAccountAsync(accountIdentifier, FullAccountDetail());

        if (account is null)
            return $"Account not found: {accountIdentifier}";

        var sb = new StringBuilder();
        sb.AppendLine($"## Account Balance");
        sb.AppendLine($"- **Public Key:** {FormattingHelpers.FormatHash(account.PublicKey)}");
        sb.AppendLine($"- **Liquid Balance:** {FormattingHelpers.MotesToCspr(account.Balance)}");
        sb.AppendLine($"- **Staked Balance:** {FormattingHelpers.MotesToCspr(account.StakedBalance)}");
        sb.AppendLine($"- **Delegated Balance:** {FormattingHelpers.MotesToCspr(account.DelegatedBalance)}");

        var totalBalance = FormattingHelpers.SumMotes(account.Balance, account.StakedBalance, account.DelegatedBalance);
        sb.AppendLine($"- **Total (liquid + staked + delegated):** {FormattingHelpers.MotesToCspr(totalBalance)}");

        return sb.ToString();
    }

    [McpServerTool, Description("Get recent deploys (transactions) for a Casper Network account.")]
    public static async Task<string> GetAccountDeploys(
        CasperCloudRestClient client,
        CasperMcpOptions options,
        [Description("The public key of the account")] string publicKey,
        [Description("Page number (default: 1)")] int page = 1,
        [Description("Number of results per page (default: 10, max: 250)")] int pageSize = 10)
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

    [McpServerTool, Description("Get delegation information for a Casper Network account, showing which validators the account has delegated to.")]
    public static async Task<string> GetAccountDelegations(
        CasperCloudRestClient client,
        CasperMcpOptions options,
        [Description("The public key of the account")] string publicKey,
        [Description("Page number (default: 1)")] int page = 1,
        [Description("Number of results per page (default: 10, max: 250)")] int pageSize = 10)
    {
        var endpoint = options.IsTestnet ? (INetworkEndpoint)client.Testnet : client.Mainnet;
        var parameters = new DelegationRequestParameters
        {
            PageNumber = page,
            PageSize = Math.Min(pageSize, 250)
        };
        // The counterparty here is the validator: a delegator asking where their stake sits wants
        // "Era Guardian", not a public key they have to look up somewhere else. Optional properties,
        // so without these flags the response carries the key alone.
        parameters.OptionalParameters.ValidatorAccountInfo = true;
        parameters.OptionalParameters.ValidatorCsprName = true;

        var result = await endpoint.Delegate.GetAccountDelegationsAsync(publicKey, parameters);

        if (result?.Data is null || result.Data.Count == 0)
            return $"No delegations found for account: {publicKey}";

        var sb = new StringBuilder();
        sb.AppendLine($"## Account Delegations (Page {page}, {result.ItemCount} total)");

        foreach (var delegation in result.Data)
        {
            sb.AppendLine($"---");
            sb.AppendLine($"- **Validator:** {NameHelpers.Labeled(NameHelpers.DisplayName(delegation.ValidatorAccountInfo, csprName: delegation.ValidatorCsprName), delegation.ValidatorPublicKey)}");
            sb.AppendLine($"- **Staked Amount:** {FormattingHelpers.MotesToCspr(delegation.Stake)}");
        }

        sb.AppendLine($"---");
        sb.AppendLine($"Page {page} of {result.PageCount}");

        return sb.ToString();
    }

    [McpServerTool, Description("Get a paginated list of all accounts on the Casper Network.")]
    public static async Task<string> GetAccounts(
        CasperCloudRestClient client,
        CasperMcpOptions options,
        [Description("Page number (default: 1)")] int page = 1,
        [Description("Number of results per page (default: 10, max: 250)")] int pageSize = 10)
    {
        var endpoint = options.IsTestnet ? (INetworkEndpoint)client.Testnet : client.Mainnet;
        var parameters = new AccountsRequestParameters
        {
            PageNumber = page,
            PageSize = Math.Min(pageSize, 250)
        };

        // Same optional-property set as the single-account views — the names are what turn a page of
        // hashes into something a caller can read, and one flag set keeps the two from drifting.
        parameters.OptionalParameters = FullAccountDetail();

        var result = await endpoint.Account.GetAccountsAsync(parameters);

        if (result?.Data is null || result.Data.Count == 0)
            return "No accounts found.";

        var sb = new StringBuilder();
        sb.AppendLine($"## Accounts (Page {page}, {result.ItemCount} total)");

        foreach (var account in result.Data)
        {
            sb.AppendLine($"---");
            sb.AppendLine($"- **Public Key:** {NameHelpers.Labeled(NameHelpers.DisplayName(account.AccountInfo, account.CentralizedAccountInfo, account.CsprName), account.PublicKey)}");
            sb.AppendLine($"  Account Hash: {FormattingHelpers.FormatHash(account.AccountHash)}");
            sb.AppendLine($"  Balance: {FormattingHelpers.MotesToCspr(account.Balance)}");
        }

        sb.AppendLine($"---");
        sb.AppendLine($"Page {page} of {result.PageCount}");

        return sb.ToString();
    }

    [McpServerTool, Description("Get contract packages deployed by a Casper Network account.")]
    public static async Task<string> GetAccountContractPackages(
        CasperCloudRestClient client,
        CasperMcpOptions options,
        [Description("The public key of the account")] string publicKey,
        [Description("Page number (default: 1)")] int page = 1,
        [Description("Number of results per page (default: 10, max: 250)")] int pageSize = 10)
    {
        var endpoint = options.IsTestnet ? (INetworkEndpoint)client.Testnet : client.Mainnet;
        var parameters = new AccountContractPackageRequestParameters
        {
            PageNumber = page,
            PageSize = Math.Min(pageSize, 250)
        };

        // The owner's CSPR.name is an optional property, and it is the only identity this endpoint
        // exposes (there is no AccountInfo flag on contract packages).
        parameters.OptionalParameters.OwnerCsprName = true;

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
            sb.AppendLine($"  Owner: {NameHelpers.Labeled(pkg.OwnerCsprName, pkg.OwnerPublicKey)}");
            sb.AppendLine($"  Created: {FormattingHelpers.FormatTimestamp(pkg.Timestamp)}");
        }

        sb.AppendLine($"---");
        sb.AppendLine($"Page {page} of {result.PageCount}");

        return sb.ToString();
    }

    [McpServerTool, Description("Get delegation rewards for a Casper Network account.")]
    public static async Task<string> GetAccountDelegationRewards(
        CasperCloudRestClient client,
        CasperMcpOptions options,
        [Description("The public key of the account")] string publicKey,
        [Description("Page number (default: 1)")] int page = 1,
        [Description("Number of results per page (default: 10, max: 250)")] int pageSize = 10)
    {
        var endpoint = options.IsTestnet ? (INetworkEndpoint)client.Testnet : client.Mainnet;
        var parameters = new AccountDelegatorRewardRequestParameters
        {
            PageNumber = page,
            PageSize = Math.Min(pageSize, 250)
        };

        // The reward is attributed to a validator, so name the validator: optional properties, and
        // without them every row reads as a key the caller has to resolve by hand.
        parameters.OptionalParameters.ValidatorAccountInfo = true;
        parameters.OptionalParameters.ValidatorCsprName = true;

        var result = await endpoint.Delegate.GetAccountDelegatorRewardsAsync(publicKey, parameters);

        if (result?.Data is null || result.Data.Count == 0)
            return $"No delegation rewards found for account: {publicKey}";

        var sb = new StringBuilder();
        sb.AppendLine($"## Account Delegation Rewards (Page {page}, {result.ItemCount} total)");

        foreach (var reward in result.Data)
        {
            sb.AppendLine($"---");
            sb.AppendLine($"- **Era:** {reward.EraId?.ToString() ?? "N/A"}");
            sb.AppendLine($"  Validator: {NameHelpers.Labeled(NameHelpers.DisplayName(reward.ValidatorAccountInfo, csprName: reward.ValidatorCsprName), reward.ValidatorPublicKey)}");
            sb.AppendLine($"  Amount: {FormattingHelpers.MotesToCspr(reward.Amount)}");
            sb.AppendLine($"  Timestamp: {FormattingHelpers.FormatTimestamp(reward.Timestamp)}");
        }

        sb.AppendLine($"---");
        sb.AppendLine($"Page {page} of {result.PageCount}");

        return sb.ToString();
    }

    [McpServerTool, Description("Get the total delegation rewards for a Casper Network account.")]
    public static async Task<string> GetTotalAccountDelegationRewards(
        CasperCloudRestClient client,
        CasperMcpOptions options,
        [Description("The public key of the account")] string publicKey)
    {
        var endpoint = options.IsTestnet ? (INetworkEndpoint)client.Testnet : client.Mainnet;
        var total = await endpoint.Delegate.GetTotalAccountDelegationRewards(publicKey);

        var sb = new StringBuilder();
        sb.AppendLine($"## Total Account Delegation Rewards");
        sb.AppendLine($"- **Public Key:** {FormattingHelpers.FormatHash(publicKey)}");
        sb.AppendLine($"- **Total Rewards:** {FormattingHelpers.MotesToCspr(total)}");

        return sb.ToString();
    }

    [McpServerTool, Description("Get the total delegation rewards paid out by a validator to its delegators.")]
    public static async Task<string> GetTotalValidatorDelegatorRewards(
        CasperCloudRestClient client,
        CasperMcpOptions options,
        [Description("The public key of the validator")] string publicKey)
    {
        var endpoint = options.IsTestnet ? (INetworkEndpoint)client.Testnet : client.Mainnet;
        var total = await endpoint.Delegate.GetTotalValidatorDelegationRewards(publicKey);

        var sb = new StringBuilder();
        sb.AppendLine($"## Total Validator Delegator Rewards");
        sb.AppendLine($"- **Validator Public Key:** {FormattingHelpers.FormatHash(publicKey)}");
        sb.AppendLine($"- **Total Rewards:** {FormattingHelpers.MotesToCspr(total)}");

        return sb.ToString();
    }

    [McpServerTool, Description("Get pending undelegations for a Casper Network account. Funds are released 7 eras after the era of creation.")]
    public static async Task<string> GetAccountUndelegations(
        CasperCloudRestClient client,
        CasperMcpOptions options,
        [Description("The public key of the account")] string publicKey,
        [Description("Page number (default: 1)")] int page = 1,
        [Description("Number of results per page (default: 10, max: 250)")] int pageSize = 10)
    {
        var endpoint = options.IsTestnet ? (INetworkEndpoint)client.Testnet : client.Mainnet;
        var parameters = new DelegationRequestParameters
        {
            PageNumber = page,
            PageSize = Math.Min(pageSize, 250)
        };

        // ValidatorAccountInfo is an optional property, and it is the only identity an undelegation
        // row can carry for the validator — UndelegationData has no ValidatorCsprName field, so
        // asking for CSPR.names here would buy nothing.
        parameters.OptionalParameters.ValidatorAccountInfo = true;

        var result = await endpoint.Delegate.GetAccountUndelegationsAsync(publicKey, parameters);

        if (result?.Data is null || result.Data.Count == 0)
            return $"No pending undelegations found for account: {publicKey}";

        var sb = new StringBuilder();
        sb.AppendLine($"## Account Undelegations (Page {page}, {result.ItemCount} total)");

        foreach (var u in result.Data)
        {
            sb.AppendLine($"---");
            sb.AppendLine($"- **Validator:** {NameHelpers.Labeled(u.ValidatorAccountInfo, u.ValidatorPublicKey)}");
            sb.AppendLine($"  Amount: {FormattingHelpers.MotesToCspr(u.Amount)}");
            sb.AppendLine($"  Era of Creation: {u.EraOfCreation?.ToString() ?? "N/A"} (released 7 eras later)");
            sb.AppendLine($"  Delegator Type: {(u.DelegatorIdentifierTypeId == 1 ? "Purse" : "Account")}");
            sb.AppendLine($"  Bonding Purse: {FormattingHelpers.FormatHash(u.BondingPurse)}");
            sb.AppendLine($"  Initiated: {FormattingHelpers.FormatTimestamp(u.Timestamp)}");
        }

        sb.AppendLine($"---");
        sb.AppendLine($"Page {page} of {result.PageCount}");

        return sb.ToString();
    }

    [McpServerTool, Description("Get delegations for a specific purse on the Casper Network.")]
    public static async Task<string> GetPurseDelegations(
        CasperCloudRestClient client,
        CasperMcpOptions options,
        [Description("The purse URef")] string purseUref,
        [Description("Page number (default: 1)")] int page = 1,
        [Description("Number of results per page (default: 10, max: 250)")] int pageSize = 10)
    {
        var endpoint = options.IsTestnet ? (INetworkEndpoint)client.Testnet : client.Mainnet;
        var parameters = new DelegationRequestParameters
        {
            PageNumber = page,
            PageSize = Math.Min(pageSize, 250)
        };

        // Same as the account view: the purse's counterparty is the validator, and its name is an
        // optional property the response omits unless asked.
        parameters.OptionalParameters.ValidatorAccountInfo = true;
        parameters.OptionalParameters.ValidatorCsprName = true;

        var result = await endpoint.Delegate.GetPurseDelegationsAsync(purseUref, parameters);

        if (result?.Data is null || result.Data.Count == 0)
            return $"No delegations found for purse: {purseUref}";

        var sb = new StringBuilder();
        sb.AppendLine($"## Purse Delegations (Page {page}, {result.ItemCount} total)");

        foreach (var delegation in result.Data)
        {
            sb.AppendLine($"---");
            sb.AppendLine($"- **Validator:** {NameHelpers.Labeled(NameHelpers.DisplayName(delegation.ValidatorAccountInfo, csprName: delegation.ValidatorCsprName), delegation.ValidatorPublicKey)}");
            sb.AppendLine($"- **Staked Amount:** {FormattingHelpers.MotesToCspr(delegation.Stake)}");
        }

        sb.AppendLine($"---");
        sb.AppendLine($"Page {page} of {result.PageCount}");

        return sb.ToString();
    }

    [McpServerTool, Description("Get delegation rewards for a specific purse on the Casper Network.")]
    public static async Task<string> GetPurseDelegationRewards(
        CasperCloudRestClient client,
        CasperMcpOptions options,
        [Description("The purse URef")] string purseUref,
        [Description("Page number (default: 1)")] int page = 1,
        [Description("Number of results per page (default: 10, max: 250)")] int pageSize = 10)
    {
        var endpoint = options.IsTestnet ? (INetworkEndpoint)client.Testnet : client.Mainnet;
        var parameters = new AccountDelegatorRewardRequestParameters
        {
            PageNumber = page,
            PageSize = Math.Min(pageSize, 250)
        };

        // As above: the validator's name is optional, so ask for it or every row is a bare key.
        parameters.OptionalParameters.ValidatorAccountInfo = true;
        parameters.OptionalParameters.ValidatorCsprName = true;

        var result = await endpoint.Delegate.GetPurseDelegationRewardsAsync(purseUref, parameters);

        if (result?.Data is null || result.Data.Count == 0)
            return $"No delegation rewards found for purse: {purseUref}";

        var sb = new StringBuilder();
        sb.AppendLine($"## Purse Delegation Rewards (Page {page}, {result.ItemCount} total)");

        foreach (var reward in result.Data)
        {
            sb.AppendLine($"---");
            sb.AppendLine($"- **Era:** {reward.EraId?.ToString() ?? "N/A"}");
            sb.AppendLine($"  Validator: {NameHelpers.Labeled(NameHelpers.DisplayName(reward.ValidatorAccountInfo, csprName: reward.ValidatorCsprName), reward.ValidatorPublicKey)}");
            sb.AppendLine($"  Amount: {FormattingHelpers.MotesToCspr(reward.Amount)}");
            sb.AppendLine($"  Timestamp: {FormattingHelpers.FormatTimestamp(reward.Timestamp)}");
        }

        sb.AppendLine($"---");
        sb.AppendLine($"Page {page} of {result.PageCount}");

        return sb.ToString();
    }

    [McpServerTool, Description("Get the total delegation rewards for a specific purse on the Casper Network.")]
    public static async Task<string> GetTotalPurseDelegationRewards(
        CasperCloudRestClient client,
        CasperMcpOptions options,
        [Description("The purse URef")] string purseUref)
    {
        var endpoint = options.IsTestnet ? (INetworkEndpoint)client.Testnet : client.Mainnet;
        var total = await endpoint.Delegate.GetTotalPurseDelegationRewardsAsync(purseUref);

        var sb = new StringBuilder();
        sb.AppendLine($"## Total Purse Delegation Rewards");
        sb.AppendLine($"- **Purse:** {FormattingHelpers.FormatHash(purseUref)}");
        sb.AppendLine($"- **Total Rewards:** {FormattingHelpers.MotesToCspr(total)}");

        return sb.ToString();
    }
}
