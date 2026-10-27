using System.Net.NetworkInformation;
using MoorkszVisoryMediaDownloader.Core.Infrastructure;
using MoorkszVisoryMediaDownloader.Core.Providers;
using MoorkszVisoryMediaDownloader.Core.Reporting;
using MoorkszVisoryMediaDownloader.Core.Storage;

namespace MoorkszVisoryMediaDownloader.Core.Download;

public enum EngineState { Idle, Running, Paused, PausedDiskFull, PausedSignInRequired, WaitingForNetwork, Stopping }

public sealed record EngineOptions
{
    public int MaxConcurrency { get; init; } = 2;
    public int MaxAttempts { get; init; } = 4;
    public TimeSpan IdleTimeout { get; init; } = TimeSpan.FromSeconds(60);
    public long BandwidthBytesPerSecond { get; init; }
    public bool UseBits { get; init; } = true;
    public PriorityMode Priority { get; init; } = PriorityMode.SmartBackground;
    public NamingRule Naming { get; init; } = NamingRule.CameraAndTime;
    public IReadOnlyList<TimeSpan> RetryDelays { get; init; } = [TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(15), TimeSpan.FromSeconds(45)];
    public double DelayScale { get; init; } = 1.0;
    public TimeSpan MaxRetryAfter { get; init; } = TimeSpan.FromMinutes(15);
    public long DiskReserveBytes { get; init; } = 256L * 1024 * 1024;
    public TimeSpan BitsPollInterval { get; init; } = TimeSpan.FromMilliseconds(500);
    public TimeSpan NetworkPollInterval { get; init; } = TimeSpan.FromSeconds(2);
    public Func<bool> NetworkAvailable { get; init; } = NetworkInterface.GetIsNetworkAvailable;
    public Action<string, TimeSpan>? RetryDelayObserver { get; init; }

    public static EngineOptions From(AppSettings settings) => new()
    {
        MaxConcurrency = settings.Priority == PriorityMode.SmartBackground ? Math.Min(settings.MaxConcurrency, 2) : settings.MaxConcurrency,
        MaxAttempts = settings.MaxAttempts,
        IdleTimeout = TimeSpan.FromSeconds(settings.TimeoutSeconds),
        BandwidthBytesPerSecond = settings.BandwidthLimitKBps * 1024L,
        UseBits = settings.UseBits,
        Priority = settings.Priority,
        Naming = settings.Naming,
    };

    public BitsPriority BitsPriority => Priority switch
    {
        PriorityMode.Fast => BitsPriority.Foreground,
        PriorityMode.Balanced => BitsPriority.High,
        _ => BitsPriority.Normal,
    };
}

public sealed record TransferSnapshot(string Key, string FileName, string Camera, string Status, TransferEngineKind Engine, int Attempt, int MaxAttempts,
    long Bytes, long? Total, double BytesPerSecond, TimeSpan Elapsed, string? Message, bool WaitingForRetry);

/// <summary>Hybrid BITS/HttpClient queue with classified retries, safe resume and atomic finalization.</summary>
public sealed class DownloadEngine : IDisposable
{
    private enum CancelReason { None, Pause, Stop, Cancel }

    private sealed class ActiveItem(string key, string fileName, string camera, int maxAttempts)
    {
        public string Key { get; } = key;
        public string FileName { get; set; } = fileName;
        public string Camera { get; } = camera;
        public int MaxAttempts { get; } = maxAttempts;
        public ProgressCounter Progress { get; } = new();
        public volatile CancelReason Reason;
        public CancellationTokenSource Cancel { get; set; } = new();
        public CancellationTokenSource? RetryWait { get; set; }
        public TransferEngineKind Engine { get; set; }
        public int Attempt { get; set; }
        public string Status { get; set; } = "Starting";
        public string? Message { get; set; }
        public DateTimeOffset Started { get; set; }
        public long SampleBytes;
        public long SampleTicks = Environment.TickCount64;
        public double Speed;
    }

    private readonly StateStore store;
    private readonly BackupLayout layout;
    private readonly IRecordingProvider provider;
    private readonly EngineOptions options;
    private readonly IBitsClient? bits;
    private readonly IDiskSpaceProbe disk;
    private readonly TimeProvider time;
    private readonly Random random;
    private readonly HttpClient http;
    private readonly HttpTransfer transfer;
    private readonly DestinationPlanner planner;
    private readonly bool customTransport;
    private readonly object gate = new();
    private readonly LinkedList<string> queue = new();
    private readonly Dictionary<string, ActiveItem> active = new(StringComparer.Ordinal);
    private TaskCompletionSource resumeSignal = NewSignal(open: true);
    private CancellationTokenSource stop = new();
    private CancelReason stopReason;
    private EngineState state = EngineState.Idle;
    private bool sessionExpiredRaised;

    public DownloadEngine(StateStore store, BackupLayout layout, IRecordingProvider provider, EngineOptions options,
        IBitsClient? bits = null, IDiskSpaceProbe? disk = null, TimeProvider? time = null, Random? random = null)
    {
        this.store = store;
        this.layout = layout;
        this.provider = provider;
        this.options = options;
        this.disk = disk ?? new DriveDiskSpaceProbe();
        this.time = time ?? TimeProvider.System;
        this.random = random ?? Random.Shared;
        var handler = provider.CreateMediaHandler();
        customTransport = handler is not null;
        this.bits = customTransport ? null : bits;
        http = HttpTransfer.CreateClient(handler, TimeSpan.FromSeconds(Math.Min(30, options.IdleTimeout.TotalSeconds)));
        Throttle = new Throttle(options.BandwidthBytesPerSecond);
        transfer = new HttpTransfer(http, provider.MediaPolicy, Throttle, options.IdleTimeout);
        planner = new DestinationPlanner(store, layout);
    }

    public Throttle Throttle { get; }
    public EngineState State { get { lock (gate) return state; } }
    public string? PauseReason { get; private set; }
    public event Action<EngineState>? StateChanged;
    public event Action<string>? SessionExpired;
    public event Action<string, RecordingStatus>? ItemFinished;
    public int QueuedCount { get { lock (gate) return queue.Count; } }

    public async Task RunAsync(RunLogger log, IReadOnlyList<string> keys, CancellationToken cancellationToken)
    {
        lock (gate)
        {
            if (state is not EngineState.Idle) throw new InvalidOperationException("The download engine is already running.");
            stop.Dispose();
            stop = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            stopReason = CancelReason.None;
            queue.Clear();
            foreach (var key in keys) queue.AddLast(key);
            sessionExpiredRaised = false;
        }
        SetState(EngineState.Running);
        log.ScheduledTotal = Math.Max(log.ScheduledTotal, keys.Count);
        var workers = Enumerable.Range(0, Math.Max(1, options.MaxConcurrency)).Select(_ => Task.Run(() => WorkerAsync(log))).ToArray();
        try
        {
            await Task.WhenAll(workers).ConfigureAwait(false);
        }
        finally
        {
            SetState(EngineState.Idle);
        }
    }

    public void Pause(string? reason = null, EngineState pausedState = EngineState.Paused)
    {
        lock (gate)
        {
            if (state is EngineState.Idle or EngineState.Stopping) return;
            PauseReason = reason;
            if (resumeSignal.Task.IsCompleted) resumeSignal = NewSignal(open: false);
            foreach (var item in active.Values)
            {
                if (item.Reason != CancelReason.None) continue;
                item.Reason = CancelReason.Pause;
                item.Cancel.Cancel();
            }
        }
        SetState(pausedState);
    }

    public void Resume()
    {
        lock (gate)
        {
            if (state is EngineState.Idle or EngineState.Stopping) return;
            PauseReason = null;
            sessionExpiredRaised = false;
            resumeSignal.TrySetResult();
        }
        SetState(EngineState.Running);
    }

    /// <summary>Stops the run. With hand-off, compatible BITS jobs continue in Windows and are reconciled on next start.</summary>
    public void Stop(bool handOffBits)
    {
        lock (gate)
        {
            stopReason = handOffBits ? CancelReason.Stop : CancelReason.Cancel;
            foreach (var item in active.Values)
            {
                item.Reason = stopReason;
                item.Cancel.Cancel();
            }
            resumeSignal.TrySetResult();
        }
        SetState(EngineState.Stopping);
        stop.Cancel();
    }

    public void CancelItem(string key)
    {
        lock (gate)
        {
            if (queue.Remove(key))
            {
                store.SetStatus(key, RecordingStatus.Canceled);
                ItemFinished?.Invoke(key, RecordingStatus.Canceled);
                return;
            }
            if (active.TryGetValue(key, out var item))
            {
                item.Reason = CancelReason.Cancel;
                item.Cancel.Cancel();
            }
        }
    }

    public void RetryNow(string key)
    {
        lock (gate)
            if (active.TryGetValue(key, out var item)) item.RetryWait?.Cancel();
    }

    public IReadOnlyList<TransferSnapshot> Snapshot()
    {
        var now = Environment.TickCount64;
        lock (gate)
        {
            var list = new List<TransferSnapshot>(active.Count + Math.Min(queue.Count, 200));
            foreach (var item in active.Values)
            {
                var bytes = item.Progress.Bytes;
                var elapsedMs = now - item.SampleTicks;
                if (elapsedMs >= 250)
                {
                    var instant = Math.Max(0, bytes - item.SampleBytes) * 1000.0 / elapsedMs;
                    item.Speed = item.Speed <= 0 ? instant : item.Speed * 0.6 + instant * 0.4;
                    item.SampleBytes = bytes;
                    item.SampleTicks = now;
                }
                list.Add(new TransferSnapshot(item.Key, item.FileName, item.Camera, item.Status, item.Engine, item.Attempt, item.MaxAttempts, bytes, item.Progress.Total,
                    item.Speed, time.GetUtcNow() - item.Started, item.Message, item.RetryWait is not null));
            }
            foreach (var key in queue.Take(200))
                list.Add(new TransferSnapshot(key, key, "", "Queued", TransferEngineKind.None, 0, options.MaxAttempts, 0, null, 0, TimeSpan.Zero, null, false));
            return list;
        }
    }

    public double CurrentBytesPerSecond => Snapshot().Where(s => s.Status != "Queued").Sum(s => s.BytesPerSecond);

    private async Task WorkerAsync(RunLogger log)
    {
        while (true)
        {
            Task wait;
            lock (gate) wait = resumeSignal.Task;
            try { await wait.WaitAsync(stop.Token).ConfigureAwait(false); }
            catch (OperationCanceledException) { return; }
            if (stop.IsCancellationRequested) return;
            string? key;
            lock (gate)
            {
                if (!resumeSignal.Task.IsCompleted) continue;
                key = queue.First?.Value;
                if (key is null) return;
                queue.RemoveFirst();
            }
            await ProcessAsync(key, log).ConfigureAwait(false);
        }
    }

    private async Task ProcessAsync(string key, RunLogger log)
    {
        var entry = store.Get(key);
        if (entry is null) return;
        try
        {
            planner.Assign(entry, options.Naming);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            FinalFail(log, entry, null, $"Cannot prepare destination: {e.Message}", null, 0, TransferEngineKind.None, null, 0);
            return;
        }
        var fileName = Path.GetFileName(entry.Destination!);
        var item = new ActiveItem(key, fileName, entry.Camera, options.MaxAttempts) { Started = time.GetUtcNow() };
        lock (gate)
        {
            item.Cancel = CancellationTokenSource.CreateLinkedTokenSource(stop.Token);
            if (stopReason != CancelReason.None) item.Reason = stopReason;
            else if (!resumeSignal.Task.IsCompleted)
            {
                item.Reason = CancelReason.Pause;
                item.Cancel.Cancel();
            }
            active[key] = item;
        }
        try
        {
            await ProcessItemAsync(entry, item, log).ConfigureAwait(false);
        }
        finally
        {
            lock (gate) active.Remove(key);
            item.Cancel.Dispose();
        }
    }

    private async Task ProcessItemAsync(RecordingEntry entry, ActiveItem item, RunLogger log)
    {
        var previous = store.Attempts(log.RunId, entry.Key);
        var attempt = previous.Count;
        var firstStart = previous.Count > 0 ? previous[0].StartedAt : time.GetUtcNow();
        var refresh = false;
        var authRefreshes = 0;
        TransferException? last = null;
        Uri? lastUrl = null;
        var fullPath = layout.DestinationPath(entry.Destination!);
        var partPath = fullPath + ".part";
        var bitsPath = fullPath + ".bits.part";
        Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
        PathSafety.EnsureNoReparsePoints(layout.Downloads, Path.GetDirectoryName(fullPath)!);

        while (attempt < options.MaxAttempts)
        {
            attempt++;
            item.Attempt = attempt;
            item.Status = "Downloading";
            item.Message = null;
            entry.Status = RecordingStatus.Downloading;
            entry.Attempts = attempt;
            entry.RunId = log.RunId;
            store.Update(entry);
            var started = time.GetUtcNow();
            try
            {
                item.Cancel.Token.ThrowIfCancellationRequested();
                EnsureDiskSpace(entry, fullPath);
                MediaRequest request;
                try
                {
                    request = await provider.GetMediaRequestAsync(entry, refresh, item.Cancel.Token).ConfigureAwait(false);
                }
                catch (ProviderUnavailableException e)
                {
                    throw new TransferException(refresh ? FailureKind.Permanent : FailureKind.Transient, e.Message);
                }
                refresh = false;
                lastUrl = request.Url;
                if (!provider.MediaPolicy.IsAllowed(request.Url, out var reason))
                    throw new TransferException(FailureKind.Permanent, $"Rejected unsafe URL: {reason}");
                if (entry.LastUrl != request.Url.AbsoluteUri && !request.RequiresHeaders)
                {
                    entry.LastUrl = request.Url.AbsoluteUri;
                    store.Update(entry);
                }

                TransferOutcome outcome;
                var bitsUrl = await BitsTargetAsync(entry, request, item.Cancel.Token).ConfigureAwait(false);
                if (bitsUrl is not null)
                {
                    item.Engine = TransferEngineKind.Bits;
                    outcome = await BitsDownloadAsync(entry, item, bitsUrl, bitsPath, log).ConfigureAwait(false);
                }
                else
                {
                    item.Engine = TransferEngineKind.Http;
                    var resume = entry.ResumeValidator is { } validator && entry.ResumeTotal is { } totalBytes ? new ResumeInfo(validator, totalBytes) : null;
                    outcome = await transfer.DownloadAsync(request, partPath, resume, entry.RemoteMd5,
                        info =>
                        {
                            entry.ResumeValidator = info?.Validator;
                            entry.ResumeTotal = info?.Total;
                            store.Update(entry);
                        },
                        item.Progress, item.Cancel.Token).ConfigureAwait(false);
                    if (outcome.Resumed) log.Debug($"{entry.Key}: resumed validated byte range from {Format.Size(outcome.Length - outcome.BytesThisAttempt)}");
                }
                Verify(entry, outcome, log);
                Finalize(entry, outcome, item.Engine == TransferEngineKind.Bits ? bitsPath : partPath, log);
                store.AddAttempt(log.RunId, entry.Key, attempt, started, time.GetUtcNow(), item.Engine, "success", null, outcome.BytesThisAttempt, null);
                var elapsed = (time.GetUtcNow() - firstStart).TotalSeconds;
                log.Success(new SuccessRecord(entry.Key, entry.RecordingId, Path.GetFileName(entry.Destination!), entry.Camera, Math.Max(outcome.Seconds, elapsed),
                    outcome.Length, outcome.BytesThisAttempt, outcome.Seconds, attempt, item.Engine, layout.DestinationPath(entry.Destination!), entry.Sha256));
                ItemFinished?.Invoke(entry.Key, RecordingStatus.Verified);
                return;
            }
            catch (Exception raw)
            {
                var e = Classify(raw, item);
                if (e.Kind is FailureKind.Paused or FailureKind.Stopped or FailureKind.Canceled or FailureKind.DiskFull)
                {
                    attempt--;
                    HandleInterruption(entry, item, e, log);
                    return;
                }
                if (raw is ReauthenticationRequiredException)
                {
                    attempt--;
                    entry.Status = RecordingStatus.Queued;
                    store.Update(entry);
                    Requeue(entry.Key);
                    RaiseSessionExpired(raw.Message, log);
                    return;
                }
                if (e.Kind == FailureKind.Network && !options.NetworkAvailable())
                {
                    attempt--;
                    try
                    {
                        await WaitForNetworkAsync(item, log).ConfigureAwait(false);
                    }
                    catch (OperationCanceledException)
                    {
                        HandleInterruption(entry, item, Classify(new OperationCanceledException(), item), log);
                        return;
                    }
                    continue;
                }
                last = e;
                store.AddAttempt(log.RunId, entry.Key, attempt, started, time.GetUtcNow(), item.Engine, e.Kind.ToString(), e.HttpStatus, e.BytesTransferred, Redactor.Clean(e.Message));
                entry.LastError = e.Message;
                entry.LastHttpStatus = e.HttpStatus;
                log.Debug($"{entry.Key} attempt {attempt}/{options.MaxAttempts} via {item.Engine}: {e.Kind} {e.Message}{(e.InnerException is null ? "" : " | " + e.InnerException.Message)}");
                if (e.Kind == FailureKind.Integrity)
                {
                    DeleteOwnPart(partPath);
                    DeleteOwnPart(bitsPath);
                    entry.ResumeValidator = null;
                    entry.ResumeTotal = null;
                }
                if (e.Kind == FailureKind.Permanent) break;
                if (e.Kind == FailureKind.AuthExpired)
                {
                    if (authRefreshes >= 2) break;
                    authRefreshes++;
                    refresh = true;
                }
                if (attempt >= options.MaxAttempts) break;
                var delay = e.Kind == FailureKind.AuthExpired ? TimeSpan.Zero : RetryDelay(attempt, e.RetryAfter);
                log.Retry(item.FileName, $"{e.Message} | Attempt {attempt}/{options.MaxAttempts} | {(refresh ? "Refreshing authorization" : $"Retrying in {delay.TotalSeconds:0.#}s")}");
                options.RetryDelayObserver?.Invoke(entry.Key, delay);
                if (!await DelayAsync(item, delay).ConfigureAwait(false))
                {
                    HandleInterruption(entry, item, Classify(new OperationCanceledException(), item), log);
                    return;
                }
            }
        }
        FinalFail(log, entry, last?.HttpStatus, last?.Message ?? "Download failed", lastUrl, attempt, item.Engine, firstStart, last?.BytesTransferred ?? 0);
    }

    private async Task<Uri?> BitsTargetAsync(RecordingEntry entry, MediaRequest request, CancellationToken cancellationToken)
    {
        if (bits is null || !options.UseBits || request.RequiresHeaders || options.BandwidthBytesPerSecond > 0 || !bits.IsAvailable) return null;
        if (entry.BitsJob is not null && Guid.TryParse(entry.BitsJob, out var existing) && bits.Query(existing).State is not (BitsJobState.Missing or BitsJobState.Cancelled or BitsJobState.Acknowledged))
            return request.Url;
        ProbeResult probe;
        try
        {
            probe = await transfer.ProbeAsync(request, cancellationToken).ConfigureAwait(false);
        }
        catch (TransferException e) when (e.Kind is FailureKind.Network)
        {
            throw;
        }
        if (probe.Status is 401 or 403 or 404 or 410 or 429 || probe.Status >= 500)
            throw TransferException.FromStatus((System.Net.HttpStatusCode)probe.Status, null);
        return probe.Status == 200 && probe.Length > 0 && probe.AcceptsRanges && !probe.Encoded && provider.MediaPolicy.IsAllowed(probe.FinalUrl, out _)
            ? probe.FinalUrl
            : null;
    }

    private async Task<TransferOutcome> BitsDownloadAsync(RecordingEntry entry, ActiveItem item, Uri url, string bitsPath, RunLogger log)
    {
        var started = Environment.TickCount64;
        Guid job;
        if (entry.BitsJob is not null && Guid.TryParse(entry.BitsJob, out var existing) && bits!.Query(existing).State is not (BitsJobState.Missing or BitsJobState.Cancelled or BitsJobState.Acknowledged))
        {
            job = existing;
            log.Debug($"{entry.Key}: re-attached to BITS job {job}");
        }
        else
        {
            DeleteOwnPart(bitsPath);
            job = bits!.Create($"Moorksz Visory Media Downloader - {item.FileName}", url, bitsPath, options.BitsPriority, options.IdleTimeout * 5);
            entry.BitsJob = job.ToString();
            store.Update(entry);
        }
        bits.Resume(job);
        var transientLogged = false;
        try
        {
            while (true)
            {
                item.Cancel.Token.ThrowIfCancellationRequested();
                var status = bits.Query(job);
                item.Progress.Total = status.BytesTotal;
                item.Progress.Set(status.BytesTransferred);
                switch (status.State)
                {
                    case BitsJobState.Transferred:
                        bits.Complete(job);
                        entry.BitsJob = null;
                        store.Update(entry);
                        for (var i = 0; i < 50 && !File.Exists(bitsPath); i++) await Task.Delay(100).ConfigureAwait(false);
                        if (!File.Exists(bitsPath)) throw new TransferException(FailureKind.Transient, "BITS completed but the file was not delivered");
                        var (sha, md5) = await HttpTransfer.HashFileAsync(bitsPath, entry.RemoteMd5 is not null, CancellationToken.None).ConfigureAwait(false);
                        var length = new FileInfo(bitsPath).Length;
                        return new TransferOutcome(length, sha, status.BytesTotal, entry.RemoteMd5, md5, length, Math.Max(0.001, (Environment.TickCount64 - started) / 1000.0), false, TransferEngineKind.Bits);
                    case BitsJobState.Error:
                        bits.Cancel(job);
                        entry.BitsJob = null;
                        store.Update(entry);
                        if (status.HttpStatus is { } code) throw TransferException.FromStatus((System.Net.HttpStatusCode)code, null);
                        throw new TransferException(FailureKind.Transient, $"BITS transfer error 0x{status.ErrorCode:X8}: {status.ErrorText ?? "unknown"}");
                    case BitsJobState.TransientError when !transientLogged:
                        transientLogged = true;
                        log.Debug($"{entry.Key}: BITS transient error 0x{status.ErrorCode:X8} {status.ErrorText}; Windows will retry");
                        break;
                    case BitsJobState.Suspended:
                        bits.Resume(job);
                        break;
                    case BitsJobState.Cancelled or BitsJobState.Acknowledged or BitsJobState.Missing:
                        entry.BitsJob = null;
                        store.Update(entry);
                        throw new TransferException(FailureKind.Transient, "The BITS job ended unexpectedly");
                }
                await Task.Delay(options.BitsPollInterval, item.Cancel.Token).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException)
        {
            switch (item.Reason)
            {
                case CancelReason.Pause:
                    bits.Suspend(job);
                    break;
                case CancelReason.Stop:
                    log.Debug($"{entry.Key}: BITS job {job} handed off to Windows");
                    break;
                default:
                    bits.Cancel(job);
                    entry.BitsJob = null;
                    store.Update(entry);
                    break;
            }
            throw;
        }
    }

    private void Verify(RecordingEntry entry, TransferOutcome outcome, RunLogger log)
    {
        if (outcome.DeclaredTotal is { } declared && outcome.Length != declared)
            throw new TransferException(FailureKind.Integrity, $"Size mismatch: expected {declared} bytes, received {outcome.Length}") { BytesTransferred = outcome.BytesThisAttempt };
        if (entry.RemoteSize is { } metadataSize && metadataSize != outcome.Length)
        {
            if (outcome.DeclaredTotal is null)
                throw new TransferException(FailureKind.Integrity, $"Size mismatch with Visory metadata: expected {metadataSize} bytes, received {outcome.Length}") { BytesTransferred = outcome.BytesThisAttempt };
            log.Warn($"@{Path.GetFileName(entry.Destination)} | Server size {outcome.Length} differs from listed size {metadataSize}; server length verified");
        }
        if (outcome.ExpectedMd5 is not null && outcome.ActualMd5 is not null && !string.Equals(outcome.ExpectedMd5, outcome.ActualMd5, StringComparison.Ordinal))
            throw new TransferException(FailureKind.Integrity, "Checksum mismatch with the server-provided MD5 digest") { BytesTransferred = outcome.BytesThisAttempt };
        if (outcome.DeclaredTotal is null && entry.RemoteSize is null)
            log.Debug($"{entry.Key}: no declared length; completeness relies on the transport's end-of-body signal");
    }

    private void Finalize(RecordingEntry entry, TransferOutcome outcome, string sourcePart, RunLogger log)
    {
        entry.Status = RecordingStatus.Finalizing;
        entry.Sha256 = outcome.Sha256;
        entry.ActualSize = outcome.Length;
        entry.Engine = outcome.Engine;
        store.Update(entry);
        var destination = layout.DestinationPath(entry.Destination!);
        if (File.Exists(destination))
        {
            var existingSha = HttpTransfer.HashFileAsync(destination, false, CancellationToken.None).GetAwaiter().GetResult().Sha256;
            if (existingSha == outcome.Sha256 && new FileInfo(destination).Length == outcome.Length)
            {
                DeleteOwnPart(sourcePart);
                log.Debug($"{entry.Key}: identical file already present at destination");
            }
            else
            {
                entry.Destination = null;
                planner.Assign(entry, options.Naming);
                destination = layout.DestinationPath(entry.Destination!);
                File.Move(sourcePart, destination, overwrite: false);
            }
        }
        else
        {
            File.Move(sourcePart, destination, overwrite: false);
        }
        DeleteOwnPart(layout.DestinationPath(entry.Destination!) + ".part");
        DeleteOwnPart(layout.DestinationPath(entry.Destination!) + ".bits.part");
        entry.Status = RecordingStatus.Verified;
        entry.LastVerifiedAt = time.GetUtcNow();
        entry.ResumeValidator = null;
        entry.ResumeTotal = null;
        entry.BitsJob = null;
        entry.LastError = null;
        entry.LastHttpStatus = null;
        store.Update(entry);
    }

    private void FinalFail(RunLogger log, RecordingEntry entry, int? httpStatus, string message, Uri? url, int attempts, TransferEngineKind engine, DateTimeOffset? firstStart, long bytes)
    {
        entry.Status = RecordingStatus.Failed;
        entry.LastError = message;
        entry.LastHttpStatus = httpStatus;
        store.Update(entry);
        log.Fail(new FailRecord(entry.Key, entry.RecordingId, entry.Destination is null ? entry.OriginalName ?? entry.RecordingId : Path.GetFileName(entry.Destination), entry.Camera,
            entry.RemoteSize ?? entry.ResumeTotal, message, httpStatus, attempts, engine, url, firstStart is null ? 0 : (time.GetUtcNow() - firstStart.Value).TotalSeconds)
            { LocalPath = entry.Destination is null ? null : layout.DestinationPath(entry.Destination) });
        ItemFinished?.Invoke(entry.Key, RecordingStatus.Failed);
    }

    private void HandleInterruption(RecordingEntry entry, ActiveItem item, TransferException e, RunLogger log)
    {
        switch (e.Kind)
        {
            case FailureKind.Canceled:
                entry.Status = RecordingStatus.Canceled;
                store.Update(entry);
                log.Info($"@{item.FileName} | Canceled; partial data kept for a later run");
                ItemFinished?.Invoke(entry.Key, RecordingStatus.Canceled);
                break;
            case FailureKind.Stopped:
                entry.Status = entry.BitsJob is not null ? RecordingStatus.Downloading : RecordingStatus.Queued;
                store.Update(entry);
                break;
            case FailureKind.DiskFull:
                entry.Status = RecordingStatus.Queued;
                store.Update(entry);
                Requeue(entry.Key);
                if (State != EngineState.PausedDiskFull)
                {
                    log.Warn($"Insufficient disk space | Downloads paused | {e.Message}");
                    Pause("Insufficient disk space. Free space, then resume.", EngineState.PausedDiskFull);
                }
                break;
            default:
                entry.Status = RecordingStatus.Queued;
                store.Update(entry);
                Requeue(entry.Key);
                break;
        }
    }

    private TransferException Classify(Exception e, ActiveItem item) => e switch
    {
        TransferException t => t,
        OperationCanceledException => item.Reason switch
        {
            CancelReason.Pause => new TransferException(FailureKind.Paused, "Paused"),
            CancelReason.Cancel when stopReason == CancelReason.Cancel => new TransferException(FailureKind.Canceled, "Run canceled"),
            CancelReason.Cancel => new TransferException(FailureKind.Canceled, "Canceled"),
            _ => new TransferException(FailureKind.Stopped, "Stopped"),
        },
        UnsafeUrlException u => new TransferException(FailureKind.Permanent, "Rejected unsafe URL: " + u.Message),
        ReauthenticationRequiredException r => new TransferException(FailureKind.AuthExpired, r.Message),
        IOException io when TransferException.IsDiskFull(io) => new TransferException(FailureKind.DiskFull, "Insufficient disk space", inner: io),
        IOException io => new TransferException(FailureKind.Transient, "File system error: " + io.Message, inner: io),
        UnauthorizedAccessException ua => new TransferException(FailureKind.Permanent, "Access denied: " + ua.Message, inner: ua),
        System.Runtime.InteropServices.COMException com => new TransferException(FailureKind.Transient, $"BITS error 0x{com.HResult:X8}", inner: com),
        _ => new TransferException(FailureKind.Transient, e.Message, inner: e),
    };

    private void EnsureDiskSpace(RecordingEntry entry, string destination)
    {
        var needed = (entry.ResumeTotal ?? entry.RemoteSize ?? 0) + options.DiskReserveBytes;
        var available = disk.AvailableBytes(destination);
        if (available < needed)
            throw new TransferException(FailureKind.DiskFull, $"Insufficient disk space: {Format.Size(available)} free, {Format.Size(needed)} needed including reserve");
    }

    private TimeSpan RetryDelay(int attempt, TimeSpan? retryAfter)
    {
        TimeSpan delay;
        if (retryAfter is { } after) delay = after > options.MaxRetryAfter ? options.MaxRetryAfter : after;
        else
        {
            var baseDelay = options.RetryDelays[Math.Min(attempt - 1, options.RetryDelays.Count - 1)];
            double jitter;
            lock (random) jitter = 0.8 + random.NextDouble() * 0.4;
            delay = baseDelay * jitter;
        }
        return delay * options.DelayScale;
    }

    private async Task<bool> DelayAsync(ActiveItem item, TimeSpan delay)
    {
        if (delay <= TimeSpan.Zero) return !item.Cancel.IsCancellationRequested;
        using var wait = CancellationTokenSource.CreateLinkedTokenSource(item.Cancel.Token);
        item.Status = "Waiting to retry";
        item.RetryWait = wait;
        try
        {
            await Task.Delay(delay, wait.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (!item.Cancel.IsCancellationRequested)
        {
        }
        catch (OperationCanceledException)
        {
            return false;
        }
        finally
        {
            item.RetryWait = null;
        }
        return true;
    }

    private async Task WaitForNetworkAsync(ActiveItem item, RunLogger log)
    {
        var previous = State;
        lock (gate)
        {
            if (state != EngineState.WaitingForNetwork) log.Warn("Network connection lost | Waiting for Windows");
        }
        SetState(EngineState.WaitingForNetwork);
        item.Status = "Waiting for network";
        while (!options.NetworkAvailable())
            await Task.Delay(options.NetworkPollInterval, item.Cancel.Token).ConfigureAwait(false);
        log.Info("Network connection restored");
        SetState(previous == EngineState.WaitingForNetwork ? EngineState.Running : previous);
    }

    private void Requeue(string key)
    {
        lock (gate) queue.AddFirst(key);
    }

    private void RaiseSessionExpired(string message, RunLogger log)
    {
        bool raise;
        lock (gate)
        {
            raise = !sessionExpiredRaised;
            sessionExpiredRaised = true;
        }
        if (!raise) return;
        log.Warn("Visory sign-in required | Downloads paused until you sign in again");
        Pause("Sign in to Visory again to continue.", EngineState.PausedSignInRequired);
        SessionExpired?.Invoke(message);
    }

    private static void DeleteOwnPart(string path)
    {
        try { if (File.Exists(path)) File.Delete(path); }
        catch (IOException) { }
    }

    private void SetState(EngineState next)
    {
        lock (gate)
        {
            if (state == next) return;
            if (state == EngineState.Stopping && next != EngineState.Idle) return;
            state = next;
        }
        StateChanged?.Invoke(next);
    }

    private static TaskCompletionSource NewSignal(bool open)
    {
        var tcs = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        if (open) tcs.SetResult();
        return tcs;
    }

    public void Dispose()
    {
        stop.Dispose();
        http.Dispose();
    }
}
