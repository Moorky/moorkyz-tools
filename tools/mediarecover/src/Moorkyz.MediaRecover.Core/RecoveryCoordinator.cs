using System.Diagnostics;
using System.Security.Cryptography;

namespace Moorkyz.Tools.MediaRecover.Core;

public sealed class RecoveryCoordinator(IManifestStore store, IEnumerable<ISourceProvider> providers, IIntegrityService integrity, IReportWriter reports) : IRecoveryCoordinator, IDisposable
{
    private readonly Dictionary<string, ISourceProvider> providers = providers.ToDictionary(p => p.Kind);
    private readonly ManualResetEventSlim pause = new(true);
    public event Action<ProgressEvent>? Progress;
    public void Pause() { pause.Reset(); Emit("Paused", "Paused; current read may finish before the next safe boundary."); }
    public void Resume() { pause.Set(); Emit("Running", "Resumed."); }
    private void Emit(string status, string text, long bytes = 0, double speed = 0) => Progress?.Invoke(new(status, text, bytes, speed));
    private static string ControlRoot(Settings s) => Path.Combine(s.Destination, ".mediarecover");
    private static FileStream DestinationLock(Settings settings)
    {
        settings.Validate();
        Directory.CreateDirectory(settings.Destination);
        var root = ControlRoot(settings);
        PathSafety.NoLinks(root);
        Directory.CreateDirectory(root);
        return new FileStream(Path.Combine(root, "operation.lock"), FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
    }
    public async Task<RunRecord> ScanAsync(Settings settings, CancellationToken token)
    {
        settings = settings.Clone(); settings.Validate();
        using var destinationLock = DestinationLock(settings);
        await ReconcileAsync(token);
        var id = DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss") + "-" + Guid.NewGuid().ToString("N")[..8];
        var run = new RunRecord(id, DateTimeOffset.Now, settings, Reports: Path.Combine(ControlRoot(settings), "reports", id));
        store.SaveRun(run);
        var seen = new HashSet<string>(StringComparer.Ordinal);
        try
        {
            // Existing bytes are only candidates; a source-ledger hash is required for trusted skipping.
            await InventoryDestinationAsync(run, token);
            foreach (var source in settings.Sources)
            {
                try
                {
                    await foreach (var entry in providers[source.Kind].InventoryAsync(source, settings, token))
                    {
                        pause.Wait(token);
                        if (entry.Issue is { } issue)
                        {
                            store.AddIssue(id, issue); run = run with { Partial = true, Inaccessible = run.Inaccessible + 1 };
                            Emit("SCAN_PARTIAL", $"{issue.Path}: {issue.Message} — {issue.Remedy}"); continue;
                        }
                        var item = entry.Item!;
                        if (!seen.Add(item.Key)) continue;
                        run = run with { Discovered = run.Discovered + 1 };
                        if (item.MediaType == "ExcludedType") { store.SaveInventory(id, new(item, ItemStatus.Excluded, ErrorCode: "EXCLUDED_BY_TYPE")); continue; }
                        run = run with { Examined = run.Examined + 1, Video = run.Video + (item.MediaType == "Video" ? 1 : 0), Audio = run.Audio + (item.MediaType == "Audio" ? 1 : 0) };
                        var decision = Filter.Date(item, settings);
                        item = item with { UnknownDate = decision.Unknown, DateConflict = decision.Conflict };
                        var record = new FileRecord(item, decision.Eligible ? ItemStatus.Eligible : ItemStatus.Excluded,
                            ErrorCode: decision.Eligible ? "" : "EXCLUDED_BY_DATE");
                        var prior = store.File(item.Key);
                        if (decision.Eligible && !item.Version.EndsWith(":provisional", StringComparison.Ordinal) && prior is not null && prior.Item.Version == item.Version && await TrustedAsync(prior, token))
                            record = prior with { Item = item, Status = ItemStatus.AlreadyVerified, Attempts = 0, Seconds = 0 };
                        store.SaveInventory(id, record);
                        Emit("Scanning", $"Found {run.Discovered}; examined media {run.Examined}; {item.RelativePath}");
                    }
                }
                catch (OperationCanceledException) { throw; }
                catch (Exception e)
                {
                    var error = Errors.Describe(e);
                    store.AddIssue(id, new(source.Id, error.Code, error.Message, error.Remedy));
                    run = run with { Partial = true, Inaccessible = run.Inaccessible + 1 };
                    Emit("SCAN_PARTIAL", error.Message + " — " + error.Remedy);
                }
            }
            run = run with { Status = run.Partial ? "Scan partial — review warnings" : "Ready to recover" };
        }
        catch (OperationCanceledException) { run = run with { Status = "Interrupted scan", Partial = true }; }
        store.SaveRun(run);
        await reports.WriteAsync(run, store, CancellationToken.None);
        Emit(run.Status, $"Discovered {run.Discovered}; media examined {run.Examined}. Review before recovery. Reports: {run.Reports}");
        return run;
    }
    private async Task InventoryDestinationAsync(RunRecord run, CancellationToken token)
    {
        var stack = new Stack<string>(); stack.Push(run.Settings.Destination);
        var knownTargets = store.Files().Select(f => f.Target).ToHashSet(StringComparer.OrdinalIgnoreCase);
        while (stack.TryPop(out var dir))
        {
            token.ThrowIfCancellationRequested();
            try
            {
                PathSafety.NoLinks(dir);
                foreach (var path in Directory.EnumerateFileSystemEntries(dir))
                {
                    token.ThrowIfCancellationRequested();
                    if (PathSafety.Within(path, ControlRoot(run.Settings))) continue;
                    var attr = File.GetAttributes(path);
                    if ((attr & FileAttributes.ReparsePoint) != 0) { store.AddIssue(run.Id, new(path, "LINK_SKIPPED", "Destination link not inspected.", "Use a real folder.")); continue; }
                    if ((attr & FileAttributes.Directory) != 0) stack.Push(path);
                    else if (!knownTargets.Contains(path) && Filter.Media(path, run.Settings) is not null)
                        store.AddIssue(run.Id, new(path, "LEGACY_UNVERIFIED", "Existing destination file has no trusted source-ledger match; preserved.", "Import legacy files to record their local hashes; recover source separately."));
                }
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException)
            {
                var error = Errors.Describe(e); store.AddIssue(run.Id, new(dir, error.Code, error.Message, error.Remedy));
            }
            await Task.Yield();
        }
    }
    public async Task<bool> TrustedAsync(FileRecord record, CancellationToken token)
    {
        if (record.Status is not (ItemStatus.Verified or ItemStatus.AlreadyVerified) || record.Hash.Length != 64 || !File.Exists(record.Target)) return false;
        PathSafety.NoLinks(record.Target);
        return new FileInfo(record.Target).Length == record.Bytes && record.Bytes > 0 && await integrity.HashAsync(record.Target, token) == record.Hash;
    }
    public async Task ReconcileAsync(CancellationToken token)
    {
        foreach (var record in store.Files())
        {
            token.ThrowIfCancellationRequested();
            if (record.Status is ItemStatus.Promoting or ItemStatus.Transferring or ItemStatus.Verifying)
            {
                var repaired = record;
                if (record.Status == ItemStatus.Promoting && record.Hash.Length == 64 && File.Exists(record.Target) &&
                    new FileInfo(record.Target).Length == record.Bytes && await integrity.HashAsync(record.Target, token) == record.Hash)
                    repaired = record with { Status = ItemStatus.Verified, VerifiedAt = DateTimeOffset.Now };
                else repaired = record with { Status = ItemStatus.Interrupted, ErrorCode = "INTERRUPTED", Error = "Prior process ended before a verified promotion. A fresh file read is required." };
                store.SaveFile(repaired);
            }
        }
    }
    public async Task<RunRecord> RecoverAsync(RunRecord run, CancellationToken token)
    {
        using var destinationLock = DestinationLock(run.Settings);
        await ReconcileAsync(token);
        pause.Set();
        run = run with { Status = "Recovering", Finished = null }; store.SaveRun(run);
        bool interrupted = false;
        string? waiting = null;
        foreach (var original in store.Inventory(run.Id))
        {
            if (original.Status == ItemStatus.Excluded) continue;
            try
            {
                token.ThrowIfCancellationRequested(); pause.Wait(token);
                var previous = store.File(original.Item.Key);
                if (!original.Item.Version.EndsWith(":provisional", StringComparison.Ordinal) && previous is not null && previous.Item.Version == original.Item.Version && await TrustedAsync(previous, token))
                {
                    store.SaveInventory(run.Id, previous with { Item = original.Item, Status = ItemStatus.AlreadyVerified, Attempts = 0, Seconds = 0 });
                    Emit("ALREADY_VERIFIED", original.Item.RelativePath); continue;
                }
                var result = await TransferWithRetryAsync(original, previous, run, token);
                store.SaveInventory(run.Id, result);
                if (result.Status == ItemStatus.Failed && result.ErrorCode is "DEVICE_DISCONNECTED" or "DEVICE_LOCKED_OR_UNAUTHORIZED" or "WAITING_FOR_SPACE" or "STORAGE_UNAVAILABLE")
                {
                    waiting = result.ErrorCode == "WAITING_FOR_SPACE" ? "Waiting for space" :
                        result.ErrorCode == "STORAGE_UNAVAILABLE" ? "Waiting for storage" : "Waiting for device";
                    break;
                }
            }
            catch (OperationCanceledException)
            {
                var pending = store.File(original.Item.Key) ?? original;
                store.SaveInventory(run.Id, pending with { Status = ItemStatus.Interrupted, ErrorCode = "INTERRUPTED", Error = "Stopped safely; incomplete staging is retained, never promoted." });
                interrupted = true; break;
            }
        }
        var records = store.Inventory(run.Id);
        var hasErrors = records.Any(r => r.Status is ItemStatus.Failed or ItemStatus.Interrupted);
        run = run with { Status = waiting ?? (interrupted ? "Interrupted" : hasErrors || run.Partial || store.Issues(run.Id).Any() ? "Completed with warnings" : "Completed"), Finished = waiting is null ? DateTimeOffset.Now : null };
        store.SaveRun(run); await reports.WriteAsync(run, store, CancellationToken.None);
        Emit(run.Status, $"Saved to: {run.Settings.Destination} | Reports: {run.Reports}");
        return run;
    }
    private async Task<FileRecord> TransferWithRetryAsync(FileRecord original, FileRecord? previous, RunRecord run, CancellationToken token)
    {
        var record = original with { Status = ItemStatus.Eligible, Attempts = 0, Error = "", ErrorCode = "", Remedy = "" };
        var delays = new[] { 2, 5, 15, 45 };
        var max = run.Settings.Retry ? 5 : 1;
        for (var attempt = 1; attempt <= max; attempt++)
        {
            token.ThrowIfCancellationRequested(); pause.Wait(token);
            record = record with { Attempts = attempt };
            try { return await TransferAsync(record, previous, run, token); }
            catch (OperationCanceledException) { throw; }
            catch (Exception e)
            {
                var error = Errors.Describe(e);
                record = (store.File(record.Item.Key) ?? record) with { Status = ItemStatus.Failed, Attempts = attempt, ErrorCode = error.Code, Error = error.Message, Remedy = error.Remedy };
                if (error.Code is "HASH_MISMATCH" or "SOURCE_HASH_MISMATCH" && File.Exists(record.Staging))
                {
                    var folder = Path.Combine(ControlRoot(run.Settings), "quarantine"); PathSafety.NoLinks(folder); Directory.CreateDirectory(folder);
                    var suspect = Path.Combine(folder, Guid.NewGuid().ToString("N") + ".suspect");
                    PathSafety.NoLinks(record.Staging); File.Copy(record.Staging, suspect, false);
                    record = record with { Quarantine = suspect };
                    await System.IO.File.WriteAllTextAsync(suspect + ".json", System.Text.Json.JsonSerializer.Serialize(new { record.Item, Cause = error.Code, Staging = record.Staging }), token);
                }
                store.SaveFile(record); store.SaveInventory(run.Id, record);
                Emit(error.Code, $"{record.Item.RelativePath}: {error.Message} — {error.Remedy} | attempt {attempt}/{max}");
                if (!error.Transient || attempt == max) return record;
                Emit(error.Code == "DEVICE_DISCONNECTED" ? "WAITING_FOR_DEVICE" : "RETRY_SCHEDULED", "Reconnect/unlock device if needed. Retrying with a new source handle from byte zero.");
                await Task.Delay(TimeSpan.FromSeconds(delays[attempt - 1] + Random.Shared.NextDouble()), token);
            }
        }
        return record;
    }
    private async Task<FileRecord> TransferAsync(FileRecord record, FileRecord? previous, RunRecord run, CancellationToken token)
    {
        var settings = run.Settings;
        var provider = providers[record.Item.Source.Kind];
        if (!await provider.IsUnchangedAsync(record.Item, token))
            throw new RecoveryException("SOURCE_CHANGED", "Source changed since scan or is no longer accessible.", "Rescan the source; old outputs are preserved.", true);
        var target = PathSafety.Unused(PathSafety.Target(settings, record.Item));
        var stagingDir = Path.Combine(ControlRoot(settings), "staging"); PathSafety.NoLinks(stagingDir); Directory.CreateDirectory(stagingDir);
        var staging = Path.Combine(stagingDir, Guid.NewGuid().ToString("N") + ".part");
        record = record with { Status = ItemStatus.Transferring, Target = target, Staging = staging, Hash = "", Bytes = 0, ErrorCode = "", Error = "", Remedy = "" };
        store.SaveFile(record); store.SaveInventory(run.Id, record);
        var clock = Stopwatch.StartNew();
        long bytes = 0;
        string transferHash;
        using (var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256))
        {
            await using var input = await provider.OpenReadAsync(record.Item, token);
            await using var output = new FileStream(staging, FileMode.CreateNew, FileAccess.Write, FileShare.None, 1024 * 1024, FileOptions.Asynchronous | FileOptions.SequentialScan);
            var buffer = new byte[1024 * 1024];
            while (true)
            {
                token.ThrowIfCancellationRequested(); pause.Wait(token);
                var read = await input.ReadAsync(buffer, token).AsTask().WaitAsync(TimeSpan.FromMinutes(2), token);
                if (read == 0) break;
                if (record.Item.Size is long expected && bytes + read > expected) throw new RecoveryException("SOURCE_CHANGED", "Read exceeds scanned source length.", "Rescan before retrying.");
                GuardSpace(stagingDir, read);
                await output.WriteAsync(buffer.AsMemory(0, read), token);
                hash.AppendData(buffer, 0, read); bytes += read;
                Emit("Transferring", record.Item.RelativePath, bytes, bytes / Math.Max(clock.Elapsed.TotalSeconds, .001));
                if (settings.SpeedLimitBytes > 0)
                {
                    var wanted = TimeSpan.FromSeconds((double)bytes / settings.SpeedLimitBytes);
                    if (wanted > clock.Elapsed) await Task.Delay(wanted - clock.Elapsed, token);
                }
            }
            await output.FlushAsync(token); output.Flush(true);
            transferHash = Convert.ToHexString(hash.GetHashAndReset()).ToLowerInvariant();
        }
        if (bytes == 0) throw new RecoveryException("ZERO_BYTE", "The source produced zero bytes.", "Check source readability; empty files are not successful media recovery.");
        if (record.Item.Size is long expectedSize && bytes != expectedSize) throw new RecoveryException("LENGTH_MISMATCH", "The source ended before its reported length.", "Reconnect and retry from the beginning.", true);
        if (!await provider.IsUnchangedAsync(record.Item, token)) throw new RecoveryException("SOURCE_CHANGED", "Source metadata changed during transfer.", "Rescan and retry.");
        record = record with { Status = ItemStatus.Verifying, Bytes = bytes }; store.SaveFile(record);
        var diskHash = await integrity.HashAsync(staging, token);
        if (diskHash != transferHash) throw new RecoveryException("HASH_MISMATCH", "Saved bytes differ from the transfer stream.", "Keep staging for diagnosis; check destination storage and retry.", true);
        if (settings.HighAssurance)
        {
            await using var independent = await provider.OpenReadAsync(record.Item, token);
            var second = Convert.ToHexString(await SHA256.HashDataAsync(independent, token)).ToLowerInvariant();
            if (second != diskHash || !await provider.IsUnchangedAsync(record.Item, token))
                throw new RecoveryException("SOURCE_HASH_MISMATCH", "Independent source read differs.", "Check device stability and retry.", true);
        }
        string quarantine = "";
        bool repaired = previous is { Hash.Length: 64 } && previous.Item.Version == record.Item.Version && !await TrustedAsync(previous, token);
        if (repaired && previous is not null && File.Exists(previous.Target))
        {
            var quarantineDir = Path.Combine(ControlRoot(settings), "quarantine"); PathSafety.NoLinks(quarantineDir); Directory.CreateDirectory(quarantineDir);
            quarantine = Path.Combine(quarantineDir, Guid.NewGuid().ToString("N") + "-" + PathSafety.SafeName(Path.GetFileName(previous.Target)));
            PathSafety.NoLinks(previous.Target);
            File.Copy(previous.Target, quarantine, false); // Keep the suspect original too: no destructive replacement.
            await File.WriteAllTextAsync(quarantine + ".json", System.Text.Json.JsonSerializer.Serialize(new { previous.Item, PreviousHash = previous.Hash, Cause = "LOCAL_CORRUPTION", Original = previous.Target }), token);
        }
        PathSafety.NoLinks(target); Directory.CreateDirectory(Path.GetDirectoryName(target)!);
        record = record with
        {
            Status = ItemStatus.Promoting,
            Hash = diskHash,
            Bytes = bytes,
            Seconds = clock.Elapsed.TotalSeconds,
            Verification = settings.HighAssurance ? "Independent source SHA-256 + saved SHA-256/length" : "Transfer stream SHA-256 + saved SHA-256/length (not independent source verification)",
            Quarantine = quarantine,
            Repaired = repaired
        };
        store.SaveFile(record); store.SaveInventory(run.Id, record);
        File.Move(staging, target, false);
        record = record with { Status = ItemStatus.Verified, VerifiedAt = DateTimeOffset.Now };
        store.SaveFile(record); store.SaveInventory(run.Id, record);
        Emit("SUCCESS", $"{record.Item.RelativePath} → {target} | {bytes:N0} bytes | {clock.Elapsed.TotalSeconds:F2}s | SHA-256 {diskHash}");
        return record;
    }
    private static void GuardSpace(string directory, int nextWrite)
    {
        var root = Path.GetPathRoot(Path.GetFullPath(directory))!;
        if (root.StartsWith(@"\\", StringComparison.Ordinal)) return; // UNC free space may not be available; writes still fail explicitly.
        if (new DriveInfo(root).AvailableFreeSpace < nextWrite + 16L * 1024 * 1024)
            throw new RecoveryException("WAITING_FOR_SPACE", "Destination has insufficient free space (16 MiB reserve).", "Free space or select a different destination.");
    }
    public async Task<IReadOnlyList<FileRecord>> VerifyAsync(CancellationToken token)
    {
        var failures = new List<FileRecord>();
        await ReconcileAsync(token);
        foreach (var record in store.Files().Where(r => r.Status is ItemStatus.Verified or ItemStatus.AlreadyVerified))
        {
            token.ThrowIfCancellationRequested();
            if (!await TrustedAsync(record, token))
            {
                var failed = record with { Status = ItemStatus.Failed, ErrorCode = "LOCAL_CORRUPTION", Error = "Saved file missing, changed or truncated.", Remedy = "Reconnect source, scan and recover; suspect original is preserved and copied to quarantine before repair." };
                store.SaveFile(failed); failures.Add(failed); Emit("LOCAL_CORRUPTION", failed.Target);
            }
            else { store.SaveFile(record with { VerifiedAt = DateTimeOffset.Now }); Emit("LOCAL_INTEGRITY_VERIFIED", record.Target); }
        }
        return failures;
    }
    public void Dispose() => pause.Dispose();
}
