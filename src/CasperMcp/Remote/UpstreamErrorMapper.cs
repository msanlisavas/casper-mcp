using System.Net;
using CSPR.Cloud.Net.Errors;

namespace CasperMcp.Remote;

/// <summary>
/// Maps exceptions raised while calling CSPR.Cloud into safe, actionable, agent-facing messages.
/// CSPR.Cloud.Net throws typed exceptions (CSPR.Cloud.Net.Errors.*) whose Message carries the
/// upstream response body. We surface the detail for parameter/not-found errors (so an agent can
/// correct its call) but never echo auth-related bodies, stack traces, or secrets.
/// </summary>
public static class UpstreamErrorMapper
{
    private const int MaxDetail = 200;

    public static string Describe(Exception ex)
    {
        switch (ex)
        {
            case TaskCanceledException:
            case OperationCanceledException:
                return "Upstream request timed out or was cancelled. Try again.";

            // Typed CSPR.Cloud.Net errors (status-code-derived).
            case UnauthorizedException:
            case AccessDeniedException:
                return "Upstream authentication failed. Check your CSPR.Cloud API key and plan.";
            case NotFoundException:
                return "The requested resource was not found on CSPR.Cloud.";
            case InvalidParamException:
                return $"CSPR.Cloud rejected the request parameters: {Detail(ex.Message)}";
            case DuplicateEntryException:
                return "CSPR.Cloud reported a duplicate or conflicting entry.";
            case InternalServerErrorException:
                return "CSPR.Cloud is temporarily unavailable (server error). Try again later.";

            // Fallback for raw HTTP failures (non-mapped statuses, transport errors).
            case HttpRequestException http when http.StatusCode is { } code:
                return DescribeStatus(code);
            default:
                return "The request failed due to an unexpected error.";
        }
    }

    /// <summary>Single-line, length-capped upstream detail (never contains the caller's key — that travels as a header).</summary>
    private static string Detail(string message)
    {
        var clean = message.Replace("\r", " ").Replace("\n", " ").Trim();
        return clean.Length > MaxDetail ? clean[..MaxDetail] + "…" : clean;
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
