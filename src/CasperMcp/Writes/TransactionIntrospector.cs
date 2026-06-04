using System.Text.Json;
using Casper.Network.SDK.Types;

namespace CasperMcp.Writes;

/// <summary>
/// Decodes an unsigned Casper TransactionV1 JSON into a <see cref="TransactionIntent"/> by reading
/// the real transaction (entry point from the raw envelope, typed args from the deserialized
/// <see cref="Transaction"/>). Fail-closed: anything it cannot positively identify as a supported
/// NATIVE transfer/delegate/undelegate/redelegate throws <see cref="TransactionDecodeException"/>.
/// </summary>
public static class TransactionIntrospector
{
    public static TransactionIntent FromUnsignedJson(string json)
    {
        string entryPoint, target;
        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            if (root.TryGetProperty("Deploy", out var dep) && dep.ValueKind != JsonValueKind.Null)
                throw new TransactionDecodeException("Legacy 'Deploy' transactions are not supported by the signer.");
            if (!root.TryGetProperty("Version1", out var v1) || v1.ValueKind != JsonValueKind.Object)
                throw new TransactionDecodeException("Not a TransactionV1 envelope.");
            var fields = v1.GetProperty("payload").GetProperty("fields");
            target = fields.GetProperty("target").GetString() ?? "";
            entryPoint = fields.GetProperty("entry_point").GetString() ?? "";
        }
        catch (TransactionDecodeException) { throw; }
        catch (Exception ex) { throw new TransactionDecodeException($"Unparseable transaction JSON: {ex.Message}"); }

        if (!string.Equals(target, "Native", StringComparison.OrdinalIgnoreCase))
            throw new TransactionDecodeException($"Only Native transactions are allowed; got target '{target}'.");

        var kind = entryPoint.ToLowerInvariant() switch
        {
            "transfer" => WriteKind.Transfer,
            "delegate" => WriteKind.Delegate,
            "undelegate" => WriteKind.Undelegate,
            "redelegate" => WriteKind.Redelegate,
            _ => throw new TransactionDecodeException($"Unsupported entry point '{entryPoint}'.")
        };

        Transaction txn;
        try { txn = JsonSerializer.Deserialize<Transaction>(json) ?? throw new TransactionDecodeException("null transaction"); }
        catch (Exception ex) { throw new TransactionDecodeException($"Could not deserialize transaction: {ex.Message}"); }

        var sender = txn.InitiatorAddr?.PublicKey?.ToString()?.ToLowerInvariant()
                     ?? throw new TransactionDecodeException("Missing initiator public key.");
        var chain = txn.ChainName ?? throw new TransactionDecodeException("Missing chain name.");

        var amount = (txn.Invocation.GetRuntimeArgValue("amount")
                      ?? throw new TransactionDecodeException("Missing 'amount' arg.")).ToBigInteger();

        string targetHex;
        string? newValidatorHex = null;
        if (kind == WriteKind.Transfer)
            targetHex = Hex(txn, "target");
        else
        {
            targetHex = Hex(txn, "validator");
            if (kind == WriteKind.Redelegate) newValidatorHex = Hex(txn, "new_validator");
        }

        return new TransactionIntent(kind, sender, targetHex, newValidatorHex, amount, chain);
    }

    private static string Hex(Transaction txn, string arg)
    {
        var v = txn.Invocation.GetRuntimeArgValue(arg)
                ?? throw new TransactionDecodeException($"Missing '{arg}' arg.");
        return v.ToPublicKey().ToString().ToLowerInvariant();
    }
}
