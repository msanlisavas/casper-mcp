# CLAUDE.md — guidance for AI agents working in this repo

## What this is
`casper-mcp` is a .NET 10 MCP server for the Casper blockchain. Read tools talk to CSPR.Cloud;
write tools sign locally. Two transports: stdio (local) and Streamable HTTP (`POST /mcp`, remote,
multi-tenant, **read-only**).

## Commit policy
- Commits are **Murat-only**. **Never** add a `Co-Authored-By` trailer.
- Conventional commit prefixes (`feat:`, `fix:`, `docs:`, `build:`, `test:`).

## Non-negotiable security invariants (writes)
1. The private key is loaded only by the local stdio signer (`--enable-writes --key-path`). Never
   server-side, per-request, in env, in model context, or returned to the agent.
2. `--enable-writes` + http transport is a fatal startup error. Write tools are **not**
   `[McpServerToolType]` and are registered only in the stdio-write branch — never on the HTTP
   surface. There is a regression test (`WriteToolsHttpExclusionTests`) — keep it green.
3. The signer validates the **decoded transaction bytes** (`TransactionIntrospector`), never a
   description. Policy is loaded once at startup and is human-owned (no tool edits it).
4. Fail-closed everywhere. Testnet by default; mainnet is an explicit local opt-in.

## Layout
- `src/CasperMcp/Writes/` — policy engine, ledger, introspector, builder, signer (the write path).
- `src/CasperMcp/Tools/` — MCP tools (reads are `[McpServerToolType]`; write tools are not).
- Tests mirror under `tests/CasperMcp.Tests/`. Integration tests use `[Collection("Integration")]`.

## When adding write capability
- Keep the tool surface small. Prefer a generic path over one tool per operation.
- Any new write tool MUST go through `CasperSigner`/`PolicyEngine`, carry destructive hints, and be
  excluded from http. Add a policy-engine unit test for every new rule.
