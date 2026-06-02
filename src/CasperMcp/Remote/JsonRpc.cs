using System.Buffers;
using System.Text.Json;
using Microsoft.AspNetCore.Http;

namespace CasperMcp.Remote;

/// <summary>
/// Helpers for emitting JSON-RPC 2.0 error responses from the HTTP pipeline.
///
/// MCP clients POST a JSON-RPC request and deserialize whatever comes back as a
/// JSON-RPC message. A bare {"error":"..."} body (the pre-dispatch rejection shape)
/// fails that deserialization and the client surfaces an opaque transport error
/// instead of our message. Wrapping the rejection in a conformant envelope lets the
/// client show the real reason while we keep the appropriate 4xx status.
/// </summary>
internal static class JsonRpc
{
    /// <summary>Predefined JSON-RPC code: the request is not a valid Request object.</summary>
    public const int InvalidRequest = -32600;

    /// <summary>Predefined JSON-RPC code: invalid method parameter(s).</summary>
    public const int InvalidParams = -32602;

    // Cap how much of the body we read just to recover the request id — the error
    // path must never buffer an unbounded body (DoS) on an unauthenticated request.
    private const int MaxIdProbeBytes = 64 * 1024;

    /// <summary>
    /// Best-effort read of the JSON-RPC request "id" so the error can be correlated by the
    /// client. Returns null (→ id:null, the spec fallback) for non-POST/GET-SSE requests,
    /// an empty/oversized/unparseable body, a batch (top-level array), or a missing id.
    /// </summary>
    public static async Task<JsonElement?> TryReadRpcId(HttpRequest request)
    {
        if (!HttpMethods.IsPost(request.Method)) return null;
        if (request.ContentLength is > MaxIdProbeBytes) return null;

        request.EnableBuffering();
        try
        {
            // When the length is known and within the cap, read exactly that; otherwise
            // read up to the cap + 1 byte so we can detect (and reject) an overflow.
            var size = request.ContentLength is long len && len >= 0 ? (int)len : MaxIdProbeBytes + 1;
            if (size == 0) return null;

            var buffer = new byte[size];
            var total = 0;
            int read;
            while (total < buffer.Length &&
                   (read = await request.Body.ReadAsync(buffer.AsMemory(total, buffer.Length - total))) > 0)
                total += read;

            if (total == 0 || total > MaxIdProbeBytes) return null;

            using var doc = JsonDocument.Parse(new ReadOnlyMemory<byte>(buffer, 0, total));
            if (doc.RootElement.ValueKind == JsonValueKind.Object &&
                doc.RootElement.TryGetProperty("id", out var idEl))
                return idEl.Clone();

            return null;
        }
        catch (JsonException)
        {
            return null;
        }
        finally
        {
            // Rewind so a downstream handler could still read the body. Our callers all
            // short-circuit (they never call next), so this is purely defensive.
            if (request.Body.CanSeek)
                request.Body.Position = 0;
        }
    }

    /// <summary>
    /// Writes a JSON-RPC 2.0 error envelope: {"jsonrpc":"2.0","error":{"code","message"},"id"}.
    /// The HTTP status is preserved (e.g. 401/400) — the envelope is what fixes opaque
    /// client deserialization, not a weakened status.
    /// </summary>
    public static async Task WriteError(HttpContext ctx, int httpStatus, int rpcCode, string message, JsonElement? id)
    {
        ctx.Response.StatusCode = httpStatus;
        ctx.Response.ContentType = "application/json";

        var output = new ArrayBufferWriter<byte>();
        using (var w = new Utf8JsonWriter(output))
        {
            w.WriteStartObject();
            w.WriteString("jsonrpc", "2.0");
            w.WriteStartObject("error");
            w.WriteNumber("code", rpcCode);
            w.WriteString("message", message);
            w.WriteEndObject();
            w.WritePropertyName("id");
            if (id is { } element) element.WriteTo(w);
            else w.WriteNullValue();
            w.WriteEndObject();
        }

        await ctx.Response.Body.WriteAsync(output.WrittenMemory);
    }
}
