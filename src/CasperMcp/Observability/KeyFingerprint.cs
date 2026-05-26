using System.Security.Cryptography;
using System.Text;

namespace CasperMcp.Observability;

/// <summary>
/// Produces a stable, non-reversible fingerprint of a CSPR.Cloud API key so that per-agent
/// traffic can be correlated across logs, metrics, and traces WITHOUT ever exposing the key.
/// The raw key is never logged anywhere — only this fingerprint is.
/// </summary>
public static class KeyFingerprint
{
    /// <summary>Returns e.g. "k_3f9a1c2b4d5e" for a key, or "anonymous" when no key is present.</summary>
    public static string Of(string? key)
    {
        if (string.IsNullOrEmpty(key))
            return "anonymous";

        Span<byte> hash = stackalloc byte[32];
        SHA256.HashData(Encoding.UTF8.GetBytes(key), hash);
        return "k_" + Convert.ToHexString(hash[..6]).ToLowerInvariant();
    }
}
