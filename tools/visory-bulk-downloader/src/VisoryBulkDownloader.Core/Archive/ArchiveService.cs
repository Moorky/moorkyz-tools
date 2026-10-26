using System.Buffers;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using VisoryBulkDownloader.Core.Infrastructure;
using VisoryBulkDownloader.Core.Storage;

namespace VisoryBulkDownloader.Core.Archive;

public sealed record ArchiveEntrySource(string EntryName, long? Length, string? Sha256, Func<Stream> Open, bool StoreOnly, DateTimeOffset? Timestamp = null);

public sealed record ArchiveProgress(string Phase, int Files, int TotalFiles, long Bytes, long TotalBytes);

public sealed record ArchiveResult(string? Path, int Entries, long Bytes, string Fingerprint, bool Reused, bool Incomplete, string Status, string Message);

public sealed record ArchiveEntryInfo(string Name, long Length, long CompressedLength, DateTimeOffset LastWrite);

public sealed record ArchiveVerification(bool Ok, int EntriesChecked, IReadOnlyList<string> Problems);

public sealed class ArchiveService(BackupLayout layout, StateStore store, IDiskSpaceProbe? disk = null)
{
    private const int BufferSize = 1024 * 1024;
    private readonly IDiskSpaceProbe disk = disk ?? new DriveDiskSpaceProbe();

    public static string Fingerprint(ArchiveScope scope, IEnumerable<RecordingEntry> recordings)
    {
        var text = new StringBuilder(scope.ToString()).Append('\n');
        foreach (var r in recordings.OrderBy(r => r.Key, StringComparer.Ordinal))
            text.Append(r.Key).Append('|').Append(r.Destination).Append('|').Append(r.Sha256).Append('|').Append(r.ActualSize).Append('\n');
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(text.ToString()))).ToLowerInvariant();
    }

    public static string EntryNameFor(RecordingEntry entry) => "videos/" + entry.Destination!.Replace('\\', '/');

    /// <summary>Creates one ZIP with all verified recordings in scope, a manifest and run reports.</summary>
    public async Task<ArchiveResult> CreateAsync(string runId, ArchiveScope scope, IReadOnlyList<RecordingEntry> scopeRecordings, IReadOnlyList<string> reportFiles,
        bool inventoryIncomplete, IProgress<ArchiveProgress>? progress, CancellationToken cancellationToken)
    {
        var verified = new List<RecordingEntry>();
        var excluded = new List<(RecordingEntry Entry, string Reason)>();
        foreach (var r in scopeRecordings)
        {
            if (r.Status != RecordingStatus.Verified || r.Destination is null || r.Sha256 is null || r.ActualSize is null)
            {
                excluded.Add((r, r.Status == RecordingStatus.Failed ? "download failed: " + (r.LastError ?? "unknown error") : $"not verified ({r.Status})"));
                continue;
            }
            var path = layout.DestinationPath(r.Destination);
            if (!File.Exists(path)) { excluded.Add((r, "file missing on disk")); continue; }
            if (new FileInfo(path).Length != r.ActualSize) { excluded.Add((r, "size differs from verified size")); continue; }
            verified.Add(r);
        }
        var incomplete = inventoryIncomplete || excluded.Count > 0;
        var fingerprint = Fingerprint(scope, verified) + (incomplete ? "-incomplete" : "");
        var previous = store.Archives().FirstOrDefault(a => a.Fingerprint == fingerprint && a.Scope == scope && a.Status == "verified" && File.Exists(a.Path));
        if (previous is not null)
        {
            return new ArchiveResult(previous.Path, previous.Entries, previous.Bytes, fingerprint, true, previous.Incomplete, "reused",
                $"Recordings unchanged since {Path.GetFileName(previous.Path)}; the existing verified archive was reused.");
        }
        if (verified.Count == 0)
            return new ArchiveResult(null, 0, 0, fingerprint, false, incomplete, "skipped", "No verified recordings are available for an archive.");

        var entries = new List<ArchiveEntrySource>();
        foreach (var r in verified)
        {
            var path = layout.DestinationPath(r.Destination!);
            entries.Add(new ArchiveEntrySource(EntryNameFor(r), r.ActualSize, r.Sha256, () => new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 1, FileOptions.SequentialScan), true, r.RecordedAt));
        }
        var manifest = BuildManifest(verified, excluded);
        entries.Add(new ArchiveEntrySource("manifest/recordings_manifest.csv", manifest.Length, null, () => new MemoryStream(manifest, writable: false), false));
        foreach (var report in reportFiles.Where(File.Exists))
        {
            var bytes = await ReadSharedAsync(report, cancellationToken).ConfigureAwait(false);
            entries.Add(new ArchiveEntrySource("reports/" + Path.GetFileName(report), bytes.Length, null, () => new MemoryStream(bytes, writable: false), false));
        }

        var totalBytes = entries.Sum(e => e.Length ?? 0);
        Directory.CreateDirectory(layout.Archives);
        var available = disk.AvailableBytes(layout.Archives);
        var required = totalBytes + totalBytes / 100 + 64L * 1024 * 1024;
        if (available < required)
            return new ArchiveResult(null, 0, 0, fingerprint, false, incomplete, "failed", $"Insufficient space for the archive: {Format.Size(available)} free, about {Format.Size(required)} needed.");

        var name = $"Visory_{PathSafety.SanitizeSegment(runId)}{(incomplete ? "_INCOMPLETE" : "")}";
        var temp = Path.Combine(layout.Archives, name + "." + Guid.NewGuid().ToString("N")[..8] + ".zip.partial");
        try
        {
            var mismatches = await WriteAsync(temp, entries, progress, cancellationToken).ConfigureAwait(false);
            if (mismatches.Count > 0)
            {
                foreach (var entryName in mismatches)
                {
                    var corrupt = verified.First(v => EntryNameFor(v) == entryName);
                    corrupt.Status = RecordingStatus.Corrupt;
                    corrupt.LastError = "Content changed after verification (detected while archiving)";
                    store.Update(corrupt);
                }
                File.Delete(temp);
                return new ArchiveResult(null, 0, 0, fingerprint, false, true, "failed", $"{mismatches.Count} recording(s) changed after verification and were marked corrupt; run the backup again to re-download them.");
            }
            var expected = entries.Where(e => e.Sha256 is not null).ToDictionary(e => e.EntryName, e => (e.Length ?? 0, e.Sha256));
            var verification = await VerifyAsync(temp, expected, entries.Count, progress, cancellationToken).ConfigureAwait(false);
            if (!verification.Ok)
            {
                File.Delete(temp);
                return new ArchiveResult(null, 0, 0, fingerprint, false, incomplete, "failed", "Archive verification failed: " + string.Join("; ", verification.Problems.Take(5)));
            }
            var final = UniquePath(Path.Combine(layout.Archives, name + ".zip"));
            File.Move(temp, final, overwrite: false);
            var size = new FileInfo(final).Length;
            var message = incomplete
                ? $"Archive marked INCOMPLETE: {excluded.Count} recording(s) in scope are not included (listed in the manifest){(inventoryIncomplete ? "; the cloud inventory was not confirmed complete" : "")}."
                : "Archive verified.";
            store.AddArchive(new StateStore.ArchiveRecord(0, runId, final, scope, DateTimeOffset.UtcNow, fingerprint, entries.Count, size, "verified", DateTimeOffset.UtcNow, incomplete, message));
            return new ArchiveResult(final, entries.Count, size, fingerprint, false, incomplete, "verified", message);
        }
        catch
        {
            try { if (File.Exists(temp)) File.Delete(temp); } catch (IOException) { }
            throw;
        }
    }

    /// <summary>Streams entries into a ZIP (ZIP64 when required); returns entries whose bytes no longer match the recorded SHA-256.</summary>
    public static async Task<List<string>> WriteAsync(string path, IReadOnlyList<ArchiveEntrySource> entries, IProgress<ArchiveProgress>? progress, CancellationToken cancellationToken)
    {
        var mismatches = new List<string>();
        var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var totalBytes = entries.Sum(e => e.Length ?? 0);
        long done = 0;
        var buffer = ArrayPool<byte>.Shared.Rent(BufferSize);
        try
        {
            await using var file = new FileStream(path, FileMode.CreateNew, FileAccess.ReadWrite, FileShare.None, BufferSize, FileOptions.Asynchronous);
            using (var zip = new ZipArchive(file, ZipArchiveMode.Create, leaveOpen: true, Encoding.UTF8))
            {
                for (var i = 0; i < entries.Count; i++)
                {
                    var source = entries[i];
                    if (!names.Add(source.EntryName)) throw new InvalidOperationException($"Duplicate archive entry: {source.EntryName}");
                    var entry = zip.CreateEntry(source.EntryName, source.StoreOnly ? CompressionLevel.NoCompression : CompressionLevel.Optimal);
                    if (source.Timestamp is { } stamp && stamp.Year >= 1980) entry.LastWriteTime = stamp.ToLocalTime();
                    using var sha = source.Sha256 is null ? null : IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
                    await using (var input = source.Open())
                    await using (var output = entry.Open())
                    {
                        int read;
                        while ((read = await input.ReadAsync(buffer.AsMemory(0, BufferSize), cancellationToken).ConfigureAwait(false)) > 0)
                        {
                            await output.WriteAsync(buffer.AsMemory(0, read), cancellationToken).ConfigureAwait(false);
                            sha?.AppendData(buffer, 0, read);
                            done += read;
                            progress?.Report(new ArchiveProgress("Writing", i, entries.Count, done, totalBytes));
                        }
                    }
                    if (sha is not null && !string.Equals(Convert.ToHexString(sha.GetHashAndReset()), source.Sha256, StringComparison.OrdinalIgnoreCase))
                        mismatches.Add(source.EntryName);
                    progress?.Report(new ArchiveProgress("Writing", i + 1, entries.Count, done, totalBytes));
                }
            }
            await file.FlushAsync(cancellationToken).ConfigureAwait(false);
            file.Flush(flushToDisk: true);
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer);
        }
        return mismatches;
    }

    /// <summary>Re-opens the archive, checks entries, sizes and SHA-256 of recordings, and reads every other entry.</summary>
    public static async Task<ArchiveVerification> VerifyAsync(string path, IReadOnlyDictionary<string, (long Length, string? Sha256)> expected, int? expectedEntryCount,
        IProgress<ArchiveProgress>? progress, CancellationToken cancellationToken)
    {
        var problems = new List<string>();
        var checkedCount = 0;
        var buffer = ArrayPool<byte>.Shared.Rent(BufferSize);
        try
        {
            using var zip = ZipFile.OpenRead(path);
            var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var entry in zip.Entries)
                if (!names.Add(entry.FullName)) problems.Add($"Duplicate entry {entry.FullName}");
            if (expectedEntryCount is { } count && zip.Entries.Count != count) problems.Add($"Expected {count} entries, found {zip.Entries.Count}");
            foreach (var name in expected.Keys.Where(n => !names.Contains(n))) problems.Add($"Missing entry {name}");
            var total = zip.Entries.Sum(e => e.Length);
            long done = 0;
            foreach (var entry in zip.Entries)
            {
                cancellationToken.ThrowIfCancellationRequested();
                using var sha = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
                long length = 0;
                try
                {
                    await using var stream = entry.Open();
                    int read;
                    while ((read = await stream.ReadAsync(buffer.AsMemory(0, BufferSize), cancellationToken).ConfigureAwait(false)) > 0)
                    {
                        sha.AppendData(buffer, 0, read);
                        length += read;
                        done += read;
                        progress?.Report(new ArchiveProgress("Verifying", checkedCount, zip.Entries.Count, done, total));
                    }
                }
                catch (InvalidDataException e)
                {
                    problems.Add($"{entry.FullName}: {e.Message}");
                    continue;
                }
                checkedCount++;
                if (length != entry.Length) problems.Add($"{entry.FullName}: read {length} bytes, header says {entry.Length}");
                if (expected.TryGetValue(entry.FullName, out var want))
                {
                    if (length != want.Length) problems.Add($"{entry.FullName}: size {length}, expected {want.Length}");
                    if (want.Sha256 is not null && !string.Equals(Convert.ToHexString(sha.GetHashAndReset()), want.Sha256, StringComparison.OrdinalIgnoreCase))
                        problems.Add($"{entry.FullName}: SHA-256 mismatch");
                }
            }
        }
        catch (InvalidDataException e)
        {
            problems.Add("Archive structure is invalid: " + e.Message);
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer);
        }
        return new ArchiveVerification(problems.Count == 0, checkedCount, problems);
    }

    public async Task<ArchiveVerification> VerifyRecordedAsync(StateStore.ArchiveRecord archive, IProgress<ArchiveProgress>? progress, CancellationToken cancellationToken)
    {
        if (!File.Exists(archive.Path))
        {
            store.UpdateArchiveVerification(archive.Id, "missing", null, "Archive file not found");
            return new ArchiveVerification(false, 0, ["Archive file not found"]);
        }
        var byEntry = store.All().Where(r => r.Destination is not null && r.Sha256 is not null).ToDictionary(EntryNameFor, r => (r.ActualSize ?? 0, (string?)r.Sha256));
        var names = List(archive.Path).Select(e => e.Name).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var expected = byEntry.Where(kv => names.Contains(kv.Key)).ToDictionary(kv => kv.Key, kv => kv.Value);
        var result = await VerifyAsync(archive.Path, expected, archive.Entries, progress, cancellationToken).ConfigureAwait(false);
        store.UpdateArchiveVerification(archive.Id, result.Ok ? "verified" : "corrupt", result.Ok ? DateTimeOffset.UtcNow : null,
            result.Ok ? archive.Message : string.Join("; ", result.Problems.Take(5)));
        return result;
    }

    public static List<ArchiveEntryInfo> List(string path)
    {
        using var zip = ZipFile.OpenRead(path);
        return zip.Entries.Select(e => new ArchiveEntryInfo(e.FullName, e.Length, e.CompressedLength, e.LastWriteTime)).ToList();
    }

    private static byte[] BuildManifest(IReadOnlyList<RecordingEntry> included, IReadOnlyList<(RecordingEntry Entry, string Reason)> excluded)
    {
        var text = new StringBuilder();
        text.Append(Csv.Line(["recording_id", "camera", "recorded_at", "recorded_at_source", "filename", "archive_entry", "size_bytes", "sha256", "included", "reason"])).Append("\r\n");
        foreach (var r in included)
            text.Append(Csv.Line([Csv.Text(r.RecordingId), Csv.Text(r.Camera), Csv.Text(r.RecordedAt?.ToString("O")), Csv.Text(r.RecordedAtSource), Csv.Text(Path.GetFileName(r.Destination)),
                Csv.Text(EntryNameFor(r)), Csv.Number(r.ActualSize), Csv.Text(r.Sha256), "true", ""])).Append("\r\n");
        foreach (var (r, reason) in excluded)
            text.Append(Csv.Line([Csv.Text(r.RecordingId), Csv.Text(r.Camera), Csv.Text(r.RecordedAt?.ToString("O")), Csv.Text(r.RecordedAtSource), Csv.Text(r.Destination is null ? r.OriginalName : Path.GetFileName(r.Destination)),
                "", Csv.Number(r.RemoteSize), "", "false", Csv.Text(reason)])).Append("\r\n");
        return [.. Csv.Utf8WithBom.GetPreamble(), .. Encoding.UTF8.GetBytes(text.ToString())];
    }

    private static async Task<byte[]> ReadSharedAsync(string path, CancellationToken cancellationToken)
    {
        await using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
        using var memory = new MemoryStream();
        await stream.CopyToAsync(memory, cancellationToken).ConfigureAwait(false);
        return memory.ToArray();
    }

    private static string UniquePath(string path)
    {
        if (!File.Exists(path)) return path;
        var dir = Path.GetDirectoryName(path)!;
        var stem = Path.GetFileNameWithoutExtension(path);
        for (var i = 2; ; i++)
        {
            var candidate = Path.Combine(dir, $"{stem}_{i}.zip");
            if (!File.Exists(candidate)) return candidate;
        }
    }
}
