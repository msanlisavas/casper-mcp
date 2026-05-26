# Remote Multi-Tenant MCP — Design

- **Date:** 2026-05-26
- **Status:** Approved for implementation planning
- **Target version:** 3.0.0 (major; remote behavior is intentionally not backward-compatible)
- **Author:** Murat Şanlısavaş (with design assistance)

## 1. Background & Problem

`casper-mcp` is an MCP server for the Casper Network, backed by `CSPR.Cloud.Net`. Today it supports two transports:

- **stdio** (default) — local, one process per user.
- **SSE/HTTP** — `WithHttpTransport()` + `MapMcp()`, advertised at `/sse`.

A prospective operator wants to run a **single shared instance** inside their infrastructure and expose it to **potentially thousands of remote AI agents** behind their own WAF/auth layer. The current server cannot serve this well:

1. **The CSPR.Cloud API key is global.** `Program.cs` builds the REST/socket clients once at startup from a single `CSPR_CLOUD_API_KEY` and registers them as singletons. Every agent would share one key; there is no way for an agent to supply its own per request.
2. **Sessions are stateful.** `WithHttpTransport()` defaults to in-memory, session-id-keyed state — requiring sticky sessions behind a load balancer and conflicting with horizontal scaling.
3. **Streaming tools hold requests up to 120s.** The `Watch*` tools keep an HTTP request and a CSPR.cloud websocket open for up to `MaxTimeoutSeconds = 120`, tripping WAF/proxy idle timeouts and consuming resources at scale.

This is a greenfield redesign: there are **no existing remote deployments to preserve**. The bar is an industry-standard remote MCP service — best practices only, no band-aids — built to serve many tenants concurrently.

## 2. Goals

- A **stateless, RPC-style** remote transport that survives WAFs/proxies and scales horizontally with no sticky sessions.
- **Per-agent CSPR.Cloud credentials**: each request carries its own key; no shared global key in remote mode.
- **Pluggable, standards-based agent→server authentication** (`none` / `apikey` / `jwt`), including OAuth 2.1 resource-server validation per the MCP authorization spec.
- **Correct connection management** at scale — pooled HTTP connections, no socket exhaustion, no per-tenant resource leak.
- **Per-request network selection** (mainnet/testnet) so one instance serves both.
- **Operational hygiene**: typed upstream error mapping, structured logging with secret redaction, and liveness/readiness probes.
- Keep **stdio** mode working for local single-user use with minimal change.

## 3. Non-Goals

- Backward compatibility with the legacy `/sse` endpoint or the global-key remote model (intentionally dropped).
- Real-time event streaming / subscriptions to agents (see §6.8 — removed; agents poll).
- Running an OAuth **authorization server** / issuing tokens (the server only *validates* tokens).
- OpenTelemetry traces/metrics — deferred to a documented follow-up (§13).
- Per-tenant rate limiting at the server edge — deferred; CSPR.Cloud enforces per-key limits and the operator's WAF can shape traffic (§13).

## 4. Requirements Traceability

| Operator requirement | Addressed by |
|---|---|
| Shared instance in their infra | Stateless HTTP profile (§6.1) |
| Expose to remote agents over HTTP | Streamable HTTP at `/mcp` (§6.1) |
| Protect with their WAF/auth | `auth.mode=none` trusts WAF; `apikey`/`jwt` optional (§6.4) |
| Avoid long-lived session issues | Stateless mode; each call is an independent request (§6.1) |
| Each agent passes its own CSPR key | Required `X-CSPR-Cloud-Api-Key` header, per-request client (§6.2–6.3) |
| 120s streaming hits WAF timeouts | Streaming tools removed entirely (§6.8) |
| RPC-like behind WAF | Stateless Streamable HTTP is request/response per call (§6.1) |

## 5. High-Level Architecture

Two **profiles**, selected by `--transport`, sharing the same ~70 tools:

- **`stdio` (local):** unchanged model. CSPR key required at startup; REST client and effective options are singletons. No HTTP, no auth, no headers.
- **`http` (remote):** stateless Streamable HTTP. No CSPR key at startup. Per request, the server resolves (a) the agent's CSPR.Cloud client and (b) the effective network from request headers, and injects them via **request-scoped DI** — so the existing tool method signatures receive the correct per-tenant objects with **no tool-file changes** to their parameters.

The two profiles already live in the `if (options.IsSseTransport) { … } else { … }` split in `Program.cs`; this design formalizes and rebuilds that split.

## 6. Detailed Design

### 6.1 Transport

- Remote mode configures `WithHttpTransport(o => o.Stateless = true)`. In stateless mode the SDK uses ASP.NET Core's per-request `HttpContext.RequestServices` as the service provider (`ScopeRequests = false`), so scoped services and `IHttpContextAccessor` resolve to the current request. Each tool call is its own HTTP request — no session id, no in-memory session state, horizontally scalable.
- The MCP endpoint is mapped at **`/mcp`** (configurable via `--mcp-path`) via `MapMcp("/mcp")`. Legacy SSE is **not** exposed (stateless mode is Streamable HTTP only; this is the intended retirement of `/sse`).
- stdio mode keeps `WithStdioServerTransport()`.

### 6.2 Per-Request Credential & Network Resolution

Two request headers (remote mode):

| Header | Required | Purpose | Default |
|---|---|---|---|
| `X-CSPR-Cloud-Api-Key` | Yes | Downstream CSPR.Cloud credential for this agent | — (401 if missing) |
| `X-Casper-Network` | No | `mainnet` \| `testnet` for this request | server `--network` |

A small middleware (after auth, before MCP dispatch) rejects requests missing `X-CSPR-Cloud-Api-Key` with `401` and a JSON body. This enforces "required, no fallback" at the edge so downstream code can assume the key is present.

Request-scoped DI registrations (remote mode only):

- `CasperCloudRestClient` (**scoped**): factory reads `X-CSPR-Cloud-Api-Key` from `IHttpContextAccessor`, constructs `new CasperCloudRestClient(new CasperCloudClientConfig(key), httpClientFactory.CreateClient("cspr"), loggerFactory)`.
- `CasperMcpOptions` (**scoped**): copy of server defaults with `Network` overridden from `X-Casper-Network` when present.

Because tools already declare `CasperCloudRestClient client` and `CasperMcpOptions options` parameters, the SDK resolves them from the request scope. **No tool method signatures change.**

> The CSPR key is a *downstream resource credential*, deliberately kept separate from agent→server auth (§6.4). The two never share a header.

### 6.3 Connection Management

`CasperCloudRestClient`'s public constructor accepts an injected `HttpClient` and the client is **not** `IDisposable` (its only resource is that injected client). Therefore:

- Register a single pooled handler via `IHttpClientFactory`: `services.AddHttpClient("cspr")` with a primary `SocketsHttpHandler { PooledConnectionLifetime = TimeSpan.FromMinutes(2), … }`.
- Per request, build a fresh lightweight `CasperCloudRestClient` over a factory `HttpClient`. The factory pools the underlying handler across all tenants; a fresh client per request guarantees the api key cannot bleed across tenants even if the SDK sets it as a default header.

This removes any need for a per-key client cache, eviction/LRU, or disposal logic. Connection reuse and DNS rotation are handled by the framework, which is correct at thousands of distinct keys.

### 6.4 Authentication (agent → server)

A single switch `--auth-mode` (env `CASPER_MCP_AUTH_MODE`) with three modes:

- **`none` (default):** no server-side auth; trust the operator's fronting WAF. The CSPR-key requirement (§6.2) still applies.
- **`apikey`:** a shared secret presented as `Authorization: Bearer <secret>` (also accepted via `X-API-Key` for convenience). Constant-time comparison. For standalone protection without a WAF.
- **`jwt`:** OAuth 2.1 **resource server** per the MCP authorization spec. Validates incoming `Authorization: Bearer <JWT>` against a configured **authority/issuer** and **audience** (JWKS auto-discovered from the authority) using ASP.NET `AddAuthentication().AddJwtBearer(...)`, and requires authorization on the MCP endpoint. Serves Protected Resource Metadata at `/.well-known/oauth-protected-resource` and returns `401` with a `WWW-Authenticate` challenge pointing at the authorization server. The server **validates** tokens only; it never issues them, and plugs into the operator's existing IdP or any standard OAuth provider.

Middleware order (remote): [auth: apikey/jwt] → [CSPR-key-present check] → [MCP dispatch]. `/health`, `/ready`, and (in jwt mode) the metadata endpoint are always unauthenticated.

### 6.5 Central Error Mapping & Cancellation

A single `WithRequestFilters(f => f.AddCallToolFilter(...))` wraps every tool invocation:

- Catches exceptions, maps CSPR.Cloud HTTP failures to typed, structured results: `401/403` → "upstream authentication/authorization failed (check your CSPR.Cloud key/plan)"; `429` → "rate limited by CSPR.Cloud, retry later"; `5xx`/timeout → "upstream temporarily unavailable"; otherwise a generic message. Never leaks stack traces or secrets. Returns `CallToolResult { IsError = true }` so the agent can reason about/recover from it.
- Logs the failure once with a correlation id (§6.6). Lets `OperationCanceledException` propagate as the SDK intends, so a client/WAF disconnect cancels in-flight upstream work (the request `CancellationToken` flows to CSPR.Cloud calls).

Because this filter is authoritative, the repetitive per-tool `try/catch { return "Error …: " + ex.Message }` blocks are **removed**, letting exceptions flow to the filter for consistent typed output. (Mechanical edit across tool files; reduces code rather than adding it.)

### 6.6 Logging & Secret Redaction

- Structured logging with a per-request **correlation id** (from `HttpContext.TraceIdentifier` or a generated GUID), attached to tool-call logs via the filter.
- **Redaction is mandatory:** `X-CSPR-Cloud-Api-Key`, `Authorization`, and `X-API-Key` values are never logged. No request bodies containing secrets are logged. Startup banner reports auth mode and whether a default network is set — never key material.

### 6.7 Health & Readiness

- `GET /health` — **liveness**: process is up. Unauthenticated.
- `GET /ready` — **readiness**: configuration is valid for the active profile (e.g. in `jwt` mode the authority is reachable/metadata resolvable; `IHttpClientFactory` registered). Unauthenticated. Suitable for k8s/WAF probes.

### 6.8 Removal of Streaming Tools

The 10 `Watch*` tools (`StreamingTools.cs`) and the `CasperCloudSocketClient` registration/usage are **removed**.

Rationale: an MCP tool call is one-shot and blocking from the model's perspective; agents have no background event loop and cannot consume a subscription. Every real agent use case maps to polling existing request/response tools — e.g. transaction confirmation via repeated `GetDeploy(hash)` (`DeployTools.cs`), recent activity via `GetDeploys`/`GetBlockDeploys`. Long-held websockets + HTTP requests are simultaneously the least useful to an agent and the worst fit for a WAF/at-scale deployment. Removing them eliminates the 120s problem outright and shrinks the surface to ~70 uniform request/response tools.

A future server-side, REST-polling `WaitForDeploy(hash, timeout)` convenience is explicitly out of scope for v1 (§13).

## 7. Configuration Surface

| Flag | Env | Applies to | Default | Notes |
|---|---|---|---|---|
| `--transport` | — | both | `stdio` | `stdio` \| `http` (replaces `sse`) |
| `--port` | — | http | `3001` | |
| `--mcp-path` | — | http | `/mcp` | MCP endpoint path |
| `--network` | — | both | `mainnet` | Server default; per-request override via `X-Casper-Network` |
| `--api-key` | `CSPR_CLOUD_API_KEY` | stdio | — | **Required in stdio**; ignored/unused in http |
| `--auth-mode` | `CASPER_MCP_AUTH_MODE` | http | `none` | `none` \| `apikey` \| `jwt` |
| `--auth-api-key` | `CASPER_MCP_AUTH_API_KEY` | http (apikey) | — | Shared secret |
| `--auth-jwt-authority` | `CASPER_MCP_AUTH_JWT_AUTHORITY` | http (jwt) | — | OAuth issuer/authority (JWKS discovery) |
| `--auth-jwt-audience` | `CASPER_MCP_AUTH_JWT_AUDIENCE` | http (jwt) | — | Expected token audience |

Remote request headers: `X-CSPR-Cloud-Api-Key` (required), `X-Casper-Network` (optional), `Authorization`/`X-API-Key` (per auth mode).

## 8. Request Lifecycle (remote, jwt mode example)

1. WAF forwards `POST /mcp` with `Authorization: Bearer <jwt>` and `X-CSPR-Cloud-Api-Key: <key>`.
2. JWT middleware validates the token (issuer/audience/signature/expiry); else `401` + `WWW-Authenticate`.
3. CSPR-key middleware confirms `X-CSPR-Cloud-Api-Key` present; else `401`.
4. SDK creates the request scope; scoped `CasperCloudRestClient` is built from the header key over a pooled `HttpClient`; scoped `CasperMcpOptions.Network` resolved from `X-Casper-Network`.
5. Tool executes; `CallTool` filter wraps it for error mapping + correlation-id logging; cancellation token flows to CSPR.Cloud.
6. Result returned in a single Streamable HTTP response; scope disposed. No residual session state.

## 9. Components & File Impact

- `Program.cs` — rebuild the http branch: stateless transport, `/mcp` mapping, `IHttpClientFactory`, scoped client/options factories, `IHttpContextAccessor`, auth-mode wiring, `CallTool` filter, `/ready`, updated banner. stdio branch largely intact.
- `Configuration/` — split server-level startup config from the per-request `CasperMcpOptions` the tools consume; add auth options.
- `Middleware/` — add `RequireCsprKeyMiddleware`; refactor existing `ApiKeyAuthMiddleware` into the `apikey` mode; add jwt wiring + protected-resource metadata endpoint.
- New: `Infrastructure/CasperClientFactory` (or inline scoped factory) and the `CallTool` error-mapping filter + redacting log helpers.
- `Tools/StreamingTools.cs` — **deleted**; remove `CasperCloudSocketClient` registration and any references.
- `Tools/*.cs` — remove now-redundant per-tool `try/catch` so errors reach the central filter (no signature/parameter changes).
- `docker-compose.yml`, `Dockerfile`, `README.md`, `mcp.json` — update to the http profile, `/mcp`, new flags/headers, auth modes, removal of `/sse` and `Watch*`.
- `tests/` — see §11.

## 10. Security Considerations

- **Tenant isolation:** a fresh `CasperCloudRestClient` per request prevents key bleed across tenants sharing the pooled handler.
- **Secret handling:** keys/tokens never logged (§6.6); not persisted; not echoed in errors (§6.5).
- **Auth:** constant-time comparison in `apikey` mode; full signature/issuer/audience/expiry validation in `jwt` mode; `none` is safe only behind a trusted WAF and is documented as such.
- **Default network:** `X-Casper-Network` is validated against an allowlist (`mainnet`/`testnet`); anything else is rejected.
- **DoS surface:** removal of long-held streaming requests materially reduces resource-holding attack surface.

## 11. Testing Strategy

- **Unit:** header→network resolution (default + override + invalid rejected); CSPR-key-missing → 401; `apikey` accept/reject (constant-time); error-mapping filter maps 401/403/429/5xx/timeout to the right typed results and redacts secrets; readiness logic.
- **Integration:** stateless `POST /mcp` tool call carrying `X-CSPR-Cloud-Api-Key` resolves a per-request client and returns a result end-to-end; two concurrent requests with different keys stay isolated; `jwt` mode rejects an unsigned/expired token and serves protected-resource metadata. Continue throttling live CSPR.Cloud integration calls to respect rate limits.
- **Regression:** stdio profile still starts with `--api-key` and serves tools unchanged.

## 12. Breaking Changes & Versioning

Target **3.0.0**. Breaking (remote only, intentional):

- `--transport sse` → `--transport http`; endpoint `/sse` → `/mcp` (Streamable HTTP).
- Remote requests must send `X-CSPR-Cloud-Api-Key`; the startup CSPR key is no longer used in http mode.
- The 10 `Watch*` tools are removed (both profiles).
- `CASPER_MCP_SERVER_API_KEY` semantics fold into `--auth-mode apikey` / `CASPER_MCP_AUTH_API_KEY`.

stdio/local users are unaffected aside from the `Watch*` removal.

## 13. Out of Scope / Follow-Ups

- OpenTelemetry traces & metrics (latency, error rate, in-flight) via the SDK's OTel hooks.
- Server-side `WaitForDeploy(hash, timeout)` REST-polling convenience (reintroduces a held request — deliberate, separate decision).
- Edge per-tenant rate limiting / quotas.
- Bounded client caching — intentionally **not** needed given `IHttpClientFactory` (recorded so it isn't reintroduced as a "fix").
