using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Http;

namespace CasperMcp.Tests;

/// <summary>
/// Helpers for exercising middleware directly against a DefaultHttpContext with a
/// capturable request body and response body. Used by the JSON-RPC error-envelope tests.
/// </summary>
internal static class MiddlewareTestContext
{
    public static DefaultHttpContext WithBody(
        string path, string method, string? jsonBody, params (string, string)[] headers)
    {
        var ctx = new DefaultHttpContext();
        ctx.Request.Path = path;
        ctx.Request.Method = method;
        foreach (var (k, v) in headers) ctx.Request.Headers[k] = v;
        if (jsonBody is not null)
        {
            var bytes = Encoding.UTF8.GetBytes(jsonBody);
            ctx.Request.Body = new MemoryStream(bytes);
            ctx.Request.ContentLength = bytes.Length;
            ctx.Request.ContentType = "application/json";
        }
        ctx.Response.Body = new MemoryStream();
        return ctx;
    }

    public static async Task<JsonDocument> ReadJson(DefaultHttpContext ctx)
    {
        ctx.Response.Body.Position = 0;
        using var reader = new StreamReader(ctx.Response.Body, Encoding.UTF8, leaveOpen: true);
        var text = await reader.ReadToEndAsync();
        return JsonDocument.Parse(text);
    }
}
