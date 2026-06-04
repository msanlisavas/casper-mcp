using System.Net.Http;
using Casper.Network.SDK;
using Casper.Network.SDK.Types;
using CasperMcp.Configuration;

namespace CasperMcp.Writes;

/// <summary>Builds a fully-wired CasperSigner from validated write config (key, policy, ledger, audit, submit).</summary>
public static class SignerFactory
{
    public static CasperSigner Create(ServerConfig config)
    {
        var keyPair = KeyPair.FromPem(config.KeyPath);
        bool testnet = !string.Equals(config.DefaultNetwork, "mainnet", StringComparison.OrdinalIgnoreCase);
        string chainName = testnet ? "casper-test" : "casper";

        var baseDir = string.IsNullOrWhiteSpace(config.PolicyPath)
            ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".casper-mcp")
            : Path.GetDirectoryName(Path.GetFullPath(config.PolicyPath))!;
        Directory.CreateDirectory(baseDir);

        var policyPath = string.IsNullOrWhiteSpace(config.PolicyPath) ? Path.Combine(baseDir, "policy.json") : config.PolicyPath;
        var policy = WritePolicy.Load(policyPath, Environment.GetEnvironmentVariable);

        var ledger = new FileSpendLedger(Path.Combine(baseDir, "spend-ledger.json"), () => DateOnly.FromDateTime(DateTime.UtcNow));
        var audit = new WriteAuditLog(Path.Combine(baseDir, "audit.log"), () => DateTime.UtcNow);

        // Submit node: pinned per network (agent cannot override → anti-SSRF). CSPR.Cloud node by
        // default, reusing the stdio CSPR.Cloud key as the Authorization header.
        var nodeUrl = !string.IsNullOrWhiteSpace(config.NodeRpcUrl)
            ? config.NodeRpcUrl
            : (testnet ? "https://node.testnet.cspr.cloud/rpc" : "https://node.cspr.cloud/rpc");
        var http = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
        if (!string.IsNullOrWhiteSpace(config.StdioApiKey))
            http.DefaultRequestHeaders.TryAddWithoutValidation("Authorization", config.StdioApiKey);
        var client = new NetCasperClient(nodeUrl, http);

        // Submit via node RPC. txn.Hash is the deterministic transaction hash (set at build time),
        // so we return it directly rather than depending on the PutTransaction result shape.
        return new CasperSigner(keyPair, chainName, policy, ledger, audit,
            submit: async txn => { await client.PutTransaction(txn); return txn.Hash; });
    }
}
