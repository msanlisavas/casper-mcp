using System.Text.Json;

namespace CasperMcp.Writes;

/// <summary>
/// Immutable, human-owned write policy. Loaded ONCE at startup; the agent has no tool to read,
/// edit, or reload it. Fail-closed: a missing/invalid file collapses to the strict default
/// (testnet-only, empty allowlists, conservative caps) — never to a permissive state.
/// Allowlist entries are stored verbatim (lowercased/trimmed); normalization to account hashes
/// for matching happens in PolicyEngine.
/// </summary>
public sealed record WritePolicy(
    bool MainnetEnabled,
    decimal PerTxCspr,
    decimal PerDayCspr,
    IReadOnlySet<string> AllowRecipients,
    IReadOnlySet<string> AllowValidators)
{
    public static WritePolicy StrictDefault() => new(
        MainnetEnabled: false, PerTxCspr: 100m, PerDayCspr: 500m,
        AllowRecipients: new HashSet<string>(), AllowValidators: new HashSet<string>());

    public static WritePolicy Load(string? policyFilePath, Func<string, string?> getEnv)
    {
        var p = StrictDefault();

        // 1) File (fail-closed: any parse error keeps the strict default).
        if (!string.IsNullOrWhiteSpace(policyFilePath) && File.Exists(policyFilePath))
        {
            try
            {
                using var doc = JsonDocument.Parse(File.ReadAllText(policyFilePath));
                var root = doc.RootElement;
                bool mainnet = root.TryGetProperty("mainnet_enabled", out var me) && me.ValueKind == JsonValueKind.True;
                decimal perTx = ReadDecimal(root, "caps", "per_tx_cspr", p.PerTxCspr);
                decimal perDay = ReadDecimal(root, "caps", "per_day_cspr", p.PerDayCspr);
                var recips = ReadList(root, "allowlist", "recipients");
                var vals = ReadList(root, "allowlist", "validators");
                p = p with { MainnetEnabled = mainnet, PerTxCspr = perTx, PerDayCspr = perDay,
                             AllowRecipients = recips, AllowValidators = vals };
            }
            catch { p = StrictDefault(); }
        }

        // 2) Env overrides (each wins over file/default when present).
        if (getEnv("CASPER_MCP_MAINNET_ENABLED") is { } meEnv)
            p = p with { MainnetEnabled = meEnv is "1" or "true" or "True" };
        if (TryDecimal(getEnv("CASPER_MCP_PER_TX_CSPR"), out var tx)) p = p with { PerTxCspr = tx };
        if (TryDecimal(getEnv("CASPER_MCP_PER_DAY_CSPR"), out var day)) p = p with { PerDayCspr = day };
        if (getEnv("CASPER_MCP_ALLOW_RECIPIENTS") is { } rEnv)
            p = p with { AllowRecipients = SplitSet(rEnv) };
        if (getEnv("CASPER_MCP_ALLOW_VALIDATORS") is { } vEnv)
            p = p with { AllowValidators = SplitSet(vEnv) };

        return p;
    }

    private static decimal ReadDecimal(JsonElement root, string obj, string key, decimal fallback) =>
        root.TryGetProperty(obj, out var o) && o.TryGetProperty(key, out var v) && v.TryGetDecimal(out var d) ? d : fallback;

    private static IReadOnlySet<string> ReadList(JsonElement root, string obj, string key)
    {
        var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (root.TryGetProperty(obj, out var o) && o.TryGetProperty(key, out var arr) && arr.ValueKind == JsonValueKind.Array)
            foreach (var e in arr.EnumerateArray())
                if (e.GetString() is { Length: > 0 } s) set.Add(s.Trim().ToLowerInvariant());
        return set;
    }

    private static IReadOnlySet<string> SplitSet(string csv)
    {
        var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var part in csv.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            set.Add(part.ToLowerInvariant());
        return set;
    }

    private static bool TryDecimal(string? s, out decimal value) =>
        decimal.TryParse(s, System.Globalization.NumberStyles.Number,
            System.Globalization.CultureInfo.InvariantCulture, out value);
}
