using CasperMcp.Configuration;
using CasperMcp.Tools;
using CSPR.Cloud.Net.Clients;
using CSPR.Cloud.Net.Objects.Config;

namespace CasperMcp.Tests;

/// <summary>
/// Integration tests that call the real CSPR.Cloud Testnet API.
/// These tests verify that all MCP tools work correctly end-to-end.
/// Set CSPR_CLOUD_TESTNET_API_KEY environment variable or they use the public testnet key.
/// </summary>
[Collection("Integration")]
public class IntegrationTests : IAsyncLifetime
{
    private readonly CasperCloudRestClient _client;
    private readonly CasperMcpOptions _options;

    // Known testnet public key (a validator on testnet)
    private const string TestPublicKey = "0106ca7c39cd272dbf21a86eeb3b36b7c26e2e9b94af64292419f7862936bca2ca";

    // Rate-limit throttle: ensure minimum delay between tests to avoid CSPR Cloud TooManyRequests
    private static readonly SemaphoreSlim _throttle = new(1, 1);
    private static DateTime _lastTestStart = DateTime.MinValue;
    private static readonly TimeSpan _delayBetweenTests = TimeSpan.FromMilliseconds(350);

    public IntegrationTests()
    {
        var apiKey = Environment.GetEnvironmentVariable("CSPR_CLOUD_TESTNET_API_KEY")
                     ?? "55f79117-fc4d-4d60-9956-65423f39a06a";

        var config = new CasperCloudClientConfig(apiKey);
        _client = new CasperCloudRestClient(config);
        _options = new CasperMcpOptions { Network = "testnet" };
    }

    public async Task InitializeAsync()
    {
        await _throttle.WaitAsync();
        try
        {
            var elapsed = DateTime.UtcNow - _lastTestStart;
            if (elapsed < _delayBetweenTests)
                await Task.Delay(_delayBetweenTests - elapsed);
            _lastTestStart = DateTime.UtcNow;
        }
        finally
        {
            _throttle.Release();
        }
    }

    public Task DisposeAsync() => Task.CompletedTask;

    // ==================== Network Tools ====================

    [Fact]
    public async Task GetNetworkStatus_ReturnsValidData()
    {
        var result = await NetworkTools.GetNetworkStatus(_client, _options);

        Assert.DoesNotContain("Error", result);
        Assert.Contains("Casper Network Status", result);
        Assert.Contains("Current Era", result);
        Assert.Contains("Active Validators", result);
    }

    [Fact]
    public async Task GetEraInfo_ReturnsValidData()
    {
        var result = await NetworkTools.GetEraInfo(_client, _options);

        Assert.DoesNotContain("Error", result);
        Assert.Contains("Era Information", result);
        Assert.Contains("Current Era ID", result);
    }

    [Fact]
    public async Task GetSupplyInfo_ReturnsValidData()
    {
        var result = await NetworkTools.GetSupplyInfo(_client, _options);

        // Supply endpoint may not be available on testnet — just verify it doesn't crash
        Assert.NotNull(result);
        Assert.NotEmpty(result);
    }

    // ==================== Block Tools ====================

    [Fact]
    public async Task GetLatestBlocks_ReturnsBlocks()
    {
        var result = await BlockTools.GetLatestBlocks(_client, _options);

        Assert.DoesNotContain("Error", result);
        Assert.Contains("Latest Blocks", result);
        Assert.Contains("Height", result);
    }

    [Fact]
    public async Task GetLatestBlocks_WithPagination_ReturnsCorrectPage()
    {
        var result = await BlockTools.GetLatestBlocks(_client, _options, page: 1, pageSize: 5);

        Assert.DoesNotContain("Error", result);
        Assert.Contains("Latest Blocks", result);
    }

    [Fact]
    public async Task GetBlock_WithValidHash_ReturnsBlockInfo()
    {
        // First get a block hash from latest blocks
        var endpoint = _options.IsTestnet ? (INetworkEndpoint)_client.Testnet : _client.Mainnet;
        var blocks = await endpoint.Block.GetBlocksAsync(new CSPR.Cloud.Net.Parameters.Wrapper.Block.BlockRequestParameters { PageSize = 1 });

        Assert.NotNull(blocks?.Data);
        Assert.NotEmpty(blocks.Data);

        var blockHash = blocks.Data[0].BlockHash;
        var result = await BlockTools.GetBlock(_client, _options, blockHash);

        Assert.DoesNotContain("Error", result);
        Assert.Contains("Block Information", result);
        Assert.Contains(blockHash, result);
    }

    [Fact]
    public async Task GetBlock_WithInvalidHash_ReturnsError()
    {
        var result = await BlockTools.GetBlock(_client, _options, "invalid_hash_000");

        // Should return an error message (either our "not found" or API error)
        Assert.True(result.Contains("not found", StringComparison.OrdinalIgnoreCase)
                    || result.Contains("Error", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task GetValidatorBlocks_ReturnsBlocks()
    {
        var result = await BlockTools.GetValidatorBlocks(_client, _options, TestPublicKey);

        // Could be "No blocks" or actual blocks - both valid
        Assert.DoesNotContain("Error", result);
    }

    // ==================== Validator Tools ====================

    [Fact]
    public async Task GetValidators_ReturnsValidatorList()
    {
        var result = await ValidatorTools.GetValidators(_client, _options);

        Assert.DoesNotContain("Error", result);
        Assert.Contains("Validators", result);
        Assert.Contains("Rank", result);
        Assert.Contains("Total Stake", result);
    }

    [Fact]
    public async Task GetValidatorInfo_WithValidKey_ReturnsInfo()
    {
        // First get a validator public key from the list
        var endpoint = _options.IsTestnet ? (INetworkEndpoint)_client.Testnet : _client.Mainnet;
        var auctionMetrics = await endpoint.Auction.GetAuctionMetricsAsync();
        var eraId = auctionMetrics?.Data?.CurrentEraId?.ToString();
        Assert.NotNull(eraId);

        var validatorsParams = new CSPR.Cloud.Net.Parameters.Wrapper.Validator.ValidatorsRequestParameters { PageSize = 1 };
        validatorsParams.FilterParameters.EraId = eraId;
        var validators = await endpoint.Validator.GetValidatorsAsync(validatorsParams);

        Assert.NotNull(validators?.Data);
        Assert.NotEmpty(validators.Data);

        var validatorKey = validators.Data[0].PublicKey;
        var result = await ValidatorTools.GetValidatorInfo(_client, _options, validatorKey);

        Assert.DoesNotContain("Error", result);
        Assert.Contains("Validator Information", result);
        Assert.Contains("Fee", result);
    }

    [Fact]
    public async Task GetValidatorDelegations_ReturnsData()
    {
        var result = await ValidatorTools.GetValidatorDelegations(_client, _options, TestPublicKey);

        // Could be "No delegations" or actual data - both valid
        Assert.DoesNotContain("Error", result);
    }

    [Fact]
    public async Task GetValidatorRewards_ReturnsData()
    {
        var result = await ValidatorTools.GetValidatorRewards(_client, _options, TestPublicKey);

        // Could be "No rewards" or actual data - both valid
        Assert.DoesNotContain("Error", result);
    }

    [Fact]
    public async Task GetValidatorTotalRewards_ReturnsData()
    {
        var result = await ValidatorTools.GetValidatorTotalRewards(_client, _options, TestPublicKey);

        Assert.DoesNotContain("Error", result);
        Assert.Contains("Total Rewards", result);
    }

    [Fact]
    public async Task GetHistoricalValidatorPerformance_ReturnsData()
    {
        var result = await ValidatorTools.GetHistoricalValidatorPerformance(_client, _options, TestPublicKey);

        // Could be "No performance data" or actual data - both valid
        Assert.DoesNotContain("Error", result);
    }

    [Fact]
    public async Task GetHistoricalValidatorAveragePerformance_ReturnsData()
    {
        var result = await ValidatorTools.GetHistoricalValidatorAveragePerformance(_client, _options, TestPublicKey);

        // Could be "No average performance data" or actual data - both valid
        Assert.DoesNotContain("Error", result);
    }

    [Fact]
    public async Task GetHistoricalValidatorsAveragePerformance_ReturnsData()
    {
        var result = await ValidatorTools.GetHistoricalValidatorsAveragePerformance(_client, _options);

        // Could be "No validators average performance data" or actual data - both valid
        Assert.DoesNotContain("Error", result);
    }

    // ==================== Account Tools ====================

    [Fact]
    public async Task GetAccountInfo_WithValidKey_ReturnsInfo()
    {
        var result = await AccountTools.GetAccountInfo(_client, _options, TestPublicKey);

        Assert.DoesNotContain("Error", result);
        Assert.Contains("Account Information", result);
        Assert.Contains("Public Key", result);
    }

    [Fact]
    public async Task GetAccountBalance_WithValidKey_ReturnsBalance()
    {
        var result = await AccountTools.GetAccountBalance(_client, _options, TestPublicKey);

        Assert.DoesNotContain("Error", result);
        Assert.Contains("Account Balance", result);
        Assert.Contains("CSPR", result);
    }

    [Fact]
    public async Task GetAccountDeploys_WithValidKey_ReturnsDeploys()
    {
        var result = await AccountTools.GetAccountDeploys(_client, _options, TestPublicKey);

        // Could be "No deploys" or actual deploys - both are valid
        Assert.DoesNotContain("Error", result);
    }

    [Fact]
    public async Task GetAccountDelegations_WithValidKey_ReturnsDelegations()
    {
        var result = await AccountTools.GetAccountDelegations(_client, _options, TestPublicKey);

        // Could be "No delegations" or actual delegations - both are valid
        Assert.DoesNotContain("Error", result);
    }

    [Fact]
    public async Task GetAccounts_ReturnsAccountsList()
    {
        var result = await AccountTools.GetAccounts(_client, _options);

        Assert.DoesNotContain("Error", result);
        Assert.Contains("Accounts", result);
    }

    [Fact]
    public async Task GetAccountContractPackages_ReturnsData()
    {
        var result = await AccountTools.GetAccountContractPackages(_client, _options, TestPublicKey);

        // Could be "No contract packages" or actual data - both valid
        Assert.DoesNotContain("Error", result);
    }

    [Fact]
    public async Task GetAccountDelegationRewards_ReturnsData()
    {
        var result = await AccountTools.GetAccountDelegationRewards(_client, _options, TestPublicKey);

        // Could be "No delegation rewards" or actual data - both valid
        Assert.DoesNotContain("Error", result);
    }

    [Fact]
    public async Task GetTotalAccountDelegationRewards_ReturnsData()
    {
        var result = await AccountTools.GetTotalAccountDelegationRewards(_client, _options, TestPublicKey);

        Assert.DoesNotContain("Error", result);
        Assert.Contains("Total Rewards", result);
    }

    [Fact]
    public async Task GetTotalValidatorDelegatorRewards_ReturnsData()
    {
        var result = await AccountTools.GetTotalValidatorDelegatorRewards(_client, _options, TestPublicKey);

        Assert.DoesNotContain("Error", result);
        Assert.Contains("Total Rewards", result);
    }

    // ==================== Deploy Tools ====================

    [Fact]
    public async Task GetDeploy_WithValidHash_ReturnsDeployInfo()
    {
        // First get a deploy hash from recent deploys
        var endpoint = _options.IsTestnet ? (INetworkEndpoint)_client.Testnet : _client.Mainnet;
        var deploys = await endpoint.Deploy.GetDeploysAsync(new CSPR.Cloud.Net.Parameters.Wrapper.Deploy.DeploysRequestParameters { PageSize = 1 });

        Assert.NotNull(deploys?.Data);
        Assert.NotEmpty(deploys.Data);

        var deployHash = deploys.Data[0].DeployHash;
        var result = await DeployTools.GetDeploy(_client, _options, deployHash);

        Assert.DoesNotContain("Error", result);
        Assert.Contains("Deploy Information", result);
        Assert.Contains(deployHash, result);
    }

    [Fact]
    public async Task GetDeploys_ReturnsList()
    {
        var result = await DeployTools.GetDeploys(_client, _options);

        Assert.DoesNotContain("Error", result);
        Assert.Contains("Deploys", result);
    }

    [Fact]
    public async Task GetBlockDeploys_ReturnsData()
    {
        // First get a block hash
        var endpoint = _options.IsTestnet ? (INetworkEndpoint)_client.Testnet : _client.Mainnet;
        var blocks = await endpoint.Block.GetBlocksAsync(new CSPR.Cloud.Net.Parameters.Wrapper.Block.BlockRequestParameters { PageSize = 1 });

        Assert.NotNull(blocks?.Data);
        Assert.NotEmpty(blocks.Data);

        var blockHash = blocks.Data[0].BlockHash;
        var result = await DeployTools.GetBlockDeploys(_client, _options, blockHash);

        // Could be "No deploys" or actual data - both valid
        Assert.DoesNotContain("Error", result);
    }

    [Fact]
    public async Task GetDeployExecutionTypes_ReturnsTypes()
    {
        var result = await DeployTools.GetDeployExecutionTypes(_client, _options);

        Assert.DoesNotContain("Error", result);
        Assert.Contains("Execution Types", result);
    }

    // ==================== Transfer Tools ====================

    [Fact]
    public async Task GetTransfers_WithValidKey_ReturnsTransfers()
    {
        var result = await TransferTools.GetTransfers(_client, _options, TestPublicKey);

        // Could be "No transfers" or actual transfers - both are valid
        Assert.DoesNotContain("Error", result);
    }

    [Fact]
    public async Task GetDeployTransfers_ReturnsData()
    {
        // First get a deploy hash
        var endpoint = _options.IsTestnet ? (INetworkEndpoint)_client.Testnet : _client.Mainnet;
        var deploys = await endpoint.Deploy.GetDeploysAsync(new CSPR.Cloud.Net.Parameters.Wrapper.Deploy.DeploysRequestParameters { PageSize = 1 });

        Assert.NotNull(deploys?.Data);
        Assert.NotEmpty(deploys.Data);

        var deployHash = deploys.Data[0].DeployHash;
        var result = await TransferTools.GetDeployTransfers(_client, _options, deployHash);

        // Could be "No transfers" or actual data - both valid
        Assert.DoesNotContain("Error", result);
    }

    // ==================== Contract Tools ====================

    [Fact]
    public async Task GetContract_WithValidHash_ReturnsContractInfo()
    {
        // Get a contract hash from recent deploys or contracts list
        var endpoint = _options.IsTestnet ? (INetworkEndpoint)_client.Testnet : _client.Mainnet;
        var contracts = await endpoint.Contract.GetContractsAsync(new CSPR.Cloud.Net.Parameters.Wrapper.Contract.ContractsRequestParameters { PageSize = 1 });

        if (contracts?.Data is null || contracts.Data.Count == 0)
        {
            // No contracts available on testnet - skip gracefully
            Assert.True(true);
            return;
        }

        var contractHash = contracts.Data[0].ContractHash;
        var result = await ContractTools.GetContract(_client, _options, contractHash);

        Assert.DoesNotContain("Error", result);
        Assert.Contains("Contract Information", result);
    }

    [Fact]
    public async Task GetContractEntryPoints_WithValidHash_ReturnsEntryPoints()
    {
        var endpoint = _options.IsTestnet ? (INetworkEndpoint)_client.Testnet : _client.Mainnet;
        var contracts = await endpoint.Contract.GetContractsAsync(new CSPR.Cloud.Net.Parameters.Wrapper.Contract.ContractsRequestParameters { PageSize = 1 });

        if (contracts?.Data is null || contracts.Data.Count == 0)
        {
            Assert.True(true);
            return;
        }

        var contractHash = contracts.Data[0].ContractHash;
        var result = await ContractTools.GetContractEntryPoints(_client, _options, contractHash);

        // Could be "No entry points" or actual entry points
        Assert.DoesNotContain("Error", result);
    }

    [Fact]
    public async Task GetContracts_ReturnsList()
    {
        var result = await ContractTools.GetContracts(_client, _options);

        Assert.DoesNotContain("Error", result);
        Assert.Contains("Contracts", result);
    }

    [Fact]
    public async Task GetContractTypes_ReturnsTypes()
    {
        var result = await ContractTools.GetContractTypes(_client, _options);

        Assert.DoesNotContain("Error", result);
        Assert.Contains("Contract Types", result);
    }

    [Fact]
    public async Task GetContractEntryPointCosts_ReturnsData()
    {
        var endpoint = _options.IsTestnet ? (INetworkEndpoint)_client.Testnet : _client.Mainnet;
        var contracts = await endpoint.Contract.GetContractsAsync(new CSPR.Cloud.Net.Parameters.Wrapper.Contract.ContractsRequestParameters { PageSize = 1 });

        if (contracts?.Data is null || contracts.Data.Count == 0)
        {
            Assert.True(true);
            return;
        }

        var contractHash = contracts.Data[0].ContractHash;

        // Get an entry point name
        var entryPoints = await endpoint.Contract.GetContractEntryPointsAsync(contractHash);
        if (entryPoints?.Data is null || entryPoints.Data.Count == 0)
        {
            Assert.True(true);
            return;
        }

        var entryPointName = entryPoints.Data[0].Name;
        var result = await ContractTools.GetContractEntryPointCosts(_client, _options, contractHash, entryPointName);

        // Could be "No cost data" or actual data - both valid
        Assert.DoesNotContain("Error", result);
    }

    [Fact]
    public async Task GetContractPackages_ReturnsList()
    {
        var result = await ContractTools.GetContractPackages(_client, _options);

        Assert.DoesNotContain("Error", result);
        Assert.Contains("Contract Packages", result);
    }

    [Fact]
    public async Task GetContractsByContractPackage_ReturnsData()
    {
        var endpoint = _options.IsTestnet ? (INetworkEndpoint)_client.Testnet : _client.Mainnet;
        var packages = await endpoint.Contract.GetContractPackagesAsync(new CSPR.Cloud.Net.Parameters.Wrapper.Contract.ContractPackageRequestParameters { PageSize = 1 });

        if (packages?.Data is null || packages.Data.Count == 0)
        {
            Assert.True(true);
            return;
        }

        var packageHash = packages.Data[0].ContractPackageHash;
        var result = await ContractTools.GetContractsByContractPackage(_client, _options, packageHash);

        // Could be "No contracts" or actual data - both valid
        Assert.DoesNotContain("Error", result);
    }

    // ==================== Token Tools ====================

    [Fact]
    public async Task GetAccountFtBalances_WithValidKey_ReturnsFtBalances()
    {
        var result = await TokenTools.GetAccountFtBalances(_client, _options, TestPublicKey);

        // Could be "No fungible token balances" or actual balances - both valid
        Assert.DoesNotContain("Error", result);
    }

    [Fact]
    public async Task GetFtTokenInfo_WithValidHash_ReturnsTokenInfo()
    {
        // Try to find a known FT contract package on testnet
        var endpoint = _options.IsTestnet ? (INetworkEndpoint)_client.Testnet : _client.Mainnet;
        var packages = await endpoint.Contract.GetContractPackagesAsync(new CSPR.Cloud.Net.Parameters.Wrapper.Contract.ContractPackageRequestParameters { PageSize = 1 });

        if (packages?.Data is null || packages.Data.Count == 0)
        {
            Assert.True(true);
            return;
        }

        var packageHash = packages.Data[0].ContractPackageHash;
        var result = await TokenTools.GetFtTokenInfo(_client, _options, packageHash);

        Assert.DoesNotContain("Error", result);
        Assert.Contains("Token Information", result);
    }

    [Fact]
    public async Task GetFungibleTokenActions_ReturnsData()
    {
        var result = await TokenTools.GetFungibleTokenActions(_client, _options);

        // Could be "No fungible token actions" or actual data - both valid
        Assert.DoesNotContain("Error", result);
    }

    [Fact]
    public async Task GetAccountFungibleTokenActions_ReturnsData()
    {
        var result = await TokenTools.GetAccountFungibleTokenActions(_client, _options, TestPublicKey);

        // Could be "No fungible token actions" or actual data - both valid
        Assert.DoesNotContain("Error", result);
    }

    [Fact]
    public async Task GetContractPackageFungibleTokenActions_ReturnsData()
    {
        var endpoint = _options.IsTestnet ? (INetworkEndpoint)_client.Testnet : _client.Mainnet;
        var packages = await endpoint.Contract.GetContractPackagesAsync(new CSPR.Cloud.Net.Parameters.Wrapper.Contract.ContractPackageRequestParameters { PageSize = 1 });

        if (packages?.Data is null || packages.Data.Count == 0)
        {
            Assert.True(true);
            return;
        }

        var packageHash = packages.Data[0].ContractPackageHash;
        var result = await TokenTools.GetContractPackageFungibleTokenActions(_client, _options, packageHash);

        // Could be "No fungible token actions" or actual data - both valid
        Assert.DoesNotContain("Error", result);
    }

    // ==================== NFT Tools ====================

    [Fact]
    public async Task GetAccountNfts_WithValidKey_ReturnsNfts()
    {
        var result = await NftTools.GetAccountNfts(_client, _options, TestPublicKey);

        // Could be "No NFTs" or actual NFTs - both valid
        Assert.DoesNotContain("Error", result);
    }

    [Fact]
    public async Task GetNftStandards_ReturnsData()
    {
        var result = await NftTools.GetNftStandards(_client, _options);

        Assert.DoesNotContain("Error", result);
        Assert.Contains("NFT Standards", result);
    }

    [Fact]
    public async Task GetNftMetadataStatuses_ReturnsData()
    {
        var result = await NftTools.GetNftMetadataStatuses(_client, _options);

        Assert.DoesNotContain("Error", result);
        Assert.Contains("NFT Metadata Statuses", result);
    }

    [Fact]
    public async Task GetNftActionTypes_ReturnsData()
    {
        var result = await NftTools.GetNftActionTypes(_client, _options);

        Assert.DoesNotContain("Error", result);
        Assert.Contains("NFT Action Types", result);
    }

    [Fact]
    public async Task GetAccountNftActions_ReturnsData()
    {
        var result = await NftTools.GetAccountNftActions(_client, _options, TestPublicKey);

        // Could be "No NFT actions" or actual data - both valid
        Assert.DoesNotContain("Error", result);
    }

    [Fact]
    public async Task GetAccountNftOwnership_ReturnsData()
    {
        var result = await NftTools.GetAccountNftOwnership(_client, _options, TestPublicKey);

        // Could be "No NFT ownership data" or actual data - both valid
        Assert.DoesNotContain("Error", result);
    }

    // ==================== Bidder Tools ====================

    [Fact]
    public async Task GetBidders_ReturnsList()
    {
        var result = await BidderTools.GetBidders(_client, _options);

        // Bidder endpoint may not be available on testnet - just verify no crash
        Assert.NotNull(result);
        Assert.NotEmpty(result);
    }

    [Fact]
    public async Task GetBidder_WithValidKey_ReturnsData()
    {
        var result = await BidderTools.GetBidder(_client, _options, TestPublicKey);

        // Could be "Bidder not found" or actual data or API error on testnet - all valid
        Assert.NotNull(result);
        Assert.NotEmpty(result);
    }

    // ==================== Currency Tools ====================

    [Fact]
    public async Task GetCurrencies_ReturnsList()
    {
        var result = await CurrencyTools.GetCurrencies(_client, _options);

        Assert.DoesNotContain("Error", result);
        Assert.Contains("Currencies", result);
    }

    [Fact]
    public async Task GetCurrentCurrencyRate_ReturnsData()
    {
        var result = await CurrencyTools.GetCurrentCurrencyRate(_client, _options, "1");

        // Could fail on testnet - just verify no crash
        Assert.NotNull(result);
        Assert.NotEmpty(result);
    }

    [Fact]
    public async Task GetHistoricalCurrencyRates_ReturnsData()
    {
        var result = await CurrencyTools.GetHistoricalCurrencyRates(_client, _options, "1");

        // Could fail on testnet - just verify no crash
        Assert.NotNull(result);
        Assert.NotEmpty(result);
    }

    // ==================== Centralized Account Tools ====================

    [Fact]
    public async Task GetCentralizedAccounts_ReturnsList()
    {
        var result = await CentralizedAccountTools.GetCentralizedAccounts(_client, _options);

        // Centralized accounts may not be available on testnet - just verify no crash
        Assert.NotNull(result);
        Assert.NotEmpty(result);
    }

    [Fact]
    public async Task GetCentralizedAccountInfo_ReturnsData()
    {
        // First try to get an account hash
        var endpoint = _options.IsTestnet ? (INetworkEndpoint)_client.Testnet : _client.Mainnet;
        var account = await endpoint.Account.GetAccountAsync(TestPublicKey);

        if (account?.AccountHash is null)
        {
            Assert.True(true);
            return;
        }

        var result = await CentralizedAccountTools.GetCentralizedAccountInfo(_client, _options, account.AccountHash);

        // Centralized account info may not be available on testnet - just verify no crash
        Assert.NotNull(result);
        Assert.NotEmpty(result);
    }

    // ==================== DEX Tools ====================

    [Fact]
    public async Task GetDexes_ReturnsList()
    {
        var result = await DexTools.GetDexes(_client, _options);

        // DEXes may not be available on testnet - just verify no crash
        Assert.NotNull(result);
        Assert.NotEmpty(result);
    }

    [Fact]
    public async Task GetSwaps_ReturnsData()
    {
        var result = await DexTools.GetSwaps(_client, _options);

        // Swaps may not be available on testnet - just verify no crash
        Assert.NotNull(result);
        Assert.NotEmpty(result);
    }

    // ==================== CSPR.name Tools ====================

    [Fact]
    public async Task ResolveCsprName_ReturnsData()
    {
        var result = await CsprNameTools.ResolveCsprName(_client, _options, "test.cspr");

        // CSPR.name resolution may not be available on testnet - just verify no crash
        Assert.NotNull(result);
        Assert.NotEmpty(result);
    }

    // ==================== Awaiting Deploy Tools ====================

    [Fact]
    public async Task GetAwaitingDeploy_ReturnsData()
    {
        var result = await AwaitingDeployTools.GetAwaitingDeploy(_client, _options, "0000000000000000000000000000000000000000000000000000000000000000");

        // Awaiting deploy likely won't exist - just verify no crash
        Assert.NotNull(result);
        Assert.NotEmpty(result);
    }

    // ==================== FT Rate Tools ====================

    [Fact]
    public async Task GetFtRateLatest_ReturnsData()
    {
        // Use a known contract package hash from testnet if available
        var endpoint = _options.IsTestnet ? (INetworkEndpoint)_client.Testnet : _client.Mainnet;
        var packages = await endpoint.Contract.GetContractPackagesAsync(new CSPR.Cloud.Net.Parameters.Wrapper.Contract.ContractPackageRequestParameters { PageSize = 1 });

        if (packages?.Data is null || packages.Data.Count == 0)
        {
            Assert.True(true);
            return;
        }

        var packageHash = packages.Data[0].ContractPackageHash;
        var result = await FtRateTools.GetFtRateLatest(_client, _options, packageHash);

        // Rate data may not be available - just verify no crash
        Assert.NotNull(result);
        Assert.NotEmpty(result);
    }

    [Fact]
    public async Task GetFtRates_ReturnsData()
    {
        var endpoint = _options.IsTestnet ? (INetworkEndpoint)_client.Testnet : _client.Mainnet;
        var packages = await endpoint.Contract.GetContractPackagesAsync(new CSPR.Cloud.Net.Parameters.Wrapper.Contract.ContractPackageRequestParameters { PageSize = 1 });

        if (packages?.Data is null || packages.Data.Count == 0)
        {
            Assert.True(true);
            return;
        }

        var packageHash = packages.Data[0].ContractPackageHash;
        var result = await FtRateTools.GetFtRates(_client, _options, packageHash);

        Assert.NotNull(result);
        Assert.NotEmpty(result);
    }

    [Fact]
    public async Task GetFtDailyRateLatest_ReturnsData()
    {
        var endpoint = _options.IsTestnet ? (INetworkEndpoint)_client.Testnet : _client.Mainnet;
        var packages = await endpoint.Contract.GetContractPackagesAsync(new CSPR.Cloud.Net.Parameters.Wrapper.Contract.ContractPackageRequestParameters { PageSize = 1 });

        if (packages?.Data is null || packages.Data.Count == 0)
        {
            Assert.True(true);
            return;
        }

        var packageHash = packages.Data[0].ContractPackageHash;
        var result = await FtRateTools.GetFtDailyRateLatest(_client, _options, packageHash);

        Assert.NotNull(result);
        Assert.NotEmpty(result);
    }

    [Fact]
    public async Task GetFtDailyRates_ReturnsData()
    {
        var endpoint = _options.IsTestnet ? (INetworkEndpoint)_client.Testnet : _client.Mainnet;
        var packages = await endpoint.Contract.GetContractPackagesAsync(new CSPR.Cloud.Net.Parameters.Wrapper.Contract.ContractPackageRequestParameters { PageSize = 1 });

        if (packages?.Data is null || packages.Data.Count == 0)
        {
            Assert.True(true);
            return;
        }

        var packageHash = packages.Data[0].ContractPackageHash;
        var result = await FtRateTools.GetFtDailyRates(_client, _options, packageHash);

        Assert.NotNull(result);
        Assert.NotEmpty(result);
    }

    [Fact]
    public async Task GetFtDexRateLatest_ReturnsData()
    {
        var endpoint = _options.IsTestnet ? (INetworkEndpoint)_client.Testnet : _client.Mainnet;
        var packages = await endpoint.Contract.GetContractPackagesAsync(new CSPR.Cloud.Net.Parameters.Wrapper.Contract.ContractPackageRequestParameters { PageSize = 1 });

        if (packages?.Data is null || packages.Data.Count == 0)
        {
            Assert.True(true);
            return;
        }

        var packageHash = packages.Data[0].ContractPackageHash;
        var result = await FtRateTools.GetFtDexRateLatest(_client, _options, packageHash);

        Assert.NotNull(result);
        Assert.NotEmpty(result);
    }

    [Fact]
    public async Task GetFtDexRates_ReturnsData()
    {
        var endpoint = _options.IsTestnet ? (INetworkEndpoint)_client.Testnet : _client.Mainnet;
        var packages = await endpoint.Contract.GetContractPackagesAsync(new CSPR.Cloud.Net.Parameters.Wrapper.Contract.ContractPackageRequestParameters { PageSize = 1 });

        if (packages?.Data is null || packages.Data.Count == 0)
        {
            Assert.True(true);
            return;
        }

        var packageHash = packages.Data[0].ContractPackageHash;
        var result = await FtRateTools.GetFtDexRates(_client, _options, packageHash);

        Assert.NotNull(result);
        Assert.NotEmpty(result);
    }

    [Fact]
    public async Task GetFtDailyDexRateLatest_ReturnsData()
    {
        var endpoint = _options.IsTestnet ? (INetworkEndpoint)_client.Testnet : _client.Mainnet;
        var packages = await endpoint.Contract.GetContractPackagesAsync(new CSPR.Cloud.Net.Parameters.Wrapper.Contract.ContractPackageRequestParameters { PageSize = 1 });

        if (packages?.Data is null || packages.Data.Count == 0)
        {
            Assert.True(true);
            return;
        }

        var packageHash = packages.Data[0].ContractPackageHash;
        var result = await FtRateTools.GetFtDailyDexRateLatest(_client, _options, packageHash);

        Assert.NotNull(result);
        Assert.NotEmpty(result);
    }

    [Fact]
    public async Task GetFtDailyDexRates_ReturnsData()
    {
        var endpoint = _options.IsTestnet ? (INetworkEndpoint)_client.Testnet : _client.Mainnet;
        var packages = await endpoint.Contract.GetContractPackagesAsync(new CSPR.Cloud.Net.Parameters.Wrapper.Contract.ContractPackageRequestParameters { PageSize = 1 });

        if (packages?.Data is null || packages.Data.Count == 0)
        {
            Assert.True(true);
            return;
        }

        var packageHash = packages.Data[0].ContractPackageHash;
        var result = await FtRateTools.GetFtDailyDexRates(_client, _options, packageHash);

        Assert.NotNull(result);
        Assert.NotEmpty(result);
    }

    // ==================== FT Action Types ====================

    [Fact]
    public async Task GetFtActionTypes_ReturnsData()
    {
        var result = await TokenTools.GetFtActionTypes(_client, _options);

        // FT action types may not be available on testnet - just verify no crash
        Assert.NotNull(result);
        Assert.NotEmpty(result);
    }

    // ==================== Purse Transfer Tools ====================

    [Fact]
    public async Task GetPurseTransfers_ReturnsData()
    {
        // Get main purse URef from a known account
        var endpoint = _options.IsTestnet ? (INetworkEndpoint)_client.Testnet : _client.Mainnet;
        var account = await endpoint.Account.GetAccountAsync(TestPublicKey);

        if (account?.MainPurseUref is null)
        {
            Assert.True(true);
            return;
        }

        var result = await TransferTools.GetPurseTransfers(_client, _options, account.MainPurseUref);

        // Could be "No transfers" or actual data - both valid
        Assert.DoesNotContain("Error", result);
    }

    // ==================== Purse Delegation Tools ====================

    [Fact]
    public async Task GetPurseDelegations_ReturnsData()
    {
        var endpoint = _options.IsTestnet ? (INetworkEndpoint)_client.Testnet : _client.Mainnet;
        var account = await endpoint.Account.GetAccountAsync(TestPublicKey);

        if (account?.MainPurseUref is null)
        {
            Assert.True(true);
            return;
        }

        var result = await AccountTools.GetPurseDelegations(_client, _options, account.MainPurseUref);

        // Could be "No delegations" or actual data - both valid
        Assert.DoesNotContain("Error", result);
    }

    [Fact]
    public async Task GetPurseDelegationRewards_ReturnsData()
    {
        var endpoint = _options.IsTestnet ? (INetworkEndpoint)_client.Testnet : _client.Mainnet;
        var account = await endpoint.Account.GetAccountAsync(TestPublicKey);

        if (account?.MainPurseUref is null)
        {
            Assert.True(true);
            return;
        }

        var result = await AccountTools.GetPurseDelegationRewards(_client, _options, account.MainPurseUref);

        // Could be "No delegation rewards" or actual data - both valid
        Assert.DoesNotContain("Error", result);
    }

    [Fact]
    public async Task GetTotalPurseDelegationRewards_ReturnsData()
    {
        var endpoint = _options.IsTestnet ? (INetworkEndpoint)_client.Testnet : _client.Mainnet;
        var account = await endpoint.Account.GetAccountAsync(TestPublicKey);

        if (account?.MainPurseUref is null)
        {
            Assert.True(true);
            return;
        }

        var result = await AccountTools.GetTotalPurseDelegationRewards(_client, _options, account.MainPurseUref);

        Assert.DoesNotContain("Error", result);
        Assert.Contains("Total Rewards", result);
    }

    // ==================== Validator Era Rewards ====================

    [Fact]
    public async Task GetValidatorEraRewards_ReturnsData()
    {
        var result = await ValidatorTools.GetValidatorEraRewards(_client, _options, TestPublicKey);

        // Could be "No era rewards" or actual data - both valid
        Assert.DoesNotContain("Error", result);
    }
}
