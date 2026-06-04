using Casper.Network.SDK;
using Casper.Network.SDK.Types;
using CasperMcp.Writes;

namespace CasperMcp.Tests;

[Collection("Integration")]
public class SignerIntegrationTests
{
    private static string? SignerPem => Environment.GetEnvironmentVariable("CASPER_MCP_TEST_SIGNER_PEM");
    private static readonly string NodeUrl =
        Environment.GetEnvironmentVariable("CASPER_MCP_TEST_NODE_URL") ?? "https://node.testnet.cspr.cloud/rpc";

    private static CasperSigner BuildSigner(KeyPair kp, WritePolicy policy, out List<Transaction> submitted)
    {
        var captured = new List<Transaction>();
        submitted = captured;
        var audit = new WriteAuditLog(Path.Combine(Path.GetTempPath(), "audit-" + Guid.NewGuid().ToString("n") + ".log"), () => DateTime.UtcNow);
        var ledger = new InMemorySpendLedger(() => DateOnly.FromDateTime(DateTime.UtcNow));
        return new CasperSigner(kp, "casper-test", policy, ledger, audit,
            submit: txn => { captured.Add(txn); return Task.FromResult("captured"); });
    }

    [Fact]
    public void Builds_And_Signs_Transfer_Offline_Always()
    {
        // No funds required: prove build→policy→sign works against a fresh key (submit is captured).
        var kp = KeyPair.CreateNew(KeyAlgo.ED25519);
        var recipient = KeyPair.CreateNew(KeyAlgo.ED25519).PublicKey.ToString();
        var policy = new WritePolicy(false, 100m, 500m,
            new HashSet<string>(new[] { recipient.ToLowerInvariant() }, StringComparer.OrdinalIgnoreCase),
            new HashSet<string>(StringComparer.OrdinalIgnoreCase));
        var signer = BuildSigner(kp, policy, out var submitted);
        var (json, _) = signer.BuildTransfer(recipient, 1m);
        var result = signer.SignAndSubmit(json, "it-1").GetAwaiter().GetResult();
        Assert.Single(submitted);
        Assert.Single(submitted[0].Approvals);
        Assert.Contains("submitted", result, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Submits_Real_Transfer_To_Testnet()
    {
        // No funded key set — no-op pass. Set CASPER_MCP_TEST_SIGNER_PEM to a funded testnet PEM to run for real.
        if (string.IsNullOrWhiteSpace(SignerPem))
            return;

        var kp = KeyPair.FromPem(SignerPem!);
        var recipient = kp.PublicKey.ToString(); // self-transfer keeps funds; still a real on-chain tx
        var policy = new WritePolicy(false, 100m, 500m,
            new HashSet<string>(new[] { recipient.ToLowerInvariant() }, StringComparer.OrdinalIgnoreCase),
            new HashSet<string>(StringComparer.OrdinalIgnoreCase));

        var http = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
        if (Environment.GetEnvironmentVariable("CSPR_CLOUD_TESTNET_API_KEY") is { Length: > 0 } key)
            http.DefaultRequestHeaders.TryAddWithoutValidation("Authorization", key);
        var client = new NetCasperClient(NodeUrl, http);

        var audit = new WriteAuditLog(Path.Combine(Path.GetTempPath(), "audit-it.log"), () => DateTime.UtcNow);
        var signer = new CasperSigner(kp, "casper-test", policy,
            new InMemorySpendLedger(() => DateOnly.FromDateTime(DateTime.UtcNow)), audit,
            submit: async txn => { await client.PutTransaction(txn); return txn.Hash; });

        var (json, _) = signer.BuildTransfer(recipient, 1m);
        var result = await signer.SignAndSubmit(json, "it-real");
        Assert.Contains("submitted", result, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Refused", result);
    }

    [Fact]
    public void Refuses_Over_Cap_Transfer()
    {
        var kp = KeyPair.CreateNew(KeyAlgo.ED25519);
        var recipient = KeyPair.CreateNew(KeyAlgo.ED25519).PublicKey.ToString();
        var policy = new WritePolicy(false, 10m, 50m,
            new HashSet<string>(new[] { recipient.ToLowerInvariant() }, StringComparer.OrdinalIgnoreCase),
            new HashSet<string>(StringComparer.OrdinalIgnoreCase));
        var signer = BuildSigner(kp, policy, out var submitted);
        var (json, _) = signer.BuildTransfer(recipient, 25m); // > per-tx 10
        var result = signer.SignAndSubmit(json, "it-cap").GetAwaiter().GetResult();
        Assert.Empty(submitted);
        Assert.Contains("per-transaction", result, StringComparison.OrdinalIgnoreCase);
    }
}
