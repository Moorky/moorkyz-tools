using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Windows;
using System.Windows.Threading;
using Microsoft.Web.WebView2.Core;
using VisoryBulkDownloader.App.Visory;
using VisoryBulkDownloader.Core;
using VisoryBulkDownloader.Core.Archive;
using VisoryBulkDownloader.Core.Backup;
using VisoryBulkDownloader.Core.Download;
using VisoryBulkDownloader.Core.Infrastructure;
using VisoryBulkDownloader.Core.Providers;
using VisoryBulkDownloader.Core.Reporting;

namespace VisoryBulkDownloader.App;

public sealed partial class MainViewModel : ObservableObject, IDisposable
{
    private readonly SettingsStore settingsStore;
    private readonly AppLog appLog;
    private readonly VisorySession visorySession;
    private readonly VisoryRecordingProvider visoryProvider;
    private readonly DemoRecordingProvider demoProvider = new(count: 36);
    private readonly DispatcherTimer timer;
    private BackupCoordinator? coordinator;
    private Task<RunSummary>? runTask;
    private long runEstimatedBytes;
    private int selectedPage;
    private string statusKey = "StateIdle";
    private string statusBrush = "Brush.TextSecondary";
    private string? banner;
    private bool bannerIsError;
    private bool isRunning, isPaused, isScanning, isDemo;
    private InventorySummary? inventory;
    private double overallPercent, currentFilePercent;
    private string currentFile = "", speedText = "", bytesText = "", etaText = "", downloadedText = "0 B", scanProgressText = "";
    private int completedCount, failedCount, pendingCount;
    private string? interruptedRunId;
    private RunSummary? lastSummary;

    public MainViewModel(DataLocations locations, SettingsStore settingsStore, AppSettings settings, AppLog appLog)
    {
        Locations = locations;
        this.settingsStore = settingsStore;
        Settings = settings;
        this.appLog = appLog;
        visorySession = new VisorySession(locations.SessionRoot, Path.Combine(locations.AppRoot, "visory-routes.json"));
        visorySession.CompletePendingDeletion();
        visoryProvider = new VisoryRecordingProvider(visorySession, () => Application.Current.MainWindow, () => Settings.DiagnosticsEnabled);
        Pages =
        [
            new(0, "NavDashboard", "\uE80F"), new(1, "NavRecordings", "\uE714"), new(2, "NavDownloads", "\uE896"),
            new(3, "NavArchives", "\uE7B8"), new(4, "NavReports", "\uE9D9"), new(5, "NavSettings", "\uE713"),
        ];
        AsyncCommand.ErrorHandler = e => ShowError(e.Message, e);
        SignInScanCommand = new AsyncCommand(SignInScanAsync, () => !isScanning && coordinator is not null);
        DemoCommand = new AsyncCommand(ToggleDemoAsync, () => !isRunning && !isScanning);
        StartBackupCommand = new AsyncCommand(() => StartBackupAsync(null, null), () => !isRunning && !isScanning && coordinator is not null && (inventory?.Discovered ?? 0) > 0);
        ResumeRunCommand = new AsyncCommand(() => StartBackupAsync(null, interruptedRunId), () => !isRunning && !isScanning && interruptedRunId is not null);
        PauseResumeCommand = new RelayCommand(PauseResume, () => isRunning);
        StopCommand = new RelayCommand(() => coordinator?.Stop(handOffBits: false), () => isRunning);
        OpenDownloadsCommand = new RelayCommand(() => { if (coordinator is not null) Ui.OpenFolder(coordinator.Layout.Downloads); });
        OpenLatestReportCommand = new RelayCommand(OpenLatestReport);
        ViewReportsCommand = new RelayCommand(() => SelectedPage = 4);
        DismissBannerCommand = new RelayCommand(() => Banner = null);
        InitializePages();
        timer = new DispatcherTimer(DispatcherPriority.Background) { Interval = TimeSpan.FromMilliseconds(500) };
        timer.Tick += (_, _) => RefreshLive();
        Loc.Instance.PropertyChanged += (_, _) => RefreshLocalized();
    }

    public DataLocations Locations { get; }
    public AppSettings Settings { get; private set; }
    public ObservableCollection<NavPage> Pages { get; }
    public ObservableCollection<ActivityItem> Activity { get; } = [];
    public TrayIcon? Tray { get; set; }
    public Action? RequestShowHub { get; set; }

    public AsyncCommand SignInScanCommand { get; }
    public AsyncCommand DemoCommand { get; }
    public AsyncCommand StartBackupCommand { get; }
    public AsyncCommand ResumeRunCommand { get; }
    public RelayCommand PauseResumeCommand { get; }
    public RelayCommand StopCommand { get; }
    public RelayCommand OpenDownloadsCommand { get; }
    public RelayCommand OpenLatestReportCommand { get; }
    public RelayCommand ViewReportsCommand { get; }
    public RelayCommand DismissBannerCommand { get; }
    private RelayCommand? navigateCommand;
    public RelayCommand NavigateCommand => navigateCommand ??= new(p => { if (p is string s && int.TryParse(s, out var index)) SelectedPage = index; });

    public int SelectedPage { get => selectedPage; set { if (Set(ref selectedPage, value)) { Raise(nameof(PageTitle)); OnPageChanged(); } } }
    public string PageTitle => Pages.Count > selectedPage && selectedPage >= 0 ? Pages[selectedPage].Title : "";
    public string StatusText => Loc.T(statusKey);
    public string StatusBrush => statusBrush;
    public string? Banner { get => banner; set { Set(ref banner, value); Raise(nameof(HasBanner)); } }
    public bool HasBanner => banner is not null;
    public bool BannerIsError { get => bannerIsError; private set => Set(ref bannerIsError, value); }
    public bool IsRunning { get => isRunning; private set { Set(ref isRunning, value); Raise(nameof(IsIdle)); } }
    public bool IsIdle => !isRunning;
    public bool IsPaused { get => isPaused; private set { Set(ref isPaused, value); Raise(nameof(PauseResumeText)); } }
    public string PauseResumeText => Loc.T(isPaused ? "Resume" : "Pause");
    public bool IsScanning { get => isScanning; private set => Set(ref isScanning, value); }
    public bool IsDemo { get => isDemo; private set { Set(ref isDemo, value); Raise(nameof(DemoButtonText)); } }
    public string DemoButtonText => Loc.T(isDemo ? "ExitDemo" : "TryDemo");
    public bool HasInventory => (inventory?.Discovered ?? 0) > 0;
    public bool ShowWelcome => !HasInventory;
    public string ScanProgressText { get => scanProgressText; private set => Set(ref scanProgressText, value); }
    public bool HasInterruptedRun => interruptedRunId is not null;

    public string RecordingsLabel => Loc.T(inventory?.Completeness == DiscoveryCompleteness.Complete ? "Recordings" : "DiscoveredRecordings");
    public string RecordingsCount => (inventory?.Discovered ?? 0).ToString("N0", Loc.Culture);
    public int CamerasCount => inventory?.Cameras ?? 0;
    public int VerifiedCount => inventory?.Verified ?? 0;
    public int MissingCount => inventory?.Missing ?? 0;
    public int CorruptCount => inventory?.Corrupt ?? 0;
    public int NeedsRetryCount => inventory?.NeedsRetry ?? 0;
    public int SelectedCount => inventory?.Selected ?? 0;
    public int NeedDownloadCount => inventory?.ToDownload ?? 0;
    public string EstimatedSizeText => inventory is null ? "-" : Format.Size(inventory.KnownSelectedBytes) + (inventory.UnknownSizeSelected > 0 ? " + " + Loc.F("UnknownSizes", inventory.UnknownSizeSelected) : "");
    public string FreeSpaceText => inventory is null ? "-" : Format.Size(inventory.AvailableBytes);
    public string CompletenessText => inventory is null ? "-" : Loc.T("Completeness" + inventory.Completeness);
    public bool CompletenessWarning => inventory is not null && inventory.Completeness != DiscoveryCompleteness.Complete && inventory.Discovered > 0;
    public string InventoryNotes => inventory is null ? "" : string.Join(" ", inventory.Notes.Concat(inventory.Errors));
    public string LastScanText => inventory?.ScannedAt?.ToLocalTime().ToString("g", Loc.Culture) ?? Loc.T("Never");

    public int CompletedCount { get => completedCount; private set => Set(ref completedCount, value); }
    public int FailedCount { get => failedCount; private set => Set(ref failedCount, value); }
    public int PendingCount { get => pendingCount; private set => Set(ref pendingCount, value); }
    public string DownloadedText { get => downloadedText; private set => Set(ref downloadedText, value); }
    public double OverallPercent { get => overallPercent; private set => Set(ref overallPercent, value); }
    public string CurrentFile { get => currentFile; private set => Set(ref currentFile, value); }
    public double CurrentFilePercent { get => currentFilePercent; private set => Set(ref currentFilePercent, value); }
    public string SpeedText { get => speedText; private set => Set(ref speedText, value); }
    public string BytesText { get => bytesText; private set => Set(ref bytesText, value); }
    public string EtaText { get => etaText; private set => Set(ref etaText, value); }
    public string LastSummaryText => lastSummary is null ? "" : SummaryText(lastSummary);
    public bool HasLastSummary => lastSummary is not null;

    public async Task InitializeAsync()
    {
        await CreateCoordinatorAsync();
        timer.Start();
    }

    private async Task CreateCoordinatorAsync()
    {
        if (isRunning) return;
        coordinator?.Dispose();
        coordinator = null;
        try
        {
            var layout = new BackupLayout(isDemo ? Locations.DemoBackupRoot : Settings.OutputDirectory);
            IRecordingProvider provider = isDemo ? demoProvider : visoryProvider;
            var created = await Task.Run(() => new BackupCoordinator(layout, provider, Settings, new BitsClient()));
            created.Log += entry => Ui.OnUi(() => AddActivity(entry));
            created.SessionExpired += message => Ui.OnUi(() => OnSessionExpired(message));
            await created.ReconcileAsync();
            coordinator = created;
            interruptedRunId = created.LatestInterruptedRunId();
            Raise(nameof(HasInterruptedRun));
            RefreshInventory();
            ReloadAll();
            SetStatus(visorySession.HasSavedSession || isDemo ? "StateConnected" : "StateSignedOut", visorySession.HasSavedSession || isDemo ? "Brush.Success" : "Brush.TextSecondary");
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or ArgumentException)
        {
            ShowError($"{Loc.T("OutputFolder")}: {e.Message}", e);
        }
        CommandManager();
    }

    private async Task SignInScanAsync()
    {
        if (coordinator is null) return;
        if (!isDemo)
        {
            var availability = WebView2Availability.Evaluate(() => CoreWebView2Environment.GetAvailableBrowserVersionString());
            if (availability.Status != WebView2Status.Available)
            {
                var install = MessageBox.Show(Loc.T("WebView2Missing") + "\n\n" + availability.Message + "\n\n" + Loc.T("InstallWebView2") + "?", Loc.T("AppName"), MessageBoxButton.YesNo, MessageBoxImage.Warning);
                if (install == MessageBoxResult.Yes) Ui.OpenUrl(WebView2Availability.InstallUrl);
                return;
            }
        }
        IsScanning = true;
        var previous = (statusKey, statusBrush);
        SetStatus("StateScanning", "Brush.Accent");
        try
        {
            var progress = new Progress<DiscoveryProgress>(p => ScanProgressText = Loc.F("ScanFound", p.Recordings, p.Cameras));
            var summary = await coordinator.ScanAsync(progress, CancellationToken.None);
            inventory = summary;
            ScanProgressText = "";
            SetStatus("StateConnected", "Brush.Success");
            if (coordinator.Engine?.State == EngineState.PausedSignInRequired) coordinator.Resume();
            RefreshInventory();
            ReloadRecordings();
        }
        catch (OperationCanceledException)
        {
            ScanProgressText = "";
            SetStatus(previous.statusKey, previous.statusBrush);
            ShowInfo(Loc.T("ScanCanceled"));
        }
        finally
        {
            IsScanning = false;
            CommandManager();
        }
    }

    private async Task ToggleDemoAsync()
    {
        IsDemo = !isDemo;
        Activity.Clear();
        lastSummary = null;
        Raise(nameof(HasLastSummary));
        await CreateCoordinatorAsync();
        if (isDemo) ShowInfo(Loc.T("DemoBadge"));
        else Banner = null;
    }

    public async Task StartBackupAsync(IReadOnlyList<string>? keys, string? resumeRunId)
    {
        if (coordinator is null || isRunning) return;
        var active = coordinator;
        IsRunning = true;
        IsPaused = false;
        SetStatus("StateDownloading", "Brush.Accent");
        runEstimatedBytes = active.Inventory(Settings.Filter).KnownSelectedBytes;
        SetProcessPriority(Settings.Priority == PriorityMode.SmartBackground ? ProcessPriorityClass.BelowNormal : ProcessPriorityClass.Normal);
        var archiveProgress = new Progress<ArchiveProgress>(p =>
        {
            SetStatus("StateArchiving", "Brush.Accent");
            ArchiveProgressPercent = p.TotalBytes > 0 ? p.Bytes * 100.0 / p.TotalBytes : 0;
            ArchiveProgressText = $"{p.Phase}: {p.Files}/{p.TotalFiles} | {Format.Size(p.Bytes)} / {Format.Size(p.TotalBytes)}";
        });
        var request = new BackupRequest
        {
            Keys = keys,
            Filter = Settings.Filter,
            ResumeRunId = resumeRunId,
            CreateArchive = Settings.AutoArchive,
            ArchiveScope = Settings.ArchiveScope,
        };
        try
        {
            runTask = Task.Run(() => active.RunBackupAsync(request, archiveProgress, CancellationToken.None));
            var summary = await runTask;
            lastSummary = summary;
            Raise(nameof(LastSummaryText));
            Raise(nameof(HasLastSummary));
            Transfers.Clear();
            Raise(nameof(HasTransfers));
            CompletedCount = summary.Succeeded;
            FailedCount = summary.Failed;
            PendingCount = summary.PendingOrCanceled;
            DownloadedText = Format.Size(summary.BytesTransferred);
            OverallPercent = summary.Scheduled == 0 ? 100 : (summary.Succeeded + summary.Failed) * 100.0 / summary.Scheduled;
            CurrentFile = "";
            CurrentFilePercent = 0;
            SpeedText = BytesText = EtaText = "";
            var errors = summary.State is RunState.CompletedWithErrors or RunState.Canceled;
            SetStatus(summary.State switch
            {
                RunState.Completed => "StateFinished",
                RunState.Interrupted => "StatePaused",
                _ => "StateFinishedErrors",
            }, errors ? "Brush.Warning" : "Brush.Success");
            if (Settings.NotifyOnCompletion && summary.State != RunState.Interrupted)
                Tray?.Notify(Loc.T("BackupComplete"), $"{summary.Outcome}\n{summary.Succeeded + summary.AlreadyVerified}/{summary.Selected} | {Loc.T("Failed")}: {summary.Failed}", errors);
        }
        finally
        {
            runTask = null;
            IsRunning = false;
            IsPaused = false;
            ArchiveProgressText = "";
            SetProcessPriority(ProcessPriorityClass.Normal);
            if (!IsExiting && coordinator == active)
            {
                interruptedRunId = active.LatestInterruptedRunId();
                Raise(nameof(HasInterruptedRun));
                RefreshInventory();
                ReloadAll();
            }
            CommandManager();
        }
    }

    public async Task RunScheduledAsync(bool exitWhenDone)
    {
        if (isRunning || isScanning || coordinator is null || isDemo) return;
        appLog.Write("INFO", "Scheduled backup started");
        try
        {
            if (interruptedRunId is not null)
            {
                await StartBackupAsync(null, interruptedRunId);
            }
            else if (visorySession.HasSavedSession)
            {
                visoryProvider.Unattended = true;
                try
                {
                    inventory = await coordinator.ScanAsync(null, CancellationToken.None);
                }
                catch (Exception e) when (e is InvalidOperationException or OperationCanceledException)
                {
                    appLog.Write("WARN", "Scheduled scan unavailable: " + e.Message);
                }
                finally
                {
                    visoryProvider.Unattended = false;
                }
                RefreshInventory();
                await StartBackupAsync(null, null);
            }
            else
            {
                Tray?.Notify(Loc.T("AppName"), Loc.T("SessionExpired"), error: true);
            }
        }
        catch (Exception e)
        {
            appLog.Error(e, "Scheduled backup");
            Tray?.Notify(Loc.T("AppName"), e.Message, error: true);
        }
        if (exitWhenDone && Application.Current.MainWindow is { IsVisible: false }) await ExitAsync(confirm: false);
    }

    public async Task ResumeAfterLoginAsync()
    {
        if (interruptedRunId is not null && !isDemo) await StartBackupAsync(null, interruptedRunId);
    }

    private void PauseResume()
    {
        if (coordinator?.Engine is not { } engine) return;
        if (engine.State is EngineState.Paused or EngineState.PausedDiskFull or EngineState.PausedSignInRequired)
        {
            coordinator.Resume();
            IsPaused = false;
            SetStatus("StateDownloading", "Brush.Accent");
        }
        else
        {
            coordinator.Pause();
            IsPaused = true;
            SetStatus("StatePaused", "Brush.Warning");
        }
    }

    private void OnSessionExpired(string message)
    {
        IsPaused = true;
        SetStatus("StateSignInRequired", "Brush.Warning");
        ShowError(Loc.T("SessionExpired"), null);
        Tray?.Notify(Loc.T("AppName"), Loc.T("SessionExpired"), error: true);
        appLog.Write("WARN", message);
    }

    public bool IsExiting { get; private set; }

    /// <summary>Safe exit: compatible BITS jobs are handed to Windows; direct downloads stop and resume next time.</summary>
    public async Task<bool> ExitAsync(bool confirm)
    {
        if (IsExiting) return true;
        if (isRunning)
        {
            if (confirm && MessageBox.Show(Loc.T("ConfirmExit"), Loc.T("ConfirmExitTitle"), MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes) return false;
            IsExiting = true;
            coordinator?.Stop(handOffBits: true);
            if (runTask is { } task)
            {
                try { await task.WaitAsync(TimeSpan.FromSeconds(20)); }
                catch (Exception e) when (e is TimeoutException or OperationCanceledException or IOException) { appLog.Write("WARN", "Exit while run finalizing: " + e.Message); }
            }
        }
        IsExiting = true;
        appLog.Write("INFO", "Exit");
        Dispose();
        Tray?.Dispose();
        Application.Current.Shutdown();
        return true;
    }

    private void RefreshLive()
    {
        Tray?.Update();
        if (!isRunning || coordinator is null) return;
        var engine = coordinator.Engine;
        var log = coordinator.CurrentLog;
        if (engine is null || log is null) return;
        var stats = log.Stats;
        var snapshots = engine.Snapshot();
        var active = snapshots.Where(s => s.Status != "Queued").ToList();
        var scheduled = Math.Max(log.ScheduledTotal, stats.Succeeded + stats.Failed);
        CompletedCount = stats.Succeeded;
        FailedCount = stats.Failed;
        PendingCount = Math.Max(0, scheduled - stats.Succeeded - stats.Failed);
        var inFlight = active.Sum(s => s.Bytes);
        DownloadedText = Format.Size(stats.TransferredBytes + inFlight);
        var partial = active.Sum(s => s.Total is > 0 ? (double)s.Bytes / s.Total.Value : 0);
        OverallPercent = scheduled == 0 ? 100 : Math.Min(100, (stats.Succeeded + stats.Failed + partial) * 100.0 / scheduled);
        var current = active.OrderByDescending(s => s.Bytes).FirstOrDefault();
        CurrentFile = current is null ? "" : DisplayName(current.Key);
        CurrentFilePercent = current?.Total is > 0 ? current.Bytes * 100.0 / current.Total.Value : 0;
        var speed = active.Sum(s => s.BytesPerSecond);
        SpeedText = speed > 0 ? Format.Speed(speed) : "-";
        BytesText = current is null ? "" : $"{Format.Size(current.Bytes)} / {(current.Total is { } t ? Format.Size(t) : Loc.T("Unknown"))}";
        var remaining = Math.Max(0, runEstimatedBytes - stats.SuccessBytes - inFlight);
        EtaText = speed > 1024 && runEstimatedBytes > 0 ? Format.Duration(TimeSpan.FromSeconds(remaining / speed)) : "-";
        switch (engine.State)
        {
            case EngineState.WaitingForNetwork: SetStatus("StateWaitingNetwork", "Brush.Warning"); break;
            case EngineState.PausedDiskFull: SetStatus("StateDiskFull", "Brush.Danger"); IsPaused = true; break;
            case EngineState.PausedSignInRequired: SetStatus("StateSignInRequired", "Brush.Warning"); IsPaused = true; break;
            case EngineState.Paused: SetStatus("StatePaused", "Brush.Warning"); IsPaused = true; break;
            case EngineState.Running when statusKey != "StateArchiving": SetStatus("StateDownloading", "Brush.Accent"); IsPaused = false; break;
        }
        UpdateTransfers(snapshots);
    }

    private void RefreshInventory()
    {
        if (coordinator is not null) inventory = coordinator.Inventory(Settings.Filter);
        foreach (var name in new[] { nameof(RecordingsLabel), nameof(RecordingsCount), nameof(CamerasCount), nameof(VerifiedCount), nameof(MissingCount), nameof(CorruptCount),
            nameof(NeedsRetryCount), nameof(SelectedCount), nameof(NeedDownloadCount), nameof(EstimatedSizeText), nameof(FreeSpaceText), nameof(CompletenessText),
            nameof(CompletenessWarning), nameof(InventoryNotes), nameof(LastScanText), nameof(HasInventory), nameof(ShowWelcome) })
            Raise(name);
        if (!isRunning && coordinator?.Store.Runs(1).FirstOrDefault()?.Summary is { } summary)
        {
            lastSummary ??= summary;
            Raise(nameof(LastSummaryText));
            Raise(nameof(HasLastSummary));
        }
    }

    private void RefreshLocalized()
    {
        foreach (var name in new[] { nameof(StatusText), nameof(PauseResumeText), nameof(DemoButtonText), nameof(LastSummaryText), nameof(PageTitle), nameof(StatusBrush) }) Raise(name);
        RefreshInventory();
        var pages = Pages.ToList();
        Pages.Clear();
        foreach (var page in pages) Pages.Add(page);
        Raise(nameof(SelectedPage));
        ReloadAll();
    }

    private void AddActivity(LogEntry entry)
    {
        Activity.Insert(0, new ActivityItem(entry.Time.ToLocalTime().ToString("HH:mm:ss", Format.Invariant), entry.Level, entry.Text));
        while (Activity.Count > 200) Activity.RemoveAt(Activity.Count - 1);
        if (SelectedRunId is not null && SelectedRunId == coordinator?.CurrentLog?.RunId) AppendLiveLog(entry);
    }

    private void SetStatus(string key, string brush)
    {
        statusKey = key;
        statusBrush = brush;
        Raise(nameof(StatusText));
        Raise(nameof(StatusBrush));
    }

    public void ShowError(string message, Exception? e)
    {
        BannerIsError = true;
        Banner = message;
        if (e is not null) appLog.Error(e, message);
    }

    public void ShowInfo(string message)
    {
        BannerIsError = false;
        Banner = message;
    }

    private void OpenLatestReport()
    {
        var run = coordinator?.Store.Runs(1).FirstOrDefault();
        if (run is not null && coordinator is not null) Ui.OpenFile(coordinator.Layout.LogPath(run.Id));
    }

    private string DisplayName(string key) =>
        coordinator?.Store.Get(key) is { } entry ? entry.DisplayName : key;

    private static string SummaryText(RunSummary s) =>
        $"{s.RunId} | {Loc.T("Run" + s.State)}{(s.State == RunState.Completed && s.InventoryCompleteness != DiscoveryCompleteness.Complete ? " - " + Loc.T("ScanCompleteness") + ": " + Loc.T("CompletenessUncertain") : "")}\n" +
        $"{Loc.T("SelectedForBackup")}: {s.Selected} | {Loc.T("AlreadyVerified")}: {s.AlreadyVerified} | {Loc.T("Completed")}: {s.Succeeded} | {Loc.T("Failed")}: {s.Failed} | {Loc.T("Pending")}: {s.PendingOrCanceled}\n" +
        $"{Loc.T("Downloaded")}: {Format.Size(s.BytesTransferred)} | {Format.Duration(TimeSpan.FromSeconds(s.ElapsedSeconds))} | {s.CompletionPercent.ToString("0.#", Loc.Culture)} % | ZIP: {Loc.ArchiveStatus(s.ArchiveStatus)}";

    private static void SetProcessPriority(ProcessPriorityClass priority)
    {
        try { Process.GetCurrentProcess().PriorityClass = priority; }
        catch (Exception e) when (e is System.ComponentModel.Win32Exception or InvalidOperationException) { }
    }

    private static void CommandManager() => System.Windows.Input.CommandManager.InvalidateRequerySuggested();

    public void Dispose()
    {
        timer.Stop();
        coordinator?.Dispose();
        coordinator = null;
    }
}
