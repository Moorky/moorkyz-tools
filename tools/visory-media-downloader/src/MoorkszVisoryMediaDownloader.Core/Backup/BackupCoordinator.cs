using System.Text.Json;
using MoorkszVisoryMediaDownloader.Core.Archive;
using MoorkszVisoryMediaDownloader.Core.Download;
using MoorkszVisoryMediaDownloader.Core.Infrastructure;
using MoorkszVisoryMediaDownloader.Core.Providers;
using MoorkszVisoryMediaDownloader.Core.Reporting;
using MoorkszVisoryMediaDownloader.Core.Storage;

namespace MoorkszVisoryMediaDownloader.Core.Backup;

public sealed record BackupRequest
{
    public IReadOnlyList<string>? Keys { get; init; }
    public RecordingFilter Filter { get; init; } = new();
    public string? ResumeRunId { get; init; }
    public bool CreateArchive { get; init; } = true;
    public ArchiveScope ArchiveScope { get; init; } = ArchiveScope.AllSelected;
}

public sealed record VerifyReport(int Checked, int Verified, int Missing, int Corrupt);

/// <summary>Coordinates scanning, verification, download runs, reports and archives for one backup folder.</summary>
public sealed class BackupCoordinator : IDisposable
{
    private readonly IBitsClient? bits;
    private readonly IDiskSpaceProbe disk;
    private readonly TimeProvider time;
    private readonly object gate = new();
    private DownloadEngine? engine;
    private RunLogger? currentLog;

    public BackupCoordinator(BackupLayout layout, IRecordingProvider provider, AppSettings settings, IBitsClient? bits = null, IDiskSpaceProbe? disk = null,
        TimeProvider? time = null, EngineOptions? engineOptions = null)
    {
        Layout = layout;
        layout.Ensure();
        Provider = provider;
        Settings = settings;
        this.bits = bits;
        this.disk = disk ?? new DriveDiskSpaceProbe();
        this.time = time ?? TimeProvider.System;
        EngineOptionsOverride = engineOptions;
        Store = new StateStore(layout.DatabasePath);
        Archives = new ArchiveService(layout, Store, this.disk);
    }

    public BackupLayout Layout { get; }
    public IRecordingProvider Provider { get; }
    public AppSettings Settings { get; set; }
    public StateStore Store { get; }
    public ArchiveService Archives { get; }
    public EngineOptions? EngineOptionsOverride { get; set; }
    public DownloadEngine? Engine { get { lock (gate) return engine; } }
    public RunLogger? CurrentLog { get { lock (gate) return currentLog; } }
    public bool IsRunning => Engine is not null;
    public event Action<LogEntry>? Log;
    public event Action<DownloadEngine>? EngineStarted;
    public event Action<string>? SessionExpired;

    /// <summary>Repairs state after an unclean exit. Never deletes completed recordings.</summary>
    public async Task<int> ReconcileAsync(CancellationToken cancellationToken = default)
    {
        var repaired = 0;
        foreach (var run in Store.Runs().Where(r => r.State == RunState.Running))
        {
            Store.UpdateRun(run.Id, RunState.Interrupted, null, null);
            repaired++;
        }
        foreach (var entry in Store.ByStatus(RecordingStatus.Finalizing, RecordingStatus.Downloading, RecordingStatus.Queued))
        {
            if (entry.Status == RecordingStatus.Finalizing && entry.Destination is not null && entry.Sha256 is not null)
            {
                var destination = Layout.DestinationPath(entry.Destination);
                foreach (var candidate in new[] { destination, destination + ".part", destination + ".bits.part" })
                {
                    if (!File.Exists(candidate) || new FileInfo(candidate).Length != entry.ActualSize) continue;
                    var (sha, _) = await HttpTransfer.HashFileAsync(candidate, false, cancellationToken).ConfigureAwait(false);
                    if (sha != entry.Sha256) continue;
                    if (candidate != destination && !File.Exists(destination)) File.Move(candidate, destination, overwrite: false);
                    if (!File.Exists(destination)) break;
                    entry.Status = RecordingStatus.Verified;
                    entry.LastVerifiedAt = time.GetUtcNow();
                    entry.ResumeValidator = null;
                    entry.ResumeTotal = null;
                    Store.Update(entry);
                    repaired++;
                    break;
                }
                if (entry.Status == RecordingStatus.Verified) continue;
            }
            if (entry.Status is RecordingStatus.Finalizing || (entry.Status == RecordingStatus.Downloading && entry.BitsJob is null))
            {
                entry.Status = RecordingStatus.Queued;
                Store.Update(entry);
                repaired++;
            }
        }
        return repaired;
    }

    public async Task<InventorySummary> ScanAsync(IProgress<DiscoveryProgress>? progress, CancellationToken cancellationToken)
    {
        var started = time.GetUtcNow();
        var result = await Provider.DiscoverAsync(progress, cancellationToken).ConfigureAwait(false);
        var scanId = "scan-" + started.ToString("yyyyMMddHHmmssfff", Format.Invariant);
        Store.MergeDiscovery(Provider.ProviderId, scanId, result.Recordings, time.GetUtcNow());
        Store.SaveScan(scanId, Provider.ProviderId, started, time.GetUtcNow(), result);
        QuickCheckVerified();
        return Inventory(Settings.Filter);
    }

    /// <summary>Cheap existence/size check of previously verified files.</summary>
    public int QuickCheckVerified()
    {
        var changed = 0;
        foreach (var entry in Store.ByStatus(RecordingStatus.Verified))
        {
            var status = CheckFile(entry, hash: false);
            if (status == RecordingStatus.Verified) continue;
            entry.Status = status;
            entry.LastError = status == RecordingStatus.Missing ? "File missing on disk" : "Size differs from verified size";
            Store.Update(entry);
            changed++;
        }
        return changed;
    }

    public InventorySummary Inventory(RecordingFilter filter)
    {
        var all = Store.All(Provider.ProviderId);
        var scan = Store.LatestScan(Provider.ProviderId);
        var selected = all.Where(filter.Matches).ToList();
        var toDownload = selected.Where(e => e.Status != RecordingStatus.Verified).ToList();
        return new InventorySummary
        {
            Discovered = all.Count,
            Cameras = all.Select(e => e.Camera).Distinct(StringComparer.OrdinalIgnoreCase).Count(),
            Verified = all.Count(e => e.Status == RecordingStatus.Verified),
            Missing = all.Count(e => e.Status == RecordingStatus.Missing),
            Corrupt = all.Count(e => e.Status == RecordingStatus.Corrupt),
            NeedsRetry = all.Count(e => e.Status is RecordingStatus.Failed or RecordingStatus.Canceled),
            New = all.Count(e => e.Status is RecordingStatus.New or RecordingStatus.Queued or RecordingStatus.Downloading or RecordingStatus.Finalizing),
            Selected = selected.Count,
            ToDownload = toDownload.Count,
            KnownSelectedBytes = toDownload.Sum(e => e.KnownSize ?? 0),
            UnknownSizeSelected = toDownload.Count(e => e.KnownSize is null),
            AvailableBytes = disk.AvailableBytes(Layout.Root),
            Completeness = scan?.Completeness ?? DiscoveryCompleteness.Incomplete,
            Notes = scan?.Notes ?? ["No cloud scan has been completed yet."],
            Errors = scan?.Errors ?? [],
            ScannedAt = scan?.FinishedAt,
        };
    }

    public async Task<VerifyReport> VerifyExistingAsync(IEnumerable<RecordingEntry> entries, IntegrityPolicy policy, IProgress<(int Done, int Total)>? progress, CancellationToken cancellationToken)
    {
        var list = entries.Where(e => e.Destination is not null && e.Status is RecordingStatus.Verified or RecordingStatus.Missing or RecordingStatus.Corrupt).ToList();
        int verified = 0, missing = 0, corrupt = 0, done = 0;
        foreach (var entry in list)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var status = await Task.Run(() => CheckFile(entry, policy == IntegrityPolicy.Sha256), cancellationToken).ConfigureAwait(false);
            if (status == RecordingStatus.Verified) { verified++; entry.LastVerifiedAt = time.GetUtcNow(); }
            else if (status == RecordingStatus.Missing) missing++;
            else corrupt++;
            if (entry.Status != status || status == RecordingStatus.Verified)
            {
                entry.Status = status;
                entry.LastError = status switch
                {
                    RecordingStatus.Missing => "File missing on disk",
                    RecordingStatus.Corrupt => "Local file no longer matches its verified size/SHA-256",
                    _ => null,
                };
                Store.Update(entry);
            }
            progress?.Report((++done, list.Count));
        }
        return new VerifyReport(list.Count, verified, missing, corrupt);
    }

    private RecordingStatus CheckFile(RecordingEntry entry, bool hash)
    {
        if (entry.Destination is null) return RecordingStatus.Missing;
        var path = Layout.DestinationPath(entry.Destination);
        if (!File.Exists(path)) return RecordingStatus.Missing;
        if (entry.ActualSize is { } size && new FileInfo(path).Length != size) return RecordingStatus.Corrupt;
        if (hash && entry.Sha256 is not null && HttpTransfer.HashFileAsync(path, false, CancellationToken.None).GetAwaiter().GetResult().Sha256 != entry.Sha256)
            return RecordingStatus.Corrupt;
        return RecordingStatus.Verified;
    }

    public string? LatestInterruptedRunId() => Store.Runs(20).FirstOrDefault(r => r.State == RunState.Interrupted)?.Id;

    public async Task<RunSummary> RunBackupAsync(BackupRequest request, IProgress<ArchiveProgress>? archiveProgress, CancellationToken cancellationToken)
    {
        var now = time.GetUtcNow();
        stopRequested = false;
        cancelRequested = false;
        var resuming = request.ResumeRunId is not null && Store.RunExists(request.ResumeRunId);
        var runId = resuming ? request.ResumeRunId! : RunIds.Create(now, id => Store.RunExists(id) || Directory.Exists(Layout.RunFolder(id)));
        var inventory = Inventory(request.Filter);
        BackupRequest effective = request;
        if (resuming && Store.Run(runId)?.SelectionJson is { } json && JsonSerializer.Deserialize<BackupRequest>(json, JsonFiles.Options) is { } original)
            effective = original with { ResumeRunId = runId };
        if (!resuming) Store.CreateRun(runId, now, JsonSerializer.Serialize(request with { ResumeRunId = null }, JsonFiles.Options));
        else Store.UpdateRun(runId, RunState.Running, null, null);

        using var log = new RunLogger(Layout, runId, Store, Settings.StoreFullFailedUrls, time);
        log.Entry += OnLog;
        lock (gate) currentLog = log;
        DownloadEngine? runEngine = null;
        try
        {
            log.Info(resuming ? $"Run resumed | {runId}" : $"Run started | {runId} | Provider: {Provider.DisplayName}{(Provider.IsSynthetic ? " (DEMO - synthetic data, not your Visory account)" : "")}");
            log.Info($"Recordings discovered: {inventory.Discovered} | Cameras: {inventory.Cameras} | Inventory: {inventory.Completeness}");
            if (inventory.Completeness != DiscoveryCompleteness.Complete)
                log.Warn("Cloud inventory not confirmed complete | Results cover discovered recordings only");

            List<string> scheduled;
            if (resuming)
            {
                scheduled = Store.RunItems(runId).Where(i => i.Role == "scheduled" && i.FinalStatus is null).Select(i => i.Key).ToList();
                log.ScheduledTotal = Store.RunItems(runId).Count(i => i.Role == "scheduled");
            }
            else
            {
                var selection = (effective.Keys is { } keys ? keys.Select(Store.Get).OfType<RecordingEntry>() : Store.All(Provider.ProviderId).Where(effective.Filter.Matches)).ToList();
                log.Info($"Recordings selected: {selection.Count}");
                await VerifyExistingAsync(selection, Settings.Integrity, null, cancellationToken).ConfigureAwait(false);
                foreach (var entry in selection.Where(e => e.Status == RecordingStatus.Corrupt && e.Destination is not null))
                {
                    var path = Layout.DestinationPath(entry.Destination!);
                    if (!File.Exists(path)) continue;
                    // Never overwrite: the changed file is preserved beside the re-download for inspection.
                    var preserved = path + ".corrupt-" + time.GetLocalNow().ToString("yyyyMMdd-HHmmss", Format.Invariant);
                    File.Move(path, preserved, overwrite: false);
                    log.Warn($"@{Path.GetFileName(path)} | Local file no longer matches its verified size/SHA-256 | Preserved as {Path.GetFileName(preserved)} and queued again");
                }
                var reused = selection.Where(e => e.Status == RecordingStatus.Verified).ToList();
                scheduled = selection.Where(e => e.Status != RecordingStatus.Verified).Select(e => e.Key).ToList();
                Store.AddRunItems(runId, reused.Select(e => e.Key), "reused");
                Store.AddRunItems(runId, scheduled, "scheduled");
                foreach (var entry in reused)
                    log.Skip(entry.Key, entry.RecordingId, Path.GetFileName(entry.Destination!), entry.Camera, entry.ActualSize, Layout.DestinationPath(entry.Destination!), entry.Sha256,
                        Settings.Integrity == IntegrityPolicy.Sha256 ? "Already downloaded and verified (SHA-256)" : "Already downloaded (size verified)");
                foreach (var key in scheduled)
                {
                    var entry = Store.Get(key)!;
                    entry.Status = RecordingStatus.Queued;
                    entry.Attempts = 0;
                    Store.Update(entry);
                }
                log.ScheduledTotal = scheduled.Count;
                log.Info($"Need download: {scheduled.Count} | Already verified: {reused.Count}");
            }

            var options = EngineOptionsOverride ?? EngineOptions.From(Settings);
            runEngine = new DownloadEngine(Store, Layout, Provider, options, bits, disk, time);
            runEngine.SessionExpired += message => SessionExpired?.Invoke(message);
            lock (gate) engine = runEngine;
            EngineStarted?.Invoke(runEngine);
            var stoppedByUser = false;
            try
            {
                await runEngine.RunAsync(log, scheduled, cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                stoppedByUser = true;
            }
            stoppedByUser |= cancellationToken.IsCancellationRequested || stopRequested;
            return await FinishRunAsync(runId, log, effective, inventory, stoppedByUser, archiveProgress, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            log.Entry -= OnLog;
            lock (gate)
            {
                engine = null;
                currentLog = null;
            }
            runEngine?.Dispose();
        }
    }

    private volatile bool cancelRequested;
    private volatile bool stopRequested;

    public void Pause() => Engine?.Pause();
    public void Resume() => Engine?.Resume();

    /// <summary>Hand-off keeps compatible BITS transfers running in Windows after the application exits.</summary>
    public void Stop(bool handOffBits)
    {
        cancelRequested = !handOffBits;
        stopRequested = true;
        Engine?.Stop(handOffBits);
    }

    private async Task<RunSummary> FinishRunAsync(string runId, RunLogger log, BackupRequest request, InventorySummary inventory, bool stopped,
        IProgress<ArchiveProgress>? archiveProgress, CancellationToken cancellationToken)
    {
        var items = Store.RunItems(runId);
        var scheduled = items.Where(i => i.Role == "scheduled").ToList();
        var reused = items.Count(i => i.Role == "reused");
        var stats = log.Stats;
        var attempts = Store.Attempts(runId);
        var pending = scheduled.Count(i => i.FinalStatus is null);
        var canceledByUser = stopped && cancelRequested;
        cancelRequested = false;
        stopRequested = false;
        var state = stopped
            ? canceledByUser ? RunState.Canceled : RunState.Interrupted
            : stats.Failed > 0 || pending > 0 || inventory.Completeness == DiscoveryCompleteness.Incomplete ? RunState.CompletedWithErrors : RunState.Completed;
        var outcome = state switch
        {
            RunState.Completed when inventory.Completeness == DiscoveryCompleteness.Complete => "Completed",
            RunState.Completed => "Completed - every discovered recording is backed up; the Visory inventory could not be confirmed complete",
            RunState.CompletedWithErrors => "Completed with errors",
            RunState.Canceled => "Canceled",
            _ => "Interrupted - resume to continue",
        };
        var started = Store.Run(runId)?.StartedAt ?? time.GetUtcNow();
        var finished = time.GetUtcNow();
        var selected = scheduled.Count + reused;
        var summary = new RunSummary
        {
            RunId = runId,
            StartedAt = started,
            FinishedAt = finished,
            ElapsedSeconds = (finished - started).TotalSeconds,
            State = state,
            Outcome = outcome,
            Discovered = inventory.Discovered,
            InventoryCompleteness = inventory.Completeness,
            Selected = selected,
            AlreadyVerified = reused,
            Scheduled = scheduled.Count,
            Succeeded = stats.Succeeded,
            Failed = stats.Failed,
            Skipped = Math.Max(0, inventory.Discovered - selected),
            PendingOrCanceled = pending,
            RetryAttempts = attempts.Count(a => a.Outcome != "success"),
            FilesRequiringRetries = attempts.GroupBy(a => a.Key).Count(g => g.Count() > 1),
            BytesTransferred = stats.TransferredBytes,
            AverageFileSeconds = stats.AverageSeconds,
            AverageFileBytes = stats.AverageBytes,
            AverageBytesPerSecond = stats.AverageBytesPerSecond,
            CompletionPercent = selected == 0 ? 100 : Math.Round((stats.Succeeded + reused) * 100.0 / selected, 2),
            ReportsFolder = Layout.RunFolder(runId),
            LogPath = log.LogPath,
            CsvReportPath = log.ReportPath,
            FailedReportPath = log.FailedPath,
            DebugLogPath = log.DebugPath,
            SummaryPath = log.SummaryPath,
        };

        if (request.CreateArchive && state is RunState.Completed or RunState.CompletedWithErrors)
        {
            try
            {
                var scopeKeys = request.ArchiveScope == ArchiveScope.ThisRunOnly
                    ? items.Where(i => i.FinalStatus == "SUCCESS").Select(i => i.Key)
                    : items.Select(i => i.Key);
                var scopeEntries = scopeKeys.Select(Store.Get).OfType<RecordingEntry>().ToList();
                log.Info($"Creating archive | Scope: {request.ArchiveScope} | Recordings in scope: {scopeEntries.Count}");
                log.WriteSummary(summary);
                var result = await Archives.CreateAsync(runId, request.ArchiveScope, scopeEntries, [log.LogPath, log.ReportPath, log.FailedPath, log.SummaryPath],
                    inventory.Completeness != DiscoveryCompleteness.Complete, archiveProgress, cancellationToken).ConfigureAwait(false);
                summary = summary with { ArchivePath = result.Path, ArchiveStatus = result.Incomplete && result.Status == "verified" ? "verified (incomplete)" : result.Status };
                if (result.Status is "verified" or "reused") log.Info($"Archive {result.Status} | {result.Path} | {result.Message}");
                else if (result.Status == "skipped") log.Info($"Archive skipped | {result.Message}");
                else log.Warn($"Archive {result.Status} | {result.Message}");
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException or InvalidOperationException or OperationCanceledException)
            {
                summary = summary with { ArchiveStatus = "failed" };
                log.Warn($"Archive failed | {e.Message}");
            }
        }

        log.Info($"{outcome} | Succeeded: {summary.Succeeded} | Failed: {summary.Failed} | Already verified: {summary.AlreadyVerified} | Pending/canceled: {summary.PendingOrCanceled} | Downloaded: {Format.Size(summary.BytesTransferred)}");
        log.WriteSummary(summary);
        Store.UpdateRun(runId, state, state == RunState.Interrupted ? null : finished, summary);
        return summary;
    }

    public async Task<ArchiveResult> CreateArchiveAsync(ArchiveScope scope, string? runId, IProgress<ArchiveProgress>? progress, CancellationToken cancellationToken)
    {
        var id = runId ?? Store.Runs(1).FirstOrDefault()?.Id ?? RunIds.Create(time.GetUtcNow(), _ => false);
        var keys = runId is null
            ? Store.All(Provider.ProviderId).Where(Settings.Filter.Matches).Select(e => e.Key)
            : scope == ArchiveScope.ThisRunOnly
                ? Store.RunItems(runId).Where(i => i.FinalStatus == "SUCCESS").Select(i => i.Key)
                : Store.RunItems(runId).Select(i => i.Key);
        var entries = keys.Select(Store.Get).OfType<RecordingEntry>().ToList();
        var reports = new[] { Layout.LogPath(id), Layout.CsvReportPath(id), Layout.FailedReportPath(id), Layout.SummaryPath(id) };
        var completeness = Store.LatestScan(Provider.ProviderId)?.Completeness ?? DiscoveryCompleteness.Incomplete;
        return await Archives.CreateAsync(id, scope, entries, reports, completeness != DiscoveryCompleteness.Complete, progress, cancellationToken).ConfigureAwait(false);
    }

    private void OnLog(LogEntry entry) => Log?.Invoke(entry);

    public void Dispose()
    {
        Engine?.Stop(handOffBits: true);
        Store.Dispose();
    }
}
