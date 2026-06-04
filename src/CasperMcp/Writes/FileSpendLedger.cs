using System.Numerics;
using System.Text.Json;

namespace CasperMcp.Writes;

/// <summary>
/// File-backed daily ledger. Single-threaded local signer ⇒ a simple read-modify-write under a
/// lock is sufficient. The file holds { "date": "yyyy-MM-dd", "spent": { "<pk>": "<motes>" } }.
/// On a date change the file is reset. This is a convenience guard — the durable limit is the
/// signing account's balance (see the threat model).
/// </summary>
public sealed class FileSpendLedger : ISpendLedger
{
    private readonly string _path;
    private readonly Func<DateOnly> _today;
    private readonly object _gate = new();

    public FileSpendLedger(string path, Func<DateOnly> today)
    {
        _path = path;
        _today = today;
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
    }

    public BigInteger TodaySpentMotes(string signerPublicKeyHex)
    {
        lock (_gate) { var (_, spent) = LoadForToday(); return spent.GetValueOrDefault(signerPublicKeyHex, BigInteger.Zero); }
    }

    public void Record(string signerPublicKeyHex, BigInteger motes)
    {
        lock (_gate)
        {
            var (date, spent) = LoadForToday();
            spent[signerPublicKeyHex] = spent.GetValueOrDefault(signerPublicKeyHex, BigInteger.Zero) + motes;
            var obj = new Dictionary<string, object?>
            {
                ["date"] = date.ToString("yyyy-MM-dd"),
                ["spent"] = spent.ToDictionary(kv => kv.Key, kv => kv.Value.ToString())
            };
            File.WriteAllText(_path, JsonSerializer.Serialize(obj));
        }
    }

    private (DateOnly date, Dictionary<string, BigInteger> spent) LoadForToday()
    {
        var today = _today();
        var spent = new Dictionary<string, BigInteger>(StringComparer.OrdinalIgnoreCase);
        if (File.Exists(_path))
        {
            try
            {
                using var doc = JsonDocument.Parse(File.ReadAllText(_path));
                var root = doc.RootElement;
                if (root.TryGetProperty("date", out var d) && DateOnly.TryParse(d.GetString(), out var fileDay) && fileDay == today
                    && root.TryGetProperty("spent", out var s) && s.ValueKind == JsonValueKind.Object)
                    foreach (var kv in s.EnumerateObject())
                        if (BigInteger.TryParse(kv.Value.GetString(), out var m)) spent[kv.Name] = m;
            }
            catch { /* fail-closed: treat unreadable ledger as empty today */ }
        }
        return (today, spent);
    }
}
