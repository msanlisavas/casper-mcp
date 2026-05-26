# Observability

casper-mcp emits **OpenTelemetry** over **OTLP** — traces, metrics, and structured logs — so it
works with **any** OTLP-compatible backend. You only need to set one environment variable on the
server: `OTEL_EXPORTER_OTLP_ENDPOINT` (when unset, telemetry is off and the server runs normally).

This folder ships two ready-to-run local stacks. Pick whichever you prefer — they're equivalent
from the server's point of view (same OTLP data, different viewer).

| | Grafana (otel-lgtm) | .NET Aspire Dashboard |
|---|---|---|
| Best for | full-featured dashboards, long-term retention | quick live viewing, no query language |
| Storage | Tempo/Prometheus/Loki (persistent-capable) | in-memory (ephemeral) |
| UI | http://localhost:3000 | http://localhost:18888 |
| Learn a query language? | a little (pre-built dashboard included) | no |

## Option A — Grafana (otel-lgtm)

```bash
docker compose -f observability/grafana/docker-compose.yml up --build
# then open the pre-built dashboard:
#   http://localhost:3000/d/casper-mcp
```
One container (`grafana/otel-lgtm`) bundles Grafana + Tempo (traces) + Prometheus (metrics) +
Loki (logs) + an OpenTelemetry Collector. A **Casper MCP dashboard is auto-provisioned** (call
rate by tool, success/error rate, p95 latency, and live log panels) — no setup, no query-writing.

## Option B — .NET Aspire Dashboard

```bash
docker compose -f observability/aspire/docker-compose.yml up --build
# then open:
#   http://localhost:18888
```
A single container shows traces, metrics, and logs in a clean .NET UI. Nothing to configure.

## What you'll see

- **Metrics:** `casper_mcp_tool_calls_total` (by `tool`, `status`) and
  `casper_mcp_tool_duration_milliseconds` (histogram), plus standard ASP.NET Core, HttpClient
  (outbound CSPR.Cloud), and .NET runtime metrics.
- **Traces:** a `tool/<name>` span per MCP call (tagged `mcp.tool`, `casper.tenant`), the ASP.NET
  server span, and the outbound CSPR.Cloud span — so you can follow one request end-to-end.
- **Logs:** one structured line per tool call — `tool`, `status`, `duration_ms`, `tenant`,
  `correlation_id` — also printed to stdout as JSON.

## Per-agent visibility without exposing keys

Traffic is tagged with `tenant` — a **non-reversible fingerprint** of the agent's CSPR.Cloud key
(`k_` + a short SHA-256 prefix, e.g. `k_3f9a1c2b4d5e`). You can break traffic down per agent and
correlate a tenant's activity across logs/traces, but **the raw key never appears** in any log,
span, metric, or error message.

## Point at your own backend instead

Don't want to run either stack? Send OTLP straight to whatever you already use (Grafana Cloud,
Datadog, Honeycomb, New Relic, SigNoz, an OpenTelemetry Collector, …):

```bash
docker run -p 3001:3001 \
  -e CASPER_MCP_TRANSPORT=http \
  -e OTEL_EXPORTER_OTLP_ENDPOINT=https://your-otlp-endpoint:4317 \
  ghcr.io/msanlisavas/casper-mcp:latest
```
Logs also go to stdout as JSON, so your existing container-log pipeline (Loki, Fluent Bit,
CloudWatch, `kubectl logs`, …) works with zero OTLP.
