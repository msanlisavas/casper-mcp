using CasperMcp.Remote;
using CSPR.Cloud.Net.Clients;

namespace CasperMcp.Tests;

public class CasperClientFactoryTests
{
    [Fact]
    public void Create_WithKeyAndHttpClient_ReturnsUsableClient()
    {
        using var http = new HttpClient();
        var client = CasperClientFactory.Create("test-key", http, loggerFactory: null);

        Assert.NotNull(client);
        Assert.NotNull(client.Mainnet);
        Assert.NotNull(client.Testnet);
    }

    [Fact]
    public void Create_DistinctKeys_ReturnDistinctInstances()
    {
        using var http = new HttpClient();
        var a = CasperClientFactory.Create("key-a", http, null);
        var b = CasperClientFactory.Create("key-b", http, null);

        Assert.NotSame(a, b);
    }
}
