namespace CasperMcp.Configuration;

public enum AuthMode
{
    None,
    ApiKey,
    Jwt
}

/// <summary>
/// Server-level configuration resolved once at startup from CLI args / environment.
/// </summary>
public class ServerConfig
{
    public string Transport { get; set; } = "stdio";
    public int Port { get; set; } = 3001;
    public string McpPath { get; set; } = "/mcp";
    public string DefaultNetwork { get; set; } = "mainnet";

    /// <summary>CSPR.Cloud key used only in stdio mode (required there).</summary>
    public string StdioApiKey { get; set; } = string.Empty;

    public AuthMode AuthMode { get; set; } = AuthMode.None;
    public string AuthApiKey { get; set; } = string.Empty;
    public string JwtAuthority { get; set; } = string.Empty;
    public string JwtAudience { get; set; } = string.Empty;

    public bool IsHttp => Transport.Equals("http", StringComparison.OrdinalIgnoreCase);
}
