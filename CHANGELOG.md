# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [1.2.0] - 2026-02-25

### Added

- **DEX Tools**: `GetDexes`, `GetSwaps` — query decentralized exchanges and token swaps
- **FT Rate Tools**: `GetFtRateLatest`, `GetFtRates`, `GetFtDailyRateLatest`, `GetFtDailyRates`, `GetFtDexRateLatest`, `GetFtDexRates`, `GetFtDailyDexRateLatest`, `GetFtDailyDexRates` — fungible token price/rate data (fiat and token-to-token)
- **CSPR.name Tools**: `ResolveCsprName` — resolve CSPR.name to account hash
- **Awaiting Deploy Tools**: `GetAwaitingDeploy`, `CreateAwaitingDeploy`, `AddAwaitingDeployApproval` — multi-signature deploy workflow
- **Token Tools**: `GetFtActionTypes` — list of fungible token action types
- **Transfer Tools**: `GetPurseTransfers` — transfers by purse URef
- **Account Tools**: `GetPurseDelegations`, `GetPurseDelegationRewards`, `GetTotalPurseDelegationRewards` — purse-based delegation queries
- **Validator Tools**: `GetValidatorEraRewards` — validator rewards aggregated by era
- Integration tests for all 20 new tools (108 tests total)

### Changed

- Upgraded `CSPR.Cloud.Net` NuGet package from `1.0.11` to `1.1.0`
- Updated README with full tool documentation (80 tools across 16 categories)
- Updated `mcp.json` manifest with all tool names

## [1.1.0] - 2025-02-25

### Added

- **Bidder Tools**: `GetBidder`, `GetBidders` — query bidder data on the Casper Network
- **Currency Tools**: `GetCurrentCurrencyRate`, `GetHistoricalCurrencyRates`, `GetCurrencies` — CSPR exchange rate data
- **Centralized Account Tools**: `GetCentralizedAccountInfo`, `GetCentralizedAccounts` — centralized account metadata
- **Account Tools**: `GetAccounts`, `GetAccountContractPackages`, `GetAccountDelegationRewards`, `GetTotalAccountDelegationRewards`, `GetTotalValidatorDelegatorRewards`
- **Block Tools**: `GetValidatorBlocks` — blocks proposed by a validator
- **Contract Tools**: `GetContracts`, `GetContractTypes`, `GetContractEntryPointCosts`, `GetContractPackages`, `GetContractsByContractPackage`
- **Deploy Tools**: `GetDeploys`, `GetBlockDeploys`, `GetDeployExecutionTypes`
- **Transfer Tools**: `GetDeployTransfers` — transfers within a specific deploy
- **Validator Tools**: `GetValidatorDelegations`, `GetValidatorRewards`, `GetValidatorTotalRewards`, `GetHistoricalValidatorPerformance`, `GetHistoricalValidatorAveragePerformance`, `GetHistoricalValidatorsAveragePerformance`
- **Token Tools**: `GetFungibleTokenActions`, `GetAccountFungibleTokenActions`, `GetContractPackageFungibleTokenActions`
- **NFT Tools**: `GetNft`, `GetNftStandards`, `GetNftMetadataStatuses`, `GetNftActionsForToken`, `GetAccountNftActions`, `GetContractPackageNftActions`, `GetNftActionTypes`, `GetContractPackageNftOwnership`, `GetAccountNftOwnership`
- `FormatDouble` and `FormatDecimal` helpers for performance scores and cost formatting
- Integration tests for all 60 tools (90 tests total)

### Changed

- Updated README with full tool documentation (60 tools across 12 categories)
- Updated `mcp.json` manifest with all tool names

## [1.0.0] - 2025-02-24

### Added

- Initial release with 20 MCP tools
- Account, Block, Deploy, Validator, Contract, Token, NFT, Transfer, and Network tools
- stdio and SSE/HTTP transport support
- Docker support with GitHub Container Registry publishing
- API key authentication middleware for SSE mode
- Integration tests against CSPR.Cloud testnet
