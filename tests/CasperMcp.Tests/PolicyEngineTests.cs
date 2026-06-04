using System.Numerics;
using CasperMcp.Writes;

namespace CasperMcp.Tests;

public class PolicyEngineTests
{
    private const string Signer = "01signerpk";
    private const string RecipPk = "01recipientpk";
    private const string ValPk = "01validatorpk";
    private static readonly BigInteger Cspr = new(1_000_000_000);

    private static TransactionIntent Transfer(BigInteger motes, string to = RecipPk, string chain = "casper-test") =>
        new(WriteKind.Transfer, Signer, to, null, motes, chain);
    private static TransactionIntent Delegate(BigInteger motes, string val = ValPk) =>
        new(WriteKind.Delegate, Signer, val, null, motes, "casper-test");

    private static WritePolicy Policy(bool mainnet = false, decimal perTx = 100, decimal perDay = 500,
        string[]? recips = null, string[]? vals = null) =>
        new(mainnet, perTx, perDay,
            new HashSet<string>(recips ?? new[] { RecipPk }, StringComparer.OrdinalIgnoreCase),
            new HashSet<string>(vals ?? new[] { ValPk }, StringComparer.OrdinalIgnoreCase));

    private static PolicyDecision Eval(TransactionIntent i, WritePolicy p, ISpendLedger? led = null) =>
        PolicyEngine.Evaluate(i, p, led ?? new InMemorySpendLedger(() => new DateOnly(2026, 6, 4)), Signer);

    [Fact] public void Allows_Transfer_To_Allowlisted_Recipient_Within_Caps() =>
        Assert.True(Eval(Transfer(10 * Cspr), Policy()).Allowed);

    [Fact] public void Blocks_Transfer_To_NonAllowlisted_Recipient()
    {
        var d = Eval(Transfer(10 * Cspr, to: "01attacker"), Policy());
        Assert.False(d.Allowed);
        Assert.Contains("allowlist", d.Reason, StringComparison.OrdinalIgnoreCase);
    }

    [Fact] public void Blocks_When_Over_PerTx_Cap()
    {
        var d = Eval(Transfer(101 * Cspr), Policy(perTx: 100));
        Assert.False(d.Allowed);
        Assert.Contains("per-transaction", d.Reason, StringComparison.OrdinalIgnoreCase);
    }

    [Fact] public void Blocks_When_Over_Daily_Cap_With_Prior_Spend()
    {
        var led = new InMemorySpendLedger(() => new DateOnly(2026, 6, 4));
        led.Record(Signer, 450 * Cspr);
        var d = Eval(Transfer(60 * Cspr), Policy(perTx: 100, perDay: 500), led); // 450+60 > 500
        Assert.False(d.Allowed);
        Assert.Contains("daily", d.Reason, StringComparison.OrdinalIgnoreCase);
    }

    [Fact] public void Blocks_Mainnet_When_Disabled()
    {
        var d = Eval(Transfer(1 * Cspr, chain: "casper"), Policy(mainnet: false));
        Assert.False(d.Allowed);
        Assert.Contains("mainnet", d.Reason, StringComparison.OrdinalIgnoreCase);
    }

    [Fact] public void Blocks_Sender_Mismatch()
    {
        var foreign = new TransactionIntent(WriteKind.Transfer, "01someoneelse", RecipPk, null, 1 * Cspr, "casper-test");
        var d = Eval(foreign, Policy());
        Assert.False(d.Allowed);
        Assert.Contains("sender", d.Reason, StringComparison.OrdinalIgnoreCase);
    }

    [Fact] public void Allows_Delegate_To_Allowlisted_Validator() =>
        Assert.True(Eval(Delegate(50 * Cspr), Policy()).Allowed);

    [Fact] public void Blocks_Delegate_To_NonAllowlisted_Validator()
    {
        var d = Eval(Delegate(50 * Cspr, val: "01rogue"), Policy());
        Assert.False(d.Allowed);
        Assert.Contains("validator", d.Reason, StringComparison.OrdinalIgnoreCase);
    }

    [Fact] public void Staking_Does_Not_Consume_Daily_Cap()
    {
        var led = new InMemorySpendLedger(() => new DateOnly(2026, 6, 4));
        led.Record(Signer, 480 * Cspr);
        // delegate of 50 would exceed daily if counted; it must NOT be (still ≤ per-tx 100)
        Assert.True(Eval(Delegate(50 * Cspr), Policy(perTx: 100, perDay: 500), led).Allowed);
    }

    [Fact] public void Empty_Allowlist_Blocks_All()
    {
        var d = Eval(Transfer(1 * Cspr), Policy(recips: Array.Empty<string>()));
        Assert.False(d.Allowed);
    }

    [Fact]
    public void Fractional_PerTx_Cap_Is_Not_Truncated()
    {
        // 2.5 CSPR cap must allow exactly 2.5 CSPR and block 2.6 CSPR.
        var p = Policy(perTx: 2.5m, perDay: 100m);
        Assert.True(Eval(Transfer(new BigInteger(2_500_000_000)), p).Allowed);   // 2.5 CSPR ok
        Assert.False(Eval(Transfer(new BigInteger(2_600_000_000)), p).Allowed);  // 2.6 CSPR blocked
    }
}
