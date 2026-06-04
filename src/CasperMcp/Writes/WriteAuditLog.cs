using System.Text.Json;

namespace CasperMcp.Writes;

/// <summary>Append-only JSONL audit log of every signer decision. No secrets — only a key fingerprint.</summary>
public sealed class WriteAuditLog
{
    private readonly string _path;
    private readonly Func<DateTime> _utcNow;
    private readonly object _gate = new();

    public WriteAuditLog(string path, Func<DateTime> utcNow)
    {
        _path = path;
        _utcNow = utcNow;
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
    }

    public void Record(string tool, string decision, string reason, string summary, string fingerprint, string correlationId)
    {
        var entry = new Dictionary<string, object?>
        {
            ["ts"] = _utcNow().ToString("O"),
            ["tool"] = tool,
            ["decision"] = decision,
            ["reason"] = reason,
            ["summary"] = summary,
            ["tenant"] = fingerprint,
            ["correlation_id"] = correlationId,
        };
        lock (_gate) { File.AppendAllText(_path, JsonSerializer.Serialize(entry) + Environment.NewLine); }
    }
}
