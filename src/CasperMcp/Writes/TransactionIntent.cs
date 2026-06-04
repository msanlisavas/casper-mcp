using System.Numerics;

namespace CasperMcp.Writes;

public enum WriteKind { Transfer, Delegate, Undelegate, Redelegate }

/// <summary>
/// The decoded meaning of an unsigned transaction, read from the ACTUAL bytes (never a caller's
/// description). Hex fields are lowercased. <see cref="TargetPublicKeyHex"/> is the transfer
/// recipient or the (re)delegation validator; <see cref="NewValidatorPublicKeyHex"/> is set only
/// for redelegate.
/// </summary>
public sealed record TransactionIntent(
    WriteKind Kind,
    string SenderPublicKeyHex,
    string TargetPublicKeyHex,
    string? NewValidatorPublicKeyHex,
    BigInteger AmountMotes,
    string ChainName);

public sealed class TransactionDecodeException : Exception
{
    public TransactionDecodeException(string message) : base(message) { }
}
