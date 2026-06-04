using System.Numerics;

namespace CasperMcp.Writes;

/// <summary>
/// Pure, side-effect-free evaluation of a decoded <see cref="TransactionIntent"/> against policy.
/// Fail-closed: every rule must explicitly pass; anything unexpected denies. Recipient/validator
/// matching is by lowercased public-key hex (the introspector lowercases). Recipient allowlists
/// MAY also contain account-hash entries; we match the pk hex directly and (best-effort) skip
/// account-hash normalization here — the CasperSigner normalizes the allowlist once at load.
/// </summary>
public static class PolicyEngine
{
    public static readonly BigInteger MotesPerCspr = new(1_000_000_000);

    public static PolicyDecision Evaluate(TransactionIntent intent, WritePolicy policy, ISpendLedger ledger, string signerPublicKeyHex)
    {
        // 1) Only ever sign our own transactions.
        if (!string.Equals(intent.SenderPublicKeyHex, signerPublicKeyHex, StringComparison.OrdinalIgnoreCase))
            return PolicyDecision.Deny("Refused: transaction sender does not match the signer's key.");

        // 2) Network gate (mainnet must be explicitly enabled).
        bool isMainnet = string.Equals(intent.ChainName, "casper", StringComparison.OrdinalIgnoreCase);
        if (isMainnet && !policy.MainnetEnabled)
            return PolicyDecision.Deny("Refused: mainnet writes are disabled (set mainnet_enabled to allow).");

        // 3) Per-tx cap (sanity bound on all ops).
        var perTxMotes = (BigInteger)(policy.PerTxCspr * 1_000_000_000m);
        if (intent.AmountMotes > perTxMotes)
            return PolicyDecision.Deny($"Refused: amount exceeds the per-transaction cap of {policy.PerTxCspr} CSPR.");

        // 4) Kind-specific allowlist + daily cap.
        switch (intent.Kind)
        {
            case WriteKind.Transfer:
                if (!policy.AllowRecipients.Contains(intent.TargetPublicKeyHex))
                    return PolicyDecision.Deny("Refused: recipient is not on the allowlist.");
                var perDayMotes = (BigInteger)(policy.PerDayCspr * 1_000_000_000m);
                if (ledger.TodaySpentMotes(signerPublicKeyHex) + intent.AmountMotes > perDayMotes)
                    return PolicyDecision.Deny($"Refused: would exceed the daily cap of {policy.PerDayCspr} CSPR.");
                return PolicyDecision.Allow();

            case WriteKind.Delegate:
            case WriteKind.Undelegate:
                if (!policy.AllowValidators.Contains(intent.TargetPublicKeyHex))
                    return PolicyDecision.Deny("Refused: validator is not on the allowlist.");
                return PolicyDecision.Allow();

            case WriteKind.Redelegate:
                if (!policy.AllowValidators.Contains(intent.TargetPublicKeyHex))
                    return PolicyDecision.Deny("Refused: source validator is not on the allowlist.");
                if (intent.NewValidatorPublicKeyHex is null || !policy.AllowValidators.Contains(intent.NewValidatorPublicKeyHex))
                    return PolicyDecision.Deny("Refused: destination validator is not on the allowlist.");
                return PolicyDecision.Allow();

            default:
                return PolicyDecision.Deny("Refused: unsupported transaction kind.");
        }
    }
}
