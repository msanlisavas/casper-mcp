# casper-mcp write tools

Local stdio signer that lets an AI agent build, preview, and submit Casper Network transactions — with a fail-closed policy engine that enforces limits in code, not by trusting the model.

---

## Table of Contents

1. [Overview](#1-overview)
2. [How safety is enforced](#2-how-safety-is-enforced)
3. [Prerequisites](#3-prerequisites)
4. [Generating a signing key](#4-generating-a-signing-key)
5. [Configuring the policy](#5-configuring-the-policy)
6. [Running the signer — testnet quick start](#6-running-the-signer--testnet-quick-start)
7. [Build → preview → sign](#7-build--preview--sign)
8. [Mainnet checklist](#8-mainnet-checklist)
9. [Docker isolation](#9-docker-isolation)
10. [Audit log](#10-audit-log)
11. [Spend ledger and daily cap](#11-spend-ledger-and-daily-cap)
12. [Policy reference](#12-policy-reference)
13. [Troubleshooting](#13-troubleshooting)

---

## 1. Overview

The server is **read-only by default**. Writes are an opt-in local stdio signer enabled by `--enable-writes`. The remote `POST /mcp` HTTP surface never exposes write tools — it is read-only by construction and by regression test.

The private key is loaded locally by the stdio process and never leaves the machine.

**Five write tools:**

| Tool | What it does |
|---|---|
| `BuildTransferTransaction` | Returns an unsigned transfer JSON and a human-readable preview. No signing. |
| `BuildDelegateTransaction` | Returns an unsigned delegation (stake) JSON and preview. No signing. |
| `BuildUndelegateTransaction` | Returns an unsigned undelegation (unstake) JSON and preview. No signing. |
| `BuildRedelegateTransaction` | Returns an unsigned redelegation (move stake) JSON and preview. No signing. |
| `SignAndSubmitTransaction` | Re-validates the unsigned JSON against policy on the decoded bytes, signs locally, submits. Returns tx hash + status or a refusal. |

**The shape is: build → preview → sign.** The build tools and `SignAndSubmitTransaction` are always two separate tool calls. The agent sees the preview before deciding to sign.

---

## 2. How safety is enforced

### Key isolation

The private key is loaded only by the local stdio signer (`--enable-writes --key-path`). It is never passed server-side, per-request, in an environment variable, in model context, or returned to the agent. Signatures are never returned — only a transaction hash and status.

### HTTP is read-only by construction

`--enable-writes` combined with the http transport is a **fatal startup error**. Write tools are not annotated `[McpServerToolType]`, so the HTTP assembly scan never registers them. This is enforced by a regression test (`WriteToolsHttpExclusionTests`) — keep it green.

### Validate-the-bytes

The signer decodes the real transaction bytes via `TransactionIntrospector` and checks those fields directly — it never trusts a description or the preview text. The transaction sender must equal the signer's own public key.

### Fail-closed policy

The policy is loaded once at startup from a file, is immutable at runtime, and is human-owned — no tool can edit or reload it. A missing or unparseable policy file collapses to the **strict default**: testnet-only, empty allowlists (block everything), caps 100/500/100 CSPR. It never fails open.

### Separate caps and allowlists

Transfers and staking have independent limits. A transfer is irreversible outflow to a third party, so it carries a tight per-tx cap and a daily rolling cap tracked in a local spend ledger. Staking keeps the delegated funds in your own account (returned via undelegate), so it has its own independent per-tx cap. A tight transfer cap never has to be loosened just to stake a larger amount. Undelegate is uncapped — it only recovers your own funds.

### Audit log

Every build, sign, and refusal is appended to `~/.casper-mcp/audit.log` with a timestamp, a decoded transaction summary, the policy decision, and the key fingerprint. The secret key itself is never written.

### Anti-SSRF

The submission node is pinned per network at startup. The agent cannot redirect it to an attacker-controlled endpoint.

### The honest threat model

The in-process guards stop a prompt-injected agent acting *through the MCP*. They **cannot** stop an agent that has general shell access *as the OS user that owns the key* — such an agent could edit the policy file, restart the signer with looser rules, or use the key directly. That is an OS-authorization problem. Defend with **isolation** (Docker or a separate OS user, `chmod 600` on the key) and, above all, a **low-balance hot account** — the one control that survives every software bypass.

---

## 3. Prerequisites

- **casper-mcp** installed as a .NET global tool (`dotnet tool install -g CasperMcp`) or available as a Docker image (`ghcr.io/msanlisavas/casper-mcp`).
- A **CSPR.Cloud API key** — required in stdio mode to read blockchain data. Get one at [cspr.cloud](https://cspr.cloud).
- A **signing key** — see §4.

---

## 4. Generating a signing key

Use the [Casper client](https://github.com/casper-ecosystem/casper-client-rs) to generate a key pair:

```bash
# ed25519 (default)
casper-client keygen <output-dir>

# secp256k1
casper-client keygen --algorithm secp256k1 <output-dir>
```

Both commands produce `secret_key.pem` in the output directory. `KeyPair.FromPem` auto-detects the algorithm, so either PEM works with `--key-path`. There is no `--key-algo` flag in casper-mcp itself. Run `casper-client keygen --help` to confirm the exact option for your client version.

Lock down the file immediately:

```bash
chmod 600 secret_key.pem
```

**Always pass the key as a file path (`--key-path`), never as an environment variable.** Secrets in environment variables leak into process listings and client config files.

---

## 5. Configuring the policy

The policy controls what the signer will sign. It is loaded from the path given by `--policy-path` (default `~/.casper-mcp/policy.json`). If you have a source checkout, copy the sample from the repo root:

```bash
# Run from the repo root
cp policy.sample.json ~/.casper-mcp/policy.json
```

If you installed via `dotnet tool install -g CasperMcp` and don't have the source, create the file directly from the schema block below.

### Schema

```json
{
  "mainnet_enabled": false,
  "transfer": { "per_tx_cspr": 100, "per_day_cspr": 500 },
  "stake": { "per_tx_cspr": 100 },
  "allowlist": {
    "recipients": ["01<allowed-recipient-public-key-hex>"],
    "validators": ["01<allowed-validator-public-key-hex>"]
  }
}
```

### Fields

| Field | Type | Default | Meaning |
|---|---|---|---|
| `mainnet_enabled` | bool | `false` | Gate for mainnet signing. Must be `true` to allow any mainnet transaction. |
| `transfer.per_tx_cspr` | number | `100` | Maximum CSPR per transfer transaction. |
| `transfer.per_day_cspr` | number | `500` | Rolling daily transfer cap tracked in the spend ledger. |
| `stake.per_tx_cspr` | number | `100` | Maximum CSPR per delegate or redelegate transaction. Independent of transfer caps. |
| `allowlist.recipients` | array of strings | `[]` | Public-key hex strings permitted as transfer recipients. **Empty = nothing moves.** |
| `allowlist.validators` | array of strings | `[]` | Public-key hex strings permitted as stake destinations. **Empty = no staking.** Undelegate is not gated by this list. |

### Precedence

Strict defaults are applied first, then the policy file overrides them, then environment variables override the file. When both a current name and its legacy alias are set — for example `CASPER_MCP_TRANSFER_PER_TX_CSPR` and the legacy `CASPER_MCP_PER_TX_CSPR` — the current name takes precedence.

### Environment variable and CLI flag reference

| Env var | CLI flag | Policy field | Default | Notes |
|---|---|---|---|---|
| `CASPER_MCP_ENABLE_WRITES` | `--enable-writes` | — | off | Enables the signer. **Never valid with http.** |
| `CASPER_MCP_KEY_PATH` | `--key-path` | — | (none) | PEM secret key path. Required with writes. |
| `CASPER_MCP_POLICY_PATH` | `--policy-path` | — | `~/.casper-mcp/policy.json` | Write-policy file. |
| `CASPER_MCP_NETWORK` | `--network` | — | testnet (in write mode) | `mainnet` or `testnet`. |
| `CASPER_MCP_NODE_RPC_URL` | `--node-rpc-url` | — | pinned per network | Operator startup override only; the agent cannot change it at runtime. |
| `CSPR_CLOUD_API_KEY` | `--api-key` | — | (none) | CSPR.Cloud key; required in stdio. |
| `CASPER_MCP_MAINNET_ENABLED` | — | `mainnet_enabled` | `false` | `true`/`True`/`1` to allow mainnet. |
| `CASPER_MCP_TRANSFER_PER_TX_CSPR` | — | `transfer.per_tx_cspr` | `100` | Legacy alias: `CASPER_MCP_PER_TX_CSPR`. |
| `CASPER_MCP_TRANSFER_PER_DAY_CSPR` | — | `transfer.per_day_cspr` | `500` | Legacy alias: `CASPER_MCP_PER_DAY_CSPR`. |
| `CASPER_MCP_STAKE_PER_TX_CSPR` | — | `stake.per_tx_cspr` | `100` | Independent of transfer caps. |
| `CASPER_MCP_ALLOW_RECIPIENTS` | — | `allowlist.recipients` | (empty) | CSV of recipient public-key hex. |
| `CASPER_MCP_ALLOW_VALIDATORS` | — | `allowlist.validators` | (empty) | CSV of validator public-key hex. |

---

## 6. Running the signer — testnet quick start

Testnet is the safe default. The signer refuses mainnet transactions unless `mainnet_enabled` is explicitly set to `true` in policy or via env var.

### Start the signer

```bash
# .NET global tool
casper-mcp --enable-writes --key-path ~/.casper/secret_key.pem --network testnet --api-key YOUR_API_KEY

# Self-contained native binary (macOS/Linux)
./CasperMcp --enable-writes --key-path ~/.casper/secret_key.pem --network testnet --api-key YOUR_API_KEY

# Self-contained native binary (Windows)
./CasperMcp.exe --enable-writes --key-path ~/.casper/secret_key.pem --network testnet --api-key YOUR_API_KEY
```

### Startup banner

On a successful start, the signer prints to stderr:

```
casper-mcp signer ENABLED (stdio) | network=testnet | key=<prefix> | writes=transfer,delegate,undelegate,redelegate
```

**Check the network and key prefix in the banner before letting an agent transact.** If the wrong network or key appears, stop the process and correct the flags.

### Two-server MCP client config

The recommended setup is two separate MCP servers: one remote read-only server for blockchain queries, and the local stdio signer for writes. This keeps the signing key off the network entirely.

```json
{
  "mcpServers": {
    "casper-read": {
      "url": "https://mcp.testnet.cspr.cloud/mcp",
      "headers": { "X-CSPR-Cloud-Api-Key": "YOUR_CSPR_CLOUD_KEY" }
    },
    "casper-sign": {
      "command": "casper-mcp",
      "args": [
        "--enable-writes",
        "--key-path", "/home/me/.casper/secret_key.pem",
        "--network", "testnet"
      ],
      "env": {
        "CSPR_CLOUD_API_KEY": "YOUR_API_KEY",
        "CASPER_MCP_TRANSFER_PER_TX_CSPR": "100",
        "CASPER_MCP_TRANSFER_PER_DAY_CSPR": "500",
        "CASPER_MCP_STAKE_PER_TX_CSPR": "100",
        "CASPER_MCP_ALLOW_RECIPIENTS": "01<allowed-recipient-pubkey>",
        "CASPER_MCP_ALLOW_VALIDATORS": "01<allowed-validator-pubkey>"
      }
    }
  }
}
```

The key is always a file path in `args`, never in `env`. Policy values are non-secret and may live in `env` or in `~/.casper-mcp/policy.json`.

---

## 7. Build → preview → sign

Write operations require exactly two tool calls: a build call that returns an unsigned transaction and a preview, followed by `SignAndSubmitTransaction` after the user has verified the preview.

### Tool signatures

**`BuildTransferTransaction(recipient, amountCspr)`**

Builds an unsigned native CSPR transfer from the signer's account. Returns unsigned transaction JSON and a preview.

- `recipient` — recipient public key (hex, e.g. `01...` or `02...`)
- `amountCspr` — amount to send, in CSPR (e.g. `12.5`)

**`BuildDelegateTransaction(validator, amountCspr)`**

Builds an unsigned delegation (stake CSPR to a validator). Returns unsigned JSON and a preview.

- `validator` — validator public key (hex)
- `amountCspr` — amount to delegate, in CSPR

**`BuildUndelegateTransaction(validator, amountCspr)`**

Builds an unsigned undelegation (unstake from a validator). Returns unsigned JSON and a preview.

- `validator` — validator public key (hex)
- `amountCspr` — amount to undelegate, in CSPR

**`BuildRedelegateTransaction(fromValidator, toValidator, amountCspr)`**

Builds an unsigned redelegation (move stake from one validator to another). Returns unsigned JSON and a preview.

- `fromValidator` — current validator public key (hex)
- `toValidator` — destination validator public key (hex)
- `amountCspr` — amount to redelegate, in CSPR

**`SignAndSubmitTransaction(unsignedTransactionJson)`**

Re-validates the unsigned transaction JSON against the local policy on the decoded bytes, then signs in-process and submits. Returns the transaction hash and submission status, or a human-readable refusal if policy blocks the transaction. The signature and private key are never returned.

- `unsignedTransactionJson` — the unsigned transaction JSON returned by a `Build*` tool

### What the preview shows

Each build tool returns a human-readable preview followed by the unsigned JSON. The preview includes:

- Action (transfer / delegate / undelegate / redelegate)
- Amount in **both CSPR and motes**
- Recipient or validator public key
- Network (testnet / mainnet)
- Estimated fee
- Sender public key
- Transaction hash

### Verify before signing

> **Verify the preview before calling `SignAndSubmitTransaction`.** Confirm the recipient or validator, the amount (in CSPR *and* motes), and the network match your intent.

The binding safety check is the signer's re-introspection of the actual transaction bytes — a tampered JSON is still policy-checked against the real decoded fields, not trusted from the preview text. Even so, an agent or user reviewing the preview is the last human checkpoint before funds move.

### After submission

Take the transaction hash from the `SignAndSubmitTransaction` result and pass it to `GetDeploy` to check its execution status on-chain. The hash is deterministic — it is set at build time and returned again after submission.
