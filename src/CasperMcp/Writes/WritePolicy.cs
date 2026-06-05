using System.Text.Json;

namespace CasperMcp.Writes;

/// <summary>
/// Immutable, human-owned write policy. Loaded ONCE at startup; the agent has no tool to read,
/// edit, or reload it. Fail-closed: a missing/invalid file collapses to the strict default
/// (testnet-only, empty allowlists, conservative caps) — never to a permissive state.
///
/// Transfer and stake limits are SEPARATE on purpose. A transfer is irreversible outflow to a third
/// party, so it gets a tight per-tx + daily cap. Staking is not outflow — the funds stay yours and
/// return via undelegate — so it is gated primarily by the validator allowlist with its own
/// independent per-tx sanity bound. This decoupling means a tight transfer cap never has to be
/// loosened just to stake a large amount.
///
/// Allowlist entries are stored lowercased/trimmed; matching happens in PolicyEngine.
/// </summary>
public sealed record WritePolicy(
    bool MainnetEnabled,
    decimal TransferPerTxCspr,
    decimal TransferPerDayCspr,
    decimal StakePerTxCspr,
    IReadOnlySet<string> AllowRecipients,
    IReadOnlySet<string> AllowValidators)
{
    public static WritePolicy StrictDefault() => new(
        MainnetEnabled: false, TransferPerTxCspr: 100m, TransferPerDayCspr: 500m, StakePerTxCspr: 100m,
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
                // Transfer caps: prefer the new "transfer" block, fall back to the legacy "caps" block.
                decimal transferTx = ReadDecimal(root, "transfer", "per_tx_cspr",
                    ReadDecimal(root, "caps", "per_tx_cspr", p.TransferPerTxCspr));
                decimal transferDay = ReadDecimal(root, "transfer", "per_day_cspr",
                    ReadDecimal(root, "caps", "per_day_cspr", p.TransferPerDayCspr));
                decimal stakeTx = ReadDecimal(root, "stake", "per_tx_cspr", p.StakePerTxCspr);
                var recips = ReadList(root, "allowlist", "recipients");
                var vals = ReadList(root, "allowlist", "validators");
                p = p with
                {
                    MainnetEnabled = mainnet,
                    TransferPerTxCspr = transferTx,
                    TransferPerDayCspr = transferDay,
                    StakePerTxCspr = stakeTx,
                    AllowRecipients = recips,
                    AllowValidators = vals
                };
            }
            catch { p = StrictDefault(); }
        }

        // 2) Env overrides (each wins over file/default when present). New names win over legacy.
        if (getEnv("CASPER_MCP_MAINNET_ENABLED") is { } meEnv)
            p = p with { MainnetEnabled = meEnv is "1" or "true" or "True" };
        if (TryDecimal(getEnv("CASPER_MCP_PER_TX_CSPR"), out var legacyTx)) p = p with { TransferPerTxCspr = legacyTx };
        if (TryDecimal(getEnv("CASPER_MCP_TRANSFER_PER_TX_CSPR"), out var transferTxEnv)) p = p with { TransferPerTxCspr = transferTxEnv };
        if (TryDecimal(getEnv("CASPER_MCP_PER_DAY_CSPR"), out var legacyDay)) p = p with { TransferPerDayCspr = legacyDay };
        if (TryDecimal(getEnv("CASPER_MCP_TRANSFER_PER_DAY_CSPR"), out var transferDayEnv)) p = p with { TransferPerDayCspr = transferDayEnv };
        if (TryDecimal(getEnv("CASPER_MCP_STAKE_PER_TX_CSPR"), out var stakeTxEnv)) p = p with { StakePerTxCspr = stakeTxEnv };
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
