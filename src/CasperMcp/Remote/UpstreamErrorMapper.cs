using System.Net;

namespace CasperMcp.Remote;

/// <summary>
/// Maps exceptions raised while calling CSPR.Cloud into safe, agent-facing messages.
/// Never includes raw upstream text, stack traces, or secrets.
/// </summary>
public static class UpstreamErrorMapper
{
    public static string Describe(Exception ex)
    {
        switch (ex)
        {
            case TaskCanceledException:
            case OperationCanceledException:
                return "Upstream request timed out or was cancelled. Try again.";
            case HttpRequestException http when http.StatusCode is { } code:
                return DescribeStatus(code);
            default:
                return "The request failed due to an unexpected error.";
        }
    }

    private static string DescribeStatus(HttpStatusCode code) => code switch
    {
        HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden =>
            "Upstream authentication failed. Check your CSPR.Cloud API key and plan.",
        HttpStatusCode.TooManyRequests =>
            "Rate limited by CSPR.Cloud. Slow down and retry shortly.",
        >= (HttpStatusCode)500 =>
            "CSPR.Cloud is temporarily unavailable. Try again later.",
        HttpStatusCode.NotFound =>
            "The requested resource was not found on CSPR.Cloud.",
        _ => $"The request failed (upstream status {(int)code})."
    };
}
