using System.Numerics;
using CasperMcp.Helpers;
using CSPR.Cloud.Net.Errors;
using CSPR.Cloud.Net.Objects.Abstract;
using CSPR.Cloud.Net.Objects.Block;
using CSPR.Cloud.Net.Objects.Deploy;

namespace CasperMcp.Tests;

/// <summary>
/// NetworkActivity against a fake chain that serves /blocks and /deploys the way CSPR.cloud does:
/// newest first, 250 to a page, and growing while it is read.
/// </summary>
public class NetworkActivityTests
{
    private static readonly DateTime Genesis = new(2026, 11, 1, 0, 0, 0, DateTimeKind.Utc);

    /// <summary>
    /// One block every 8 s; half of them hold one transaction (a native transfer when h % 4 == 0, a
    /// small contract call when h % 4 == 1), about 5,400 a day, near mainnet's order of magnitude.
    /// </summary>
    private sealed class FakeChain
    {
        private readonly object _lock = new();
        public ulong Tip;
        public int BlockCalls;
        public int DeployCalls;

        public FakeChain(ulong tip) => Tip = tip;

        public static DateTime TimeOf(ulong h) => Genesis.AddSeconds(8 * (double)h);

        public static bool HasTransaction(ulong h) => h % 4 <= 1;

        public static BlockData Block(ulong h) => new()
        {
            BlockHeight = h,
            Timestamp = TimeOf(h),
            NativeTransfersNumber = (ushort)(h % 4 == 0 ? 1 : 0),
            AuctionTxnNumber = 0,
            InstallUpgradeTxnNumber = 0,
            SmallTxnNumber = (ushort)(h % 4 == 1 ? 1 : 0),
            MediumTxnNumber = 0,
            LargeTxnNumber = 0,
            ContractCallsNumber = (ushort)(h % 4 == 1 ? 1 : 0),
        };

        /// <summary>The transaction in block h: sent up to 6 minutes before it, by one of five senders.</summary>
        public static DeployData Deploy(ulong h) => new()
        {
            DeployHash = $"d{h}",
            BlockHeight = h,
            Timestamp = TimeOf(h).AddMinutes(-(double)(h % 7)),
            CallerHash = $"c{h % 5}",
            Cost = "1000000000",
            RefundAmount = h % 8 == 1 ? "250000000" : null,
            ErrorMessage = h % 100 == 0 ? "User error: 1" : null,
        };

        public Func<int, Task>? BeforeBlockPage;
        public Action<int>? AfterBlockPage;

        public async Task<PaginatedResponse<BlockData>?> Blocks(int page, CancellationToken ct)
        {
            if (BeforeBlockPage is not null) await BeforeBlockPage(page);
            ulong tip;
            lock (_lock) { BlockCalls++; tip = Tip; }
            var rows = new List<BlockData>();
            for (var i = 0; i < 250; i++)
            {
                var index = (ulong)((page - 1) * 250 + i);
                if (index > tip) break;
                rows.Add(Block(tip - index));
            }
            AfterBlockPage?.Invoke(page);
            return new PaginatedResponse<BlockData> { Data = rows, ItemCount = (int)tip + 1 };
        }

        public Task<PaginatedResponse<DeployData>?> Deploys(int page, CancellationToken ct)
        {
            ulong tip;
            lock (_lock) { DeployCalls++; tip = Tip; }
            // Sorted by the transaction's own time, newest first, as /deploys is.
            var all = Enumerable.Range(0, (int)Math.Min(tip + 1, 20_000))
                .Select(i => tip - (ulong)i)
                .Where(HasTransaction)
                .Select(Deploy)
                .OrderByDescending(d => d.Timestamp)
                .ToList();
            var rows = all.Skip((page - 1) * 250).Take(250).ToList();
            return Task.FromResult<PaginatedResponse<DeployData>?>(new PaginatedResponse<DeployData> { Data = rows, ItemCount = all.Count });
        }
    }

    private static Task NoDelay(TimeSpan _, CancellationToken __) => Task.CompletedTask;

    private static (long native, long small, long calls, int failed, BigInteger fees, int senders) Expected(ulong lowest, ulong highest)
    {
        long native = 0, small = 0, calls = 0; var failed = 0; BigInteger fees = 0; var senders = new HashSet<string>();
        for (var h = lowest; h <= highest; h++)
        {
            var b = FakeChain.Block(h);
            native += b.NativeTransfersNumber!.Value; small += b.SmallTxnNumber!.Value; calls += b.ContractCallsNumber!.Value;
            if (!FakeChain.HasTransaction(h)) continue;
            var d = FakeChain.Deploy(h);
            if (d.ErrorMessage is not null) failed++;
            fees += BigInteger.Parse(d.Cost) - (d.RefundAmount is null ? 0 : BigInteger.Parse(d.RefundAmount));
            senders.Add(d.CallerHash);
        }
        return (native, small, calls, failed, fees, senders.Count);
    }

    private static void AssertExact(NetworkActivityReport r, ulong lowest, ulong highest)
    {
        var e = Expected(lowest, highest);
        Assert.Equal(lowest, r.LowestHeight);
        Assert.Equal(highest, r.HighestHeight);
        Assert.Equal((int)(highest - lowest + 1), r.BlockCount);
        Assert.Equal(0, r.MissingBlocks);
        Assert.Equal(e.native, r.NativeTransfers);
        Assert.Equal(e.small, r.Small);
        Assert.Equal(e.native + e.small, r.Transactions);
        Assert.Equal(e.calls, r.ContractCalls);
        Assert.Equal(r.Transactions, r.TransactionRows);
        Assert.Equal(e.failed, r.Failed);
        Assert.Equal(e.fees, r.FeesMotes);
        Assert.Equal(e.senders, r.DistinctSenders);
        Assert.False(r.BlockPagesCapped);
        Assert.False(r.TransactionPagesCapped);
    }

    [Fact]
    public async Task One_Hour_Counts_Exactly_The_Blocks_Newer_Than_The_Cutoff()
    {
        var chain = new FakeChain(tip: 100_000);

        var r = await NetworkActivity.ComputeAsync(chain.Blocks, chain.Deploys, hours: 1, delay: NoDelay);

        // 8 s blocks: the 450 blocks whose time is within the hour before the tip's.
        Assert.NotNull(r);
        AssertExact(r!, 100_000 - 449, 100_000);
        Assert.Equal(FakeChain.TimeOf(100_000), r!.LatestBlockTime);
        Assert.Equal(FakeChain.TimeOf(100_000 - 449), r.EarliestBlockTime);
    }

    [Fact]
    public async Task Twenty_Four_Hours_Reads_About_44_Block_Pages_In_Parallel()
    {
        var chain = new FakeChain(tip: 100_000);

        var r = await NetworkActivity.ComputeAsync(chain.Blocks, chain.Deploys, hours: 24, delay: NoDelay);

        AssertExact(r!, 100_000 - 10_799, 100_000);
        Assert.InRange(chain.BlockCalls, 44, 46);
        Assert.Equal(r!.Requests, chain.BlockCalls + chain.DeployCalls);
    }

    [Fact]
    public async Task Hours_Are_Clamped_To_One_Through_Twenty_Four()
    {
        Assert.Equal(1, NetworkActivity.ClampHours(0));
        Assert.Equal(24, NetworkActivity.ClampHours(48));

        var chain = new FakeChain(tip: 100_000);
        var r = await NetworkActivity.ComputeAsync(chain.Blocks, chain.Deploys, hours: 72, delay: NoDelay);
        Assert.Equal(24, r!.Hours);
        Assert.Equal(10_800, r.BlockCount);
    }

    [Fact]
    public async Task Blocks_Added_While_Reading_Neither_Repeat_Nor_Enter_The_Window()
    {
        // A block lands after every block page: later pages are shifted, repeating rows, and the new
        // blocks sit above the height the first page anchored.
        var chain = new FakeChain(tip: 100_000);
        chain.AfterBlockPage = _ => Interlocked.Increment(ref chain.Tip);

        var r = await NetworkActivity.ComputeAsync(chain.Blocks, chain.Deploys, hours: 2, delay: NoDelay);

        AssertExact(r!, 100_000 - 899, 100_000);
    }

    /// <summary>Page 3 is served, then five blocks land, then page 2: page 2 now ends five heights above where page 3 begins.</summary>
    private static FakeChain ChainThatServesPage3BeforePage2()
    {
        var chain = new FakeChain(tip: 100_000);
        var page3Served = new TaskCompletionSource();
        chain.BeforeBlockPage = page => page == 2 && !page3Served.Task.IsCompleted ? page3Served.Task : Task.CompletedTask;
        chain.AfterBlockPage = page =>
        {
            if (page == 3 && !page3Served.Task.IsCompleted)
            {
                chain.Tip += 5;
                page3Served.SetResult();
            }
        };
        return chain;
    }

    [Fact]
    public async Task A_Higher_Page_Read_Before_A_Lower_One_Leaves_A_Gap_That_Is_Repaired()
    {
        var unrepaired = ChainThatServesPage3BeforePage2();
        var gap = await NetworkActivity.ComputeAsync(unrepaired.Blocks, unrepaired.Deploys, hours: 2,
            limits: new NetworkActivityLimits { RepairRounds = 0 }, delay: NoDelay);
        Assert.Equal(5, gap!.MissingBlocks);
        Assert.Contains("5 blocks inside the window could not be read", NetworkActivity.Format(gap, isTestnet: false));

        var chain = ChainThatServesPage3BeforePage2();
        var r = await NetworkActivity.ComputeAsync(chain.Blocks, chain.Deploys, hours: 2, delay: NoDelay);

        AssertExact(r!, 100_000 - 899, 100_000);
    }

    [Fact]
    public async Task A_Rate_Limited_Page_Is_Retried_With_Backoff()
    {
        var chain = new FakeChain(tip: 100_000);
        var failures = 2;
        var waits = new List<TimeSpan>();
        Task<PaginatedResponse<BlockData>?> Flaky(int page, CancellationToken ct)
        {
            if (page == 1 && failures-- > 0) throw new RateLimitException("Too many requests", null);
            return chain.Blocks(page, ct);
        }

        var r = await NetworkActivity.ComputeAsync(Flaky, chain.Deploys, hours: 1,
            delay: (wait, _) => { waits.Add(wait); return Task.CompletedTask; });

        AssertExact(r!, 100_000 - 449, 100_000);
        Assert.Equal(new[] { TimeSpan.FromMilliseconds(500), TimeSpan.FromSeconds(1) }, waits);
        Assert.Equal(chain.BlockCalls + chain.DeployCalls + 2, r!.Requests);
    }

    [Fact]
    public async Task The_Transaction_Read_Says_When_It_Hit_Its_Page_Limit()
    {
        var chain = new FakeChain(tip: 100_000);

        var r = await NetworkActivity.ComputeAsync(chain.Blocks, chain.Deploys, hours: 24,
            limits: new NetworkActivityLimits { MaxDeployPages = 3 }, delay: NoDelay);

        Assert.True(r!.TransactionPagesCapped);
        Assert.Equal(5_400, r.Transactions);
        Assert.Equal(750, r.TransactionRows);
        Assert.Contains("Failures, fees and senders cover 750 of the 5,400 transactions (the transaction read stopped at its page limit).",
            NetworkActivity.Format(r, isTestnet: false));
    }

    [Fact]
    public async Task An_Empty_Chain_Returns_Nothing()
    {
        Task<PaginatedResponse<BlockData>?> None(int page, CancellationToken ct) =>
            Task.FromResult<PaginatedResponse<BlockData>?>(new PaginatedResponse<BlockData> { Data = [] });

        Assert.Null(await NetworkActivity.ComputeAsync(None, new FakeChain(0).Deploys, hours: 24, delay: NoDelay));
    }

    [Fact]
    public void Format_Labels_Every_Figure_And_Keeps_Contract_Calls_Out_Of_The_Total()
    {
        var r = new NetworkActivityReport
        {
            Hours = 24, LowestHeight = 9_551_986, HighestHeight = 9_562_786, BlockCount = 10_801,
            EarliestBlockTime = new DateTime(2026, 11, 11, 7, 37, 42, DateTimeKind.Utc),
            LatestBlockTime = new DateTime(2026, 11, 12, 7, 37, 42, DateTimeKind.Utc),
            NativeTransfers = 400, Auction = 20, InstallUpgrade = 1_100, Small = 572,
            ContractCalls = 1_660, TransactionRows = 2_092, FeeRows = 2_092, Failed = 3,
            FeesMotes = BigInteger.Parse("10930262050560"), DistinctSenders = 26, Requests = 55,
        };

        var text = NetworkActivity.Format(r, isTestnet: false);

        Assert.Contains("## Network Activity (Mainnet, last 24 hours)", text);
        Assert.Contains("- **Window:** blocks 9,551,986 to 9,562,786 (10,801 blocks), 2026-11-11 07:37:42 UTC to 2026-11-12 07:37:42 UTC", text);
        Assert.Contains("- **Average Block Time:** 8.00 s", text);
        Assert.Contains("- **Transactions:** 2,092", text);
        Assert.Contains("  Native Transfers: 400 | Auction: 20 | Install/Upgrade: 1,100 | Small: 572 | Medium: 0 | Large: 0", text);
        Assert.Contains("- **Contract Calls:** 1,660 (CSPR.cloud's own per-block count; it overlaps the categories above and is not part of the total)", text);
        Assert.Contains("- **Failed Transactions:** 3", text);
        Assert.Contains("- **Fees Paid (cost minus refund):** 10,930.262050560 CSPR", text);
        Assert.Contains("- **Distinct Senders:** 26", text);
        Assert.Contains("This tool covers at most the last 24 hours. Read from 55 CSPR.cloud pages.", text);
        Assert.DoesNotContain("cover", text.Split("---")[1].Split("Figures are")[0]);
        Assert.DoesNotContain("burn", text, StringComparison.OrdinalIgnoreCase);
    }
}
