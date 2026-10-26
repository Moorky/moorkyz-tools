using System.Text.RegularExpressions;
using MoorkszVisoryMediaDownloader.Core.Infrastructure;
using MoorkszVisoryMediaDownloader.Core.Storage;

namespace MoorkszVisoryMediaDownloader.Core.Reporting;

public sealed record SuccessRecord(string Key, string RecordingId, string FileName, string? Camera, double ElapsedSeconds, long FileSize,
    long BytesTransferred, double TransferSeconds, int Attempts, TransferEngineKind Engine, string LocalPath, string? Sha256);

public sealed record FailRecord(string Key, string RecordingId, string FileName, string? Camera, long? FileSize, string Message, int? HttpStatus,
    int Attempts, TransferEngineKind Engine, Uri? Url, double ElapsedSeconds);

public sealed record LogEntry(DateTimeOffset Time, string Level, string Text);

public sealed record RunStatistics(int Succeeded, int Failed, int Skipped, double SuccessSeconds, long SuccessBytes, long TransferredBytes, double TransferSeconds)
{
    public double? AverageSeconds => Succeeded == 0 ? null : SuccessSeconds / Succeeded;
    public double? AverageBytes => Succeeded == 0 ? null : (double)SuccessBytes / Succeeded;
    /// <summary>Cumulative average rate: bytes moved by successful transfers divided by their transfer time.</summary>
    public double? AverageBytesPerSecond => TransferSeconds <= 0 ? null : TransferredBytes / TransferSeconds;
}

public static partial class Redactor
{
    public static string Clean(string? text)
    {
        if (string.IsNullOrEmpty(text)) return string.Empty;
        var result = Jwt().Replace(text, "[REDACTED-TOKEN]");
        result = AuthHeader().Replace(result, m => m.Groups[1].Value + "[REDACTED]");
        result = CookieHeader().Replace(result, m => m.Groups[1].Value + "[REDACTED]");
        result = BearerToken().Replace(result, m => m.Groups[1].Value + "[REDACTED]");
        result = UrlQuery().Replace(result, m => m.Groups[1].Value + "?[REDACTED]");
        return result;
    }

    [GeneratedRegex(@"eyJ[A-Za-z0-9_-]{8,}\.[A-Za-z0-9_-]{8,}\.[A-Za-z0-9_-]{8,}")]
    private static partial Regex Jwt();

    [GeneratedRegex(@"(?i)\b((?:proxy-)?authorization\s*[:=]\s*(?:(?:bearer|firebase|basic)\s+)?)[^\s,;|]+")]
    private static partial Regex AuthHeader();

    [GeneratedRegex(@"(?i)\b((?:set-)?cookie\s*[:=]\s*)[^|\r\n]+")]
    private static partial Regex CookieHeader();

    [GeneratedRegex(@"(?i)\b(bearer\s+)[A-Za-z0-9._~+/=-]{8,}")]
    private static partial Regex BearerToken();

    [GeneratedRegex(@"(https?://[^\s?#|""']+)\?[^\s|""']*")]
    private static partial Regex UrlQuery();
}

/// <summary>Writes the run's text log, CSV report, failed report and debug log as events occur.</summary>
public sealed class RunLogger : IDisposable
{
    public static readonly string[] ReportColumns =
    [
        "run_id", "timestamp", "recording_id", "filename", "camera", "status", "elapsed_seconds", "filesize_bytes", "filesize_human",
        "average_seconds", "average_filesize_bytes", "average_speed_bytes_per_sec", "bytes_transferred", "transfer_seconds",
        "attempts", "engine", "local_path", "sha256", "message",
    ];

    public static readonly string[] FailedColumns =
        ["timestamp", "filename", "filesize", "message", "url", "recording_id", "http_status", "attempts", "engine", "run_id"];

    private readonly object gate = new();
    private readonly StateStore store;
    private readonly TimeProvider time;
    private readonly bool storeFullUrls;
    private readonly StreamWriter log;
    private readonly StreamWriter report;
    private readonly StreamWriter failed;
    private readonly StreamWriter debug;
    private int succeeded, failedCount, skipped;
    private double successSeconds, transferSeconds;
    private long successBytes, transferredBytes;

    public RunLogger(BackupLayout layout, string runId, StateStore store, bool storeFullUrls, TimeProvider? time = null)
    {
        RunId = runId;
        this.store = store;
        this.storeFullUrls = storeFullUrls;
        this.time = time ?? TimeProvider.System;
        Directory.CreateDirectory(layout.LogFolder(runId));
        Directory.CreateDirectory(layout.FailedFolder(runId));
        LogPath = layout.LogPath(runId);
        ReportPath = layout.CsvReportPath(runId);
        FailedPath = layout.FailedReportPath(runId);
        DebugPath = layout.DebugLogPath(runId);
        SummaryPath = layout.SummaryPath(runId);
        SeedFromExistingReport();
        log = Open(LogPath, null);
        report = Open(ReportPath, Csv.Line(ReportColumns));
        failed = Open(FailedPath, Csv.Line(FailedColumns));
        debug = Open(DebugPath, null);
    }

    public string RunId { get; }
    public string LogPath { get; }
    public string ReportPath { get; }
    public string FailedPath { get; }
    public string DebugPath { get; }
    public string SummaryPath { get; }
    public int ScheduledTotal { get; set; }
    public event Action<LogEntry>? Entry;

    public RunStatistics Stats
    {
        get { lock (gate) return new RunStatistics(succeeded, failedCount, skipped, successSeconds, successBytes, transferredBytes, transferSeconds); }
    }

    public void Info(string message) => Write("INFO", Redactor.Clean(message));
    public void Warn(string message) => Write("WARN", Redactor.Clean(message));
    public void Retry(string fileName, string message) => Write("RETRY", $"@{fileName} | {Redactor.Clean(message)}");

    public void Debug(string message)
    {
        lock (gate) debug.WriteLine($"[{Format.LogTime(time.GetLocalNow())}] [DEBUG] {Redactor.Clean(message)}");
    }

    public void Skip(string key, string recordingId, string fileName, string? camera, long? size, string localPath, string? sha256, string message)
    {
        lock (gate)
        {
            if (!store.TryRecordFinal(RunId, key, "SKIP", time.GetUtcNow())) { Debug($"Duplicate final result suppressed for {key}"); return; }
            skipped++;
            var now = time.GetLocalNow();
            Write("SKIP", $"@{fileName} | {message}", now);
            report.WriteLine(Csv.Line([Csv.Text(RunId), Csv.Text(Format.LogTime(now)), Csv.Text(recordingId), Csv.Text(fileName), Csv.Text(camera), "SKIP", "",
                Csv.Number(size), Csv.Text(size is null ? "unknown" : Format.Size(size)), "", "", "", "0", "", "0", "", Csv.Text(localPath), Csv.Text(sha256), Csv.Text(message)]));
        }
    }

    /// <summary>Writes exactly one SUCCESS for a file per run. Returns false if a final result already exists.</summary>
    public bool Success(SuccessRecord r)
    {
        lock (gate)
        {
            if (!store.TryRecordFinal(RunId, r.Key, "SUCCESS", time.GetUtcNow())) { Debug($"Duplicate final result suppressed for {r.Key}"); return false; }
            succeeded++;
            successSeconds += r.ElapsedSeconds;
            successBytes += r.FileSize;
            transferredBytes += r.BytesTransferred;
            transferSeconds += r.TransferSeconds;
            var stats = Stats;
            var now = time.GetLocalNow();
            var progress = $"{succeeded + failedCount}/{Math.Max(ScheduledTotal, succeeded + failedCount)}";
            Write("SUCCESS", $"@{r.FileName} | Time: {Format.Seconds(r.ElapsedSeconds)} | Filesize: {Format.Size(r.FileSize)} | Current Average: {Format.Seconds(stats.AverageSeconds ?? 0)} / {Format.Size((long)(stats.AverageBytes ?? 0))} | Avg Speed: {(stats.AverageBytesPerSecond is { } speed ? Format.Speed(speed) : "unknown")} | Progress: {progress}", now);
            report.WriteLine(Csv.Line([Csv.Text(RunId), Csv.Text(Format.LogTime(now)), Csv.Text(r.RecordingId), Csv.Text(r.FileName), Csv.Text(r.Camera), "SUCCESS",
                Csv.Number(r.ElapsedSeconds, "0.000"), Csv.Number(r.FileSize), Csv.Text(Format.Size(r.FileSize)), Csv.Number(stats.AverageSeconds, "0.000"),
                Csv.Number(stats.AverageBytes, "0"), Csv.Number(stats.AverageBytesPerSecond, "0"), Csv.Number(r.BytesTransferred), Csv.Number(r.TransferSeconds, "0.000"),
                Csv.Number(r.Attempts), r.Engine.ToString(), Csv.Text(r.LocalPath), Csv.Text(r.Sha256), ""]));
            return true;
        }
    }

    /// <summary>Writes exactly one FAIL for a file per run and appends it to the failed report.</summary>
    public bool Fail(FailRecord r)
    {
        lock (gate)
        {
            if (!store.TryRecordFinal(RunId, r.Key, "FAIL", time.GetUtcNow())) { Debug($"Duplicate final result suppressed for {r.Key}"); return false; }
            failedCount++;
            var now = time.GetLocalNow();
            var message = Redactor.Clean(r.Message);
            Write("FAIL", $"@{r.FileName} | Added to failed report | {message}", now);
            var url = r.Url is null ? "" : storeFullUrls ? r.Url.AbsoluteUri : UrlPolicy.Redact(r.Url);
            report.WriteLine(Csv.Line([Csv.Text(RunId), Csv.Text(Format.LogTime(now)), Csv.Text(r.RecordingId), Csv.Text(r.FileName), Csv.Text(r.Camera), "FAIL",
                Csv.Number(r.ElapsedSeconds, "0.000"), Csv.Number(r.FileSize), Csv.Text(r.FileSize is null ? "unknown" : Format.Size(r.FileSize)), "", "", "", "", "",
                Csv.Number(r.Attempts), r.Engine.ToString(), "", "", Csv.Text(message)]));
            failed.WriteLine(Csv.Line([Csv.Text(Format.LogTime(now)), Csv.Text(r.FileName), Csv.Number(r.FileSize), Csv.Text(message), Csv.Text(url),
                Csv.Text(r.RecordingId), Csv.Number(r.HttpStatus), Csv.Number(r.Attempts), r.Engine.ToString(), Csv.Text(RunId)]));
            return true;
        }
    }

    public void WriteSummary(RunSummary summary) => JsonFiles.WriteAtomic(SummaryPath, summary);

    private void Write(string level, string text, DateTimeOffset? at = null)
    {
        var now = at ?? time.GetLocalNow();
        lock (gate) log.WriteLine($"[{Format.LogTime(now)}] [{level}] {text}");
        Entry?.Invoke(new LogEntry(now, level, text));
    }

    private static StreamWriter Open(string path, string? header)
    {
        var isNew = !File.Exists(path) || new FileInfo(path).Length == 0;
        var stream = new FileStream(path, FileMode.Append, FileAccess.Write, FileShare.ReadWrite | FileShare.Delete);
        var writer = new StreamWriter(stream, isNew && header is not null ? Csv.Utf8WithBom : new System.Text.UTF8Encoding(false)) { AutoFlush = true, NewLine = "\r\n" };
        if (isNew && header is not null) writer.WriteLine(header);
        return writer;
    }

    private void SeedFromExistingReport()
    {
        if (!File.Exists(ReportPath)) return;
        var rows = Csv.Parse(File.ReadAllText(ReportPath));
        if (rows.Count < 2) return;
        var columns = rows[0];
        int Col(string name) => columns.IndexOf(name);
        foreach (var row in rows.Skip(1).Where(r => r.Count == columns.Count))
        {
            switch (row[Col("status")])
            {
                case "SUCCESS":
                    succeeded++;
                    successSeconds += Parse(row[Col("elapsed_seconds")]);
                    successBytes += (long)Parse(row[Col("filesize_bytes")]);
                    transferredBytes += (long)Parse(row[Col("bytes_transferred")]);
                    transferSeconds += Parse(row[Col("transfer_seconds")]);
                    break;
                case "FAIL": failedCount++; break;
                case "SKIP": skipped++; break;
            }
        }
        static double Parse(string text) => double.TryParse(text, System.Globalization.NumberStyles.Float, Format.Invariant, out var value) ? value : 0;
    }

    public void Dispose()
    {
        lock (gate)
        {
            log.Dispose();
            report.Dispose();
            failed.Dispose();
            debug.Dispose();
        }
    }
}
