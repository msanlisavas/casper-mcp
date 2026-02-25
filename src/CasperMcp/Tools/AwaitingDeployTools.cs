using System.ComponentModel;
using System.Text;
using CasperMcp.Configuration;
using CasperMcp.Helpers;
using CSPR.Cloud.Net.Clients;
using CSPR.Cloud.Net.Objects.AwaitingDeploy;
using ModelContextProtocol.Server;
using Newtonsoft.Json.Linq;

namespace CasperMcp.Tools;

[McpServerToolType]
public static class AwaitingDeployTools
{
    [McpServerTool, Description("Get an awaiting deploy by its deploy hash on the Casper Network.")]
    public static async Task<string> GetAwaitingDeploy(
        CasperCloudRestClient client,
        CasperMcpOptions options,
        [Description("The deploy hash")] string deployHash)
    {
        try
        {
            var endpoint = options.IsTestnet ? (INetworkEndpoint)client.Testnet : client.Mainnet;
            var result = await endpoint.AwaitingDeploy.GetAwaitingDeployAsync(deployHash);

            if (result?.Deploy is null)
                return $"Awaiting deploy not found: {deployHash}";

            var sb = new StringBuilder();
            sb.AppendLine($"## Awaiting Deploy");
            sb.AppendLine($"- **Deploy Hash:** {deployHash}");
            sb.AppendLine($"- **Deploy JSON:**");
            sb.AppendLine($"```json");
            sb.AppendLine(result.Deploy.ToString());
            sb.AppendLine($"```");

            return sb.ToString();
        }
        catch (Exception ex)
        {
            return $"Error retrieving awaiting deploy: {ex.Message}";
        }
    }

    [McpServerTool, Description("Create an awaiting deploy on the Casper Network. Submits a deploy JSON for multi-signature collection.")]
    public static async Task<string> CreateAwaitingDeploy(
        CasperCloudRestClient client,
        CasperMcpOptions options,
        [Description("The deploy JSON string")] string deployJson)
    {
        try
        {
            var endpoint = options.IsTestnet ? (INetworkEndpoint)client.Testnet : client.Mainnet;
            var request = new CreateAwaitingDeployRequest
            {
                Deploy = JObject.Parse(deployJson)
            };

            var result = await endpoint.AwaitingDeploy.CreateAwaitingDeployAsync(request);

            if (result?.Data == true)
                return "Awaiting deploy created successfully.";

            return "Failed to create awaiting deploy.";
        }
        catch (Exception ex)
        {
            return $"Error creating awaiting deploy: {ex.Message}";
        }
    }

    [McpServerTool, Description("Add an approval (signature) to an awaiting deploy on the Casper Network.")]
    public static async Task<string> AddAwaitingDeployApproval(
        CasperCloudRestClient client,
        CasperMcpOptions options,
        [Description("The deploy hash")] string deployHash,
        [Description("The signer's public key")] string signer,
        [Description("The signature")] string signature)
    {
        try
        {
            var endpoint = options.IsTestnet ? (INetworkEndpoint)client.Testnet : client.Mainnet;
            var request = new AddApprovalRequest
            {
                Signer = signer,
                Signature = signature
            };

            var result = await endpoint.AwaitingDeploy.AddAwaitingDeployApprovalsAsync(deployHash, request);

            if (result?.Data == true)
                return $"Approval added successfully to deploy: {deployHash}";

            return $"Failed to add approval to deploy: {deployHash}";
        }
        catch (Exception ex)
        {
            return $"Error adding approval to awaiting deploy: {ex.Message}";
        }
    }
}
