using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Windows;
using System.Windows.Data;
using System.Windows.Input;
using Microsoft.Win32;
using Moorkyz.Tools.MediaRecover.Core;
using Moorkyz.Tools.MediaRecover.Providers.Local;
using Moorkyz.Tools.MediaRecover.Providers.Wpd;
using Moorkyz.Tools.MediaRecover.Reporting;
using Moorkyz.Tools.MediaRecover.Storage;

namespace Moorkyz.Tools.MediaRecover.App;

public sealed class MainViewModel : INotifyPropertyChanged, IDisposable
{
    private readonly string stateDirectory;
    private readonly FileStream instanceLock;
    private readonly JsonSettingsStore settingsStore;
    private readonly SqliteManifestStore store;
    private readonly RecoveryCoordinator engine;
    private readonly WpdProvider wpd = new();
    private readonly ReportWriter reports = new();
    private CancellationTokenSource? operation;
    private Settings settings;
    private bool busy;
    private string status;
    private string search = "";
    private RunRecord? current;
    private string selectedLog = "";
    private DateTime lastProgress;
    private RunRecord? selectedRun;
    private int selectedTab;
    public MainViewModel(string stateDirectory)
    {
        this.stateDirectory = stateDirectory; PathSafety.NoLinks(stateDirectory); Directory.CreateDirectory(stateDirectory);
        instanceLock = new(Path.Combine(stateDirectory, "app.lock"), FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
        settingsStore = new(Path.Combine(stateDirectory, "settings.json")); settings = settingsStore.Load();
        store = new(Path.Combine(stateDirectory, "state.db"));
        engine = new(store, [new LocalFolderProvider(), wpd], new IntegrityService(), reports);
        engine.Progress += p =>
        {
            // Per-chunk UI dispatch is throttled; per-file/error records are never dropped from reports.
            if (p.Status == "Transferring" && DateTime.UtcNow - lastProgress < TimeSpan.FromMilliseconds(200)) return;
            lastProgress = DateTime.UtcNow;
            Application.Current.Dispatcher.Invoke(() =>
            {
                Status = p.Status + " · " + p.Message + (p.Speed > 0 ? $" | {p.Bytes:N0} bytes | {p.Speed / 1048576:F2} MiB/s" : "");
                if (p.Status != "Transferring") AddLog($"[{DateTimeOffset.Now:yyyy-MM-dd HH:mm:ss}] [{p.Status}] {p.Message}");
            });
        };
        foreach (var source in settings.Sources) Sources.Add(source);
        status = T["Ready"];
        LogsView = CollectionViewSource.GetDefaultView(Logs); LogsView.Filter = item => ((string)item).Contains(Search, StringComparison.CurrentCultureIgnoreCase);
        RefreshHistory();
        AddFolder = new RelayCommand(() => { var d = new OpenFolderDialog { Multiselect = true }; if (d.ShowDialog() == true) foreach (var path in d.FolderNames) { var spec = new SourceSpec("LocalFolder", path); if (!Sources.Contains(spec)) Sources.Add(spec); } Save(false); }, () => !Busy);
        RemoveSource = new RelayCommand(() => { if (SelectedSource is not null) Sources.Remove(SelectedSource); Save(false); }, () => !Busy);
        DetectDevices = new RelayCommand(() => Run(async () => { var devices = await wpd.DevicesAsync(); Devices.Clear(); foreach (var d in devices) Devices.Add(d); Status = devices.Count == 0 ? T["DeviceHelp"] : $"{devices.Count} WPD device(s)"; }));
        AddDevice = new RelayCommand(() => { if (SelectedDevice is not null) { var spec = new SourceSpec(wpd.Kind, SelectedDevice.Id); if (!Sources.Contains(spec)) Sources.Add(spec); Save(false); } }, () => !Busy);
        BrowseDestination = new RelayCommand(() => { var d = new OpenFolderDialog(); if (d.ShowDialog() == true) { Settings.Destination = d.FolderName; Changed(nameof(Settings)); Save(false); } }, () => !Busy);
        SaveSettings = new RelayCommand(() => Save(true), () => !Busy);
        ResetSettings = new RelayCommand(() => { Settings = new(); Sources.Clear(); Save(false); }, () => !Busy);
        Scan = new RelayCommand(() => Run(async () => { Save(false); Current = await Task.Run(() => engine.ScanAsync(Settings, operation!.Token)); RefreshHistory(); }), () => !Busy);
        Recover = new RelayCommand(() => RecoverRun(Current), () => !Busy && Current is not null);
        Pause = new RelayCommand(engine.Pause, () => Busy); Resume = new RelayCommand(engine.Resume, () => Busy);
        Stop = new RelayCommand(StopSafely, () => Busy);
        Retry = new RelayCommand(() => RecoverRun(ActiveRun), () => !Busy && ActiveRun is not null);
        OpenFolder = new RelayCommand(() => Open(ActiveRun?.Settings.Destination ?? Settings.Destination));
        CopyPath = new RelayCommand(() => Clipboard.SetText(ActiveRun?.Settings.Destination ?? Settings.Destination));
        OpenReports = new RelayCommand(() => { if (ActiveRun is { } run) Open(run.Reports); });
        ViewFailures = new RelayCommand(() => { var run = ActiveRun; if (run is not null) Open(Path.Combine(run.Reports, $"failed_report_{run.Id}.csv")); });
        OpenArchive = new RelayCommand(() => { var run = ActiveRun; if (run is not null && run.Archive.Length > 0) Open(run.Archive); });
        Verify = new RelayCommand(() => Run(async () => { var failed = await Task.Run(() => engine.VerifyAsync(operation!.Token)); Status = $"Local integrity checked. Changed/missing: {failed.Count}. Source consistency and decodability are not established."; }), () => !Busy);
        ImportLegacy = new RelayCommand(() =>
        {
            var d = new OpenFolderDialog();
            if (d.ShowDialog() != true) return;
            Run(async () => { var path = await Task.Run(() => new LegacyImporter(store, new IntegrityService()).ImportAsync(d.FolderName, Path.Combine(stateDirectory, "imports"), operation!.Token)); Status = "LEGACY_UNVERIFIED — receipt: " + path; });
        }, () => !Busy);
        Diagnostics = new RelayCommand(() =>
        {
            var d = new SaveFileDialog { FileName = "mediarecover-diagnostics.json", Filter = "JSON|*.json" };
            if (d.ShowDialog() == true)
            {
                var output = new
                {
                    Product = "Moorkyz Media Recover",
                    Version = "0.1.0",
                    Runtime = Environment.Version.ToString(),
                    OS = Environment.OSVersion.VersionString,
                    Paths = "[REDACTED]",
                    Sources = Sources.Select(s => s.Kind).ToArray(),
                    Settings.Language,
                    Settings.Theme,
                    Errors = store.Runs().Take(10).Select(r => new { r.Status, r.Partial, r.Inaccessible })
                };
                File.WriteAllText(d.FileName, JsonSerializer.Serialize(output, new JsonSerializerOptions { WriteIndented = true }));
                Status = "Redacted diagnostics: " + d.FileName;
            }
        });
        OpenState = new RelayCommand(() => Open(stateDirectory));
        Help = new RelayCommand(() => MessageBox.Show(T["About"], "Moorkyz Media Recover", MessageBoxButton.OK, MessageBoxImage.Information));
        DismissIntro = new RelayCommand(() => { Settings.IntroductionSeen = true; settingsStore.Save(Settings); Changed(nameof(ShowIntro)); });
    }
    public event PropertyChangedEventHandler? PropertyChanged;
    private void Changed([CallerMemberName] string? name = null) => PropertyChanged?.Invoke(this, new(name));
    public Settings Settings { get => settings; private set { settings = value; Changed(); Changed(nameof(T)); Changed(nameof(ShowIntro)); } }
    public Dictionary<string, string> T => Words.For(Settings.Language);
    public bool ShowIntro => !Settings.IntroductionSeen;
    public bool Busy { get => busy; private set { busy = value; Changed(); Changed(nameof(Idle)); CommandManager.InvalidateRequerySuggested(); } }
    public bool Idle => !Busy;
    public string Status { get => status; private set { status = value; Changed(); } }
    public string Summary { get; private set; } = "";
    public string Search { get => search; set { search = value; Changed(); LogsView.Refresh(); } }
    public string SelectedLog { get => selectedLog; set { selectedLog = value; Changed(); } }
    public RunRecord? Current { get => current; private set { current = value; Changed(); Changed(nameof(SavedPath)); Summary = value is null ? "" : $"Video: {value.Video} | Audio: {value.Audio} | Found: {value.Discovered} | Media examined: {value.Examined} | Unavailable subtrees: {value.Inaccessible}\n{Review(value)}"; Changed(nameof(Summary)); CommandManager.InvalidateRequerySuggested(); } }
    private RunRecord? ActiveRun => SelectedTab == 2 ? SelectedRun ?? Current : Current ?? SelectedRun;
    public string SavedPath => ActiveRun?.Settings.Destination ?? Settings.Destination;
    public ObservableCollection<SourceSpec> Sources { get; } = [];
    public ObservableCollection<DeviceInfo> Devices { get; } = [];
    public ObservableCollection<string> Logs { get; } = [];
    public ObservableCollection<RunRecord> History { get; } = [];
    public ICollectionView LogsView { get; }
    public SourceSpec? SelectedSource { get; set; }
    public DeviceInfo? SelectedDevice { get; set; }
    public RunRecord? SelectedRun { get => selectedRun; set { selectedRun = value; Changed(); Changed(nameof(SavedPath)); CommandManager.InvalidateRequerySuggested(); } }
    public int SelectedTab { get => selectedTab; set { selectedTab = value; Changed(); Changed(nameof(SavedPath)); CommandManager.InvalidateRequerySuggested(); } }
    public string[] Languages { get; } = ["en", "de"];
    public string[] Themes { get; } = ["Light", "Dark", "System"];
    public string[] Layouts { get; } = ["Flat", "Type", "Tree"];
    public ICommand AddFolder { get; }
    public ICommand RemoveSource { get; }
    public ICommand DetectDevices { get; }
    public ICommand AddDevice { get; }
    public ICommand BrowseDestination { get; }
    public ICommand SaveSettings { get; }
    public ICommand ResetSettings { get; }
    public ICommand Scan { get; }
    public ICommand Recover { get; }
    public ICommand Pause { get; }
    public ICommand Resume { get; }
    public ICommand Stop { get; }
    public ICommand OpenFolder { get; }
    public ICommand CopyPath { get; }
    public ICommand OpenReports { get; }
    public ICommand ViewFailures { get; }
    public ICommand OpenArchive { get; }
    public ICommand Verify { get; }
    public ICommand Retry { get; }
    public ICommand ImportLegacy { get; }
    public ICommand Diagnostics { get; }
    public ICommand OpenState { get; }
    public ICommand Help { get; }
    public ICommand DismissIntro { get; }
    public event Action? AppearanceChanged;
    private void Save(bool inform)
    {
        Settings.Sources = Sources.ToList(); settingsStore.Save(Settings); Changed(nameof(Settings)); Changed(nameof(T)); Changed(nameof(SavedPath)); AppearanceChanged?.Invoke();
        if (inform) Status = Settings.Language == "de" ? "Einstellungen gespeichert." : "Settings saved.";
    }
    private void RecoverRun(RunRecord? run)
    {
        if (run is null || MessageBox.Show(T["Confirm"] + "\n\n" + T["SavedTo"] + run.Settings.Destination + "\n" + string.Join("\n", run.Settings.Sources.Select(s => s.Kind + ": " + s.Id)), T["ConfirmTitle"], MessageBoxButton.YesNo, MessageBoxImage.Question, MessageBoxResult.No) != MessageBoxResult.Yes) return;
        Run(async () =>
        {
            Current = await Task.Run(() => engine.RecoverAsync(run, operation!.Token));
            if (Current.Settings.Zip && Current.Status.StartsWith("Completed", StringComparison.Ordinal))
            {
                try
                {
                    Status = "Creating and verifying ZIP (new copies + reports)…";
                    var archive = await Task.Run(() => reports.ArchiveAsync(Current, store, operation!.Token));
                    Current = Current with { Archive = archive }; store.SaveRun(Current);
                    await reports.WriteAsync(Current, store, operation!.Token);
                }
                catch (Exception e) when (e is not OperationCanceledException)
                {
                    store.AddIssue(Current.Id, new(Current.Settings.Destination, "ARCHIVE_FAILED", e.Message, "Individual saved files remain valid. Retry archive separately in a later run."));
                    Current = Current with { Status = "Completed with warnings — archive failed" }; store.SaveRun(Current);
                    await reports.WriteAsync(Current, store, CancellationToken.None);
                }
            }
            Status = Current.Status + " | " + T["SavedTo"] + Current.Settings.Destination + " | Reports: " + Current.Reports + (Current.Archive.Length > 0 ? " | ZIP: " + Current.Archive : "");
            Current = Current; RefreshHistory();
        });
    }
    private string Review(RunRecord run)
    {
        var counts = store.Inventory(run.Id).GroupBy(r => r.Status).Select(g => $"{g.Key}: {g.LongCount()}");
        var unknown = store.Inventory(run.Id).LongCount(r => r.Item.UnknownDate && r.Status != ItemStatus.Excluded);
        var conflicts = store.Inventory(run.Id).LongCount(r => r.Item.DateConflict);
        return string.Join(" | ", counts) + $" | Unknown dates included: {unknown} | Date conflicts: {conflicts}\nReports: {run.Reports}";
    }
    private void RefreshHistory() { History.Clear(); foreach (var run in store.Runs()) History.Add(run); }
    private void AddLog(string text) { Logs.Add(text); if (Logs.Count > 500) Logs.RemoveAt(0); }
    private async void Run(Func<Task> action)
    {
        if (Busy) return;
        Busy = true; operation = new();
        try { await action(); }
        catch (OperationCanceledException) { Status = "Interrupted safely. Completed files retained; resume the run from History."; }
        catch (Exception e) { var error = Errors.Describe(e); Status = error.Code + ": " + error.Message + " — " + error.Remedy; AddLog(Status); }
        finally { operation.Dispose(); operation = null; Busy = false; RefreshHistory(); }
    }
    public void StopSafely() { operation?.Cancel(); engine.Resume(); }
    private void Open(string path)
    {
        if (!File.Exists(path) && !Directory.Exists(path)) { Status = "Path does not exist yet: " + path; return; }
        Process.Start(new ProcessStartInfo(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "explorer.exe")) { ArgumentList = { Path.GetFullPath(path) }, UseShellExecute = false });
    }
    public void Dispose() { engine.Dispose(); store.Dispose(); instanceLock.Dispose(); }
}

public sealed class RelayCommand(Action execute, Func<bool>? canExecute = null) : ICommand
{
    public event EventHandler? CanExecuteChanged { add => CommandManager.RequerySuggested += value; remove => CommandManager.RequerySuggested -= value; }
    public bool CanExecute(object? parameter) => canExecute?.Invoke() ?? true;
    public void Execute(object? parameter)
    {
        try { execute(); }
        catch (Exception e) { MessageBox.Show(e.Message, "Moorkyz Media Recover — error", MessageBoxButton.OK, MessageBoxImage.Error); }
    }
}
