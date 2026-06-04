using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace CasperMcp.Tests;

// Shares the process-wide env-var / test-server setup with HttpPipelineTests, so it runs in the
// same non-parallel collection (both flip CASPER_MCP_TRANSPORT=http on the same process).
[Collection("HttpPipeline")]
public class WriteToolsHttpExclusionTests : IDisposable
{
    private static readonly string[] ManagedEnvVars = ["CASPER_MCP_TRANSPORT"];

    public void Dispose()
    {
        foreach (var v in ManagedEnvVars)
            Environment.SetEnvironmentVariable(v, null);
    }

    // The 5 stdio-only write tools. The remote HTTP surface must stay read-only — none of these
    // may appear in the http tools/list response.
    private static readonly string[] WriteToolNames =
    [
        "BuildTransferTransaction",
        "BuildDelegateTransaction",
        "BuildUndelegateTransaction",
        "BuildRedelegateTransaction",
        "SignAndSubmitTransaction",
    ];

    [Fact]
    public async Task ToolsList_Over_Http_Contains_No_Write_Tools()
    {
        // Mirror HttpPipelineTests: http transport, CSPR key header, testnet network, dual Accept.
        using var factory = new CasperAppFactory(
            new Dictionary<string, string?> { ["CASPER_MCP_TRANSPORT"] = "http" });
        var client = factory.CreateClient();

        var body = JsonSerializer.Serialize(new
        {
            jsonrpc = "2.0",
            id = 1,
            method = "tools/list",
            @params = new { }
        });
        var msg = new HttpRequestMessage(HttpMethod.Post, "/mcp")
        {
            Content = new StringContent(body, Encoding.UTF8, "application/json")
        };
        msg.Headers.Accept.Clear();
        msg.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        msg.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("text/event-stream"));
        msg.Headers.Add("X-CSPR-Cloud-Api-Key", "any-key");
        msg.Headers.Add("X-Casper-Network", "testnet");

        var response = await client.SendAsync(msg);
        var responseBody = await response.Content.ReadAsStringAsync();

        // Sanity: a known read tool IS present (mirrors HttpPipelineTests Test 4), proving we read
        // an actual populated tools/list and the absence below is meaningful.
        Assert.Contains("get_network_status", responseBody);

        foreach (var name in WriteToolNames)
            Assert.DoesNotContain(name, responseBody);
    }
}
