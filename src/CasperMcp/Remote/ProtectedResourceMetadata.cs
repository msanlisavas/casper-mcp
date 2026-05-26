namespace CasperMcp.Remote;

/// <summary>
/// Minimal OAuth 2.0 Protected Resource Metadata (RFC 9728) served at
/// /.well-known/oauth-protected-resource in jwt auth mode.
/// </summary>
public static class ProtectedResourceMetadata
{
    public static object Build(string resource, string authority) => new
    {
        resource,
        authorization_servers = new[] { authority },
        bearer_methods_supported = new[] { "header" }
    };
}
