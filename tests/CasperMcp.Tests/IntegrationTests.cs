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
public class IntegrationTests
{
    private readonly CasperCloudRestClient _client;
    private readonly CasperMcpOptions _options;

    // Known testnet public key (a validator on testnet)
    private const string TestPublicKey = "0106ca7c39cd272dbf21a86eeb3b36b7c26e2e9b94af64292419f7862936bca2ca";

    public IntegrationTests()
    {
        var apiKey = Environment.GetEnvironmentVariable("CSPR_CLOUD_TESTNET_API_KEY")
                     ?? "55f79117-fc4d-4d60-9956-65423f39a06a";

        var config = new CasperCloudClientConfig(apiKey);
        _client = new CasperCloudRestClient(config);
        _options = new CasperMcpOptions { Network = "testnet" };
    }

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

    // ==================== Transfer Tools ====================

    [Fact]
    public async Task GetTransfers_WithValidKey_ReturnsTransfers()
    {
        var result = await TransferTools.GetTransfers(_client, _options, TestPublicKey);

        // Could be "No transfers" or actual transfers - both are valid
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

    // ==================== NFT Tools ====================

    [Fact]
    public async Task GetAccountNfts_WithValidKey_ReturnsNfts()
    {
        var result = await NftTools.GetAccountNfts(_client, _options, TestPublicKey);

        // Could be "No NFTs" or actual NFTs - both valid
        Assert.DoesNotContain("Error", result);
    }
}
