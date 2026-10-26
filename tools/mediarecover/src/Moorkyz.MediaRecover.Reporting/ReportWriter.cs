using System.Globalization;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Moorkyz.Tools.MediaRecover.Core;

namespace Moorkyz.Tools.MediaRecover.Reporting;

public sealed class ReportWriter : IReportWriter
{
    public async Task WriteAsync(RunRecord run, IManifestStore store, CancellationToken token)
    {
        PathSafety.NoLinks(run.Reports); Directory.CreateDirectory(run.Reports);
        var counts = new Dictionary<string, long>();
        long copiedBytes = 0, unknown = 0, conflicts = 0, repaired = 0, attempts = 0;
        double seconds = 0;
        using var log = Create(run, "log", "txt");
        using var csv = Create(run, "log_report", "csv");
        using var failed = Create(run, "failed_report", "csv");
        using var manifest = Create(run, "manifest", "csv");
        var headers = new[] { "timestamp", "run_id", "status", "provider", "device_or_root", "source_id", "source_relative_path", "media_type", "filename", "raw_dates_and_provenance_json", "unknown_date_included", "date_conflict", "expected_size", "actual_bytes", "destination", "sha256", "seconds", "bytes_per_second", "attempts", "engine", "error_code", "error_message", "recommended_action", "verification_level", "quarantine" };
        csv.WriteLine(Csv.Row(headers)); manifest.WriteLine(Csv.Row(headers));
        failed.WriteLine(Csv.Row(["timestamp", "run_id", "filename", "source", "source_path", "size", "dates", "error_code", "message", "attempts", "destination", "staging", "next_action", "url"]));
        foreach (var record in store.Inventory(run.Id))
        {
            token.ThrowIfCancellationRequested();
            var status = record.Status.ToString();
            counts[status] = counts.GetValueOrDefault(status) + 1;
            if (record.Item.UnknownDate && record.Status != ItemStatus.Excluded) unknown++;
            if (record.Item.DateConflict) conflicts++;
            if (record.Repaired) repaired++;
            attempts += Math.Max(0, record.Attempts - 1);
            if (record.Status == ItemStatus.Verified) { copiedBytes += record.Bytes; seconds += record.Seconds; }
            var row = new[] {
                (record.VerifiedAt ?? run.Finished ?? run.Started).ToString("O"), run.Id, status, record.Item.Source.Kind, record.Item.Source.Id,
                record.Item.ObjectId, record.Item.RelativePath, record.Item.MediaType, Path.GetFileName(record.Item.RelativePath),
                JsonSerializer.Serialize(record.Item.Dates), record.Item.UnknownDate.ToString(), record.Item.DateConflict.ToString(),
                record.Item.Size?.ToString(CultureInfo.InvariantCulture) ?? "", record.Bytes.ToString(CultureInfo.InvariantCulture),
                record.Target, record.Hash, record.Seconds.ToString("F3", CultureInfo.InvariantCulture),
                (record.Seconds > 0 ? record.Bytes / record.Seconds : 0).ToString("F2", CultureInfo.InvariantCulture),
                record.Attempts.ToString(CultureInfo.InvariantCulture), "Stream/SHA-256", record.ErrorCode, record.Error, record.Remedy, record.Verification, record.Quarantine };
            csv.WriteLine(Csv.Row(row));
            if (record.Status is ItemStatus.Verified or ItemStatus.AlreadyVerified) manifest.WriteLine(Csv.Row(row));
            log.WriteLine($"[{row[0]}] [{status}] {record.Item.RelativePath} | {record.Bytes:N0} bytes | {record.Seconds:F2}s | {record.Target} | {record.ErrorCode}: {record.Error} | {record.Remedy}");
            if (record.Status is ItemStatus.Failed or ItemStatus.Interrupted)
                failed.WriteLine(Csv.Row([row[0], run.Id, Path.GetFileName(record.Item.RelativePath), record.Item.Source.Id, record.Item.RelativePath, row[12], row[9], record.ErrorCode, record.Error, row[18], record.Target, record.Staging, record.Remedy, ""]));
        }
        long issueCount = 0;
        foreach (var issue in store.Issues(run.Id))
        {
            issueCount++;
            log.WriteLine($"[{run.Started:O}] [WARN] {issue.Code} | {issue.Path} | {issue.Message} | {issue.Remedy}");
            failed.WriteLine(Csv.Row([run.Started.ToString("O"), run.Id, Path.GetFileName(issue.Path), "", issue.Path, "", "", issue.Code, issue.Message, "0", "", "", issue.Remedy, ""]));
        }
        var copies = counts.GetValueOrDefault(nameof(ItemStatus.Verified));
        var summary = new
        {
            run.Id,
            run.Started,
            run.Finished,
            run.Status,
            run.Partial,
            run.Discovered,
            run.Examined,
            run.Video,
            run.Audio,
            run.Inaccessible,
            ReconciledInventoryCount = counts.Values.Sum(),
            Counts = counts,
            UnknownDateIncluded = unknown,
            DateConflicts = conflicts,
            Eligible = counts.Where(c => c.Key != nameof(ItemStatus.Excluded)).Sum(c => c.Value),
            ExcludedByDate = store.Inventory(run.Id).LongCount(r => r.ErrorCode == "EXCLUDED_BY_DATE"),
            ExcludedByType = store.Inventory(run.Id).LongCount(r => r.ErrorCode == "EXCLUDED_BY_TYPE"),
            ExistingVerified = counts.GetValueOrDefault(nameof(ItemStatus.AlreadyVerified)),
            Failures = counts.GetValueOrDefault(nameof(ItemStatus.Failed)),
            Pending = store.Inventory(run.Id).LongCount(r => r.Status is ItemStatus.Eligible or ItemStatus.Interrupted or ItemStatus.Transferring or ItemStatus.Verifying or ItemStatus.Promoting),
            Processed = store.Inventory(run.Id).LongCount(r => r.Status is ItemStatus.Verified or ItemStatus.AlreadyVerified or ItemStatus.Failed),
            Repaired = repaired,
            NewCopies = copies,
            CopiedBytes = copiedBytes,
            Retries = attempts,
            InventoryIssues = issueCount,
            AverageFileSeconds = copies > 0 ? seconds / copies : 0,
            AverageFileBytes = copies > 0 ? copiedBytes / copies : 0,
            AverageBytesPerSecond = seconds > 0 ? copiedBytes / seconds : 0,
            ElapsedSeconds = ((run.Finished ?? DateTimeOffset.Now) - run.Started).TotalSeconds,
            SavedTo = Path.GetFullPath(run.Settings.Destination),
            Reports = run.Reports,
            run.Archive,
            Assurance = "Hashes are not media decode tests or proof that source recordings are undamaged.",
            CsvSafety = "Formula-leading cells prefixed with an apostrophe. Raw provenance in ledger JSON."
        };
        var jsonPath = Path.Combine(run.Reports, $"summary_{run.Id}.json"); PathSafety.NoLinks(jsonPath);
        await File.WriteAllTextAsync(jsonPath, JsonSerializer.Serialize(summary, new JsonSerializerOptions { WriteIndented = true }), token);
        log.WriteLine($"Saved to: {run.Settings.Destination}\nReports: {run.Reports}\nState: {run.Status}");
    }
    private static StreamWriter Create(RunRecord run, string prefix, string extension)
    {
        var path = Path.Combine(run.Reports, $"{prefix}_{run.Id}.{extension}"); PathSafety.NoLinks(path);
        return new StreamWriter(new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.Read), new UTF8Encoding(true));
    }
    public async Task<string> ArchiveAsync(RunRecord run, IManifestStore store, CancellationToken token)
    {
        if (!run.Status.StartsWith("Completed", StringComparison.Ordinal)) throw new InvalidOperationException("Finish recovery before archiving.");
        var root = Path.Combine(run.Settings.Destination, ".mediarecover", "archives"); PathSafety.NoLinks(root); Directory.CreateDirectory(root);
        var final = PathSafety.Unused(Path.Combine(root, $"mediarecover_{run.Id}.zip"));
        var part = final + "." + Guid.NewGuid().ToString("N") + ".part";
        var expected = new Dictionary<string, string>(StringComparer.Ordinal);
        await using (var file = new FileStream(part, FileMode.CreateNew, FileAccess.ReadWrite, FileShare.None))
        {
            using (var zip = new ZipArchive(file, ZipArchiveMode.Create, true))
            {
                foreach (var record in store.Inventory(run.Id).Where(f => f.Status == ItemStatus.Verified))
                    await Add(record.Target, $"media/{record.Item.Key}/{Path.GetFileName(record.Target)}", record.Hash);
                foreach (var report in Directory.EnumerateFiles(run.Reports))
                    await Add(report, "reports/" + Path.GetFileName(report), null);
                async Task Add(string path, string name, string? savedHash)
                {
                    token.ThrowIfCancellationRequested(); PathSafety.NoLinks(path);
                    await using var source = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
                    var hash = Convert.ToHexString(await SHA256.HashDataAsync(source, token)).ToLowerInvariant();
                    if (savedHash is not null && hash != savedHash) throw new InvalidDataException("A saved file changed before archiving: " + path);
                    expected.Add(name, hash); source.Position = 0;
                    var entry = zip.CreateEntry(name, CompressionLevel.NoCompression);
                    await using var output = entry.Open(); await source.CopyToAsync(output, token);
                }
            }
            await file.FlushAsync(token); file.Flush(true);
        }
        using (var file = File.OpenRead(part))
        using (var zip = new ZipArchive(file, ZipArchiveMode.Read))
        {
            if (zip.Entries.Count != expected.Count) throw new InvalidDataException("ZIP entry count mismatch; incomplete archive retained.");
            foreach (var entry in zip.Entries)
            {
                token.ThrowIfCancellationRequested();
                await using var stream = entry.Open();
                var hash = Convert.ToHexString(await SHA256.HashDataAsync(stream, token)).ToLowerInvariant();
                if (!expected.TryGetValue(entry.FullName, out var wanted) || hash != wanted) throw new InvalidDataException("ZIP hash verification failed; incomplete archive retained.");
            }
        }
        PathSafety.NoLinks(final); File.Move(part, final, false);
        return final;
    }
}
public static class Csv
{
    public static string Row(IEnumerable<string> values) => string.Join(",", values.Select(value =>
    {
        if (value.Length > 0 && "=+-@\t\r".Contains(value[0], StringComparison.Ordinal)) value = "'" + value;
        return "\"" + value.Replace("\"", "\"\"") + "\"";
    }));
    public static IEnumerable<string[]> Parse(TextReader reader)
    {
        var cells = new List<string>(); var cell = new StringBuilder(); bool quoted = false;
        while (reader.Read() is var n && n >= 0)
        {
            var c = (char)n;
            if (c == '"')
            {
                if (quoted && reader.Peek() == '"') { reader.Read(); cell.Append('"'); }
                else quoted = !quoted;
            }
            else if (c == ',' && !quoted) { cells.Add(cell.ToString()); cell.Clear(); }
            else if ((c == '\r' || c == '\n') && !quoted)
            {
                if (c == '\r' && reader.Peek() == '\n') reader.Read();
                cells.Add(cell.ToString()); cell.Clear(); yield return cells.ToArray(); cells.Clear();
            }
            else cell.Append(c);
        }
        if (quoted) throw new InvalidDataException("Unclosed quote in legacy CSV.");
        if (cell.Length > 0 || cells.Count > 0) { cells.Add(cell.ToString()); yield return cells.ToArray(); }
    }
}
