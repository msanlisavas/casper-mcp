using System.Globalization;

namespace CasperMcp.Helpers;

public static class FormattingHelpers
{
    private const decimal MotesToCsprDivisor = 1_000_000_000m;
    private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

    public static string MotesToCspr(ulong? motes)
    {
        if (motes is null) return "N/A";
        return string.Format(Inv, "{0:N9} CSPR", motes.Value / MotesToCsprDivisor);
    }

    public static string MotesToCspr(string? motes)
    {
        if (string.IsNullOrEmpty(motes)) return "N/A";
        if (!ulong.TryParse(motes, out var value)) return "N/A";
        return MotesToCspr(value);
    }

    public static string FormatTimestamp(DateTime? timestamp)
    {
        if (timestamp is null) return "N/A";
        return timestamp.Value.ToString("yyyy-MM-dd HH:mm:ss UTC", Inv);
    }

    public static string FormatPercentage(float? value)
    {
        if (value is null) return "N/A";
        return string.Format(Inv, "{0:F2}%", value.Value);
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
}
