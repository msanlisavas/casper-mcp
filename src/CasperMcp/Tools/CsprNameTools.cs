using System.ComponentModel;
using System.Text;
using CasperMcp.Configuration;
using CasperMcp.Helpers;
using CSPR.Cloud.Net.Clients;
using ModelContextProtocol.Server;

namespace CasperMcp.Tools;

[McpServerToolType]
public static class CsprNameTools
{
    [McpServerTool, Description("Resolve a CSPR.name to an account hash on the Casper Network.")]
    public static async Task<string> ResolveCsprName(
        CasperCloudRestClient client,
        CasperMcpOptions options,
        [Description("The CSPR.name to resolve (e.g., 'alice.cspr')")] string name)
    {
        try
        {
            var endpoint = options.IsTestnet ? (INetworkEndpoint)client.Testnet : client.Mainnet;
            var result = await endpoint.CsprName.GetCsprNameResolutionAsync(name);

            if (result is null)
                return $"CSPR.name not found: {name}";

            var sb = new StringBuilder();
            sb.AppendLine($"## CSPR.name Resolution");
            sb.AppendLine($"- **Name:** {result.Name ?? "N/A"}");
            sb.AppendLine($"- **Token ID:** {result.NameTokenId ?? "N/A"}");
            sb.AppendLine($"- **Resolved Hash:** {FormattingHelpers.FormatHash(result.ResolvedHash)}");
            sb.AppendLine($"- **Is Primary:** {(result.IsPrimary.HasValue ? FormattingHelpers.FormatBool(result.IsPrimary.Value) : "N/A")}");
            sb.AppendLine($"- **Expires At:** {result.ExpiresAt ?? "N/A"}");

            return sb.ToString();
        }
        catch (Exception ex)
        {
            return $"Error resolving CSPR.name: {ex.Message}";
        }
    }
}
