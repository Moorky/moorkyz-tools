using MoorkszVisoryMediaDownloader.Core.Infrastructure;
using MoorkszVisoryMediaDownloader.Core.Reporting;

namespace MoorkszVisoryMediaDownloader.Tests;

internal static partial class Program
{
    private static int ReportBrowserFixture(string folder, bool empty = false)
    {
        if (Directory.Exists(folder) && Directory.EnumerateFileSystemEntries(folder).Any())
            throw new IOException("Report fixtures require an empty disposable folder.");
        var layout = new BackupLayout(Path.Combine(folder, "backup"));
        layout.Ensure();
        if (empty)
        {
            DailyReport.Render(layout);
            return 0;
        }
        Directory.CreateDirectory(layout.ReportFolder("synthetic-browser"));
        var path = layout.CsvReportPath("synthetic-browser");
        using (var csv = new DurableLines(path, csv: true))
        using (var log = new DiagnosticLog(layout.LogPath("synthetic-browser"), layout.FailedReportPath("synthetic-browser"), "synthetic-browser"))
        {
            csv.WriteLine(Csv.Line(RunLogger.ReportColumns.Select(Csv.Text)));
            for (var day = 25; day <= 27; day++)
            for (var index = 0; index < 55; index++)
            {
                var status = index < 40 ? "SUCCESS" : index < 50 ? "FAIL" : "ERROR";
                var values = Enumerable.Repeat("", RunLogger.ReportColumns.Length).ToArray();
                var location = log.Write(status, $"SYNTHETIC fixture {day}-{index}");
                values[0] = "synthetic-browser";
                values[1] = $"{(day == 25 ? "2026-09-25" : $"2026-10-{day}")} 12:00:{index:00}";
                values[3] = $"fixture-{day}-{index}.mp4";
                values[4] = day == 25 && index == 54 ? "Camera, <b>North Gate</b>" : $"Camera {index % 3 + 1}";
                values[5] = status;
                values[6] = index < 40 ? index < 20 ? "1" : "3" : "";
                values[7] = index < 40 ? "1048576" : "";
                values[16] = Path.Combine(layout.Downloads, values[3]);
                values[18] = index == 54 ? "SYNTHETIC <img src=x onerror=alert(1)> error" : "SYNTHETIC fixture";
                values[21] = $"fixture-{day}-{index}";
                values[22] = index < 40 ? "15" : "";
                values[23] = Path.GetRelativePath(layout.Root, layout.LogPath("synthetic-browser"));
                values[24] = location.LogLine.ToString(Format.Invariant);
                values[25] = location.ErrorLine is null ? "" : Path.GetRelativePath(layout.Root, layout.FailedReportPath("synthetic-browser"));
                values[26] = location.ErrorLine?.ToString(Format.Invariant) ?? "";
                csv.WriteLine(Csv.Line(values.Select(Csv.Text)));
            }
        }
        DailyReport.Render(layout);
        Console.WriteLine("SYNTHETIC_REPORT=" + layout.DailyHtmlPath);
        return 0;
    }
}
