using System.Collections.Concurrent;
using System.ComponentModel;
using System.Text;
using CasperMcp.Configuration;
using CasperMcp.Helpers;
using CSPR.Cloud.Net.Clients;
using CSPR.Cloud.Net.Objects.Socket;
using CSPR.Cloud.Net.Parameters.Socket;
using ModelContextProtocol.Server;

namespace CasperMcp.Tools;

[McpServerToolType]
public static class StreamingTools
{
    private const int MaxEventsCap = 50;
    private const int MaxTimeoutSeconds = 120;
    private const int DefaultMaxEvents = 5;
    private const int DefaultTimeoutSeconds = 30;

    private static List<string>? SplitFilter(string? csv)
    {
        if (string.IsNullOrWhiteSpace(csv)) return null;
        return csv.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList();
    }

    private static (int events, int timeout) ClampLimits(int maxEvents, int timeoutSeconds) =>
        (Math.Clamp(maxEvents, 1, MaxEventsCap), Math.Clamp(timeoutSeconds, 1, MaxTimeoutSeconds));

    /// <summary>
    /// Subscribes to a stream until either <paramref name="maxEvents"/> messages arrive or the timeout elapses.
    /// </summary>
    private static async Task<List<WebSocketMessage<T>>> CaptureAsync<T>(
        Func<Func<WebSocketMessage<T>, Task>, CancellationToken, Task> subscribe,
        int maxEvents,
        int timeoutSeconds)
    {
        var captured = new ConcurrentBag<WebSocketMessage<T>>();
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(timeoutSeconds));

        try
        {
            await subscribe(msg =>
            {
                captured.Add(msg);
                if (captured.Count >= maxEvents)
                {
                    try { cts.Cancel(); } catch { /* already cancelled */ }
                }
                return Task.CompletedTask;
            }, cts.Token);
        }
        catch (OperationCanceledException) { /* expected when limit reached or timeout */ }

        return captured.OrderBy(m => m.Timestamp ?? DateTime.MinValue).Take(maxEvents).ToList();
    }

    private static string FormatHeader(string title, int captured, int requested, int timeoutSeconds, bool isTestnet)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"## {title} ({(isTestnet ? "Testnet" : "Mainnet")})");
        sb.AppendLine($"Captured **{captured}** of up to **{requested}** events (timeout {timeoutSeconds}s).");
        sb.AppendLine();
        return sb.ToString();
    }

    [McpServerTool, Description("Subscribe to the Casper Network blocks stream and return up to maxEvents new blocks (or until timeoutSeconds elapses).")]
    public static async Task<string> WatchBlocks(
        CasperCloudSocketClient socketClient,
        CasperMcpOptions options,
        [Description("Maximum events to capture (1-50, default 5)")] int maxEvents = DefaultMaxEvents,
        [Description("Timeout in seconds (1-120, default 30)")] int timeoutSeconds = DefaultTimeoutSeconds,
        [Description("Optional comma-separated proposer public keys to filter on")] string? proposerPublicKey = null)
    {
        try
        {
            (maxEvents, timeoutSeconds) = ClampLimits(maxEvents, timeoutSeconds);
            var endpoint = options.IsTestnet ? socketClient.Testnet : socketClient.Mainnet;
            var parameters = new BlockStreamParameters { ProposerPublicKey = SplitFilter(proposerPublicKey)! };

            var messages = await CaptureAsync<CSPR.Cloud.Net.Objects.Block.BlockData>(
                (handler, ct) => endpoint.Block.SubscribeAsync(parameters, handler, cancellationToken: ct),
                maxEvents, timeoutSeconds);

            var sb = new StringBuilder(FormatHeader("Block Stream", messages.Count, maxEvents, timeoutSeconds, options.IsTestnet));
            foreach (var m in messages)
            {
                var b = m.Data;
                sb.AppendLine($"---");
                sb.AppendLine($"- **Action:** {m.Action} | **At:** {FormattingHelpers.FormatTimestamp(m.Timestamp)}");
                sb.AppendLine($"  Height: {b?.BlockHeight?.ToString() ?? "N/A"} | Era: {b?.EraId} | Switch: {(b is null ? "?" : FormattingHelpers.FormatBool(b.IsSwitchBlock))}");
                sb.AppendLine($"  Hash: {FormattingHelpers.FormatHash(b?.BlockHash)}");
                sb.AppendLine($"  Proposer: {FormattingHelpers.FormatHash(b?.ProposerPublicKey)}");
            }
            return sb.ToString();
        }
        catch (Exception ex)
        {
            return $"Error watching blocks: {ex.Message}";
        }
    }

    [McpServerTool, Description("Subscribe to the Casper Network deploys stream and return up to maxEvents new deploys (or until timeoutSeconds elapses).")]
    public static async Task<string> WatchDeploys(
        CasperCloudSocketClient socketClient,
        CasperMcpOptions options,
        [Description("Maximum events to capture (1-50, default 5)")] int maxEvents = DefaultMaxEvents,
        [Description("Timeout in seconds (1-120, default 30)")] int timeoutSeconds = DefaultTimeoutSeconds,
        [Description("Optional comma-separated caller public keys")] string? callerPublicKey = null,
        [Description("Optional comma-separated contract package hashes")] string? contractPackageHash = null,
        [Description("Optional comma-separated contract hashes")] string? contractHash = null,
        [Description("Optional comma-separated contract entry-point IDs")] string? contractEntrypointId = null,
        [Description("Optional comma-separated deploy hashes")] string? deployHash = null)
    {
        try
        {
            (maxEvents, timeoutSeconds) = ClampLimits(maxEvents, timeoutSeconds);
            var endpoint = options.IsTestnet ? socketClient.Testnet : socketClient.Mainnet;
            var parameters = new DeployStreamParameters
            {
                CallerPublicKey = SplitFilter(callerPublicKey)!,
                ContractPackageHash = SplitFilter(contractPackageHash)!,
                ContractHash = SplitFilter(contractHash)!,
                ContractEntrypointId = SplitFilter(contractEntrypointId)!,
                DeployHash = SplitFilter(deployHash)!,
            };

            var messages = await CaptureAsync<CSPR.Cloud.Net.Objects.Deploy.DeployData>(
                (handler, ct) => endpoint.Deploy.SubscribeAsync(parameters, handler, cancellationToken: ct),
                maxEvents, timeoutSeconds);

            var sb = new StringBuilder(FormatHeader("Deploy Stream", messages.Count, maxEvents, timeoutSeconds, options.IsTestnet));
            foreach (var m in messages)
            {
                var d = m.Data;
                sb.AppendLine($"---");
                sb.AppendLine($"- **Action:** {m.Action} | **At:** {FormattingHelpers.FormatTimestamp(m.Timestamp)}");
                sb.AppendLine($"  Hash: {FormattingHelpers.FormatHash(d?.DeployHash)}");
                sb.AppendLine($"  Caller: {FormattingHelpers.FormatHash(d?.CallerPublicKey)}");
                sb.AppendLine($"  Status: {d?.Status ?? "N/A"} | Cost: {FormattingHelpers.MotesToCspr(d?.Cost)}");
                if (!string.IsNullOrEmpty(d?.ContractHash))
                    sb.AppendLine($"  Contract: {FormattingHelpers.FormatHash(d.ContractHash)}");
            }
            return sb.ToString();
        }
        catch (Exception ex)
        {
            return $"Error watching deploys: {ex.Message}";
        }
    }

    [McpServerTool, Description("Subscribe to the Casper Network native CSPR transfers stream and return up to maxEvents new transfers (or until timeoutSeconds elapses).")]
    public static async Task<string> WatchTransfers(
        CasperCloudSocketClient socketClient,
        CasperMcpOptions options,
        [Description("Maximum events to capture (1-50, default 5)")] int maxEvents = DefaultMaxEvents,
        [Description("Timeout in seconds (1-120, default 30)")] int timeoutSeconds = DefaultTimeoutSeconds,
        [Description("Optional comma-separated account hashes")] string? accountHash = null,
        [Description("Optional comma-separated public keys")] string? publicKey = null)
    {
        try
        {
            (maxEvents, timeoutSeconds) = ClampLimits(maxEvents, timeoutSeconds);
            var endpoint = options.IsTestnet ? socketClient.Testnet : socketClient.Mainnet;
            var parameters = new TransferStreamParameters
            {
                AccountHash = SplitFilter(accountHash)!,
                PublicKey = SplitFilter(publicKey)!,
            };

            var messages = await CaptureAsync<CSPR.Cloud.Net.Objects.Transfer.TransferData>(
                (handler, ct) => endpoint.Transfer.SubscribeAsync(parameters, handler, cancellationToken: ct),
                maxEvents, timeoutSeconds);

            var sb = new StringBuilder(FormatHeader("Transfer Stream", messages.Count, maxEvents, timeoutSeconds, options.IsTestnet));
            foreach (var m in messages)
            {
                var t = m.Data;
                sb.AppendLine($"---");
                sb.AppendLine($"- **Action:** {m.Action} | **At:** {FormattingHelpers.FormatTimestamp(m.Timestamp)}");
                sb.AppendLine($"  Deploy: {FormattingHelpers.FormatHash(t?.DeployHash)}");
                sb.AppendLine($"  From: {FormattingHelpers.FormatHash(t?.FromPursePublicKey ?? t?.InitiatorAccountHash)}");
                sb.AppendLine($"  To: {FormattingHelpers.FormatHash(t?.ToPublicKey ?? t?.ToAccountHash)}");
                sb.AppendLine($"  Amount: {FormattingHelpers.MotesToCspr(t?.Amount)}");
            }
            return sb.ToString();
        }
        catch (Exception ex)
        {
            return $"Error watching transfers: {ex.Message}";
        }
    }

    [McpServerTool, Description("Subscribe to the Casper Network account-balance stream and return up to maxEvents balance updates (or until timeoutSeconds elapses).")]
    public static async Task<string> WatchAccountBalances(
        CasperCloudSocketClient socketClient,
        CasperMcpOptions options,
        [Description("Maximum events to capture (1-50, default 5)")] int maxEvents = DefaultMaxEvents,
        [Description("Timeout in seconds (1-120, default 30)")] int timeoutSeconds = DefaultTimeoutSeconds,
        [Description("Optional comma-separated account hashes")] string? accountHash = null,
        [Description("Optional comma-separated public keys")] string? publicKey = null)
    {
        try
        {
            (maxEvents, timeoutSeconds) = ClampLimits(maxEvents, timeoutSeconds);
            var endpoint = options.IsTestnet ? socketClient.Testnet : socketClient.Mainnet;
            var parameters = new AccountBalanceStreamParameters
            {
                AccountHash = SplitFilter(accountHash)!,
                PublicKey = SplitFilter(publicKey)!,
            };

            var messages = await CaptureAsync<AccountBalanceStreamData>(
                (handler, ct) => endpoint.AccountBalance.SubscribeAsync(parameters, handler, cancellationToken: ct),
                maxEvents, timeoutSeconds);

            var sb = new StringBuilder(FormatHeader("Account Balance Stream", messages.Count, maxEvents, timeoutSeconds, options.IsTestnet));
            foreach (var m in messages)
            {
                var b = m.Data;
                sb.AppendLine($"---");
                sb.AppendLine($"- **Action:** {m.Action} | **At:** {FormattingHelpers.FormatTimestamp(m.Timestamp)}");
                sb.AppendLine($"  Account Hash: {FormattingHelpers.FormatHash(b?.AccountHash)}");
                sb.AppendLine($"  Liquid: {FormattingHelpers.MotesToCspr(b?.LiquidBalance)} | Staked: {FormattingHelpers.MotesToCspr(b?.StakedBalance)} | Undelegating: {FormattingHelpers.MotesToCspr(b?.UndelegatingBalance)}");
            }
            return sb.ToString();
        }
        catch (Exception ex)
        {
            return $"Error watching account balances: {ex.Message}";
        }
    }

    [McpServerTool, Description("Subscribe to the Casper Network contracts stream and return up to maxEvents new contract creations (or until timeoutSeconds elapses).")]
    public static async Task<string> WatchContracts(
        CasperCloudSocketClient socketClient,
        CasperMcpOptions options,
        [Description("Maximum events to capture (1-50, default 5)")] int maxEvents = DefaultMaxEvents,
        [Description("Timeout in seconds (1-120, default 30)")] int timeoutSeconds = DefaultTimeoutSeconds,
        [Description("Optional comma-separated contract package hashes")] string? contractPackageHash = null,
        [Description("Optional comma-separated deploy hashes")] string? deployHash = null)
    {
        try
        {
            (maxEvents, timeoutSeconds) = ClampLimits(maxEvents, timeoutSeconds);
            var endpoint = options.IsTestnet ? socketClient.Testnet : socketClient.Mainnet;
            var parameters = new ContractStreamParameters
            {
                ContractPackageHash = SplitFilter(contractPackageHash)!,
                DeployHash = SplitFilter(deployHash)!,
            };

            var messages = await CaptureAsync<CSPR.Cloud.Net.Objects.Contract.ContractData>(
                (handler, ct) => endpoint.Contract.SubscribeAsync(parameters, handler, cancellationToken: ct),
                maxEvents, timeoutSeconds);

            var sb = new StringBuilder(FormatHeader("Contract Stream", messages.Count, maxEvents, timeoutSeconds, options.IsTestnet));
            foreach (var m in messages)
            {
                var c = m.Data;
                sb.AppendLine($"---");
                sb.AppendLine($"- **Action:** {m.Action} | **At:** {FormattingHelpers.FormatTimestamp(m.Timestamp)}");
                sb.AppendLine($"  Contract: {FormattingHelpers.FormatHash(c?.ContractHash)}");
                sb.AppendLine($"  Package: {FormattingHelpers.FormatHash(c?.ContractPackageHash)}");
                sb.AppendLine($"  Deploy: {FormattingHelpers.FormatHash(c?.DeployHash)} | Block: {c?.BlockHeight}");
            }
            return sb.ToString();
        }
        catch (Exception ex)
        {
            return $"Error watching contracts: {ex.Message}";
        }
    }

    [McpServerTool, Description("Subscribe to the Casper Network contract-packages stream (created and updated) and return up to maxEvents events (or until timeoutSeconds elapses).")]
    public static async Task<string> WatchContractPackages(
        CasperCloudSocketClient socketClient,
        CasperMcpOptions options,
        [Description("Maximum events to capture (1-50, default 5)")] int maxEvents = DefaultMaxEvents,
        [Description("Timeout in seconds (1-120, default 30)")] int timeoutSeconds = DefaultTimeoutSeconds,
        [Description("Optional comma-separated contract package hashes")] string? contractPackageHash = null,
        [Description("Optional comma-separated owner public keys")] string? ownerPublicKey = null)
    {
        try
        {
            (maxEvents, timeoutSeconds) = ClampLimits(maxEvents, timeoutSeconds);
            var endpoint = options.IsTestnet ? socketClient.Testnet : socketClient.Mainnet;
            var parameters = new ContractPackageStreamParameters
            {
                ContractPackageHash = SplitFilter(contractPackageHash)!,
                OwnerPublicKey = SplitFilter(ownerPublicKey)!,
            };

            var messages = await CaptureAsync<CSPR.Cloud.Net.Objects.Contract.ContractPackageData>(
                (handler, ct) => endpoint.ContractPackage.SubscribeAsync(parameters, handler, cancellationToken: ct),
                maxEvents, timeoutSeconds);

            var sb = new StringBuilder(FormatHeader("Contract Package Stream", messages.Count, maxEvents, timeoutSeconds, options.IsTestnet));
            foreach (var m in messages)
            {
                var p = m.Data;
                sb.AppendLine($"---");
                sb.AppendLine($"- **Action:** {m.Action} | **At:** {FormattingHelpers.FormatTimestamp(m.Timestamp)}");
                sb.AppendLine($"  Package: {FormattingHelpers.FormatHash(p?.ContractPackageHash)}");
                sb.AppendLine($"  Name: {p?.Name ?? "N/A"} | Owner: {FormattingHelpers.FormatHash(p?.OwnerPublicKey ?? p?.OwnerHash)}");
            }
            return sb.ToString();
        }
        catch (Exception ex)
        {
            return $"Error watching contract packages: {ex.Message}";
        }
    }

    [McpServerTool, Description("Subscribe to a contract's emitted events stream. ContractHash or ContractPackageHash is REQUIRED. Returns up to maxEvents events (or until timeoutSeconds elapses).")]
    public static async Task<string> WatchContractEvents(
        CasperCloudSocketClient socketClient,
        CasperMcpOptions options,
        [Description("Comma-separated contract hashes (required if contractPackageHash is empty)")] string? contractHash = null,
        [Description("Comma-separated contract package hashes (required if contractHash is empty)")] string? contractPackageHash = null,
        [Description("Maximum events to capture (1-50, default 5)")] int maxEvents = DefaultMaxEvents,
        [Description("Timeout in seconds (1-120, default 30)")] int timeoutSeconds = DefaultTimeoutSeconds)
    {
        if (string.IsNullOrWhiteSpace(contractHash) && string.IsNullOrWhiteSpace(contractPackageHash))
            return "Error: WatchContractEvents requires either contractHash or contractPackageHash.";

        try
        {
            (maxEvents, timeoutSeconds) = ClampLimits(maxEvents, timeoutSeconds);
            var endpoint = options.IsTestnet ? socketClient.Testnet : socketClient.Mainnet;
            var parameters = new ContractEventStreamParameters
            {
                ContractHash = SplitFilter(contractHash)!,
                ContractPackageHash = SplitFilter(contractPackageHash)!,
            };

            var messages = await CaptureAsync<ContractEventStreamData>(
                (handler, ct) => endpoint.ContractEvent.SubscribeAsync(parameters, handler, cancellationToken: ct),
                maxEvents, timeoutSeconds);

            var sb = new StringBuilder(FormatHeader("Contract Event Stream", messages.Count, maxEvents, timeoutSeconds, options.IsTestnet));
            foreach (var m in messages)
            {
                var e = m.Data;
                sb.AppendLine($"---");
                sb.AppendLine($"- **Action:** {m.Action} | **At:** {FormattingHelpers.FormatTimestamp(m.Timestamp)}");
                sb.AppendLine($"  Event: **{e?.Name ?? "(unnamed)"}**");
                sb.AppendLine($"  Contract: {FormattingHelpers.FormatHash(e?.ContractHash)}");
                sb.AppendLine($"  Package: {FormattingHelpers.FormatHash(e?.ContractPackageHash)}");
                if (e?.Data is not null)
                    sb.AppendLine($"  Data: {e.Data.ToString(Newtonsoft.Json.Formatting.None)}");
            }
            return sb.ToString();
        }
        catch (Exception ex)
        {
            return $"Error watching contract events: {ex.Message}";
        }
    }

    [McpServerTool, Description("Subscribe to the fungible-token actions stream (mint/transfer/approve/burn) and return up to maxEvents events (or until timeoutSeconds elapses).")]
    public static async Task<string> WatchFtTokenActions(
        CasperCloudSocketClient socketClient,
        CasperMcpOptions options,
        [Description("Maximum events to capture (1-50, default 5)")] int maxEvents = DefaultMaxEvents,
        [Description("Timeout in seconds (1-120, default 30)")] int timeoutSeconds = DefaultTimeoutSeconds,
        [Description("Optional comma-separated contract package hashes")] string? contractPackageHash = null,
        [Description("Optional comma-separated owner account hashes")] string? ownerHash = null)
    {
        try
        {
            (maxEvents, timeoutSeconds) = ClampLimits(maxEvents, timeoutSeconds);
            var endpoint = options.IsTestnet ? socketClient.Testnet : socketClient.Mainnet;
            var parameters = new FTTokenActionStreamParameters
            {
                ContractPackageHash = SplitFilter(contractPackageHash)!,
                OwnerHash = SplitFilter(ownerHash)!,
            };

            var messages = await CaptureAsync<CSPR.Cloud.Net.Objects.Ft.FTTokenActionData>(
                (handler, ct) => endpoint.FTTokenAction.SubscribeAsync(parameters, handler, cancellationToken: ct),
                maxEvents, timeoutSeconds);

            var sb = new StringBuilder(FormatHeader("FT Token Action Stream", messages.Count, maxEvents, timeoutSeconds, options.IsTestnet));
            foreach (var m in messages)
            {
                var a = m.Data;
                sb.AppendLine($"---");
                sb.AppendLine($"- **Action:** {m.Action} | **At:** {FormattingHelpers.FormatTimestamp(m.Timestamp)}");
                sb.AppendLine($"  Token: {FormattingHelpers.FormatHash(a?.ContractPackageHash)}");
                sb.AppendLine($"  From: {FormattingHelpers.FormatHash(a?.FromPublicKey ?? a?.FromHash)}");
                sb.AppendLine($"  To: {FormattingHelpers.FormatHash(a?.ToPublicKey ?? a?.ToHash)}");
                sb.AppendLine($"  Amount: {a?.Amount ?? "N/A"}");
            }
            return sb.ToString();
        }
        catch (Exception ex)
        {
            return $"Error watching FT token actions: {ex.Message}";
        }
    }

    [McpServerTool, Description("Subscribe to the NFT stream (created/updated) and return up to maxEvents events (or until timeoutSeconds elapses).")]
    public static async Task<string> WatchNfts(
        CasperCloudSocketClient socketClient,
        CasperMcpOptions options,
        [Description("Maximum events to capture (1-50, default 5)")] int maxEvents = DefaultMaxEvents,
        [Description("Timeout in seconds (1-120, default 30)")] int timeoutSeconds = DefaultTimeoutSeconds,
        [Description("Optional comma-separated contract package hashes")] string? contractPackageHash = null,
        [Description("Optional comma-separated owner account hashes")] string? ownerHash = null)
    {
        try
        {
            (maxEvents, timeoutSeconds) = ClampLimits(maxEvents, timeoutSeconds);
            var endpoint = options.IsTestnet ? socketClient.Testnet : socketClient.Mainnet;
            var parameters = new NFTStreamParameters
            {
                ContractPackageHash = SplitFilter(contractPackageHash)!,
                OwnerHash = SplitFilter(ownerHash)!,
            };

            var messages = await CaptureAsync<CSPR.Cloud.Net.Objects.Nft.NFTTokenData>(
                (handler, ct) => endpoint.NFT.SubscribeAsync(parameters, handler, cancellationToken: ct),
                maxEvents, timeoutSeconds);

            var sb = new StringBuilder(FormatHeader("NFT Stream", messages.Count, maxEvents, timeoutSeconds, options.IsTestnet));
            foreach (var m in messages)
            {
                var n = m.Data;
                sb.AppendLine($"---");
                sb.AppendLine($"- **Action:** {m.Action} | **At:** {FormattingHelpers.FormatTimestamp(m.Timestamp)}");
                sb.AppendLine($"  Token: {n?.TokenId ?? "N/A"} | Collection: {FormattingHelpers.FormatHash(n?.ContractPackageHash)}");
                sb.AppendLine($"  Owner: {FormattingHelpers.FormatHash(n?.OwnerPublicKey ?? n?.OwnerHash)}");
                sb.AppendLine($"  Burned: {(n is null ? "?" : FormattingHelpers.FormatBool(n.IsBurned))}");
            }
            return sb.ToString();
        }
        catch (Exception ex)
        {
            return $"Error watching NFTs: {ex.Message}";
        }
    }

    [McpServerTool, Description("Subscribe to the NFT-actions stream (mints/transfers/burns) and return up to maxEvents events (or until timeoutSeconds elapses).")]
    public static async Task<string> WatchNftActions(
        CasperCloudSocketClient socketClient,
        CasperMcpOptions options,
        [Description("Maximum events to capture (1-50, default 5)")] int maxEvents = DefaultMaxEvents,
        [Description("Timeout in seconds (1-120, default 30)")] int timeoutSeconds = DefaultTimeoutSeconds,
        [Description("Optional comma-separated contract package hashes")] string? contractPackageHash = null,
        [Description("Optional comma-separated owner account hashes")] string? ownerHash = null)
    {
        try
        {
            (maxEvents, timeoutSeconds) = ClampLimits(maxEvents, timeoutSeconds);
            var endpoint = options.IsTestnet ? socketClient.Testnet : socketClient.Mainnet;
            var parameters = new NFTActionStreamParameters
            {
                ContractPackageHash = SplitFilter(contractPackageHash)!,
                OwnerHash = SplitFilter(ownerHash)!,
            };

            var messages = await CaptureAsync<CSPR.Cloud.Net.Objects.Nft.NFTTokenActionData>(
                (handler, ct) => endpoint.NFTAction.SubscribeAsync(parameters, handler, cancellationToken: ct),
                maxEvents, timeoutSeconds);

            var sb = new StringBuilder(FormatHeader("NFT Action Stream", messages.Count, maxEvents, timeoutSeconds, options.IsTestnet));
            foreach (var m in messages)
            {
                var a = m.Data;
                sb.AppendLine($"---");
                sb.AppendLine($"- **Action:** {m.Action} | **At:** {FormattingHelpers.FormatTimestamp(m.Timestamp)}");
                sb.AppendLine($"  Deploy: {FormattingHelpers.FormatHash(a?.DeployHash)}");
                sb.AppendLine($"  Token: {a?.TokenId ?? "N/A"} | Collection: {FormattingHelpers.FormatHash(a?.ContractPackageHash)}");
                sb.AppendLine($"  From: {FormattingHelpers.FormatHash(a?.FromPublicKey ?? a?.FromHash)}");
                sb.AppendLine($"  To: {FormattingHelpers.FormatHash(a?.ToPublicKey ?? a?.ToHash)}");
            }
            return sb.ToString();
        }
        catch (Exception ex)
        {
            return $"Error watching NFT actions: {ex.Message}";
        }
    }
}
