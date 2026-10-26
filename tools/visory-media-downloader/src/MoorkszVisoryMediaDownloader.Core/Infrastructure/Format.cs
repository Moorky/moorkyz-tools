using System.Globalization;
using System.Text;

namespace MoorkszVisoryMediaDownloader.Core.Infrastructure;

public static class Format
{
    public static readonly CultureInfo Invariant = CultureInfo.InvariantCulture;

    /// <summary>Binary units (1 MB = 1,048,576 bytes), matching the logging contract examples.</summary>
    public static string Size(long? bytes)
    {
        if (bytes is null || bytes < 0) return "unknown";
        string[] units = ["B", "KB", "MB", "GB", "TB"];
        double value = bytes.Value;
        var unit = 0;
        while (value >= 1024 && unit < units.Length - 1)
        {
            value /= 1024;
            unit++;
        }
        return unit == 0 ? $"{bytes.Value.ToString(Invariant)} B" : $"{value.ToString("0.00", Invariant)} {units[unit]}";
    }

    public static string Speed(double bytesPerSecond) => double.IsFinite(bytesPerSecond) && bytesPerSecond >= 0
        ? Size((long)bytesPerSecond) + "/s"
        : "unknown";

    public static string Seconds(double seconds) => seconds.ToString("0.00", Invariant) + "s";

    public static string Duration(TimeSpan span) => span.TotalHours >= 1
        ? $"{(int)span.TotalHours:00}:{span.Minutes:00}:{span.Seconds:00}"
        : $"{span.Minutes:00}:{span.Seconds:00}";

    public static string LogTime(DateTimeOffset time) => time.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss", Invariant);

    public static string Number(double value, string format = "0.###") => value.ToString(format, Invariant);
}

public static class RunIds
{
    public const string Pattern = "yyyy-MM-dd_HH-mm-ss";

    /// <summary>Creates yyyy-MM-dd_HH-mm-ss and appends _2, _3 ... only when that run already exists.</summary>
    public static string Create(DateTimeOffset now, Func<string, bool> exists)
    {
        var baseId = now.ToLocalTime().ToString(Pattern, CultureInfo.InvariantCulture);
        if (!exists(baseId)) return baseId;
        for (var suffix = 2; suffix < 10000; suffix++)
        {
            var candidate = $"{baseId}_{suffix}";
            if (!exists(candidate)) return candidate;
        }
        throw new InvalidOperationException("Unable to allocate a unique run ID.");
    }
}

public static class Csv
{
    public static readonly UTF8Encoding Utf8WithBom = new(encoderShouldEmitUTF8Identifier: true);

    /// <summary>RFC 4180 quoting with spreadsheet formula neutralization for text cells.</summary>
    public static string Text(string? value)
    {
        if (string.IsNullOrEmpty(value)) return string.Empty;
        var text = value;
        if (text[0] is '=' or '+' or '-' or '@' or '\t' or '\r') text = "'" + text;
        var needsQuotes = text.IndexOfAny([',', '"', '\r', '\n', ';']) >= 0 || text[0] == ' ' || text[^1] == ' ';
        return needsQuotes ? "\"" + text.Replace("\"", "\"\"", StringComparison.Ordinal) + "\"" : text;
    }

    public static string Number(long? value) => value?.ToString(CultureInfo.InvariantCulture) ?? string.Empty;
    public static string Number(int? value) => value?.ToString(CultureInfo.InvariantCulture) ?? string.Empty;
    public static string Number(double? value, string format = "0.###") =>
        value is { } number && double.IsFinite(number) ? number.ToString(format, CultureInfo.InvariantCulture) : string.Empty;

    public static string Line(IEnumerable<string> cells) => string.Join(',', cells);

    /// <summary>Parses RFC 4180 records (used for verification and report viewers).</summary>
    public static List<List<string>> Parse(string text)
    {
        var rows = new List<List<string>>();
        var row = new List<string>();
        var cell = new StringBuilder();
        var quoted = false;
        for (var i = 0; i < text.Length; i++)
        {
            var c = text[i];
            if (quoted)
            {
                if (c == '"' && i + 1 < text.Length && text[i + 1] == '"') { cell.Append('"'); i++; }
                else if (c == '"') quoted = false;
                else cell.Append(c);
                continue;
            }
            switch (c)
            {
                case '"': quoted = true; break;
                case ',': row.Add(cell.ToString()); cell.Clear(); break;
                case '\r': break;
                case '\n': row.Add(cell.ToString()); cell.Clear(); rows.Add(row); row = []; break;
                case '\uFEFF' when i == 0: break;
                default: cell.Append(c); break;
            }
        }
        if (cell.Length > 0 || row.Count > 0) { row.Add(cell.ToString()); rows.Add(row); }
        return rows;
    }
}
