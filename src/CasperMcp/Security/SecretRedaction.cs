namespace CasperMcp.Security;

public static class SecretRedaction
{
    private static readonly HashSet<string> Sensitive = new(StringComparer.OrdinalIgnoreCase)
    {
        "X-CSPR-Cloud-Api-Key",
        "Authorization",
        "X-API-Key"
    };

    public static bool IsSensitiveHeader(string name) => Sensitive.Contains(name);

    public static string Redact(string value) => string.IsNullOrEmpty(value) ? string.Empty : "***";
}
