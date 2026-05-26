using CSPR.Cloud.Net.Clients;
using CSPR.Cloud.Net.Objects.Config;
using Microsoft.Extensions.Logging;

namespace CasperMcp.Remote;

/// <summary>
/// Builds a lightweight, per-request CasperCloudRestClient bound to a single agent's API key.
/// The HttpClient is supplied by IHttpClientFactory so the underlying connection pool is shared
/// across all tenants; a fresh client per request keeps the api key isolated between tenants.
/// </summary>
public static class CasperClientFactory
{
    public static CasperCloudRestClient Create(string apiKey, HttpClient httpClient, ILoggerFactory? loggerFactory)
    {
        var config = new CasperCloudClientConfig(apiKey);
        return new CasperCloudRestClient(config, httpClient, loggerFactory!);
    }
}
