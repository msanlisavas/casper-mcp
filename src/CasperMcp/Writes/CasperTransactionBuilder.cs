using System.Numerics;
using System.Text;
using System.Text.Json;
using Casper.Network.SDK.Types;

namespace CasperMcp.Writes;

/// <summary>
/// Builds unsigned native TransactionV1s (transfer/delegate/undelegate/redelegate) for the loaded
/// signer key, returning (unsignedJson, humanPreview). Recipients/validators are public-key hex
/// (v1 scope). Amounts are CSPR (decimal) → motes. No key material is touched here.
/// </summary>
public sealed class CasperTransactionBuilder
{
    public const ulong TransferPaymentMotes = 100_000_000;   // 0.1 CSPR — verify on testnet (Task 11)
    public const ulong StakingPaymentMotes = 2_500_000_000;  // 2.5 CSPR — verify on testnet (Task 11)
    private const byte GasPriceTolerance = 1;

    private readonly PublicKey _signer;
    private readonly string _chainName;

    public CasperTransactionBuilder(PublicKey signer, string chainName)
    {
        _signer = signer;
        _chainName = chainName;
    }

    public static BigInteger ToMotes(decimal cspr) => (BigInteger)(cspr * 1_000_000_000m);

    public (string json, string preview) BuildTransfer(string recipientPublicKeyHex, decimal amountCspr)
    {
        var recipient = PublicKey.FromHexString(recipientPublicKeyHex);
        var txn = new Transaction.NativeTransferBuilder()
            .From(_signer).Target(recipient).Amount((BigInteger)ToMotes(amountCspr))
            .ChainName(_chainName).Payment(TransferPaymentMotes, GasPriceTolerance).Build();
        return (JsonSerializer.Serialize(txn),
            Preview("Transfer", txn.Hash, amountCspr, $"to {recipientPublicKeyHex}", TransferPaymentMotes));
    }

    public (string json, string preview) BuildDelegate(string validatorPublicKeyHex, decimal amountCspr) =>
        BuildStake(new Transaction.NativeDelegateBuilder()
            .From(_signer).Validator(PublicKey.FromHexString(validatorPublicKeyHex)).Amount((BigInteger)ToMotes(amountCspr))
            .ChainName(_chainName).Payment(StakingPaymentMotes, GasPriceTolerance).Build(),
            "Delegate", amountCspr, $"to validator {validatorPublicKeyHex}");

    public (string json, string preview) BuildUndelegate(string validatorPublicKeyHex, decimal amountCspr) =>
        BuildStake(new Transaction.NativeUndelegateBuilder()
            .From(_signer).Validator(PublicKey.FromHexString(validatorPublicKeyHex)).Amount((BigInteger)ToMotes(amountCspr))
            .ChainName(_chainName).Payment(StakingPaymentMotes, GasPriceTolerance).Build(),
            "Undelegate", amountCspr, $"from validator {validatorPublicKeyHex}");

    public (string json, string preview) BuildRedelegate(string fromValidatorHex, string toValidatorHex, decimal amountCspr) =>
        BuildStake(new Transaction.NativeRedelegateBuilder()
            .From(_signer).Validator(PublicKey.FromHexString(fromValidatorHex)).NewValidator(PublicKey.FromHexString(toValidatorHex))
            .Amount((BigInteger)ToMotes(amountCspr)).ChainName(_chainName).Payment(StakingPaymentMotes, GasPriceTolerance).Build(),
            "Redelegate", amountCspr, $"from {fromValidatorHex} to {toValidatorHex}");

    private (string json, string preview) BuildStake(Transaction txn, string label, decimal amountCspr, string targetDesc) =>
        (JsonSerializer.Serialize(txn), Preview(label, txn.Hash, amountCspr, targetDesc, StakingPaymentMotes));

    private string Preview(string action, string hash, decimal amountCspr, string targetDesc, ulong paymentMotes)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"## {action} — UNSIGNED (review before signing)");
        sb.AppendLine($"- **Action:** {action} {targetDesc}");
        sb.AppendLine($"- **Amount:** {amountCspr.ToString(System.Globalization.CultureInfo.InvariantCulture)} CSPR ({ToMotes(amountCspr)} motes)");
        sb.AppendLine($"- **Network:** {_chainName}");
        sb.AppendLine($"- **Est. fee (payment):** {(paymentMotes / 1_000_000_000m).ToString(System.Globalization.CultureInfo.InvariantCulture)} CSPR ({paymentMotes} motes)");
        sb.AppendLine($"- **Sender:** {_signer}");
        sb.AppendLine($"- **Transaction hash (to be signed):** {hash}");
        sb.AppendLine();
        sb.AppendLine("Pass this transaction's JSON to `SignAndSubmitTransaction` to sign and broadcast. " +
                      "The signer re-validates it against policy (allowlist, caps, network) before signing.");
        return sb.ToString();
    }
}
