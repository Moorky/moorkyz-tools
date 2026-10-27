using System.Net;
using System.Text;
using MoorkszVisoryMediaDownloader.Core.Infrastructure;

namespace MoorkszVisoryMediaDownloader.Core.Reporting;

public static class CsvReport
{
    public static void Render(string csvPath, string markdownPath, string htmlPath, string title, string summary)
    {
        foreach (var path in new[] { csvPath, markdownPath, htmlPath })
            PathSafety.EnsureNoReparsePoints(Path.GetPathRoot(Path.GetFullPath(path))!, path);
        var rows = Csv.Parse(File.ReadAllText(csvPath));
        if (rows.Count == 0) throw new InvalidDataException("A report must contain a CSV header.");
        var md = new StringBuilder("# ").AppendLine(DiagnosticLog.MarkdownText(title)).AppendLine()
            .AppendLine(DiagnosticLog.MarkdownText(summary)).AppendLine()
            .Append("[CSV report](").Append(Path.GetFileName(csvPath)).AppendLine(")").AppendLine();
        var html = new StringBuilder(DocumentHtml.Start(title)).Append("<main id=\"main\"><h1>")
            .Append(WebUtility.HtmlEncode(title)).Append("</h1><p>").Append(WebUtility.HtmlEncode(summary))
            .Append("</p><p><a href=\"").Append(WebUtility.HtmlEncode(Path.GetFileName(csvPath))).Append("\">CSV report</a></p>")
            .Append("<div class=\"table-scroll\" tabindex=\"0\" role=\"region\" aria-label=\"CSV report contents\"><table><caption>CSV report contents</caption><thead>");
        for (var index = 0; index < rows.Count; index++)
        {
            var row = rows[index].Select(Redactor.Clean).ToList();
            if (row.Count != rows[0].Count) throw new InvalidDataException($"Malformed report row {index + 1}.");
            md.Append("| ").AppendJoin(" | ", row.Select(cell => DiagnosticLog.MarkdownText(cell)
                .Replace("|", "&#124;", StringComparison.Ordinal).Replace("\r", "", StringComparison.Ordinal).Replace("\n", "<br>", StringComparison.Ordinal))).AppendLine(" |");
            if (index == 0) md.Append("| ").AppendJoin(" | ", row.Select(_ => "---")).AppendLine(" |");
            html.Append("<tr>");
            foreach (var cell in row)
                html.Append(index == 0 ? "<th scope=\"col\">" : "<td>").Append(WebUtility.HtmlEncode(cell)).Append(index == 0 ? "</th>" : "</td>");
            html.Append("</tr>");
            if (index == 0) html.Append("</thead><tbody>");
        }
        html.Append("</tbody></table></div></main>").Append(DocumentHtml.End);
        WriteAtomic(markdownPath, md.ToString());
        WriteAtomic(htmlPath, html.ToString());
    }

    internal static void WriteAtomic(string path, string content)
    {
        var temp = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            using (var file = new FileStream(temp, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            {
                var bytes = Encoding.UTF8.GetBytes(content);
                file.Write(bytes);
                file.Flush(flushToDisk: true);
            }
            File.Move(temp, path, overwrite: true);
        }
        finally
        {
            if (File.Exists(temp)) File.Delete(temp);
        }
    }
}
