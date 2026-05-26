# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [3.0.0] - 2026-05-26

### Added

- **Multi-tenant remote profile**: stateless Streamable HTTP transport at `/mcp` (path configurable via `--mcp-path`). Each request must carry `X-CSPR-Cloud-Api-Key: <key>` — there is no global startup CSPR key in http mode. Optional `X-Casper-Network: mainnet|testnet` header overrides the server default per request.
- **Pluggable auth** via `--auth-mode` / `CASPER_MCP_AUTH_MODE`:
  - `none` (default) — no built-in auth; trust a fronting WAF/proxy.
  - `apikey` — shared secret via `Authorization: Bearer <secret>` or `X-API-Key`; configured with `--auth-api-key` / `CASPER_MCP_AUTH_API_KEY`.
  - `jwt` — OAuth 2.1 resource server: validates `Authorization: Bearer <JWT>` against `--auth-jwt-authority` / `CASPER_MCP_AUTH_JWT_AUTHORITY` and `--auth-jwt-audience` / `CASPER_MCP_AUTH_JWT_AUDIENCE`; serves OAuth 2.0 protected-resource metadata at `/.well-known/oauth-protected-resource`.
- **Readiness endpoint**: `GET /ready` (joins `/health` as an unauthenticated probe).
- Pooled HTTP connections via `IHttpClientFactory` with central typed error mapping.
- `--mcp-path` flag to customize the MCP endpoint path (default `/mcp`).

### Removed (BREAKING)

- **`/sse` endpoint removed** — use `/mcp` (Streamable HTTP, stateless).
- **`--transport sse` removed** — use `--transport http`.
- **`--server-api-key` / `CASPER_MCP_SERVER_API_KEY` removed** — use `--auth-mode apikey` with `--auth-api-key` / `CASPER_MCP_AUTH_API_KEY`.
- **10 `Watch*` WebSocket streaming tools removed**: `WatchBlocks`, `WatchDeploys`, `WatchTransfers`, `WatchAccountBalances`, `WatchContracts`, `WatchContractPackages`, `WatchContractEvents`, `WatchFtTokenActions`, `WatchNfts`, `WatchNftActions`. Poll request/response tools instead (e.g. repeated `GetDeploy` for transaction confirmation).
- Per-request CSPR.Cloud key is now required in http mode (missing `X-CSPR-Cloud-Api-Key` → HTTP 401).
- Tool count: 92 → 82 (removed 10 Watch* tools).

## [2.9.0] - 2026-05-07

Version jumps directly from `1.2.0` to `2.9.0` to align with `CSPR.Cloud.Net` SDK and the CSPR Cloud API revision the MCP wraps. Going forward, the MCP version tracks the SDK + API version so consumers can see at a glance which API revision a given MCP build covers.

### Added

- **Streaming Tools** (10 new): `WatchBlocks`, `WatchDeploys`, `WatchTransfers`, `WatchAccountBalances`, `WatchContracts`, `WatchContractPackages`, `WatchContractEvents`, `WatchFtTokenActions`, `WatchNfts`, `WatchNftActions` — subscribe to CSPR.Cloud Streaming API and capture up to N events or timeout. Each tool exposes `maxEvents` (1-50) and `timeoutSeconds` (1-120), plus stream-specific filters.
- **Account Tools**: `GetAccountUndelegations` — pending undelegations (released 7 eras after creation).
- **NFT Tools**: `GetNetworkNfts` — paginated network-wide NFT list with optional contract-package, owner, and block-height filters.
- New `CasperCloudSocketClient` registered in DI for streaming.
- `FormattingHelpers.SumMotes` (BigInteger-safe summation), `FormatUnixSeconds`, `FormatPercentage(string?)` overload, and `MotesToCspr(string?)` upgraded to BigInteger to support uint64-overflow-safe motes from SDK 2.x.
- `GetAccountInfo` now surfaces `UndelegatingBalance` and `CSPR.name` when present.
- `GetDeploy` now surfaces `CallerHash`, `CallerCsprName`, `ConsumedGas`, `RefundAmount`, and Casper 2.0 `VersionId`.
- `GetFtTokenInfo` now surfaces `IconUrl` and `WebsiteUrl` for the contract package.
- `GetSupplyInfo` now surfaces `TotalAnnualIssuance`, `AnnualStakingRewardsIssuance`, `AnnualEcosystemSustainIssuance`.

### Changed

- Upgraded `CSPR.Cloud.Net` NuGet package from `1.1.0` to `2.9.0` (SDK version now tracks the CSPR Cloud API version it covers).
- All monetary fields in SDK responses now flow through `BigInteger`-based formatting to handle uint64 overflow (was: `ulong?`).
- Supply timestamp now correctly parsed as Unix seconds (was: ISO timestamp).
- Validator percentage fields (Fee, SelfShare, NetworkShare) now formatted from `string` (SDK type changed in v2.4.3+).
- Tool count: 80 → 92 (added 10 streaming snapshot tools + 2 new REST tools).

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
