# casper-mcp

MCP (Model Context Protocol) server for the **Casper Network** blockchain. Gives any AI assistant or MCP-compatible tool access to on-chain data — accounts, blocks, deploys, validators, smart contracts, tokens, NFTs, transfers, and network status.

**Language-agnostic.** Supports both **stdio** (local) and **Streamable HTTP** (remote, multi-tenant) transports. Use it from any project — Python, Node.js, Rust, Go, or any MCP client.

Built with [CSPR.Cloud.Net](https://www.nuget.org/packages/CSPR.Cloud.Net) and the [ModelContextProtocol](https://www.nuget.org/packages/ModelContextProtocol) SDK.

---

> **v3.0.0 BREAKING CHANGES** — if you are upgrading from v2.x, see the [Breaking Changes](#breaking-changes-v300) section before updating.

---

## Quick Start

### Option 1: .NET Global Tool (recommended)

```bash
dotnet tool install -g CasperMcp
casper-mcp --api-key YOUR_API_KEY
```

### Option 2: Docker (no .NET required)

```bash
# Pre-built image from GitHub Container Registry
docker pull ghcr.io/msanlisavas/casper-mcp:latest
docker run -i ghcr.io/msanlisavas/casper-mcp:latest --api-key YOUR_API_KEY

# Or build locally
docker build -t casper-mcp .
docker run -i casper-mcp --api-key YOUR_API_KEY
```

### Option 3: Build from source

```bash
git clone https://github.com/msanlisavas/casper-mcp.git
cd casper-mcp
dotnet build
dotnet run --project src/CasperMcp -- --api-key YOUR_API_KEY
```

### Configuration

| Argument | Environment Variable | Description |
|---|---|---|
| `--api-key` | `CSPR_CLOUD_API_KEY` | CSPR.Cloud API key (**required in stdio mode**). Get one at [cspr.cloud](https://cspr.cloud) |
| `--network` | — | `mainnet` (default) or `testnet` |
| `--transport` | — | `stdio` (default) or `http` (Streamable HTTP for remote/multi-tenant access) |
| `--port` | — | HTTP port for http mode (default: `3001`) |
| `--mcp-path` | — | URL path for the MCP endpoint in http mode (default: `/mcp`) |
| `--auth-mode` | `CASPER_MCP_AUTH_MODE` | Auth mode for http transport: `none` (default), `apikey`, or `jwt` |
| `--auth-api-key` | `CASPER_MCP_AUTH_API_KEY` | Shared secret for `apikey` auth mode |
| `--auth-jwt-authority` | `CASPER_MCP_AUTH_JWT_AUTHORITY` | JWT authority URL for `jwt` auth mode |
| `--auth-jwt-audience` | `CASPER_MCP_AUTH_JWT_AUDIENCE` | JWT audience for `jwt` auth mode |

## Breaking Changes (v3.0.0)

| What changed | Old (v2.x) | New (v3.0) |
|---|---|---|
| Remote MCP endpoint | `GET /sse` (SSE transport) | `POST /mcp` (Streamable HTTP, stateless) |
| Transport flag value | `--transport sse` | `--transport http` |
| CSPR.Cloud key in remote mode | Set once at startup via `--api-key` | Sent per-request via header `X-CSPR-Cloud-Api-Key` |
| Server protection | `--server-api-key` / `CASPER_MCP_SERVER_API_KEY` | `--auth-mode apikey` + `--auth-api-key` / `CASPER_MCP_AUTH_API_KEY` |
| WebSocket streaming tools | 10 `Watch*` tools | Removed — poll request/response tools instead (e.g. `GetDeploy`) |
| Tool count | 92 | 82 |

## Client Setup

The server works with **any MCP client** regardless of what language your project uses. Below are copy-paste configs for popular clients.

### Claude Desktop

Add to `claude_desktop_config.json`:

**macOS:** `~/Library/Application Support/Claude/claude_desktop_config.json`
**Windows:** `%APPDATA%\Claude\claude_desktop_config.json`

```json
{
  "mcpServers": {
    "casper": {
      "command": "casper-mcp",
      "args": ["--api-key", "YOUR_API_KEY"]
    }
  }
}
```

<details>
<summary>Alternative: using Docker</summary>

```json
{
  "mcpServers": {
    "casper": {
      "command": "docker",
      "args": ["run", "-i", "--rm", "ghcr.io/msanlisavas/casper-mcp:latest", "--api-key", "YOUR_API_KEY"]
    }
  }
}
```
</details>

<details>
<summary>Alternative: using environment variable</summary>

```json
{
  "mcpServers": {
    "casper": {
      "command": "casper-mcp",
      "env": {
        "CSPR_CLOUD_API_KEY": "YOUR_API_KEY"
      }
    }
  }
}
```
</details>

### Claude Code (CLI)

```bash
claude mcp add casper -- casper-mcp --api-key YOUR_API_KEY
```

Or add to your MCP settings JSON:

```json
{
  "mcpServers": {
    "casper": {
      "command": "casper-mcp",
      "args": ["--api-key", "YOUR_API_KEY"]
    }
  }
}
```

### Cursor

Add to `.cursor/mcp.json` in your project root:

```json
{
  "mcpServers": {
    "casper": {
      "command": "casper-mcp",
      "args": ["--api-key", "YOUR_API_KEY"]
    }
  }
}
```

### VS Code (GitHub Copilot)

Add to `.vscode/mcp.json` in your project root:

```json
{
  "servers": {
    "casper": {
      "type": "stdio",
      "command": "casper-mcp",
      "args": ["--api-key", "YOUR_API_KEY"]
    }
  }
}
```

### Windsurf

Add to `~/.codeium/windsurf/mcp_config.json`:

```json
{
  "mcpServers": {
    "casper": {
      "command": "casper-mcp",
      "args": ["--api-key", "YOUR_API_KEY"]
    }
  }
}
```

### Continue.dev

Add to `~/.continue/config.json`:

```json
{
  "experimental": {
    "modelContextProtocolServers": [
      {
        "transport": {
          "type": "stdio",
          "command": "casper-mcp",
          "args": ["--api-key", "YOUR_API_KEY"]
        }
      }
    ]
  }
}
```

### Any MCP Client (generic)

The server supports **stdio** and **Streamable HTTP** transports. For stdio, start the process and communicate over stdin/stdout using JSON-RPC. For http, connect over HTTP:

```bash
# stdio mode (local, default)
casper-mcp --api-key YOUR_API_KEY

# Or with environment variable
CSPR_CLOUD_API_KEY=YOUR_API_KEY casper-mcp

# Or with Docker
docker run -i --rm casper-mcp --api-key YOUR_API_KEY

# HTTP mode (remote/multi-tenant, stateless Streamable HTTP at /mcp)
casper-mcp --transport http --port 3001
# Clients must send: X-CSPR-Cloud-Api-Key: <key> in each request
```

## Transport Modes

The server supports two transport modes. Choose based on your use case:

| Scenario | Transport | Command |
|---|---|---|
| Local AI tool (Claude Desktop, Cursor, VS Code) | **stdio** (default) | `casper-mcp --api-key KEY` |
| Building your own AI app / multi-agent backend | **http** | `casper-mcp --transport http` |
| Multi-tenant / multi-user access with auth | **http + auth** | Add `--auth-mode apikey --auth-api-key SECRET` |
| Production cloud deployment | **http + Docker** | `docker compose up -d` |

### Understanding the Two API Keys

In **stdio mode** there is a single API key that identifies the local user to CSPR.Cloud:

| Key | Purpose | Required? |
|---|---|---|
| `--api-key` / `CSPR_CLOUD_API_KEY` | Authenticates with **CSPR.Cloud** to fetch blockchain data | Required in stdio mode |

In **http mode** the CSPR.Cloud key is supplied per request (no startup key required):

| Key / Header | Purpose | Required? |
|---|---|---|
| `X-CSPR-Cloud-Api-Key` request header | Per-request CSPR.Cloud key — identifies the calling agent | Required (missing → HTTP 401) |
| `X-Casper-Network` request header | Per-request network override (`mainnet`/`testnet`) | Optional (defaults to `--network`) |
| `--auth-api-key` / `CASPER_MCP_AUTH_API_KEY` | Protects **your MCP server** in `apikey` auth mode | Optional (set when `--auth-mode apikey`) |

### Authentication Modes (http transport)

Configure with `--auth-mode` / `CASPER_MCP_AUTH_MODE`:

| Mode | Description | Configuration |
|---|---|---|
| `none` (default) | No built-in auth — trust a fronting WAF/reverse proxy to handle access control | — |
| `apikey` | Shared secret; clients send `Authorization: Bearer <secret>` or `X-API-Key: <secret>` | `--auth-api-key` / `CASPER_MCP_AUTH_API_KEY` |
| `jwt` | OAuth 2.1 resource server; validates `Authorization: Bearer <JWT>` from your IdP; serves `/.well-known/oauth-protected-resource` metadata | `--auth-jwt-authority`, `--auth-jwt-audience` |

The server validates tokens only — it does not issue them.

## Remote Deployment (v3.0.0)

From v3.0.0, casper-mcp is designed for **stateless multi-tenant remote deployment**:

- **Endpoint:** `POST /mcp` (Streamable HTTP, stateless — no persistent SSE sessions).
- **Per-agent CSPR.Cloud key:** each HTTP request must carry `X-CSPR-Cloud-Api-Key: <key>`. Different agents can use different keys in the same server instance.
- **Per-request network:** optionally override the network via `X-Casper-Network: mainnet|testnet`.
- **Pluggable auth:** choose `none` (WAF-trust), `apikey` (shared secret), or `jwt` (OAuth 2.1) at startup.
- **Designed to sit behind a WAF/reverse proxy** for TLS termination, rate limiting, and IP filtering.

### Built for remote AI agents behind a WAF (at scale)

One shared instance can serve thousands of agents as a remote MCP endpoint. How each common requirement is met:

| Requirement | How casper-mcp supports it |
|---|---|
| Run one shared instance in your infrastructure | A single **stateless** process — scale it horizontally behind your load balancer with no sticky sessions required |
| Expose it to remote agents over HTTP | **Streamable HTTP** at `POST /mcp` — the transport the MCP C# SDK recommends for remote deployments (replaces the old `/sse`) |
| Protect it with your WAF/auth layer | `--auth-mode none` trusts your fronting WAF; or enable `apikey` / `jwt` (OAuth 2.1) at the server edge |
| Avoid long-lived/stateful session issues behind a WAF/proxy | **Stateless mode** — every tool call is an independent request → response (RPC-like). There is no persistent SSE session to pin to one backend |
| Let each agent pass its own CSPR.Cloud key | Per-request `X-CSPR-Cloud-Api-Key` header. A fresh CSPR.Cloud client is built per request over a pooled connection, so credentials never mix between tenants |
| No request that hangs for ~120s and trips proxy timeouts | The WebSocket `Watch*` tools (which held a request open up to 120s) were **removed**. Every remaining tool is request/response and returns within normal latency — safe under typical WAF/proxy idle timeouts |

Per-tenant isolation and connection reuse are handled by `IHttpClientFactory` (pooled sockets) plus a fresh per-request client, so a single instance handles many distinct agent keys concurrently without leaking credentials across tenants or exhausting sockets.

### Run locally in http mode

```bash
dotnet run --project src/CasperMcp -- --transport http --port 3001
```

Output:
```
Casper MCP (http) on http://0.0.0.0:3001/mcp | auth=None | default-network=mainnet
```

Endpoints:
- `POST /mcp` — Streamable HTTP transport for MCP clients (requires `X-CSPR-Cloud-Api-Key` header)
- `GET /health` — Liveness check (returns JSON status, always public)
- `GET /ready` — Readiness check (always public)

### Docker (http mode)

```bash
docker pull ghcr.io/msanlisavas/casper-mcp:latest
docker run -p 3001:3001 ghcr.io/msanlisavas/casper-mcp:latest \
  --transport http --port 3001
```

Each MCP client request must include `X-CSPR-Cloud-Api-Key: YOUR_API_KEY`.

### Docker Compose

```bash
# Optional: set auth mode
echo "CASPER_MCP_AUTH_MODE=none" > .env

# Optional: protect the endpoint with a shared API key
# echo "CASPER_MCP_AUTH_MODE=apikey" > .env
# echo "CASPER_MCP_AUTH_API_KEY=your-secret-key" >> .env

# Start the server
docker compose up -d

# Verify
curl http://localhost:3001/health
curl http://localhost:3001/ready
```

### Authentication Examples

**apikey mode:**
```bash
# Start with apikey auth
casper-mcp --transport http --auth-mode apikey --auth-api-key my-secret

# Client sends both headers
curl -X POST http://localhost:3001/mcp \
  -H "Authorization: Bearer my-secret" \
  -H "X-CSPR-Cloud-Api-Key: YOUR_CSPR_CLOUD_KEY" \
  -H "Content-Type: application/json" \
  -d '{"jsonrpc":"2.0","id":1,"method":"tools/list"}'
```

**jwt mode:**
```bash
# Start with JWT auth
casper-mcp --transport http \
  --auth-mode jwt \
  --auth-jwt-authority https://your-idp.example.com \
  --auth-jwt-audience casper-mcp

# Client sends JWT + CSPR key
curl -X POST http://localhost:3001/mcp \
  -H "Authorization: Bearer <JWT>" \
  -H "X-CSPR-Cloud-Api-Key: YOUR_CSPR_CLOUD_KEY" \
  -H "Content-Type: application/json" \
  -d '{"jsonrpc":"2.0","id":1,"method":"tools/list"}'
```

### HTTP Client Configs

Clients that support Streamable HTTP transport can connect to a running server:

<details>
<summary>Claude Desktop (HTTP remote)</summary>

```json
{
  "mcpServers": {
    "casper": {
      "url": "http://localhost:3001/mcp",
      "headers": {
        "X-CSPR-Cloud-Api-Key": "YOUR_CSPR_CLOUD_KEY"
      }
    }
  }
}
```
</details>

<details>
<summary>VS Code / GitHub Copilot (HTTP remote)</summary>

```json
{
  "servers": {
    "casper": {
      "type": "http",
      "url": "http://localhost:3001/mcp",
      "headers": {
        "X-CSPR-Cloud-Api-Key": "YOUR_CSPR_CLOUD_KEY"
      }
    }
  }
}
```
</details>

<details>
<summary>Cursor (HTTP remote)</summary>

```json
{
  "mcpServers": {
    "casper": {
      "url": "http://localhost:3001/mcp",
      "headers": {
        "X-CSPR-Cloud-Api-Key": "YOUR_CSPR_CLOUD_KEY"
      }
    }
  }
}
```
</details>

### Connecting from Python

```bash
pip install mcp
```

```python
import asyncio
from mcp import ClientSession
from mcp.client.streamable_http import streamablehttp_client

async def main():
    headers = {
        "X-CSPR-Cloud-Api-Key": "YOUR_CSPR_CLOUD_KEY",
        # Optional: "X-Casper-Network": "testnet"
    }
    async with streamablehttp_client("http://localhost:3001/mcp", headers=headers) as (read, write, _):
        async with ClientSession(read, write) as session:
            await session.initialize()
            tools = await session.list_tools()
            result = await session.call_tool("GetNetworkStatus", {})
            print(result)

asyncio.run(main())
```

### Connecting from Node.js

```bash
npm install @modelcontextprotocol/sdk
```

```javascript
import { Client } from "@modelcontextprotocol/sdk/client/index.js";
import { StreamableHTTPClientTransport } from "@modelcontextprotocol/sdk/client/streamableHttp.js";

const transport = new StreamableHTTPClientTransport(
  new URL("http://localhost:3001/mcp"),
  {
    requestInit: {
      headers: {
        "X-CSPR-Cloud-Api-Key": "YOUR_CSPR_CLOUD_KEY",
        // Optional: "X-Casper-Network": "testnet"
      }
    }
  }
);
const client = new Client({ name: "my-app", version: "1.0.0" });
await client.connect(transport);

const tools = await client.listTools();
const result = await client.callTool({ name: "GetNetworkStatus", arguments: {} });
console.log(result);
```

### Connecting from OpenAI Agents SDK

```bash
pip install openai-agents mcp
```

```python
from agents import Agent
from agents.mcp import MCPServerStreamableHttp

casper = MCPServerStreamableHttp(
    url="http://localhost:3001/mcp",
    headers={
        "X-CSPR-Cloud-Api-Key": "YOUR_CSPR_CLOUD_KEY",
        # Optional server auth if auth-mode is apikey:
        # "X-API-Key": "your-server-secret"
    }
)

agent = Agent(
    name="Blockchain Assistant",
    instructions="You help users query the Casper blockchain.",
    mcp_servers=[casper]
)
```

## Available Tools (82 tools)

### Account Tools
| Tool | Description |
|---|---|
| `GetAccountInfo` | Get detailed account info including balance, staking, and delegation status |
| `GetAccountBalance` | Get CSPR balance breakdown (liquid, staked, delegated) |
| `GetAccountDeploys` | Get deploy (transaction) history for an account |
| `GetAccountDelegations` | Get delegation info — which validators the account delegates to |
| `GetAccounts` | Get a paginated list of all accounts |
| `GetAccountContractPackages` | Get contract packages deployed by an account |
| `GetAccountDelegationRewards` | Get delegation rewards for an account |
| `GetTotalAccountDelegationRewards` | Get total delegation rewards for an account |
| `GetTotalValidatorDelegatorRewards` | Get total delegation rewards paid out by a validator |
| `GetPurseDelegations` | Get delegations for a specific purse |
| `GetPurseDelegationRewards` | Get delegation rewards for a specific purse |
| `GetTotalPurseDelegationRewards` | Get total delegation rewards for a specific purse |
| `GetAccountUndelegations` | Get pending undelegations (released 7 eras after creation) |

### Block Tools
| Tool | Description |
|---|---|
| `GetBlock` | Get block details by hash |
| `GetLatestBlocks` | Get the most recent blocks |
| `GetValidatorBlocks` | Get blocks proposed by a specific validator |

### Deploy Tools
| Tool | Description |
|---|---|
| `GetDeploy` | Get deploy (transaction) details by hash |
| `GetDeploys` | Get a paginated list of all deploys |
| `GetBlockDeploys` | Get deploys included in a specific block |
| `GetDeployExecutionTypes` | Get the list of deploy execution types |

### Validator Tools
| Tool | Description |
|---|---|
| `GetValidators` | List validators with stake, fee, and performance data |
| `GetValidatorInfo` | Get detailed info about a specific validator |
| `GetValidatorDelegations` | Get delegations to a specific validator |
| `GetValidatorRewards` | Get rewards earned by a validator |
| `GetValidatorTotalRewards` | Get total rewards earned by a validator |
| `GetHistoricalValidatorPerformance` | Get historical performance scores for a validator |
| `GetHistoricalValidatorAveragePerformance` | Get historical average performance for a validator |
| `GetHistoricalValidatorsAveragePerformance` | Get historical average performance for all validators |
| `GetValidatorEraRewards` | Get validator rewards aggregated by era |

### Contract Tools
| Tool | Description |
|---|---|
| `GetContract` | Get smart contract info by hash |
| `GetContractEntryPoints` | Get callable entry points of a contract |
| `GetContracts` | Get a paginated list of all contracts |
| `GetContractTypes` | Get the list of contract types |
| `GetContractEntryPointCosts` | Get cost statistics for a contract entry point |
| `GetContractPackages` | Get a paginated list of contract packages |
| `GetContractsByContractPackage` | Get contracts belonging to a contract package |

### Token Tools (CEP-18)
| Tool | Description |
|---|---|
| `GetFtTokenInfo` | Get fungible token contract info |
| `GetFtTokenHolders` | Get token holder list with balances |
| `GetAccountFtBalances` | Get all fungible token balances for an account |
| `GetFungibleTokenActions` | Get fungible token actions (transfers, mints, burns) |
| `GetAccountFungibleTokenActions` | Get fungible token actions for an account |
| `GetContractPackageFungibleTokenActions` | Get fungible token actions for a contract package |
| `GetFtActionTypes` | Get the list of fungible token action types |

### FT Rate Tools
| Tool | Description |
|---|---|
| `GetFtRateLatest` | Get the latest fungible token rate |
| `GetFtRates` | Get historical fungible token rates |
| `GetFtDailyRateLatest` | Get the latest daily aggregated FT rate |
| `GetFtDailyRates` | Get historical daily aggregated FT rates |
| `GetFtDexRateLatest` | Get the latest token-to-token DEX rate |
| `GetFtDexRates` | Get historical token-to-token DEX rates |
| `GetFtDailyDexRateLatest` | Get the latest daily token-to-token DEX rate |
| `GetFtDailyDexRates` | Get historical daily token-to-token DEX rates |

### NFT Tools (CEP-47 / CEP-78)
| Tool | Description |
|---|---|
| `GetNetworkNfts` | Paginated network-wide list of NFTs (filters: package, owner, block range) |
| `GetNftCollection` | Get NFTs in a collection |
| `GetAccountNfts` | Get NFTs owned by an account |
| `GetNft` | Get a specific NFT by contract package hash and token ID |
| `GetNftStandards` | Get the list of supported NFT standards |
| `GetNftMetadataStatuses` | Get the list of offchain NFT metadata statuses |
| `GetNftActionsForToken` | Get actions for a specific NFT token |
| `GetAccountNftActions` | Get NFT actions for an account |
| `GetContractPackageNftActions` | Get NFT actions for a contract package |
| `GetNftActionTypes` | Get the list of NFT action types |
| `GetContractPackageNftOwnership` | Get NFT ownership distribution for a contract package |
| `GetAccountNftOwnership` | Get NFT ownership summary for an account |

### Transfer Tools
| Tool | Description |
|---|---|
| `GetTransfers` | Get native CSPR transfer history for an account |
| `GetDeployTransfers` | Get native CSPR transfers for a specific deploy |
| `GetPurseTransfers` | Get native CSPR transfers for a specific purse |

### Network Tools
| Tool | Description |
|---|---|
| `GetNetworkStatus` | Get network status — active validators, era, total stake |
| `GetEraInfo` | Get current era information |
| `GetSupplyInfo` | Get CSPR total and circulating supply |

### Bidder Tools
| Tool | Description |
|---|---|
| `GetBidder` | Get information about a specific bidder |
| `GetBidders` | Get a list of bidders |

### Currency Tools
| Tool | Description |
|---|---|
| `GetCurrentCurrencyRate` | Get the current CSPR exchange rate for a currency |
| `GetHistoricalCurrencyRates` | Get historical CSPR exchange rates |
| `GetCurrencies` | Get the list of supported currencies |

### Centralized Account Tools
| Tool | Description |
|---|---|
| `GetCentralizedAccountInfo` | Get centralized account information by account hash |
| `GetCentralizedAccounts` | Get a list of centralized account information entries |

### DEX Tools
| Tool | Description |
|---|---|
| `GetDexes` | Get a list of all decentralized exchanges |
| `GetSwaps` | Get a paginated list of token swaps |

### CSPR.name Tools
| Tool | Description |
|---|---|
| `ResolveCsprName` | Resolve a CSPR.name to an account hash |

### Awaiting Deploy Tools
| Tool | Description |
|---|---|
| `GetAwaitingDeploy` | Get an awaiting deploy by deploy hash |
| `CreateAwaitingDeploy` | Create an awaiting deploy for multi-signature collection |
| `AddAwaitingDeployApproval` | Add an approval (signature) to an awaiting deploy |

> **Note:** The 10 `Watch*` WebSocket streaming tools (`WatchBlocks`, `WatchDeploys`, etc.) were removed in v3.0.0. Use polling with the request/response tools above instead (e.g. call `GetDeploy` repeatedly to wait for transaction confirmation).

## Development

### Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0)
- A [CSPR.Cloud](https://cspr.cloud) API key

### Build

```bash
dotnet build
```

### Test

```bash
# Run all tests (unit + integration against testnet)
dotnet test

# Run with a specific testnet API key
CSPR_CLOUD_TESTNET_API_KEY=YOUR_KEY dotnet test
```

### Pack as global tool

```bash
dotnet pack src/CasperMcp -c Release
dotnet tool install -g --add-source src/CasperMcp/nupkg CasperMcp
```

### CI/CD

The project uses GitHub Actions for continuous integration and Docker image publishing.

| Workflow | Trigger | What it does |
|---|---|---|
| **Build and Test** | Push/PR to `main` | Restore, build, run all tests |
| **Docker** | Push to `main` or version tag (`v*`) | Run tests, then build & push Docker image to `ghcr.io` |

Docker images are published to `ghcr.io/msanlisavas/casper-mcp` with these tags:

| Event | Tags |
|---|---|
| Push to `main` | `:latest`, `:sha-abc1234` |
| Tag `v3.0.0` | `:3.0.0`, `:3.0`, `:latest`, `:sha-abc1234` |

To release a new version:
```bash
git tag v3.0.0
git push origin v3.0.0
```

### Debug with MCP Inspector

```bash
npx @modelcontextprotocol/inspector casper-mcp --api-key YOUR_API_KEY
```

### Project Structure

```
casper-mcp/
├── src/CasperMcp/
│   ├── Configuration/     # Options and config
│   ├── Helpers/           # Formatting (motes → CSPR, etc.)
│   ├── Middleware/        # HTTP middleware (auth)
│   ├── Tools/             # MCP tool implementations (82 tools)
│   └── Program.cs         # Entry point, DI setup, dual transport
├── tests/CasperMcp.Tests/ # Unit + integration tests
├── mcp.json               # MCP server manifest
├── Dockerfile
└── casper-mcp.slnx
```

## How It Works

```
┌──────────────┐  stdio / HTTP  ┌──────────────┐    HTTPS     ┌──────────────┐
│  MCP Client  │◄──────────────►│  casper-mcp   │◄────────────►│  CSPR.Cloud  │
│  (any lang)  │   JSON-RPC     │  (.NET 10)    │   REST API   │    API       │
└──────────────┘                └──────────────┘               └──────────────┘
  Claude, GPT,                    This server                    Casper Network
  Cursor, etc.                  (stdio or HTTP)                  blockchain data
```

The MCP server is a **bridge** between AI tools and the Casper blockchain. It supports two transport modes:
- **stdio** — spawn the process locally, communicate over stdin/stdout (default). Pass `--api-key` at startup.
- **http** — run as a stateless Streamable HTTP server; each request carries its own `X-CSPR-Cloud-Api-Key`. Designed for multi-tenant remote deployments behind a WAF.

## License

MIT — see [LICENSE](LICENSE).
