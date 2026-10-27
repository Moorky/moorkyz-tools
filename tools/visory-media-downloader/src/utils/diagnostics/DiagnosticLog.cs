using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using MoorkszVisoryMediaDownloader.Core.Infrastructure;

namespace MoorkszVisoryMediaDownloader.Core.Reporting;

public sealed class DurableLines : IDisposable
{
    private readonly object gate = new();
    private readonly string path;
    private readonly Encoding encoding;
    private bool disposed;
    public int LineCount { get; private set; }

    public DurableLines(string path, bool csv = false)
    {
        path = Path.GetFullPath(path);
        PathSafety.EnsureNoReparsePoints(Path.GetPathRoot(path)!, path);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        PathSafety.EnsureNoReparsePoints(Path.GetPathRoot(path)!, path);
        this.path = path;
        encoding = csv ? Csv.Utf8WithBom : new UTF8Encoding(false);
        using (new FileStream(path, FileMode.Append, FileAccess.Write, FileShare.Read)) { }
        LineCount = File.ReadLines(path).Count();
    }

    public int WriteLine(string line)
    {
        lock (gate)
        {
            ObjectDisposedException.ThrowIf(disposed, this);
            PathSafety.EnsureNoReparsePoints(Path.GetPathRoot(path)!, path);
            using var stream = new FileStream(path, FileMode.Append, FileAccess.Write, FileShare.Read);
            using var writer = new StreamWriter(stream, encoding) { NewLine = "\r\n" };
            writer.WriteLine(line);
            writer.Flush();
            stream.Flush(flushToDisk: true);
            return ++LineCount;
        }
    }

    public void Dispose() { lock (gate) disposed = true; }
}

public sealed record LogLocation(int LogLine, int? ErrorLine);

public sealed class DiagnosticLog : IDisposable
{
    private readonly object gate = new();
    private readonly DurableLines log;
    private readonly DurableLines errors;
    private readonly TimeProvider time;

    public DiagnosticLog(string logPath, string errorPath, string runId, TimeProvider? time = null)
    {
        RunId = runId;
        this.time = time ?? TimeProvider.System;
        log = new DurableLines(logPath);
        try { errors = new DurableLines(errorPath); }
        catch { log.Dispose(); throw; }
    }

    public string RunId { get; }
    public event Action<LogEntry>? Entry;

    public LogLocation Write(string level, string message, string area = "app", DateTimeOffset? at = null)
    {
        var clean = Redactor.Clean(message);
        lock (gate)
        {
            var now = at ?? time.GetLocalNow();
            var location = new LogLocation(log.LineCount + 1, level is "ERROR" or "FATAL" or "FAIL" ? errors.LineCount + 1 : null);
            foreach (var part in clean.Replace("\r\n", "\n", StringComparison.Ordinal).Replace('\r', '\n').Split('\n'))
            {
                var text = $"{part} | Run: {RunId} | Area: {area}";
                var prefix = $"[{Format.LogTime(now)}] [{level}] ";
                var line = prefix + text;
                var markdown = MarkdownLine(Format.LogTime(now), level, text);
                log.WriteLine(markdown + $"<a id=\"L{log.LineCount + 1}\"></a>");
                if (level is "ERROR" or "FATAL" or "FAIL")
                    errors.WriteLine(markdown + $"<a id=\"L{errors.LineCount + 1}\"></a>");
                Console.WriteLine(line);
                Entry?.Invoke(new LogEntry(now, level, text));
            }
            return location;
        }
    }

    public static string MarkdownText(string value) => WebUtility.HtmlEncode(value)
        .Replace("[", "&#91;", StringComparison.Ordinal).Replace("]", "&#93;", StringComparison.Ordinal)
        .Replace("`", "&#96;", StringComparison.Ordinal);

    public static string MarkdownLine(string timestamp, string level, string text)
    {
        var color = level switch { "SUCCESS" or "SKIP" => "#216234", "FAIL" => "#b42323", "ERROR" or "FATAL" => "#711b24", _ => "#344054" };
        var messageColor = level is "FAIL" or "ERROR" or "FATAL" ? "#b42323" : "inherit";
        const string mono = "font-family:'DM Mono',Consolas,monospace";
        var encoded = MarkdownText(text);
        encoded = Regex.Replace(encoded, @"(?<=Run: )[^|]+", match => $"<code style=\"{mono}\">{match.Value.TrimEnd()}</code> ");
        return $"<code style=\"{mono}\">[{MarkdownText(timestamp)}]</code> " +
            $"<strong style=\"color:{color};background:#fafbf7\">[{MarkdownText(level)}]</strong> " +
            $"<span style=\"color:{messageColor}{(messageColor == "inherit" ? "" : ";background:#fafbf7")}\">{encoded}</span><br>";
    }

    public static string PlainText(string markdown) => WebUtility.HtmlDecode(Regex.Replace(markdown, "<[^>]*>", ""));

    public void Dispose()
    {
        lock (gate)
        {
            try { log.Dispose(); }
            finally { errors.Dispose(); }
        }
    }
}
