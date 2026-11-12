using System.Globalization;
using System.Numerics;
using System.Text;
using CSPR.Cloud.Net.Errors;
using CSPR.Cloud.Net.Objects.Abstract;
using CSPR.Cloud.Net.Objects.Block;
using CSPR.Cloud.Net.Objects.Deploy;

namespace CasperMcp.Helpers;

/// <summary>What <see cref="NetworkActivity.ComputeAsync"/> counted over one window of blocks.</summary>
public sealed class NetworkActivityReport
{
    public int Hours { get; init; }
    public ulong LowestHeight { get; set; }
    public ulong HighestHeight { get; set; }
    public int BlockCount { get; set; }
    public DateTime EarliestBlockTime { get; set; }
    public DateTime LatestBlockTime { get; set; }

    public long NativeTransfers { get; set; }
    public long Auction { get; set; }
    public long InstallUpgrade { get; set; }
    public long Small { get; set; }
    public long Medium { get; set; }
    public long Large { get; set; }

    /// <summary>CSPR.cloud's own per-block count. It overlaps the categories above, so it is never part of <see cref="Transactions"/>.</summary>
    public long ContractCalls { get; set; }

    /// <summary>
    /// Every transaction in the window: each block's categories partition its transactions. Checked
    /// against the live API on 2026-11-12: over 1,000 mainnet blocks the categories summed to 206,
    /// /deploys' item_count for the same height range.
    /// </summary>
    public long Transactions => NativeTransfers + Auction + InstallUpgrade + Small + Medium + Large;

    /// <summary>Heights inside the window that no page returned, even after the repair rounds.</summary>
    public int MissingBlocks { get; set; }

    /// <summary>True when the block pages ran out (<see cref="NetworkActivityLimits.MaxBlockPages"/>) before reaching the window's start.</summary>
    public bool BlockPagesCapped { get; set; }

    public int TransactionRows { get; set; }
    public int Failed { get; set; }
    public int FeeRows { get; set; }
    public BigInteger FeesMotes { get; set; }
    public int DistinctSenders { get; set; }

    /// <summary>True when the transaction pages ran out (<see cref="NetworkActivityLimits.MaxDeployPages"/>) before passing the window.</summary>
    public bool TransactionPagesCapped { get; set; }

    /// <summary>CSPR.cloud requests made, retries included.</summary>
    public int Requests { get; set; }
}

public sealed record NetworkActivityLimits
{
    public int PageSize { get; init; } = 250;
    public int MaxBlockPages { get; init; } = 100;
    public int MaxDeployPages { get; init; } = 40;
    public int Concurrency { get; init; } = 4;
    public int RepairRounds { get; init; } = 3;
}

/// <summary>
/// Counts what happened on the network over the last few hours. CSPR.cloud has no statistics
/// endpoint, and /blocks ignores height and time filters, so this pages /blocks newest-first and sums
/// each block's transaction counters, then pages /deploys newest-first for the fees, failures and
/// senders of the transactions in those same blocks.
/// <para>
/// Both lists are read while the chain grows. A block or transaction added during the read pushes the
/// rows down, which only repeats a row on the next page (both reads de-duplicate). Pages fetched in
/// parallel can also complete out of order, and a higher page read before a lower one, with a block
/// added in between, skips rows: the repair rounds re-read whichever pages now hold the missing heights.
/// </para>
/// </summary>
public static class NetworkActivity
{
    public const int MaxHours = 24;

    /// <summary>
    /// casper-node v2.2.2 resources/mainnet/chainspec.toml:192 sets max_ttl = '2 hours': a transaction's
    /// header timestamp is at most this long before the block that includes it, so once /deploys
    /// (sorted by that timestamp) is this far behind the window's first block, no later row is in it.
    /// </summary>
    public static readonly TimeSpan MaxTtl = TimeSpan.FromHours(2);

    private static readonly TimeSpan[] RateLimitBackoff =
        [TimeSpan.FromMilliseconds(500), TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(2)];

    public delegate Task<PaginatedResponse<T>?> PageFetcher<T>(int page, CancellationToken cancellationToken);

    public static int ClampHours(int hours) => Math.Clamp(hours, 1, MaxHours);

    public static async Task<NetworkActivityReport?> ComputeAsync(
        PageFetcher<BlockData> fetchBlocks,
        PageFetcher<DeployData> fetchDeploys,
        int hours,
        CancellationToken cancellationToken = default,
        NetworkActivityLimits? limits = null,
        Func<TimeSpan, CancellationToken, Task>? delay = null)
    {
        limits ??= new NetworkActivityLimits();
        delay ??= Task.Delay;
        hours = ClampHours(hours);
        var requests = 0;

        async Task<List<T>> Fetch<T>(PageFetcher<T> fetch, int page)
        {
            for (var attempt = 0; ; attempt++)
            {
                Interlocked.Increment(ref requests);
                try
                {
                    var response = await fetch(page, cancellationToken);
                    return response?.Data ?? [];
                }
                catch (RateLimitException) when (attempt < RateLimitBackoff.Length)
                {
                    await delay(RateLimitBackoff[attempt], cancellationToken);
                }
            }
        }

        // ---- Blocks -------------------------------------------------------------------------------
        var firstPage = Usable(await Fetch(fetchBlocks, 1));
        if (firstPage.Count == 0)
            return null;

        var top = firstPage.MaxBy(b => b.BlockHeight!.Value)!;
        var highest = top.BlockHeight!.Value;
        var latestTime = top.Timestamp!.Value;
        var cutoff = latestTime - TimeSpan.FromHours(hours);

        var blocks = new Dictionary<ulong, BlockData>();
        void Add(IEnumerable<BlockData> rows)
        {
            lock (blocks)
            {
                // Blocks proposed after the first read are outside the window it anchored.
                foreach (var b in rows)
                    if (b.BlockHeight <= highest)
                        blocks[b.BlockHeight!.Value] = b;
            }
        }
        DateTime OldestTime() { lock (blocks) return blocks[blocks.Keys.Min()].Timestamp!.Value; }

        Add(firstPage);
        var lastPage = 1;

        if (OldestTime() > cutoff)
        {
            // Page 1's span says how many pages the window needs; read those in parallel.
            var spanSeconds = (latestTime - firstPage.Min(b => b.Timestamp!.Value)).TotalSeconds;
            var needed = spanSeconds > 0
                ? (int)Math.Ceiling(hours * 3600 / spanSeconds)
                : limits.MaxBlockPages;
            needed = Math.Clamp(needed, 1, limits.MaxBlockPages);

            if (needed > 1)
            {
                using var gate = new SemaphoreSlim(limits.Concurrency);
                var reads = Enumerable.Range(2, needed - 1).Select(async page =>
                {
                    await gate.WaitAsync(cancellationToken);
                    try { Add(Usable(await Fetch(fetchBlocks, page))); }
                    finally { gate.Release(); }
                });
                await Task.WhenAll(reads);
                lastPage = needed;
            }
        }

        // The estimate can fall short; carry on one page at a time until the window's start is read.
        var exhausted = false;
        while (OldestTime() > cutoff && lastPage < limits.MaxBlockPages)
        {
            lastPage++;
            var rows = Usable(await Fetch(fetchBlocks, lastPage));
            if (rows.Count == 0) { exhausted = true; break; }
            Add(rows);
        }
        var blockPagesCapped = !exhausted && OldestTime() > cutoff;

        for (var round = 0; round < limits.RepairRounds; round++)
        {
            var missing = Missing(blocks, highest);
            if (missing.Count == 0) break;

            var fresh = Usable(await Fetch(fetchBlocks, 1));
            if (fresh.Count == 0) break;
            var tip = fresh.Max(b => b.BlockHeight!.Value);
            Add(fresh);

            // Ascending and one at a time: a page read after a lower one can only repeat rows.
            var pages = missing
                .Select(h => (int)((tip - h) / (ulong)limits.PageSize) + 1)
                .Distinct()
                .Order()
                .Take(limits.MaxBlockPages)
                .ToList();
            foreach (var page in pages)
                Add(Usable(await Fetch(fetchBlocks, page)));
        }

        var window = blocks.Values.Where(b => b.Timestamp > cutoff).OrderBy(b => b.BlockHeight).ToList();
        var report = new NetworkActivityReport
        {
            Hours = hours,
            HighestHeight = highest,
            LowestHeight = window[0].BlockHeight!.Value,
            BlockCount = window.Count,
            EarliestBlockTime = window[0].Timestamp!.Value,
            LatestBlockTime = latestTime,
            BlockPagesCapped = blockPagesCapped,
        };
        foreach (var b in window)
        {
            report.NativeTransfers += b.NativeTransfersNumber ?? 0;
            report.Auction += b.AuctionTxnNumber ?? 0;
            report.InstallUpgrade += b.InstallUpgradeTxnNumber ?? 0;
            report.Small += b.SmallTxnNumber ?? 0;
            report.Medium += b.MediumTxnNumber ?? 0;
            report.Large += b.LargeTxnNumber ?? 0;
            report.ContractCalls += b.ContractCallsNumber ?? 0;
        }
        report.MissingBlocks = (int)(highest - report.LowestHeight + 1) - window.Count;

        // ---- Transactions -------------------------------------------------------------------------
        // /deploys sorts by the transaction's own header time, not its block's, so rows are matched to
        // the window by block height, and the read stops once a page reaches back past the first
        // block's time by more than a transaction can wait to be included.
        var stopBefore = report.EarliestBlockTime - MaxTtl;
        var seen = new HashSet<string>(StringComparer.Ordinal);
        var senders = new HashSet<string>(StringComparer.Ordinal);
        var passedWindow = false;
        for (var page = 1; page <= limits.MaxDeployPages; page++)
        {
            var rows = await Fetch(fetchDeploys, page);
            if (rows.Count == 0) { passedWindow = true; break; }

            foreach (var row in rows)
            {
                if (row.BlockHeight is not { } height || height < report.LowestHeight || height > highest) continue;
                if (string.IsNullOrEmpty(row.DeployHash) || !seen.Add(row.DeployHash)) continue;

                report.TransactionRows++;
                if (!string.IsNullOrEmpty(row.ErrorMessage)) report.Failed++;
                if (Motes(row.Cost) is { } cost)
                {
                    report.FeeRows++;
                    report.FeesMotes += cost - (Motes(row.RefundAmount) ?? BigInteger.Zero);
                }
                if ((string.IsNullOrEmpty(row.CallerHash) ? row.CallerPublicKey : row.CallerHash) is { Length: > 0 } sender)
                    senders.Add(sender);
            }

            var oldest = rows.Where(r => r.Timestamp is not null).Select(r => r.Timestamp!.Value).DefaultIfEmpty(DateTime.MaxValue).Min();
            if (oldest < stopBefore) { passedWindow = true; break; }
        }
        report.TransactionPagesCapped = !passedWindow;
        report.DistinctSenders = senders.Count;
        report.Requests = requests;
        return report;
    }

    public static string Format(NetworkActivityReport r, bool isTestnet)
    {
        var inv = CultureInfo.InvariantCulture;
        string N(long v) => v.ToString("N0", inv);

        var sb = new StringBuilder();
        sb.AppendLine($"## Network Activity ({(isTestnet ? "Testnet" : "Mainnet")}, last {r.Hours} {(r.Hours == 1 ? "hour" : "hours")})");
        sb.AppendLine($"- **Window:** blocks {N((long)r.LowestHeight)} to {N((long)r.HighestHeight)} ({N(r.BlockCount)} blocks), " +
                      $"{FormattingHelpers.FormatTimestamp(r.EarliestBlockTime)} to {FormattingHelpers.FormatTimestamp(r.LatestBlockTime)}");
        sb.AppendLine(r.BlockCount > 1
            ? $"- **Average Block Time:** {((r.LatestBlockTime - r.EarliestBlockTime).TotalSeconds / (r.BlockCount - 1)).ToString("F2", inv)} s"
            : "- **Average Block Time:** N/A");
        sb.AppendLine($"- **Transactions:** {N(r.Transactions)}");
        sb.AppendLine($"  Native Transfers: {N(r.NativeTransfers)} | Auction: {N(r.Auction)} | Install/Upgrade: {N(r.InstallUpgrade)} | " +
                      $"Small: {N(r.Small)} | Medium: {N(r.Medium)} | Large: {N(r.Large)}");
        sb.AppendLine($"- **Contract Calls:** {N(r.ContractCalls)} (CSPR.cloud's own per-block count; it overlaps the categories above and is not part of the total)");
        sb.AppendLine($"- **Failed Transactions:** {N(r.Failed)}");
        sb.AppendLine($"- **Fees Paid (cost minus refund):** {FormattingHelpers.MotesToCspr(r.FeesMotes.ToString(inv))}");
        sb.AppendLine($"- **Distinct Senders:** {N(r.DistinctSenders)}");
        sb.AppendLine("---");

        if (r.MissingBlocks > 0)
            sb.AppendLine($"{N(r.MissingBlocks)} blocks inside the window could not be read; their transactions are not counted.");
        if (r.BlockPagesCapped)
            sb.AppendLine("The block read stopped at its page limit before reaching the start of the window, so the window is shorter than asked.");
        if (r.TransactionRows != r.Transactions)
            sb.AppendLine($"Failures, fees and senders cover {N(r.TransactionRows)} of the {N(r.Transactions)} transactions" +
                          (r.TransactionPagesCapped ? " (the transaction read stopped at its page limit)." : "."));
        if (r.FeeRows != r.TransactionRows)
            sb.AppendLine($"Fees cover {N(r.FeeRows)} of those {N(r.TransactionRows)} transactions; the others carry no cost.");

        sb.AppendLine($"Figures are CSPR.cloud's per-block counters summed over the blocks in the window; failures, fees and senders " +
                      $"come from its transaction records for the same blocks. This tool covers at most the last {MaxHours} hours. " +
                      $"Read from {N(r.Requests)} CSPR.cloud pages.");
        return sb.ToString();
    }

    private static List<BlockData> Usable(List<BlockData> rows) =>
        rows.Where(b => b.BlockHeight is not null && b.Timestamp is not null).ToList();

    private static List<ulong> Missing(Dictionary<ulong, BlockData> blocks, ulong highest)
    {
        lock (blocks)
        {
            var lowest = blocks.Keys.Min();
            var missing = new List<ulong>();
            for (var h = lowest; h <= highest; h++)
                if (!blocks.ContainsKey(h))
                    missing.Add(h);
            return missing;
        }
    }

    private static BigInteger? Motes(string? value) =>
        !string.IsNullOrEmpty(value) && BigInteger.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var motes)
            ? motes
            : null;
}
