using System.Text.Json;
using Casper.Network.SDK.Types;
using CasperMcp.Observability;

namespace CasperMcp.Writes;

/// <summary>
/// The local signer. Holds the loaded KeyPair and owns the build → policy → sign → submit pipeline.
/// The private key never leaves this object; signatures are never returned to the caller — only a
/// tx hash and status. Policy denials are returned as readable refusal strings (not exceptions),
/// so an autonomous agent gets a clear, non-fatal answer.
/// </summary>
public sealed class CasperSigner
{
    private readonly KeyPair _keyPair;
    private readonly string _signerPkHex;
    private readonly string _fingerprint;
    private readonly WritePolicy _policy;
    private readonly ISpendLedger _ledger;
    private readonly WriteAuditLog _audit;
    private readonly CasperTransactionBuilder _builder;
    private readonly Func<Transaction, Task<string>> _submit;

    public CasperSigner(KeyPair keyPair, string chainName, WritePolicy policy, ISpendLedger ledger,
        WriteAuditLog audit, Func<Transaction, Task<string>> submit)
    {
        _keyPair = keyPair;
        _signerPkHex = keyPair.PublicKey.ToString().ToLowerInvariant();
        _fingerprint = KeyFingerprint.Of(_signerPkHex);
        _policy = policy;
        _ledger = ledger;
        _audit = audit;
        _builder = new CasperTransactionBuilder(keyPair.PublicKey, chainName);
        _submit = submit;
    }

    /// <summary>A short, log-safe prefix of the signer's PUBLIC key (never the secret) for the startup banner.</summary>
    public string SignerPublicKeyShort => _signerPkHex.Length > 12 ? _signerPkHex[..12] + "…" : _signerPkHex;

    public (string json, string preview) BuildTransfer(string recipient, decimal cspr) => _builder.BuildTransfer(recipient, cspr);
    public (string json, string preview) BuildDelegate(string validator, decimal cspr) => _builder.BuildDelegate(validator, cspr);
    public (string json, string preview) BuildUndelegate(string validator, decimal cspr) => _builder.BuildUndelegate(validator, cspr);
    public (string json, string preview) BuildRedelegate(string from, string to, decimal cspr) => _builder.BuildRedelegate(from, to, cspr);

    public async Task<string> SignAndSubmit(string unsignedJson, string correlationId)
    {
        TransactionIntent intent;
        try { intent = TransactionIntrospector.FromUnsignedJson(unsignedJson); }
        catch (TransactionDecodeException ex)
        {
            _audit.Record("SignAndSubmitTransaction", "deny", ex.Message, "(undecodable)", _fingerprint, correlationId);
            return $"Refused: could not decode the transaction — {ex.Message}";
        }

        var cspr = ((decimal)intent.AmountMotes / 1_000_000_000m).ToString(System.Globalization.CultureInfo.InvariantCulture);
        var summary = $"{intent.Kind} {cspr} CSPR -> {intent.TargetPublicKeyHex}";
        var decision = PolicyEngine.Evaluate(intent, _policy, _ledger, _signerPkHex);
        if (!decision.Allowed)
        {
            _audit.Record("SignAndSubmitTransaction", "deny", decision.Reason, summary, _fingerprint, correlationId);
            return decision.Reason;
        }

        var txn = JsonSerializer.Deserialize<Transaction>(unsignedJson)!;
        string submitResult;
        try
        {
            txn.Sign(_keyPair);
            submitResult = await _submit(txn);
        }
        catch (Exception ex)
        {
            // The transaction passed policy and was signed locally, but the node did not accept it
            // (e.g. a sub-minimum transfer amount, insufficient balance, or a transient node error).
            // Surface the node's reason as a graceful, agent-readable message instead of throwing.
            // No ledger debit — nothing was committed on-chain.
            _audit.Record("SignAndSubmitTransaction", "error", ex.Message, summary, _fingerprint, correlationId);
            return $"Submission failed — the transaction passed policy and was signed locally, but the " +
                   $"node did not accept it: {ex.Message}";
        }

        if (intent.Kind == WriteKind.Transfer) _ledger.Record(_signerPkHex, intent.AmountMotes);
        _audit.Record("SignAndSubmitTransaction", "allow", "ok", summary, _fingerprint, correlationId);

        return $"## {intent.Kind} submitted\n- **Transaction hash:** {txn.Hash}\n- **Network:** {intent.ChainName}\n" +
               $"- **Result:** {submitResult}\n\nPoll `GetDeploy`/`GetTransaction`-style read tools to confirm execution.";
    }
}
