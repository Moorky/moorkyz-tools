using System.Text.RegularExpressions;
using MoorkszVisoryMediaDownloader.Core.Infrastructure;
using MoorkszVisoryMediaDownloader.Core.Storage;

namespace MoorkszVisoryMediaDownloader.Core.Reporting;

public sealed record SuccessRecord(string Key, string RecordingId, string FileName, string? Camera, double ElapsedSeconds, long FileSize,
    long BytesTransferred, double TransferSeconds, int Attempts, TransferEngineKind Engine, string LocalPath, string? Sha256)
{
    public double? MediaLengthSeconds { get; init; }
}

public sealed record FailRecord(string Key, string RecordingId, string FileName, string? Camera, long? FileSize, string Message, int? HttpStatus,
    int Attempts, TransferEngineKind Engine, Uri? Url, double ElapsedSeconds)
{
    public string? LocalPath { get; init; }
    public double? MediaLengthSeconds { get; init; }
}

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
        result = UrlCredentials().Replace(result, "$1[REDACTED]@");
        result = SecretValue().Replace(result, "$1[REDACTED]");
        result = Controls().Replace(result, "");
        return result;
    }

    [GeneratedRegex(@"eyJ[A-Za-z0-9_-]{8,}\.[A-Za-z0-9_-]{8,}\.[A-Za-z0-9_-]{8,}")]
    private static partial Regex Jwt();

    [GeneratedRegex("""(?i)\b((?:proxy-)?authorization["']?\s*[:=]\s*["']?(?:(?:bearer|firebase|basic)\s+)?)[^\s,;|"'\\]+""")]
    private static partial Regex AuthHeader();

    [GeneratedRegex("""(?i)\b((?:set-)?cookie["']?\s*[:=]\s*["']?)[^|\r\n]+""")]
    private static partial Regex CookieHeader();

    [GeneratedRegex(@"(?i)\b(bearer\s+)[A-Za-z0-9._~+/=-]{8,}")]
    private static partial Regex BearerToken();

    [GeneratedRegex(@"(?i)(https?://[^\s?#|""']+)[?#][^\s|""']*")]
    private static partial Regex UrlQuery();

    [GeneratedRegex(@"(?i)(https?://)[^/\s@]+@")]
    private static partial Regex UrlCredentials();

    [GeneratedRegex("""(?i)(\b(?:password|passwd|token|access_token|refresh_token|id_token|api[_-]?key|secret)\b["']?\s*[:=]\s*)(?:\[REDACTED\]|"[^"\r\n]*"|'[^'\r\n]*'|[^\s,;|}\]]+)""")]
    private static partial Regex SecretValue();

    [GeneratedRegex(@"[\x00-\x08\x0B\x0C\x0E-\x1F\x7F]")]
    private static partial Regex Controls();
}

/// <summary>Persists every event and file result before returning to the caller.</summary>
public sealed class RunLogger : IDisposable
{
    public static readonly string[] ReportColumns =
    [
        "run_id", "timestamp", "recording_id", "filename", "camera", "status", "elapsed_seconds", "filesize_bytes", "filesize_human",
        "average_seconds", "average_filesize_bytes", "average_speed_bytes_per_sec", "bytes_transferred", "transfer_seconds",
        "attempts", "engine", "local_path", "sha256", "message", "url", "http_status",
        "id", "length_seconds", "log_file", "log_line", "error_file", "error_line", "timestamp_offset",
    ];

    private readonly object gate = new();
    private readonly StateStore store;
    private readonly TimeProvider time;
    private readonly DiagnosticLog diagnostics;
    private readonly DurableLines report;
    private readonly BackupLayout layout;
    private string summaryText = "Run not completed. CSV contains results durably saved so far.";
    private bool disposed;
    private int succeeded, failedCount, skipped;
    private double successSeconds, transferSeconds;
    private long successBytes, transferredBytes;

    public RunLogger(BackupLayout layout, string runId, StateStore store, bool storeFullUrls, TimeProvider? time = null)
    {
        RunId = runId;
        this.layout = layout;
        this.store = store;
        this.time = time ?? TimeProvider.System;
        PathSafety.EnsureNoReparsePoints(Path.GetPathRoot(layout.Root)!, layout.RunFolder(runId));
        LogPath = layout.LogPath(runId);
        ReportPath = layout.CsvReportPath(runId);
        FailedPath = layout.FailedReportPath(runId);
        DebugPath = layout.DebugLogPath(runId);
        SummaryPath = layout.SummaryPath(runId);
        HtmlPath = layout.HtmlReportPath(runId);
        foreach (var path in new[] { LogPath, ReportPath, FailedPath, SummaryPath, HtmlPath })
        {
            PathSafety.EnsureNoReparsePoints(Path.GetPathRoot(path)!, path);
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        }
        ImportLegacyReport(layout);
        UpgradeReportSchema();
        SeedFromExistingReport();
        var isNew = !File.Exists(ReportPath) || new FileInfo(ReportPath).Length == 0;
        report = new DurableLines(ReportPath, csv: true);
        try
        {
            if (isNew) report.WriteLine(Csv.Line(ReportColumns));
            diagnostics = new DiagnosticLog(LogPath, FailedPath, runId, this.time);
            diagnostics.Entry += entry => Entry?.Invoke(entry);
            RenderReport();
        }
        catch { report.Dispose(); throw; }
        if (storeFullUrls) Info("Full URL logging is disabled: credentials and URL parameters are always redacted.");
    }

    public string RunId { get; }
    public string LogPath { get; }
    public string ReportPath { get; }
    public string FailedPath { get; }
    public string DebugPath { get; }
    public string SummaryPath { get; }
    public string HtmlPath { get; }
    public int ScheduledTotal { get; set; }
    public event Action<LogEntry>? Entry;

    public RunStatistics Stats
    {
        get { lock (gate) return new RunStatistics(succeeded, failedCount, skipped, successSeconds, successBytes, transferredBytes, transferSeconds); }
    }

    public void Info(string message) => Write("INFO", Redactor.Clean(message));
    public void Warn(string message) => Write("WARN", Redactor.Clean(message));
    public void Error(string message)
    {
        lock (gate)
        {
            var location = Write("ERROR", message);
            AppendRunRow("ERROR", null, message, location);
        }
    }
    public void Retry(string fileName, string message) => Write("RETRY", $"@{fileName} | {Redactor.Clean(message)}");

    public void Debug(string message)
    {
        Write("DEBUG", message);
    }

    public void Skip(string key, string recordingId, string fileName, string? camera, long? size, string localPath, string? sha256, string message)
    {
        lock (gate)
        {
            if (!store.TryRecordFinal(RunId, key, "SKIP", time.GetUtcNow())) { Debug($"Duplicate final result suppressed for {key}"); return; }
            skipped++;
            var now = time.GetLocalNow();
            var location = Write("SKIP", $"@{fileName} | {message}", now);
            AppendResult([Text(RunId), Text(now.ToString("yyyy-MM-dd HH:mm:ss", Format.Invariant)), Text(recordingId), Text(fileName), Text(camera), "SKIP", "",
                Csv.Number(size), Text(size is null ? "unknown" : Format.Size(size)), "", "", "", "0", "", "0", "", Text(localPath), Text(sha256), Text(message), "", ""], now, location);
        }
    }

    /// <summary>Writes exactly one SUCCESS for a file per run. Returns false if a final result already exists.</summary>
    public bool Success(SuccessRecord r)
    {
        ValidateLength(r.MediaLengthSeconds);
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
            var location = Write("SUCCESS", $"@{r.FileName} | Time: {Format.Seconds(r.ElapsedSeconds)} | Filesize: {Format.Size(r.FileSize)} | Current Average: {Format.Seconds(stats.AverageSeconds ?? 0)} / {Format.Size((long)(stats.AverageBytes ?? 0))} | Avg Speed: {(stats.AverageBytesPerSecond is { } speed ? Format.Speed(speed) : "unknown")} | Progress: {progress}", now);
            AppendResult([Text(RunId), Text(now.ToString("yyyy-MM-dd HH:mm:ss", Format.Invariant)), Text(r.RecordingId), Text(r.FileName), Text(r.Camera), "SUCCESS",
                Csv.Number(r.ElapsedSeconds, "0.000"), Csv.Number(r.FileSize), Text(Format.Size(r.FileSize)), Csv.Number(stats.AverageSeconds, "0.000"),
                Csv.Number(stats.AverageBytes, "0"), Csv.Number(stats.AverageBytesPerSecond, "0"), Csv.Number(r.BytesTransferred), Csv.Number(r.TransferSeconds, "0.000"),
                Csv.Number(r.Attempts), r.Engine.ToString(), Text(r.LocalPath), Text(r.Sha256), Text("Verified download"), "", ""], now, location, r.MediaLengthSeconds);
            return true;
        }
    }

    /// <summary>Writes exactly one FAIL for a file per run and appends it to the failed report.</summary>
    public bool Fail(FailRecord r)
    {
        ValidateLength(r.MediaLengthSeconds);
        lock (gate)
        {
            if (!store.TryRecordFinal(RunId, r.Key, "FAIL", time.GetUtcNow())) { Debug($"Duplicate final result suppressed for {r.Key}"); return false; }
            failedCount++;
            var now = time.GetLocalNow();
            var message = Redactor.Clean(r.Message);
            var location = Write("FAIL", $"@{r.FileName} | Added to failed report | {message}", now);
            var url = r.Url is null ? "" : UrlPolicy.Redact(r.Url);
            AppendResult([Text(RunId), Text(now.ToString("yyyy-MM-dd HH:mm:ss", Format.Invariant)), Text(r.RecordingId), Text(r.FileName), Text(r.Camera), "FAIL",
                Csv.Number(r.ElapsedSeconds, "0.000"), Csv.Number(r.FileSize), Text(r.FileSize is null ? "unknown" : Format.Size(r.FileSize)), "", "", "", "", "",
                Csv.Number(r.Attempts), r.Engine.ToString(), Text(r.LocalPath), "", Text(message), Text(url), Csv.Number(r.HttpStatus)], now, location, r.MediaLengthSeconds);
            return true;
        }
    }

    public void WriteSummary(RunSummary summary)
    {
        lock (gate)
        {
            summaryText = Redactor.Clean($"Run: {RunId} | Start: {summary.StartedAt:O} | End: {summary.FinishedAt:O} | Duration: {Format.Seconds(summary.ElapsedSeconds)} | Status: {summary.State} | {summary.Outcome} | Discovered: {summary.Discovered} | Succeeded: {summary.Succeeded} | Failed: {summary.Failed} | Already verified: {summary.AlreadyVerified} | Pending/canceled: {summary.PendingOrCanceled} | Archive: {summary.ArchiveStatus}");
            AppendRunRow("RUN", summary.ElapsedSeconds, summaryText);
            RenderReport();
        }
    }

    private static string Text(string? value) => Csv.Text(Redactor.Clean(value));
    private static void ValidateLength(double? length)
    {
        if (length is { } value && (!double.IsFinite(value) || value < 0))
            throw new ArgumentOutOfRangeException(nameof(length), "Media length must be finite and nonnegative.");
    }
    private void AppendRunRow(string status, double? seconds, string message, LogLocation? location = null)
    {
        var cells = Enumerable.Repeat("", 21).ToArray();
        cells[0] = Text(RunId);
        var now = time.GetLocalNow();
        cells[1] = Text(now.ToString("yyyy-MM-dd HH:mm:ss", Format.Invariant));
        cells[5] = status;
        cells[6] = Csv.Number(seconds, "0.000");
        cells[18] = Text(message);
        AppendResult(cells, now, location);
    }

    private void AppendResult(string[] cells, DateTimeOffset now, LogLocation? location, double? length = null)
    {
        report.WriteLine(Csv.Line(cells.Concat([
            Text(now.ToUnixTimeMilliseconds().ToString(Format.Invariant) + "-" + Guid.NewGuid().ToString("N")),
            Csv.Number(length, "0.000"),
            location is null ? "" : Text(Path.GetRelativePath(layout.Root, LogPath)),
            Csv.Number(location?.LogLine),
            location?.ErrorLine is null ? "" : Text(Path.GetRelativePath(layout.Root, FailedPath)),
            Csv.Number(location?.ErrorLine),
            Text(now.ToString("O", Format.Invariant)),
        ])));
    }

    private void RenderReport()
    {
        CsvReport.Render(ReportPath, SummaryPath, HtmlPath, $"Visory report - {RunId}", summaryText);
        DailyReport.Render(layout);
    }

    private LogLocation Write(string level, string text, DateTimeOffset? at = null)
    {
        lock (gate) return diagnostics.Write(level, text, "backup", at);
    }

    private void SeedFromExistingReport()
    {
        if (!File.Exists(ReportPath)) return;
        var rows = Csv.Parse(File.ReadAllText(ReportPath));
        if (rows.Count < 2) return;
        var columns = rows[0];
        int Col(string name) => columns.IndexOf(name);
        if (ReportColumns.Any(column => Col(column) < 0))
            throw new InvalidDataException("This CSV report uses an incompatible schema; preserve it and start a new run.");
        foreach (var row in rows.Skip(1))
        {
            if (row.Count != columns.Count) throw new InvalidDataException("A CSV result is incomplete. Preserve the report and start a new run.");
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
                case "RUN" or "ERROR": break;
                default: throw new InvalidDataException("A saved CSV result has an unsupported status.");
            }
        }
        static double Parse(string text) => double.TryParse(text, System.Globalization.NumberStyles.Float, Format.Invariant, out var value) && double.IsFinite(value) && value >= 0
            ? value : throw new InvalidDataException("A saved CSV statistic is invalid. Preserve the report and start a new run.");
    }

    private void ImportLegacyReport(BackupLayout layout)
    {
        var legacy = layout.LegacyCsvReportPath(RunId);
        if (File.Exists(ReportPath) || !File.Exists(legacy)) return;
        PathSafety.EnsureNoReparsePoints(Path.GetPathRoot(legacy)!, legacy);
        var rows = Csv.Parse(File.ReadAllText(legacy));
        if (rows.Count == 0 || !rows[0].SequenceEqual(ReportColumns.Take(19)))
            throw new InvalidDataException("The legacy CSV report has an unsupported header; it has been preserved.");
        var temp = ReportPath + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            using (var imported = new DurableLines(temp, csv: true))
            {
                imported.WriteLine(Csv.Line(ReportColumns));
                foreach (var row in rows.Skip(1))
                {
                    if (row.Count != 19) throw new InvalidDataException("The legacy CSV contains an incomplete row; it has been preserved.");
                    imported.WriteLine(Csv.Line(row.Select(Text).Concat(Enumerable.Repeat("", ReportColumns.Length - row.Count))));
                }
            }
            File.Move(temp, ReportPath, overwrite: false);
        }
        finally { if (File.Exists(temp)) File.Delete(temp); }
    }

    private void UpgradeReportSchema()
    {
        if (!File.Exists(ReportPath)) return;
        var rows = Csv.Parse(File.ReadAllText(ReportPath));
        if (rows.Count == 0 || rows[0].SequenceEqual(ReportColumns)) return;
        if (!rows[0].SequenceEqual(ReportColumns.Take(21)))
            throw new InvalidDataException("The saved CSV schema is not supported; preserve it and start a new run.");
        var temp = ReportPath + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            using (var upgraded = new DurableLines(temp, csv: true))
            {
                upgraded.WriteLine(Csv.Line(ReportColumns));
                foreach (var row in rows.Skip(1))
                {
                    if (row.Count != 21) throw new InvalidDataException("An earlier CSV row is incomplete.");
                    upgraded.WriteLine(Csv.Line(row.Select(Text).Concat(Enumerable.Repeat("", ReportColumns.Length - row.Count))));
                }
            }
            File.Copy(ReportPath, ReportPath + ".legacy-" + Guid.NewGuid().ToString("N") + ".csv", overwrite: false);
            File.Move(temp, ReportPath, overwrite: true);
        }
        finally { if (File.Exists(temp)) File.Delete(temp); }
    }

    public void Dispose()
    {
        lock (gate)
        {
            if (disposed) return;
            disposed = true;
            try { RenderReport(); }
            finally
            {
                try { diagnostics.Dispose(); }
                finally { report.Dispose(); }
            }
        }
    }
}
