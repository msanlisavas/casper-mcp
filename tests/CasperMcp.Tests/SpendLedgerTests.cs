using System.Numerics;
using CasperMcp.Writes;

namespace CasperMcp.Tests;

public class SpendLedgerTests
{
    [Fact]
    public void InMemory_Accumulates_For_Same_Key_And_Day()
    {
        var led = new InMemorySpendLedger(() => new DateOnly(2026, 6, 4));
        Assert.Equal(BigInteger.Zero, led.TodaySpentMotes("01aa"));
        led.Record("01aa", new BigInteger(100));
        led.Record("01aa", new BigInteger(50));
        Assert.Equal(new BigInteger(150), led.TodaySpentMotes("01aa"));
        Assert.Equal(BigInteger.Zero, led.TodaySpentMotes("01bb")); // isolated per key
    }

    [Fact]
    public void InMemory_Resets_On_New_Day()
    {
        var day = new DateOnly(2026, 6, 4);
        var led = new InMemorySpendLedger(() => day);
        led.Record("01aa", new BigInteger(100));
        day = new DateOnly(2026, 6, 5);
        Assert.Equal(BigInteger.Zero, led.TodaySpentMotes("01aa"));
    }

    [Fact]
    public void File_Persists_Across_Instances()
    {
        var path = Path.Combine(Path.GetTempPath(), "led-" + Guid.NewGuid().ToString("n") + ".json");
        try
        {
            var d = new DateOnly(2026, 6, 4);
            var a = new FileSpendLedger(path, () => d);
            a.Record("01aa", new BigInteger(200));
            var b = new FileSpendLedger(path, () => d);            // fresh instance, same file
            Assert.Equal(new BigInteger(200), b.TodaySpentMotes("01aa"));
        }
        finally { if (File.Exists(path)) File.Delete(path); }
    }
}
