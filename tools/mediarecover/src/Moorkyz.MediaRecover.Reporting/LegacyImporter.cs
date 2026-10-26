using System.Text.Json;
using Moorkyz.Tools.MediaRecover.Core;

namespace Moorkyz.Tools.MediaRecover.Reporting;

public sealed class LegacyImporter(IManifestStore store, IIntegrityService integrity)
{
    public async Task<string> ImportAsync(string root, string receiptDirectory, CancellationToken token)
    {
        root = Path.GetFullPath(root); PathSafety.NoLinks(root);
        PathSafety.NoLinks(receiptDirectory); Directory.CreateDirectory(receiptDirectory);
        var receipt = new List<object>();
        var csv = Path.Combine(root, "S26-Backup-Index.csv");
        var claims = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (File.Exists(csv))
        {
            PathSafety.NoLinks(csv);
            using var reader = File.OpenText(csv); using var rows = Csv.Parse(reader).GetEnumerator();
            if (rows.MoveNext())
            {
                var headers = rows.Current;
                var pathColumn = Array.FindIndex(headers, h => new[] { "FullPath", "Path", "Destination", "TargetPath", "FileName", "Filename" }.Contains(h, StringComparer.OrdinalIgnoreCase));
                var hashColumn = Array.FindIndex(headers, h => new[] { "SHA256", "SHA-256", "Hash" }.Contains(h, StringComparer.OrdinalIgnoreCase));
                while (rows.MoveNext())
                {
                    var row = rows.Current;
                    if (pathColumn < 0 || pathColumn >= row.Length) continue;
                    var path = Path.GetFullPath(Path.IsPathFullyQualified(row[pathColumn]) ? row[pathColumn] : Path.Combine(root, row[pathColumn]));
                    if (!PathSafety.Within(path, root)) { receipt.Add(new { Path = "(outside selected root)", Status = "CSV_PATH_REJECTED" }); continue; }
                    claims[path] = hashColumn >= 0 && hashColumn < row.Length ? row[hashColumn] : "";
                }
            }
        }
        var stack = new Stack<string>(); stack.Push(root);
        while (stack.TryPop(out var directory))
        {
            token.ThrowIfCancellationRequested();
            try
            {
                PathSafety.NoLinks(directory);
                foreach (var path in Directory.EnumerateFileSystemEntries(directory))
                {
                    token.ThrowIfCancellationRequested();
                    var attributes = File.GetAttributes(path);
                    if ((attributes & FileAttributes.ReparsePoint) != 0) { receipt.Add(new { Path = path, Status = "LINK_SKIPPED" }); continue; }
                    if ((attributes & FileAttributes.Directory) != 0)
                    {
                        if (!Path.GetFileName(path).Equals(".mediarecover", StringComparison.OrdinalIgnoreCase)) stack.Push(path);
                        continue;
                    }
                    if (Filter.Media(path, new Settings()) is null && !path.EndsWith(".incomplete", StringComparison.OrdinalIgnoreCase) && !path.EndsWith(".damaged", StringComparison.OrdinalIgnoreCase)) continue;
                    try
                    {
                        var hash = await integrity.HashAsync(path, token);
                        var info = new FileInfo(path);
                        var item = new SourceItem(PathSafety.Key("legacy|" + path.ToUpperInvariant()), new("Legacy", root), path, Path.GetRelativePath(root, path), info.Length, "legacy", [], "Legacy");
                        store.SaveFile(new(item, ItemStatus.LegacyUnverified, path, Hash: hash, Bytes: info.Length,
                            Verification: "Local baseline only; source identity and transfer consistency NOT verified"));
                        receipt.Add(new { Path = path, Status = "LEGACY_UNVERIFIED", SHA256 = hash, ClaimedHash = claims.GetValueOrDefault(path), PriorHashMatches = claims.TryGetValue(path, out var claimed) && string.Equals(claimed, hash, StringComparison.OrdinalIgnoreCase) });
                    }
                    catch (Exception e) when (e is IOException or UnauthorizedAccessException) { receipt.Add(new { Path = path, Status = "IMPORT_READ_FAILED", Error = e.Message }); }
                }
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException) { receipt.Add(new { Path = directory, Status = "IMPORT_PARTIAL", Error = e.Message }); }
        }
        var output = Path.Combine(receiptDirectory, "legacy-import-" + DateTime.UtcNow.ToString("yyyyMMdd-HHmmss") + "-" + Guid.NewGuid().ToString("N")[..8] + ".json");
        await using var stream = new FileStream(output, FileMode.CreateNew, FileAccess.Write, FileShare.Read);
        await JsonSerializer.SerializeAsync(stream, new { Root = root, Imported = DateTimeOffset.Now, SourceFilesModified = false, Records = receipt }, new JsonSerializerOptions { WriteIndented = true }, token);
        return output;
    }
}
