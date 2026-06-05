using System.Globalization;
using System.Numerics;

namespace CasperMcp.Writes;

/// <summary>
/// Pure, side-effect-free evaluation of a decoded <see cref="TransactionIntent"/> against policy.
/// Fail-closed: every rule must explicitly pass; anything unexpected denies. Recipient/validator
/// matching is by lowercased public-key hex (the introspector lowercases).
///
/// Transfers and staking are gated differently on purpose:
/// <list type="bullet">
/// <item><b>Transfer</b> (irreversible outflow): recipient allowlist + the tight transfer per-tx and
///   daily caps.</item>
/// <item><b>Delegate / Redelegate</b> (stake lands at a validator): the RECEIVING validator must be
///   allowlisted + the separate, independent stake per-tx cap. The redelegate SOURCE is not gated —
///   you may always move stake away to an allowlisted validator.</item>
/// <item><b>Undelegate</b> (returns your own staked funds): sender + network only — no validator
///   allowlist and no amount cap, so fund recovery is never blocked.</item>
/// </list>
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

        // 3) Kind-specific gates.
        switch (intent.Kind)
        {
            case WriteKind.Transfer:
                if (!policy.AllowRecipients.Contains(intent.TargetPublicKeyHex))
                    return PolicyDecision.Deny("Refused: recipient is not on the allowlist.");
                var transferTxMotes = (BigInteger)(policy.TransferPerTxCspr * 1_000_000_000m);
                if (intent.AmountMotes > transferTxMotes)
                    return PolicyDecision.Deny($"Refused: transfer amount exceeds the per-transaction cap of {Cspr(policy.TransferPerTxCspr)} CSPR.");
                var transferDayMotes = (BigInteger)(policy.TransferPerDayCspr * 1_000_000_000m);
                if (ledger.TodaySpentMotes(signerPublicKeyHex) + intent.AmountMotes > transferDayMotes)
                    return PolicyDecision.Deny($"Refused: transfer would exceed the daily cap of {Cspr(policy.TransferPerDayCspr)} CSPR.");
                return PolicyDecision.Allow();

            case WriteKind.Delegate:
                if (!policy.AllowValidators.Contains(intent.TargetPublicKeyHex))
                    return PolicyDecision.Deny("Refused: validator is not on the allowlist.");
                return CheckStakeCap(intent, policy);

            case WriteKind.Redelegate:
                // Gate where the stake LANDS: the destination validator must be allowlisted.
                if (intent.NewValidatorPublicKeyHex is null || !policy.AllowValidators.Contains(intent.NewValidatorPublicKeyHex))
                    return PolicyDecision.Deny("Refused: destination validator is not on the allowlist.");
                return CheckStakeCap(intent, policy);

            case WriteKind.Undelegate:
                // Pure recovery of your own staked funds — no validator allowlist, no amount cap.
                return PolicyDecision.Allow();

            default:
                return PolicyDecision.Deny("Refused: unsupported transaction kind.");
        }
    }

    private static PolicyDecision CheckStakeCap(TransactionIntent intent, WritePolicy policy)
    {
        var stakeTxMotes = (BigInteger)(policy.StakePerTxCspr * 1_000_000_000m);
        if (intent.AmountMotes > stakeTxMotes)
            return PolicyDecision.Deny($"Refused: stake amount exceeds the per-transaction stake cap of {Cspr(policy.StakePerTxCspr)} CSPR.");
        return PolicyDecision.Allow();
    }

    private static string Cspr(decimal value) => value.ToString(CultureInfo.InvariantCulture);
}
