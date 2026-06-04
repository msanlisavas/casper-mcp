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

    /// <summary>Set to true when the network was explicitly chosen via --network arg or CASPER_MCP_NETWORK env var.</summary>
    public bool NetworkExplicitlySet { get; set; }

    /// <summary>When true, the stdio signer (write tools) is enabled. Incompatible with http transport.</summary>
    public bool WritesEnabled { get; set; }

    /// <summary>Path to the signer's PEM secret key. Required when WritesEnabled.</summary>
    public string KeyPath { get; set; } = string.Empty;

    /// <summary>Signer key algorithm hint: "ed25519" (default) or "secp256k1".</summary>
    public string KeyAlgo { get; set; } = "ed25519";

    /// <summary>Path to the write-policy JSON file. Empty ⇒ default ~/.casper-mcp/policy.json (+ env overrides).</summary>
    public string PolicyPath { get; set; } = string.Empty;

    /// <summary>Optional override for the node JSON-RPC URL used for submission. Empty ⇒ CSPR.Cloud node by network.</summary>
    public string NodeRpcUrl { get; set; } = string.Empty;

    /// <summary>When the signer is enabled and no network was explicitly chosen, default the whole
    /// write-mode process to testnet (the safest path). Explicit --network/env is always honored.</summary>
    public static void ApplyWriteModeNetworkDefault(ServerConfig config)
    {
        if (config.WritesEnabled && !config.NetworkExplicitlySet)
            config.DefaultNetwork = "testnet";
    }

    /// <summary>Fail-closed validation of write-mode config. Returns (ok, error).</summary>
    public static (bool ok, string error) ValidateWriteConfig(ServerConfig config)
    {
        if (!config.WritesEnabled) return (true, string.Empty);
        if (!string.Equals(config.Transport, "stdio", StringComparison.OrdinalIgnoreCase))
            return (false, "Writes (--enable-writes) require stdio transport; they are never available over http.");
        if (string.IsNullOrWhiteSpace(config.KeyPath))
            return (false, "Writes require a signing key: pass --key-path <pem> (or CASPER_MCP_KEY_PATH).");
        if (!File.Exists(config.KeyPath))
            return (false, $"Signing key not found at '{config.KeyPath}'.");
        return (true, string.Empty);
    }
}
