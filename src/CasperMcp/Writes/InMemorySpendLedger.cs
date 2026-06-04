using System.Numerics;

namespace CasperMcp.Writes;

public sealed class InMemorySpendLedger : ISpendLedger
{
    private readonly Func<DateOnly> _today;
    private readonly object _gate = new();
    private DateOnly _day;
    private readonly Dictionary<string, BigInteger> _spent = new(StringComparer.OrdinalIgnoreCase);

    public InMemorySpendLedger(Func<DateOnly> today)
    {
        _today = today;
        _day = today();
    }

    public BigInteger TodaySpentMotes(string signerPublicKeyHex)
    {
        lock (_gate) { Roll(); return _spent.GetValueOrDefault(signerPublicKeyHex, BigInteger.Zero); }
    }

    public void Record(string signerPublicKeyHex, BigInteger motes)
    {
        lock (_gate) { Roll(); _spent[signerPublicKeyHex] = _spent.GetValueOrDefault(signerPublicKeyHex, BigInteger.Zero) + motes; }
    }

    private void Roll()
    {
        var now = _today();
        if (now != _day) { _day = now; _spent.Clear(); }
    }
}
