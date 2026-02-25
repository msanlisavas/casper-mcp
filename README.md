# casper-mcp

MCP (Model Context Protocol) server for the **Casper Network** blockchain. Gives any AI assistant or MCP-compatible tool access to on-chain data — accounts, blocks, deploys, validators, smart contracts, tokens, NFTs, transfers, and network status.

**Language-agnostic.** Supports both **stdio** (local) and **SSE/HTTP** (remote) transports. Use it from any project — Python, Node.js, Rust, Go, or any MCP client.

Built with [CSPR.Cloud.Net](https://www.nuget.org/packages/CSPR.Cloud.Net) and the [ModelContextProtocol](https://www.nuget.org/packages/ModelContextProtocol) SDK.

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
| `--api-key` | `CSPR_CLOUD_API_KEY` | CSPR.Cloud API key (**required**). Get one at [cspr.cloud](https://cspr.cloud) |
| `--network` | — | `mainnet` (default) or `testnet` |
| `--transport` | — | `stdio` (default) or `sse` (HTTP/SSE for remote access) |
| `--port` | — | HTTP port for SSE mode (default: `3001`) |
| `--server-api-key` | `CASPER_MCP_SERVER_API_KEY` | Optional API key to protect the SSE endpoint |

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

The server supports **stdio** and **SSE (HTTP)** transports. For stdio, start the process and communicate over stdin/stdout using JSON-RPC. For SSE, connect over HTTP:

```bash
# Start the server
casper-mcp --api-key YOUR_API_KEY

# Or with environment variable
CSPR_CLOUD_API_KEY=YOUR_API_KEY casper-mcp

# Or with Docker
docker run -i --rm casper-mcp --api-key YOUR_API_KEY

# SSE mode (HTTP, for remote access)
casper-mcp --api-key YOUR_API_KEY --transport sse --port 3001
```

## Transport Modes

The server supports two transport modes. Choose based on your use case:

| Scenario | Transport | Command |
|---|---|---|
| Local AI tool (Claude Desktop, Cursor, VS Code) | **stdio** (default) | `casper-mcp --api-key KEY` |
| Building your own AI app | **SSE** | `casper-mcp --api-key KEY --transport sse` |
| Multi-client / multi-user access | **SSE + auth** | Add `--server-api-key SECRET` |
| Production cloud deployment | **SSE + Docker** | `docker compose up -d` |

### Understanding the Two API Keys

| Key | Purpose | Required? |
|---|---|---|
| `--api-key` / `CSPR_CLOUD_API_KEY` | Authenticates with **CSPR.Cloud** to fetch blockchain data | Always required |
| `--server-api-key` / `CASPER_MCP_SERVER_API_KEY` | Protects **your MCP server** from unauthorized clients | Optional (recommended in SSE mode) |

## Production Deployment (SSE Mode)

For production use — such as building an AI assistant backend — run the server in SSE mode so any MCP client can connect over HTTP.

### Run locally in SSE mode

```bash
dotnet run --project src/CasperMcp -- --api-key YOUR_API_KEY --transport sse --port 3001
```

Output:
```
Casper MCP server starting on http://0.0.0.0:3001
  Transport: SSE
  Network:   mainnet
  Auth:      disabled
  Health:    http://localhost:3001/health
  MCP:       http://localhost:3001/sse
```

Endpoints:
- `GET /sse` — SSE transport for MCP clients
- `GET /health` — Health check (returns JSON status, always public)

### Docker (SSE mode)

```bash
docker pull ghcr.io/msanlisavas/casper-mcp:latest
docker run -p 3001:3001 ghcr.io/msanlisavas/casper-mcp:latest \
  --api-key YOUR_API_KEY --transport sse --port 3001
```

### Docker Compose

```bash
# Create a .env file
echo "CSPR_CLOUD_API_KEY=your-api-key-here" > .env

# Optional: protect the endpoint with an API key
echo "CASPER_MCP_SERVER_API_KEY=your-secret-key" >> .env

# Start the server
docker compose up -d

# Verify
curl http://localhost:3001/health
```

### Authentication

When `--server-api-key` (or `CASPER_MCP_SERVER_API_KEY`) is set, all requests except `/health` require authentication. Provide the key via:
- **Header:** `X-API-Key: your-secret-key`
- **Query parameter:** `?api_key=your-secret-key`

```bash
# Without auth → 401
curl http://localhost:3001/sse

# With auth → SSE stream
curl -H "X-API-Key: your-secret-key" http://localhost:3001/sse
```

### SSE Client Configs

Clients that support SSE/HTTP transport can connect to a running server instead of spawning a local process.

<details>
<summary>Claude Desktop (SSE)</summary>

```json
{
  "mcpServers": {
    "casper": {
      "url": "http://localhost:3001/sse"
    }
  }
}
```
</details>

<details>
<summary>VS Code / GitHub Copilot (SSE)</summary>

```json
{
  "servers": {
    "casper": {
      "type": "sse",
      "url": "http://localhost:3001/sse"
    }
  }
}
```
</details>

<details>
<summary>Cursor (SSE)</summary>

```json
{
  "mcpServers": {
    "casper": {
      "url": "http://localhost:3001/sse"
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
from mcp.client.sse import sse_client

async def main():
    # Without auth
    async with sse_client("http://localhost:3001/sse") as (read, write):
        async with ClientSession(read, write) as session:
            await session.initialize()
            tools = await session.list_tools()
            result = await session.call_tool("GetNetworkStatus", {})
            print(result)

    # With auth
    headers = {"X-API-Key": "your-secret-key"}
    async with sse_client("http://localhost:3001/sse", headers=headers) as (read, write):
        async with ClientSession(read, write) as session:
            await session.initialize()
            result = await session.call_tool("GetAccountBalance", {
                "accountHash": "account-hash-xxx..."
            })
            print(result)

asyncio.run(main())
```

### Connecting from Node.js

```bash
npm install @modelcontextprotocol/sdk
```

```javascript
import { Client } from "@modelcontextprotocol/sdk/client/index.js";
import { SSEClientTransport } from "@modelcontextprotocol/sdk/client/sse.js";

// Without auth
const transport = new SSEClientTransport(new URL("http://localhost:3001/sse"));
const client = new Client({ name: "my-app", version: "1.0.0" });
await client.connect(transport);

const tools = await client.listTools();
const result = await client.callTool({ name: "GetNetworkStatus", arguments: {} });
console.log(result);

// With auth
const authTransport = new SSEClientTransport(new URL("http://localhost:3001/sse"), {
  requestInit: { headers: { "X-API-Key": "your-secret-key" } }
});
```

### Connecting from OpenAI Agents SDK

```bash
pip install openai-agents mcp
```

```python
from agents import Agent
from agents.mcp import MCPServerSse

casper = MCPServerSse(
    url="http://localhost:3001/sse",
    headers={"X-API-Key": "your-secret-key"}  # omit if no auth
)

agent = Agent(
    name="Blockchain Assistant",
    instructions="You help users query the Casper blockchain.",
    mcp_servers=[casper]
)
```

## Available Tools

### Account Tools
| Tool | Description |
|---|---|
| `GetAccountInfo` | Get detailed account info including balance, staking, and delegation status |
| `GetAccountBalance` | Get CSPR balance breakdown (liquid, staked, delegated) |
| `GetAccountDeploys` | Get deploy (transaction) history for an account |
| `GetAccountDelegations` | Get delegation info — which validators the account delegates to |

### Block Tools
| Tool | Description |
|---|---|
| `GetBlock` | Get block details by hash |
| `GetLatestBlocks` | Get the most recent blocks |

### Deploy Tools
| Tool | Description |
|---|---|
| `GetDeploy` | Get deploy (transaction) details by hash |

### Validator Tools
| Tool | Description |
|---|---|
| `GetValidators` | List validators with stake, fee, and performance data |
| `GetValidatorInfo` | Get detailed info about a specific validator |

### Contract Tools
| Tool | Description |
|---|---|
| `GetContract` | Get smart contract info by hash |
| `GetContractEntryPoints` | Get callable entry points of a contract |

### Token Tools (CEP-18)
| Tool | Description |
|---|---|
| `GetFtTokenInfo` | Get fungible token contract info |
| `GetFtTokenHolders` | Get token holder list with balances |
| `GetAccountFtBalances` | Get all fungible token balances for an account |

### NFT Tools (CEP-47 / CEP-78)
| Tool | Description |
|---|---|
| `GetNftCollection` | Get NFTs in a collection |
| `GetAccountNfts` | Get NFTs owned by an account |

### Transfer Tools
| Tool | Description |
|---|---|
| `GetTransfers` | Get native CSPR transfer history for an account |

### Network Tools
| Tool | Description |
|---|---|
| `GetNetworkStatus` | Get network status — active validators, era, total stake |
| `GetEraInfo` | Get current era information |
| `GetSupplyInfo` | Get CSPR total and circulating supply |

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
| Tag `v1.2.0` | `:1.2.0`, `:1.2`, `:latest`, `:sha-abc1234` |

To release a new version:
```bash
git tag v1.2.0
git push origin v1.2.0
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
│   ├── Middleware/        # HTTP middleware (API key auth)
│   ├── Tools/             # MCP tool implementations (9 files, 20 tools)
│   └── Program.cs         # Entry point, DI setup, dual transport
├── tests/CasperMcp.Tests/ # Unit + integration tests
├── mcp.json               # MCP server manifest
├── Dockerfile
└── casper-mcp.sln
```

## How It Works

```
┌──────────────┐  stdio / SSE   ┌──────────────┐    HTTPS     ┌──────────────┐
│  MCP Client  │◄──────────────►│  casper-mcp   │◄────────────►│  CSPR.Cloud  │
│  (any lang)  │   JSON-RPC     │  (.NET 10)    │   REST API   │    API       │
└──────────────┘                └──────────────┘               └──────────────┘
  Claude, GPT,                    This server                    Casper Network
  Cursor, etc.                  (stdio or HTTP)                  blockchain data
```

The MCP server is a **bridge** between AI tools and the Casper blockchain. It supports two transport modes:
- **stdio** — spawn the process locally, communicate over stdin/stdout (default)
- **SSE** — run as an HTTP server, connect from any network client

## License

MIT — see [LICENSE](LICENSE).
