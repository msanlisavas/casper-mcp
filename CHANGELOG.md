# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

### Fixed

- **Pre-dispatch rejections on `/mcp` are now valid JSON-RPC 2.0 errors.** When a request to the MCP endpoint is rejected before tool dispatch — missing `X-CSPR-Cloud-Api-Key`, invalid `X-Casper-Network`, or a failed `apikey`-mode shared secret — the server now returns a JSON-RPC error envelope (`{"jsonrpc":"2.0","error":{"code","message"},"id"}`) instead of a bare `{"error":"..."}` body. MCP clients (e.g. Codex/`rmcp`) deserialize the response body as a JSON-RPC message regardless of HTTP status, so the old shape produced an opaque "did not match any variant of `JsonRpcMessage`" error instead of the real reason; the envelope lets clients surface the actual message. HTTP status is unchanged (missing key → `401`, invalid network → `400`) and the request `id` is echoed back when present. The missing-key message is now self-explanatory for agents — it names the `CSPR_CLOUD_API_KEY` environment variable, the `X-CSPR-Cloud-Api-Key` header it maps to, and that the client must be restarted — so an assistant can walk the user through their local MCP-client config (e.g. a Codex `env_http_headers = { "X-CSPR-Cloud-Api-Key" = "CSPR_CLOUD_API_KEY" }` entry in `~/.codex/config.toml`). Non-`/mcp` paths and the `/health`·`/ready`·`/.well-known` probes keep their existing plain shape. The framework-emitted JWT `401` (`--auth-mode jwt`) is unchanged.

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
- **Observability**: opt-in OpenTelemetry (traces + metrics) enabled by setting `OTEL_EXPORTER_OTLP_ENDPOINT`. Captures ASP.NET Core, outbound HttpClient, .NET runtime, and custom `casper-mcp` tool instrumentation — `casper_mcp.tool.calls` (counter) and `casper_mcp.tool.duration` (histogram), tagged by `tool` and `status`. Plus structured JSON logs (one line per tool call: `tool`, `status`, `duration_ms`, `tenant`, `correlation_id`). Traffic is tagged with a **non-reversible fingerprint** of the CSPR.Cloud key, so per-agent activity is correlatable across logs/metrics/traces without exposing the key.
- **Environment-variable configuration**: `CASPER_MCP_TRANSPORT`, `CASPER_MCP_PORT`, `CASPER_MCP_NETWORK`, `CASPER_MCP_PATH` (container-friendly; CLI args still override).

### Removed (BREAKING)

- **`/sse` endpoint removed** — use `/mcp` (Streamable HTTP, stateless).
- **`--transport sse` removed** — use `--transport http`.
- **`--server-api-key` / `CASPER_MCP_SERVER_API_KEY` removed** — use `--auth-mode apikey` with `--auth-api-key` / `CASPER_MCP_AUTH_API_KEY`.
- **10 `Watch*` WebSocket streaming tools removed**: `WatchBlocks`, `WatchDeploys`, `WatchTransfers`, `WatchAccountBalances`, `WatchContracts`, `WatchContractPackages`, `WatchContractEvents`, `WatchFtTokenActions`, `WatchNfts`, `WatchNftActions`. Poll request/response tools instead (e.g. repeated `GetDeploy` for transaction confirmation).
- Per-request CSPR.Cloud key is now required in http mode (missing `X-CSPR-Cloud-Api-Key` → HTTP 401).
- Tool count: 92 → 82 (removed 10 Watch* tools).

### Changed

- Upgraded `CSPR.Cloud.Net` dependency to **3.0.0**, which standardizes upstream response handling: `404 Not Found` now returns `null` (surfaced as a clean "not found" tool result) instead of throwing, `429` maps to a rate-limit error, and all `5xx` map to a server error.
- Upstream errors are now mapped centrally to clear, safe messages (authentication failed / invalid parameters / rate limited / temporarily unavailable). Raw exception text, stack traces, and secrets are never returned to the agent or written to logs.

### Fixed

- **Centralized-accounts tools now work** (`get_centralized_accounts`, `get_centralized_account_info`) — previously returned `404` due to a wrong upstream path in the SDK (fixed in `CSPR.Cloud.Net` 3.0.0).
- **Bidder tools** (`get_bidders`, `get_bidder`) now send the required `era_id`, defaulting to the current era — previously failed with `400 Empty era_id`.
- **FT rate tools** (`get_ft_rates`, `get_ft_daily_rates`, and the `*_rate_latest` variants) now send the required `currency_id` (default USD) — previously failed with `400 Empty currency_id`.
- **FT DEX rate tools** (`get_ft_dex_rate_latest`, `get_ft_dex_rates`, `get_ft_daily_dex_rate_latest`, `get_ft_daily_dex_rates`) now take a required `targetContractPackageHash` (the API mandates a target token) — previously failed with `400`.
- **Block list views** (`get_latest_blocks`, `get_validator_blocks`) now emit the full block hash so an agent can chain it into `get_block` / `get_block_deploys` — previously truncated to 16 characters.
- **Tools no longer swallow exceptions** — a central `ToolInvocationFilter` owns error handling: failures return an MCP `IsError` result, are logged once with a correlation id, and `OperationCanceledException` propagates so a client/WAF disconnect cancels in-flight work.
- **429 Too Many Requests** is now surfaced as a clear "rate limited, retry shortly" message instead of a generic error.
- **OAuth discovery** (`GET /.well-known/oauth-protected-resource`) is no longer rejected by the per-agent-key requirement in `jwt` mode.
- **Upstream timeout**: the CSPR.Cloud HTTP client now has a 30s timeout so a hung upstream cannot hold a request past typical WAF/proxy idle limits.
- **Log redaction**: the SDK client is no longer handed the app logger (its exception text can include raw upstream bodies); only the redacted tenant fingerprint is ever logged.

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
