using System.Numerics;

namespace CasperMcp.Writes;

/// <summary>Tracks transfer outflow per signer public key for the current UTC day (rolling).</summary>
public interface ISpendLedger
{
    BigInteger TodaySpentMotes(string signerPublicKeyHex);
    void Record(string signerPublicKeyHex, BigInteger motes);
}
