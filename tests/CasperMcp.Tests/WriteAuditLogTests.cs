using System.Text.Json;
using CasperMcp.Writes;

namespace CasperMcp.Tests;

public class WriteAuditLogTests
{
    [Fact]
    public void Appends_One_Json_Line_Per_Entry()
    {
        var path = Path.Combine(Path.GetTempPath(), "audit-" + Guid.NewGuid().ToString("n") + ".log");
        try
        {
            var log = new WriteAuditLog(path, () => new DateTime(2026, 6, 4, 12, 0, 0, DateTimeKind.Utc));
            log.Record(tool: "SignAndSubmitTransaction", decision: "allow", reason: "ok",
                summary: "Transfer 10 CSPR", fingerprint: "k_abc", correlationId: "c1");
            log.Record(tool: "SignAndSubmitTransaction", decision: "deny", reason: "recipient not on allowlist",
                summary: "Transfer 99 CSPR", fingerprint: "k_abc", correlationId: "c2");

            var lines = File.ReadAllLines(path);
            Assert.Equal(2, lines.Length);
            using var doc = JsonDocument.Parse(lines[1]);
            Assert.Equal("deny", doc.RootElement.GetProperty("decision").GetString());
            Assert.Equal("k_abc", doc.RootElement.GetProperty("tenant").GetString());
        }
        finally { if (File.Exists(path)) File.Delete(path); }
    }
}
