using Casper.Network.SDK.Types;
using CasperMcp.Writes;

namespace CasperMcp.Tests;

public class CasperSignerTests
{
    private static (CasperSigner signer, KeyPair kp, List<Transaction> submitted, string auditPath) Make(
        WritePolicy policy, ISpendLedger? ledger = null)
    {
        var kp = KeyPair.CreateNew(KeyAlgo.ED25519);
        var submitted = new List<Transaction>();
        var auditPath = Path.Combine(Path.GetTempPath(), "audit-" + Guid.NewGuid().ToString("n") + ".log");
        var audit = new WriteAuditLog(auditPath, () => new DateTime(2026, 6, 4, 0, 0, 0, DateTimeKind.Utc));
        var signer = new CasperSigner(
            kp, chainName: "casper-test", policy,
            ledger ?? new InMemorySpendLedger(() => new DateOnly(2026, 6, 4)), audit,
            submit: txn => { submitted.Add(txn); return Task.FromResult("submitted-hash-" + txn.Hash); });
        return (signer, kp, submitted, auditPath);
    }

    private static WritePolicy AllowTo(string recipientHex) =>
        new(false, 100m, 500m, new HashSet<string>(new[] { recipientHex.ToLowerInvariant() }, StringComparer.OrdinalIgnoreCase),
            new HashSet<string>(StringComparer.OrdinalIgnoreCase));

    [Fact]
    public async Task Allowed_Transfer_Signs_Submits_And_Records()
    {
        var recipient = KeyPair.CreateNew(KeyAlgo.ED25519).PublicKey.ToString();
        var (signer, _, submitted, _) = Make(AllowTo(recipient));
        var (json, _) = signer.BuildTransfer(recipient, 10m);

        var result = await signer.SignAndSubmit(json, "corr-1");

        Assert.Single(submitted);
        Assert.Single(submitted[0].Approvals);                 // it was signed
        Assert.Contains("submitted", result, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("signature", result, StringComparison.OrdinalIgnoreCase); // never returns the signature
    }

    [Fact]
    public async Task Denied_Transfer_Does_Not_Submit_And_Returns_Reason()
    {
        var recipient = KeyPair.CreateNew(KeyAlgo.ED25519).PublicKey.ToString();
        var (signer, _, submitted, _) = Make(AllowTo("01differentrecipient"));
        var (json, _) = signer.BuildTransfer(recipient, 10m);

        var result = await signer.SignAndSubmit(json, "corr-2");

        Assert.Empty(submitted);
        Assert.Contains("allowlist", result, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Allowed_Transfer_Debits_Daily_Ledger()
    {
        var recipient = KeyPair.CreateNew(KeyAlgo.ED25519).PublicKey.ToString();
        var ledger = new InMemorySpendLedger(() => new DateOnly(2026, 6, 4));
        var (signer, kp, _, _) = Make(AllowTo(recipient), ledger);
        var (json, _) = signer.BuildTransfer(recipient, 10m);
        await signer.SignAndSubmit(json, "corr-3");
        Assert.Equal(PolicyEngine.MotesPerCspr * 10, ledger.TodaySpentMotes(kp.PublicKey.ToString().ToLowerInvariant()));
    }

    [Fact]
    public async Task Undecodable_Json_Is_Refused_Not_Thrown()
    {
        var (signer, _, submitted, _) = Make(AllowTo("01x"));
        var result = await signer.SignAndSubmit("garbage", "corr-4");
        Assert.Empty(submitted);
        Assert.Contains("could not", result, StringComparison.OrdinalIgnoreCase);
    }
}
