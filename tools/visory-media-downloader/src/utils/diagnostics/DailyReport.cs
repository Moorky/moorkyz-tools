using System.Globalization;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using MoorkszVisoryMediaDownloader.Core.Infrastructure;

namespace MoorkszVisoryMediaDownloader.Core.Reporting;

public static class DailyReport
{
    public static readonly string[] Columns =
    [
        "ID", "Timestamp", "Status", "Filename", "Full Path", "Time Spent (s)",
        "File Size (bytes)", "Length (s)", "Log", "Error", "Success Ratio", "Message",
    ];
    private static readonly object Gate = new();
    private sealed record Result(string[] Cells, double? Seconds, long? Bytes);

    public static void Render(BackupLayout layout)
    {
        lock (Gate)
        {
            foreach (var path in new[] { layout.Runs, layout.DailyCsvPath, layout.DailyHtmlPath, layout.DailyMarkdownPath })
                PathSafety.EnsureNoReparsePoints(Path.GetPathRoot(path)!, path);
            Directory.CreateDirectory(layout.Reports);
            var results = ReadResults(layout).OrderByDescending(row => row.Cells[1], StringComparer.Ordinal).ToList();
            var csv = new StringBuilder(Csv.Line(Columns.Select(Csv.Text))).Append("\r\n");
            var md = new StringBuilder("# Visory daily report\n\n")
                .AppendLine("Recorded local days. Success ratio = SUCCESS / (SUCCESS + FAIL + ERROR). SKIP and run summaries are excluded.")
                .AppendLine("Length is media duration; unavailable values stay blank. Log links name the exact Markdown file and physical line.")
                .AppendLine("\n[CSV report](report.csv)\n");
            var html = new StringBuilder(DocumentHtml.Start("Visory daily report"))
                .Append("<main id=\"main\"><header><p class=\"note\">LOCAL RUN HISTORY</p><h1>Visory daily report</h1>")
                .Append("<p>SUCCESS, FAIL and ERROR outcomes, grouped by the local day recorded in each run.</p>")
                .Append("<nav aria-label=\"Report formats\"><a href=\"report.csv\">CSV</a><a href=\"report.md\">Markdown</a></nav></header>")
                .Append("<p class=\"note\">Snapshot generated ").Append(WebUtility.HtmlEncode(DateTimeOffset.Now.ToString("O", Format.Invariant)))
                .Append(". Success ratio = SUCCESS / (SUCCESS + FAIL + ERROR). Skipped files and run summaries are excluded. ")
                .Append("Length is media duration, not processing time. Blank values are unknown. ")
                .Append("Log links include the file and physical line; Markdown viewers support the line anchors, while plain-text browsers may not scroll to them.</p>");
            if (results.Count == 0) html.Append("<p>No SUCCESS, FAIL or ERROR outcomes have been recorded yet.</p>");
            foreach (var day in results.GroupBy(row => row.Cells[1][..10]))
            {
                var rows = day.ToList();
                var successes = rows.Count(row => row.Cells[2] == "SUCCESS");
                var failures = rows.Count(row => row.Cells[2] == "FAIL");
                var errors = rows.Count(row => row.Cells[2] == "ERROR");
                var ratio = (100.0 * successes / rows.Count).ToString("0.00", Format.Invariant) + "%";
                var stats = $"{rows.Count} outcomes | SUCCESS: {successes} | FAIL: {failures} | ERROR: {errors} | Success ratio: {ratio} | Known time: {Csv.Number(rows.Sum(row => row.Seconds ?? 0), "0.000")}s | Successful bytes: {rows.Where(row => row.Cells[2] == "SUCCESS").Sum(row => row.Bytes ?? 0)}";
                md.Append("## ").AppendLine(day.Key).AppendLine().AppendLine(stats).AppendLine();
                md.Append("| ").AppendJoin(" | ", Columns).AppendLine(" |");
                md.Append("| ").AppendJoin(" | ", Columns.Select(_ => "---")).AppendLine(" |");
                html.Append("<section aria-labelledby=\"day-").Append(day.Key).Append("\"><h2 id=\"day-").Append(day.Key).Append("\">")
                    .Append(day.Key).Append("</h2><p>").Append(WebUtility.HtmlEncode(stats)).Append("</p>")
                    .Append("<div class=\"table-scroll\" tabindex=\"0\" role=\"region\" aria-label=\"Results for ").Append(day.Key)
                    .Append("\"><table><caption>Outcomes for ").Append(day.Key).Append("</caption><thead><tr>");
                foreach (var column in Columns) html.Append("<th scope=\"col\">").Append(WebUtility.HtmlEncode(column)).Append("</th>");
                html.Append("</tr></thead><tbody>");
                foreach (var result in rows)
                {
                    var cells = result.Cells;
                    cells[10] = ratio;
                    csv.Append(Csv.Line(cells.Select(Csv.Text))).Append("\r\n");
                    html.Append("<tr id=\"result-").Append(WebUtility.HtmlEncode(cells[0])).Append("\">");
                    var mdCells = new List<string>();
                    for (var index = 0; index < cells.Length; index++)
                    {
                        var cell = cells[index];
                        var encoded = WebUtility.HtmlEncode(cell);
                        var markdown = DiagnosticLog.MarkdownText(cell).Replace("|", "&#124;", StringComparison.Ordinal)
                            .Replace("\r", "", StringComparison.Ordinal).Replace("\n", "<br>", StringComparison.Ordinal);
                        if (index is 8 or 9 && cell.Length > 0)
                        {
                            var hash = cell.LastIndexOf("#L", StringComparison.Ordinal);
                            var label = Path.GetFileName(cell[..hash].Replace('/', Path.DirectorySeparatorChar)) + " : line " + cell[(hash + 2)..];
                            encoded = $"<a href=\"{WebUtility.HtmlEncode(cell)}\">{WebUtility.HtmlEncode(label)}</a>";
                            markdown = $"[{DiagnosticLog.MarkdownText(label)}]({cell})";
                        }
                        html.Append(index == 4 ? "<td class=\"path\">" : index == 11 ? "<td class=\"message\">" : "<td>").Append(encoded).Append("</td>");
                        mdCells.Add(markdown);
                    }
                    md.Append("| ").AppendJoin(" | ", mdCells).AppendLine(" |");
                    html.Append("</tr>");
                }
                html.Append("</tbody></table></div></section>");
                md.AppendLine();
            }
            html.Append("</main><footer>Local files only. No account data is uploaded and no network request is made by this report.</footer>").Append(DocumentHtml.End);
            CsvReport.WriteAtomic(layout.DailyCsvPath, csv.ToString());
            CsvReport.WriteAtomic(layout.DailyMarkdownPath, md.ToString());
            CsvReport.WriteAtomic(layout.DailyHtmlPath, html.ToString());
        }
    }

    private static IEnumerable<Result> ReadResults(BackupLayout layout)
    {
        var ids = new HashSet<string>(StringComparer.Ordinal);
        foreach (var folder in Directory.EnumerateDirectories(layout.Runs).Order(StringComparer.Ordinal))
        {
            PathSafety.EnsureNoReparsePoints(layout.Runs, folder);
            var run = Path.GetFileName(folder);
            var file = layout.CsvReportPath(run);
            if (!File.Exists(file)) file = layout.LegacyCsvReportPath(run);
            if (!File.Exists(file)) continue;
            PathSafety.EnsureNoReparsePoints(layout.Root, file);
            var rows = Csv.Parse(File.ReadAllText(file));
            if (rows.Count == 0) throw new InvalidDataException("A run report has no CSV header: " + file);
            var header = rows[0];
            if (new[] { "timestamp", "status", "filename", "local_path", "message", "elapsed_seconds", "filesize_bytes" }.Any(name => !header.Contains(name)))
                throw new InvalidDataException("A run report is missing required columns: " + file);
            for (var index = 1; index < rows.Count; index++)
            {
                var row = rows[index];
                if (row.Count != header.Count) throw new InvalidDataException("A run report contains an incomplete row: " + file);
                string Value(string name) => header.IndexOf(name) is var col && col >= 0 ? Redactor.Clean(row[col]) : "";
                var status = Value("status");
                if (status is "RUN" or "SKIP") continue;
                if (status is not ("SUCCESS" or "FAIL" or "ERROR")) throw new InvalidDataException("Unsupported report outcome: " + status);
                var stamp = Value("timestamp");
                if (!DateTime.TryParseExact(stamp, "yyyy-MM-dd HH:mm:ss", Format.Invariant, DateTimeStyles.None, out var local))
                {
                    if (!DateTimeOffset.TryParse(stamp, Format.Invariant, DateTimeStyles.None, out var offset))
                        throw new InvalidDataException("A report timestamp is invalid: " + file);
                    local = offset.DateTime;
                }
                var id = Value("id");
                if (id.Length == 0) id = "legacy-" + Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(run + ":" + index)))[..24].ToLowerInvariant();
                if (!ids.Add(id)) throw new InvalidDataException("Duplicate report outcome ID: " + id);
                var seconds = Number(Value("elapsed_seconds"));
                var length = Number(Value("length_seconds"));
                var size = Value("filesize_bytes");
                long? bytes = size.Length == 0 ? null : long.TryParse(size, NumberStyles.None, Format.Invariant, out var parsed) ? parsed
                    : throw new InvalidDataException("A report size is invalid: " + file);
                yield return new Result([
                    id, local.ToString("yyyy-MM-dd HH:mm:ss", Format.Invariant), status, Value("filename"), Value("local_path"),
                    Csv.Number(seconds, "0.000"), Csv.Number(bytes), Csv.Number(length, "0.000"),
                    TraceLink(layout, Value("log_file"), Value("log_line")), TraceLink(layout, Value("error_file"), Value("error_line")), "", Value("message"),
                ], seconds, bytes);
            }
        }
    }

    private static double? Number(string value) => value.Length == 0 ? null
        : double.TryParse(value, NumberStyles.Float, Format.Invariant, out var number) && double.IsFinite(number) && number >= 0 ? number
        : throw new InvalidDataException("A report duration is invalid.");

    private static string TraceLink(BackupLayout layout, string relative, string line)
    {
        if (relative.Length == 0 && line.Length == 0) return "";
        if (!int.TryParse(line, NumberStyles.None, Format.Invariant, out var number) || number < 1)
            throw new InvalidDataException("A report log line is invalid.");
        var target = PathSafety.Combine(layout.Root, relative);
        PathSafety.EnsureNoReparsePoints(layout.Root, target);
        if (!target.StartsWith(layout.Runs + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase) || Path.GetExtension(target) != ".md" || !File.Exists(target))
            throw new InvalidDataException("A report log reference is missing or unsafe.");
        return string.Join('/', Path.GetRelativePath(layout.Reports, target).Split(Path.DirectorySeparatorChar).Select(part => part == ".." ? part : Uri.EscapeDataString(part))) + "#L" + number;
    }
}
