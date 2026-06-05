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
    private static TransactionIntent Undelegate(BigInteger motes, string val) =>
        new(WriteKind.Undelegate, Signer, val, null, motes, "casper-test");
    private static TransactionIntent Redelegate(BigInteger motes, string fromVal, string toVal) =>
        new(WriteKind.Redelegate, Signer, fromVal, toVal, motes, "casper-test");

    private static WritePolicy Policy(bool mainnet = false, decimal transferPerTx = 100, decimal transferPerDay = 500,
        decimal stakePerTx = 100, string[]? recips = null, string[]? vals = null) =>
        new(mainnet, transferPerTx, transferPerDay, stakePerTx,
            new HashSet<string>(recips ?? new[] { RecipPk }, StringComparer.OrdinalIgnoreCase),
            new HashSet<string>(vals ?? new[] { ValPk }, StringComparer.OrdinalIgnoreCase));

    private static PolicyDecision Eval(TransactionIntent i, WritePolicy p, ISpendLedger? led = null) =>
        PolicyEngine.Evaluate(i, p, led ?? new InMemorySpendLedger(() => new DateOnly(2026, 6, 4)), Signer);

    // ---- Transfers: recipient allowlist + tight transfer caps ----

    [Fact] public void Allows_Transfer_To_Allowlisted_Recipient_Within_Caps() =>
        Assert.True(Eval(Transfer(10 * Cspr), Policy()).Allowed);

    [Fact] public void Blocks_Transfer_To_NonAllowlisted_Recipient()
    {
        var d = Eval(Transfer(10 * Cspr, to: "01attacker"), Policy());
        Assert.False(d.Allowed);
        Assert.Contains("allowlist", d.Reason, StringComparison.OrdinalIgnoreCase);
    }

    [Fact] public void Blocks_Transfer_Over_Transfer_PerTx_Cap()
    {
        var d = Eval(Transfer(101 * Cspr), Policy(transferPerTx: 100));
        Assert.False(d.Allowed);
        Assert.Contains("per-transaction", d.Reason, StringComparison.OrdinalIgnoreCase);
    }

    [Fact] public void Blocks_Over_Daily_Cap_With_Prior_Spend()
    {
        var led = new InMemorySpendLedger(() => new DateOnly(2026, 6, 4));
        led.Record(Signer, 450 * Cspr);
        var d = Eval(Transfer(60 * Cspr), Policy(transferPerTx: 100, transferPerDay: 500), led); // 450+60 > 500
        Assert.False(d.Allowed);
        Assert.Contains("daily", d.Reason, StringComparison.OrdinalIgnoreCase);
    }

    [Fact] public void Empty_Recipient_Allowlist_Blocks_All_Transfers() =>
        Assert.False(Eval(Transfer(1 * Cspr), Policy(recips: Array.Empty<string>())).Allowed);

    [Fact] public void Fractional_Transfer_Cap_Is_Not_Truncated()
    {
        var p = Policy(transferPerTx: 2.5m, transferPerDay: 100m);
        Assert.True(Eval(Transfer(new BigInteger(2_500_000_000)), p).Allowed);   // 2.5 CSPR ok
        Assert.False(Eval(Transfer(new BigInteger(2_600_000_000)), p).Allowed);  // 2.6 CSPR blocked
    }

    // ---- Global gates ----

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

    // ---- Staking: validator allowlist + a SEPARATE stake cap (decoupled from transfers) ----

    [Fact] public void Allows_Delegate_To_Allowlisted_Validator() =>
        Assert.True(Eval(Delegate(50 * Cspr), Policy()).Allowed);

    [Fact] public void Blocks_Delegate_To_NonAllowlisted_Validator()
    {
        var d = Eval(Delegate(50 * Cspr, val: "01rogue"), Policy());
        Assert.False(d.Allowed);
        Assert.Contains("validator", d.Reason, StringComparison.OrdinalIgnoreCase);
    }

    [Fact] public void Blocks_Delegate_Over_Stake_Cap()
    {
        var d = Eval(Delegate(200 * Cspr), Policy(stakePerTx: 100));
        Assert.False(d.Allowed);
        Assert.Contains("stake", d.Reason, StringComparison.OrdinalIgnoreCase);
    }

    [Fact] public void Stake_Cap_Is_Independent_From_Transfer_Cap()
    {
        // The whole point: a tight transfer cap, a large stake cap. A 500-CSPR delegate is allowed
        // while a 50-CSPR transfer is blocked — without loosening the transfer guardrail.
        var p = Policy(transferPerTx: 10, transferPerDay: 10, stakePerTx: 1000);
        Assert.True(Eval(Delegate(500 * Cspr), p).Allowed);
        Assert.False(Eval(Transfer(50 * Cspr), p).Allowed);
    }

    [Fact] public void Staking_Does_Not_Consume_Daily_Transfer_Cap()
    {
        var led = new InMemorySpendLedger(() => new DateOnly(2026, 6, 4));
        led.Record(Signer, 480 * Cspr);
        // a 50 delegate would exceed the transfer daily cap if it counted; it must not (≤ stake cap 100)
        Assert.True(Eval(Delegate(50 * Cspr), Policy(transferPerTx: 100, transferPerDay: 500, stakePerTx: 100), led).Allowed);
    }

    // ---- Undelegate: pure recovery of your own funds — uncapped, not allowlist-gated ----

    [Fact] public void Undelegate_Is_Uncapped_And_Not_Validator_Allowlist_Gated()
    {
        // Huge amount, from a validator NOT on the allowlist → still allowed (returns your own funds).
        var p = Policy(stakePerTx: 1, vals: Array.Empty<string>());
        Assert.True(Eval(Undelegate(5_000_000 * Cspr, val: "01delisted"), p).Allowed);
    }

    // ---- Redelegate: gate the DESTINATION (where stake lands) ----

    [Fact] public void Redelegate_Allows_To_Allowlisted_Destination_From_Any_Source()
    {
        // Source not on the allowlist, destination is → allowed (you may move stake to a trusted validator).
        var p = Policy(stakePerTx: 1000, vals: new[] { "01dest" });
        Assert.True(Eval(Redelegate(500 * Cspr, fromVal: "01anysource", toVal: "01dest"), p).Allowed);
    }

    [Fact] public void Redelegate_Blocks_NonAllowlisted_Destination()
    {
        var p = Policy(stakePerTx: 1000, vals: new[] { "01dest" });
        var d = Eval(Redelegate(500 * Cspr, fromVal: "01dest", toVal: "01rogue"), p);
        Assert.False(d.Allowed);
        Assert.Contains("destination", d.Reason, StringComparison.OrdinalIgnoreCase);
    }
}
