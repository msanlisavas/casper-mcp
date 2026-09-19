using CSPR.Cloud.Net.Objects.AccountInfo;
using CSPR.Cloud.Net.Objects.CentralizedAccountInfo;

namespace CasperMcp.Helpers;

/// <summary>
/// Turns CSPR.cloud identity data into the human-readable label a caller actually wants.
///
/// Every one of these names is an OPTIONAL property: CSPR.cloud omits it unless the request asked
/// for it with <c>includes</c>, which the SDK exposes as the <c>OptionalParameters</c> flags on a
/// request-parameters object. A tool that renders a name without setting those flags shows nothing,
/// forever — so these helpers are only half the fix; the call site must opt in too.
/// </summary>
public static class NameHelpers
{
    /// <summary>
    /// The display name for an account, in descending order of trustworthiness:
    /// the on-chain Account Info Standard record, then the centralized (CSPR.cloud-curated)
    /// record, then a CSPR.name. Returns null when none is present.
    /// </summary>
    /// <remarks>
    /// Account-info names are SELF-DECLARED and are not unique or verified — two validators may
    /// claim the same one. They are safe to display next to the key that produced them, which is
    /// what every caller here does; they are not safe to treat as an identifier.
    /// </remarks>
    public static string? DisplayName(
        AccountInfoData? accountInfo,
        CentralizedAccountInfoData? centralized = null,
        string? csprName = null)
    {
        if (accountInfo?.Info?.Owner?.Name is { Length: > 0 } owner) return owner;
        if (centralized?.Name is { Length: > 0 } curated) return curated;
        if (csprName is { Length: > 0 } cspr) return cspr;
        return null;
    }

    /// <summary>
    /// "Era Guardian (0111…99a5e5)" when a name is known, the formatted hash alone otherwise.
    /// Always keeps the hash: the name is a convenience, the key is the identity.
    /// </summary>
    public static string Labeled(string? name, string? hash) =>
        name is { Length: > 0 }
            ? $"{name} ({FormattingHelpers.FormatHash(hash)})"
            : FormattingHelpers.FormatHash(hash);

    /// <summary>Convenience overload for the common account-info-only case.</summary>
    public static string Labeled(AccountInfoData? accountInfo, string? hash) =>
        Labeled(DisplayName(accountInfo), hash);
}
