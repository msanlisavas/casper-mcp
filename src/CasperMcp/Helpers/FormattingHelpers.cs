using System.Globalization;
using System.Numerics;

namespace CasperMcp.Helpers;

public static class FormattingHelpers
{
    private static readonly BigInteger MotesToCsprDivisor = BigInteger.Pow(10, 9);
    private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

    public static string MotesToCspr(ulong? motes)
    {
        if (motes is null) return "N/A";
        return FormatMotes(new BigInteger(motes.Value));
    }

    public static string MotesToCspr(string? motes)
    {
        if (string.IsNullOrEmpty(motes)) return "N/A";
        if (!BigInteger.TryParse(motes, NumberStyles.Integer, Inv, out var value)) return "N/A";
        return FormatMotes(value);
    }

    private static string FormatMotes(BigInteger motes)
    {
        var whole = BigInteger.DivRem(motes, MotesToCsprDivisor, out var fraction);
        var sign = fraction.Sign < 0 ? "-" : string.Empty;
        var absFraction = BigInteger.Abs(fraction);
        return string.Format(Inv, "{0}{1:N0}.{2:D9} CSPR", sign, whole, (long)absFraction);
    }

    /// <summary>
    /// Sums a set of mote values represented as strings without overflow.
    /// Returns the sum as a decimal-string (motes), or null if all inputs were null/empty/unparseable.
    /// </summary>
    public static string? SumMotes(params string?[] motes)
    {
        BigInteger total = 0;
        var anyParsed = false;
        foreach (var m in motes)
        {
            if (string.IsNullOrEmpty(m)) continue;
            if (!BigInteger.TryParse(m, NumberStyles.Integer, Inv, out var value)) continue;
            total += value;
            anyParsed = true;
        }
        return anyParsed ? total.ToString(Inv) : null;
    }

    public static string FormatTimestamp(DateTime? timestamp)
    {
        if (timestamp is null) return "N/A";
        return timestamp.Value.ToString("yyyy-MM-dd HH:mm:ss UTC", Inv);
    }

    public static string FormatUnixSeconds(long? seconds)
    {
        if (seconds is null) return "N/A";
        return DateTimeOffset.FromUnixTimeSeconds(seconds.Value)
            .UtcDateTime.ToString("yyyy-MM-dd HH:mm:ss UTC", Inv);
    }

    public static string FormatPercentage(float? value)
    {
        if (value is null) return "N/A";
        return string.Format(Inv, "{0:F2}%", value.Value);
    }

    public static string FormatPercentage(string? value)
    {
        if (string.IsNullOrEmpty(value)) return "N/A";
        if (!decimal.TryParse(value, NumberStyles.Float, Inv, out var d)) return value;
        return string.Format(Inv, "{0:F2}%", d);
    }

    public static string FormatHash(string? hash)
    {
        return string.IsNullOrEmpty(hash) ? "N/A" : hash;
    }

    public static string FormatNumber(ulong? value)
    {
        if (value is null) return "N/A";
        return value.Value.ToString("N0", Inv);
    }

    public static string FormatNumber(uint? value)
    {
        if (value is null) return "N/A";
        return value.Value.ToString("N0", Inv);
    }

    public static string FormatBool(bool value) => value ? "Yes" : "No";

    public static string FormatDouble(double? value)
    {
        if (value is null) return "N/A";
        return value.Value.ToString("F4", Inv);
    }

    public static string FormatDecimal(decimal? value)
    {
        if (value is null) return "N/A";
        return value.Value.ToString("N9", Inv);
    }
}
