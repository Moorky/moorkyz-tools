using System.Diagnostics;
using System.Net;
using System.Text.RegularExpressions;
using MoorkszVisoryMediaDownloader.Core;
using MoorkszVisoryMediaDownloader.Core.Archive;
using MoorkszVisoryMediaDownloader.Core.Backup;
using MoorkszVisoryMediaDownloader.Core.Infrastructure;
using MoorkszVisoryMediaDownloader.Core.Reporting;
using MoorkszVisoryMediaDownloader.Core.Storage;

namespace MoorkszVisoryMediaDownloader.Tests;

internal static partial class Program
{
    private static async Task DurableDiagnostics()
    {
        await using var f = Fixture.Create();
        var folder = f.Sub("diagnostics");
        var logPath = Path.Combine(folder, "log.md");
        var errorPath = Path.Combine(folder, "errors.md");
        var entries = new List<LogEntry>();
        using (var log = new DiagnosticLog(logPath, errorPath, "diagnostics-test"))
        {
            log.Entry += entries.Add;
            log.Write("INFO", "first");
            Assert.Equal(1, File.ReadAllLines(logPath).Length, "visible before disposal, not buffered until exit");
            log.Write("ERROR", "two\r\nthree\nfour\rpassword=hide-me <script>bad</script>");
            Assert.Equal(5, File.ReadAllLines(logPath).Length, "each physical line persists separately");
            Assert.Equal(4, File.ReadAllLines(errorPath).Length, "every error detail is timestamped");
            Parallel.For(0, 40, index => log.Write("DEBUG", "worker " + index));
            Assert.Equal(45, File.ReadAllLines(logPath).Length, "concurrent lines are complete");
            Assert.Equal(45, entries.Count, "live events and saved lines share a source");
            foreach (var line in File.ReadAllLines(logPath))
                Assert.True(Regex.IsMatch(line, @"^\[\d{4}-\d{2}-\d{2} \d{2}:\d{2}:\d{2}\.\d{3} [+-]\d{2}:\d{2}\] \[[A-Z]+\] .+ \| Run: diagnostics-test \| Area: app<a id=""L\d+""></a>$"), "timestamp, severity, run and area on each line");
            using (var locked = new FileStream(logPath, FileMode.Open, FileAccess.Read, FileShare.None))
                Assert.Throws<IOException>(() => log.Write("INFO", "blocked"), "storage failures propagate explicitly");
        }
        var text = File.ReadAllText(logPath);
        Assert.True(!text.Contains("hide-me", StringComparison.Ordinal) && !text.Contains("<script>", StringComparison.Ordinal), "Markdown escaped and redacted");
        foreach (var secret in new[] { "password=\"with spaces\"", "token=abc123", "Authorization: Bearer abcsecret", "Cookie: session=private", "https://user:pass@h.example/p?q=secret#fragment", "https://h.example/p#secret" })
        {
            var clean = Redactor.Clean(secret);
            Assert.Equal(clean, Redactor.Clean(clean), "redaction is idempotent across terminal/log/report sinks");
            Assert.True(!clean.Contains("with spaces", StringComparison.Ordinal) && !clean.Contains("abc123", StringComparison.Ordinal)
                && !clean.Contains("abcsecret", StringComparison.Ordinal) && !clean.Contains("private", StringComparison.Ordinal)
                && !clean.Contains("user:pass", StringComparison.Ordinal) && !clean.Contains("#secret", StringComparison.Ordinal), "sensitive field redacted");
        }
        Assert.True(!Redactor.Clean("""{"Authorization": "Basic JSON-HEADER-SECRET", "Cookie": "JSON-COOKIE-SECRET"}""").Contains("JSON-", StringComparison.Ordinal), "quoted headers redacted");
        using (var resumed = new DiagnosticLog(logPath, errorPath, "diagnostics-test")) resumed.Write("INFO", "resumed");
        Assert.Equal(46, File.ReadAllLines(logPath).Length, "resume appends, never truncates");
    }

    private static async Task ReportViews()
    {
        await using var f = Fixture.Create();
        f.Layout.Ensure();
        using var store = new StateStore(f.Layout.DatabasePath);
        store.AddRunItems("views", ["bad"], "scheduled");
        using (var log = new RunLogger(f.Layout, "views", store, storeFullUrls: true))
        {
            log.Fail(new FailRecord("bad", "id", "=bad|<img src=x>.mp4", "token=CAMERA-SECRET", null,
                "password=RESULT-SECRET\nsecond line", 403, 1, TransferEngineKind.Http, new Uri("https://example.test/a?token=URL-SECRET"), 1.234));
            Assert.Equal(2, Csv.Parse(File.ReadAllText(log.ReportPath)).Count, "CSV result persisted before logger disposal");
            log.WriteSummary(new RunSummary { RunId = "views", State = RunState.CompletedWithErrors, Failed = 1, ElapsedSeconds = 1.234 });
            var csv = Csv.Parse(File.ReadAllText(log.ReportPath));
            var html = File.ReadAllText(log.HtmlPath);
            var cells = Regex.Matches(html, @"<(?:td|th scope=""col"")>(.*?)</(?:td|th)>", RegexOptions.Singleline)
                .Select(match => WebUtility.HtmlDecode(match.Groups[1].Value)).ToList();
            Assert.True(csv.SelectMany(row => row).SequenceEqual(cells), "HTML embeds the exact CSV cells, not merely a link");
            var md = File.ReadAllText(log.SummaryPath);
            Assert.True(md.Contains("&#124;&lt;img", StringComparison.Ordinal) && md.Contains("<br>", StringComparison.Ordinal), "Markdown table escaping");
            var markdownCells = md.Split('\n').Where(line => line.StartsWith("| ", StringComparison.Ordinal) && !line.StartsWith("| ---", StringComparison.Ordinal))
                .SelectMany(line => line.TrimEnd('\r').Split('|').Skip(1).SkipLast(1).Select(cell => WebUtility.HtmlDecode(cell.Trim().Replace("<br>", "\n", StringComparison.Ordinal))));
            Assert.True(csv.SelectMany(row => row).SequenceEqual(markdownCells), "Markdown embeds the exact CSV cells");
            Assert.True(html.Contains("default-src &#39;none&#39;", StringComparison.Ordinal) || html.Contains("default-src 'none'", StringComparison.Ordinal), "offline CSP");
            foreach (var path in Directory.GetFiles(f.Layout.RunFolder("views"), "*", SearchOption.AllDirectories))
            {
                var content = File.ReadAllText(path);
                foreach (var secret in new[] { "CAMERA-SECRET", "RESULT-SECRET", "URL-SECRET" })
                    Assert.True(!content.Contains(secret, StringComparison.Ordinal), "redacted in every output: " + path);
                Assert.True(Path.GetExtension(path) is ".csv" or ".md" or ".html", "only requested output formats");
            }
        }
        using (var empty = new RunLogger(f.Layout, "empty", store, false))
        {
            empty.WriteSummary(new RunSummary { RunId = "empty", State = RunState.Completed, Discovered = 0, Outcome = "No recordings" });
            var rows = Csv.Parse(File.ReadAllText(empty.ReportPath));
            Assert.Equal(2, rows.Count, "zero recordings still has a CSV run summary");
            Assert.Equal("RUN", rows[1][5], "run-summary row, not an invented file");
            Assert.True(File.ReadAllText(empty.SummaryPath).Contains("Discovered: 0", StringComparison.Ordinal), "zero-result report is explicit");
        }
        using (var resumed = new RunLogger(f.Layout, "views", store, false))
            Assert.Equal(1, resumed.Stats.Failed, "statistics reseeded without duplicate CSV results");
    }

    private static async Task AbruptDiagnostics()
    {
        await using var f = Fixture.Create();
        var folder = f.Sub("abrupt");
        var info = new ProcessStartInfo(Environment.ProcessPath!) { UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true };
        info.ArgumentList.Add("--diagnostic-child");
        info.ArgumentList.Add(folder);
        using var process = Process.Start(info) ?? throw new InvalidOperationException("Diagnostic test child did not start.");
        var stdout = process.StandardOutput.ReadToEndAsync();
        var stderr = process.StandardError.ReadToEndAsync();
        await process.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(15));
        Assert.Equal(37, process.ExitCode, "child exited without disposal");
        Assert.True(File.ReadAllText(Path.Combine(folder, "log.md")).Contains("durable-before-exit", StringComparison.Ordinal), "last log line survives abrupt exit");
        Assert.True(File.ReadAllText(Path.Combine(folder, "errors.md")).Contains("durable-before-exit", StringComparison.Ordinal), "error line survives abrupt exit");
        Assert.True((await stdout).Contains("durable-before-exit", StringComparison.Ordinal), "terminal receives same event");
        Assert.Equal("", await stderr, "child had no incidental error");
        var csv = Csv.Parse(File.ReadAllText(Path.Combine(folder, "result.csv")));
        Assert.Equal(2, csv.Count, "CSV row survives exit without writer disposal");
        Assert.Equal("durable-result", csv[1][1], "last result present");
    }

    private static async Task LegacyReports()
    {
        await using var f = Fixture.Create();
        f.Layout.Ensure();
        using var store = new StateStore(f.Layout.DatabasePath);
        Directory.CreateDirectory(f.Layout.LogFolder("legacy"));
        var old = f.Layout.LegacyCsvReportPath("legacy");
        var row = new[] { "legacy", "2026-10-26 12:00:00", "id", "old.mp4", "", "SUCCESS", "2.000", "1024", "1.00 KB", "2.000", "1024", "512", "1024", "2.000", "1", "Http", @"C:\fixture\old.mp4", "ab", "" };
        var original = Csv.Line(RunLogger.ReportColumns.Take(19)) + "\r\n" + Csv.Line(row.Select(Csv.Text)) + "\r\n";
        File.WriteAllText(old, original, Csv.Utf8WithBom);
        using (var resumed = new RunLogger(f.Layout, "legacy", store, false))
        {
            Assert.Equal(1, resumed.Stats.Succeeded, "legacy success statistics retained");
            Assert.Equal(1024L, resumed.Stats.SuccessBytes, "legacy byte statistics retained");
            Assert.Equal(RunLogger.ReportColumns.Length, Csv.Parse(File.ReadAllText(resumed.ReportPath))[1].Count, "legacy CSV expanded to current schema");
        }
        Assert.Equal(original, File.ReadAllText(old), "legacy report not overwritten");
        using (var log = new RunLogger(f.Layout, "invalid", store, false)) { }
        File.AppendAllText(f.Layout.CsvReportPath("invalid"), "broken,row\r\n");
        Assert.Throws<InvalidDataException>(() => new RunLogger(f.Layout, "invalid", store, false), "invalid results do not silently reset counts");
    }

    private sealed class ReportClock : TimeProvider
    {
        public DateTimeOffset Now = new(2026, 10, 27, 0, 0, 0, TimeSpan.Zero);
        public override DateTimeOffset GetUtcNow() => Now;
    }

    private sealed class FailingArchiveProgress(ReportClock clock) : IProgress<ArchiveProgress>
    {
        public void Report(ArchiveProgress value)
        {
            clock.Now = clock.Now.AddSeconds(12);
            throw new IOException("Synthetic archive failure");
        }
    }

    private static async Task ArchiveErrorReports()
    {
        await using var f = Fixture.Create();
        f.Server.MapMedia("/archive.mp4", Data(1000, 42));
        f.Provider.Items.Add(f.Recording("archive", "/archive.mp4"));
        var clock = new ReportClock();
        var coordinator = new BackupCoordinator(f.Layout, f.Provider, new AppSettings(), disk: new FakeDisk(long.MaxValue / 4), time: clock, engineOptions: Fixture.FastOptions());
        f.Coordinators.Add(coordinator);
        await coordinator.ScanAsync(null, CancellationToken.None);
        var summary = await coordinator.RunBackupAsync(new BackupRequest { CreateArchive = true }, new FailingArchiveProgress(clock), CancellationToken.None);
        Assert.Equal(RunState.CompletedWithErrors, summary.State, "archive failure cannot masquerade as run success");
        Assert.Equal("failed", summary.ArchiveStatus, "archive has its own outcome");
        Assert.Equal(12.0, summary.ElapsedSeconds, "whole-run span includes archive time");
        Assert.Equal(1, summary.Succeeded, "verified recording is retained");
        var csv = Csv.Parse(File.ReadAllText(summary.CsvReportPath!));
        Assert.True(csv.Any(row => row[5] == "ERROR" && row[18].Contains("Synthetic archive failure", StringComparison.Ordinal)), "non-file error in CSV report");
        Assert.True(File.ReadAllText(summary.FailedReportPath!).Contains("Synthetic archive failure", StringComparison.Ordinal), "archive error also in timestamped Markdown");
    }

    private static async Task DailyReports()
    {
        await using var f = Fixture.Create();
        f.Layout.Ensure();
        using var store = new StateStore(f.Layout.DatabasePath);
        var clock = new ReportClock();
        clock.Now = new DateTimeOffset(2026, 10, 27, 12, 0, 0, TimeZoneInfo.Local.GetUtcOffset(new DateTime(2026, 10, 27))).ToUniversalTime();
        store.AddRunItems("day-one", ["one", "two", "skip"], "scheduled");
        using (var log = new RunLogger(f.Layout, "day-one", store, false, clock))
        {
            log.Success(new SuccessRecord("one", "one", "door.mp4", null, 2.5, 1200, 1200, 2, 1, TransferEngineKind.Http,
                Path.Combine(f.Layout.Downloads, "door.mp4"), null) { MediaLengthSeconds = 12.25 });
            log.Fail(new FailRecord("two", "two", "bad.mp4", null, null, "failed\nsecond detail", 404, 1, TransferEngineKind.Http, null, 3.25)
                { LocalPath = Path.Combine(f.Layout.Downloads, "bad.mp4") });
            log.Error("Synthetic operation error");
            log.Skip("skip", "skip", "existing.mp4", null, 4, Path.Combine(f.Layout.Downloads, "existing.mp4"), null, "verified");
        }
        store.AddRunItems("same-day", ["four"], "scheduled");
        using (var log = new RunLogger(f.Layout, "same-day", store, false, clock))
            log.Fail(new FailRecord("four", "four", "another.mp4", null, 20, "another failure", 404, 1, TransferEngineKind.Http, null, 1));
        clock.Now = clock.Now.AddDays(1);
        store.AddRunItems("day-two", ["three"], "scheduled");
        using (var log = new RunLogger(f.Layout, "day-two", store, false, clock))
            log.Success(new SuccessRecord("three", "three", "next.mp4", null, 1, 2000, 2000, 1, 1, TransferEngineKind.Http, Path.Combine(f.Layout.Downloads, "next.mp4"), null));
        var rows = Csv.Parse(File.ReadAllText(f.Layout.DailyCsvPath));
        Assert.True(rows[0].SequenceEqual(DailyReport.Columns), "requested daily columns in order");
        Assert.Equal(6, rows.Count, "four outcomes across two runs day one, one day two; no invented SKIP/RUN file rows");
        Assert.Equal(5, rows.Skip(1).Select(row => row[0]).Distinct().Count(), "outcome IDs are unique");
        foreach (var row in rows.Skip(1))
        {
            Assert.True(Regex.IsMatch(row[1], @"^\d{4}-\d{2}-\d{2} \d{2}:\d{2}:\d{2}$"), "human timestamp format");
            Assert.Equal(row[1].StartsWith("2026-10-28", StringComparison.Ordinal) ? "100.00%" : "25.00%", row[10], "ratio includes SUCCESS/FAIL/ERROR across runs on each day");
            foreach (var trace in new[] { row[8], row[9] }.Where(value => value.Length > 0))
            {
                var parts = trace.Split("#L", StringSplitOptions.None);
                var target = Path.GetFullPath(Path.Combine(f.Layout.Reports, Uri.UnescapeDataString(parts[0]).Replace('/', Path.DirectorySeparatorChar)));
                var number = int.Parse(parts[1], Format.Invariant);
                Assert.True(File.ReadAllLines(target)[number - 1].Contains($"<a id=\"L{number}\"></a>", StringComparison.Ordinal), "trace opens the exact Markdown file and physical line");
            }
        }
        Assert.Equal("12.250", rows.Single(row => row[3] == "door.mp4")[7], "known media length");
        var failure = rows.Single(row => row[3] == "bad.mp4");
        Assert.Equal("", failure[6], "unknown size stays empty");
        Assert.Equal("", failure[7], "unknown media length stays empty");
        Assert.True(Path.IsPathFullyQualified(failure[4]), "failed outcome has planned absolute destination");
        Assert.True(failure[8].Length > 0 && failure[9].Length > 0, "failure links both log and error lines");
        var html = File.ReadAllText(f.Layout.DailyHtmlPath);
        Assert.True(html.Contains("day-2026-10-27", StringComparison.Ordinal) && html.Contains("day-2026-10-28", StringComparison.Ordinal), "HTML groups both days");
        Assert.True(html.Contains("href=\"../runs/", StringComparison.Ordinal) && html.Contains("#L", StringComparison.Ordinal), "HTML trace links are clickable local files");
        Assert.True(!html.Contains("<script", StringComparison.Ordinal), "no website or executable report scripts");
        using (var resumed = new RunLogger(f.Layout, "day-one", store, false, clock))
            Assert.Equal(1, resumed.Stats.Succeeded, "resumed run retains file statistics");
        Assert.True(rows.Skip(1).Select(row => row[0]).SequenceEqual(Csv.Parse(File.ReadAllText(f.Layout.DailyCsvPath)).Skip(1).Select(row => row[0])), "stable IDs and no duplicates after resume");
    }
}
