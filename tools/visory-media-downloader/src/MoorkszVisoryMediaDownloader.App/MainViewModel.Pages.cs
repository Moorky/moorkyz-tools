using System.Collections;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Text.Json;
using System.Windows.Data;
using Microsoft.Win32;
using MoorkszVisoryMediaDownloader.Core;
using MoorkszVisoryMediaDownloader.Core.Archive;
using MoorkszVisoryMediaDownloader.Core.Download;
using MoorkszVisoryMediaDownloader.Core.Infrastructure;
using MoorkszVisoryMediaDownloader.Core.Reporting;

namespace MoorkszVisoryMediaDownloader.App;

public sealed partial class MainViewModel
{
    private readonly List<RecordingRow> recordingRows = [];
    private string searchText = "";
    private Choice<RecordingStatus?>? statusFilter;
    private string? cameraFilter;
    private RecordingRow? selectedRecording;
    private ArchiveRow? selectedArchive;
    private Choice<ArchiveScope>? archiveScopeChoice;
    private double archiveProgressPercent;
    private string archiveProgressText = "";
    private RunRow? selectedRun;
    private Choice<string>? logFilter;
    private string runSummaryText = "";
    private readonly List<LogLineRow> runLogLines = [];
    private string verifyProgressText = "";

    public ICollectionView RecordingsView { get; private set; } = null!;
    public ObservableCollection<Choice<RecordingStatus?>> StatusFilters { get; } = [];
    public ObservableCollection<string> CameraFilters { get; } = [];
    public ObservableCollection<TransferRow> Transfers { get; } = [];
    public ObservableCollection<ArchiveRow> Archives { get; } = [];
    public ObservableCollection<ArchiveEntryRow> ArchiveEntries { get; } = [];
    public ObservableCollection<Choice<ArchiveScope>> ArchiveScopes { get; } = [];
    public ObservableCollection<RunRow> Runs { get; } = [];
    public ObservableCollection<LogLineRow> LogLines { get; } = [];
    public ObservableCollection<FailedRow> FailedRows { get; } = [];
    public ObservableCollection<Choice<string>> LogFilters { get; } = [];

    public AsyncCommand BackupSelectedCommand { get; private set; } = null!;
    public AsyncCommand VerifySelectedCommand { get; private set; } = null!;
    public AsyncCommand VerifyAllCommand { get; private set; } = null!;
    public RelayCommand CancelItemCommand { get; private set; } = null!;
    public RelayCommand RetryNowCommand { get; private set; } = null!;
    public AsyncCommand CreateArchiveCommand { get; private set; } = null!;
    public AsyncCommand VerifyArchiveCommand { get; private set; } = null!;
    public RelayCommand BrowseArchiveCommand { get; private set; } = null!;
    public RelayCommand OpenArchiveFolderCommand { get; private set; } = null!;
    public AsyncCommand RetryFailedCommand { get; private set; } = null!;
    public RelayCommand ExportCsvCommand { get; private set; } = null!;
    public RelayCommand ExportJsonCommand { get; private set; } = null!;
    public RelayCommand OpenReportFolderCommand { get; private set; } = null!;

    public string SearchText { get => searchText; set { if (Set(ref searchText, value)) RecordingsView.Refresh(); } }
    public Choice<RecordingStatus?>? StatusFilter { get => statusFilter; set { if (Set(ref statusFilter, value)) RecordingsView.Refresh(); } }
    public string? CameraFilter { get => cameraFilter; set { if (Set(ref cameraFilter, value)) RecordingsView.Refresh(); } }
    public RecordingRow? SelectedRecording { get => selectedRecording; set { Set(ref selectedRecording, value); Raise(nameof(HasSelectedRecording)); } }
    public bool HasSelectedRecording => selectedRecording is not null;
    public string VerifyProgressText { get => verifyProgressText; private set => Set(ref verifyProgressText, value); }
    public bool HasTransfers => Transfers.Count > 0;
    public ArchiveRow? SelectedArchive { get => selectedArchive; set => Set(ref selectedArchive, value); }
    public Choice<ArchiveScope>? ArchiveScopeChoice { get => archiveScopeChoice; set => Set(ref archiveScopeChoice, value); }
    public double ArchiveProgressPercent { get => archiveProgressPercent; private set => Set(ref archiveProgressPercent, value); }
    public string ArchiveProgressText { get => archiveProgressText; private set { Set(ref archiveProgressText, value); Raise(nameof(IsArchiving)); } }
    public bool IsArchiving => archiveProgressText.Length > 0;
    public RunRow? SelectedRun { get => selectedRun; set { if (Set(ref selectedRun, value)) LoadRun(); } }
    public string? SelectedRunId => selectedRun?.Id;
    public bool HasSelectedRun => selectedRun is not null;
    public Choice<string>? LogFilter { get => logFilter; set { if (Set(ref logFilter, value)) ApplyLogFilter(); } }
    public string RunSummaryText { get => runSummaryText; private set => Set(ref runSummaryText, value); }

    private void InitializePages()
    {
        RecordingsView = CollectionViewSource.GetDefaultView(new List<RecordingRow>());
        BackupSelectedCommand = new AsyncCommand(p => StartBackupAsync(SelectedKeys(p), null), p => !isRunning && SelectedKeys(p).Count > 0);
        VerifySelectedCommand = new AsyncCommand(p => VerifyAsync(SelectedKeys(p)), p => !isRunning && SelectedKeys(p).Count > 0);
        VerifyAllCommand = new AsyncCommand(() => VerifyAsync(null), () => !isRunning && coordinator is not null);
        CancelItemCommand = new RelayCommand(p => { if (p is string key) coordinator?.Engine?.CancelItem(key); });
        RetryNowCommand = new RelayCommand(p => { if (p is string key) coordinator?.Engine?.RetryNow(key); });
        CreateArchiveCommand = new AsyncCommand(CreateArchiveAsync, () => !isRunning && !IsArchiving && coordinator is not null);
        VerifyArchiveCommand = new AsyncCommand(VerifyArchiveAsync, () => selectedArchive is not null && !IsArchiving);
        BrowseArchiveCommand = new RelayCommand(BrowseArchive, () => selectedArchive is not null);
        OpenArchiveFolderCommand = new RelayCommand(() => Ui.OpenFolder(selectedArchive?.Record.Path ?? coordinator?.Layout.Archives ?? Locations.AppRoot));
        RetryFailedCommand = new AsyncCommand(RetryFailedAsync, () => !isRunning && FailedRows.Count > 0);
        ExportCsvCommand = new RelayCommand(() => Export("csv"), () => selectedRun is not null);
        ExportJsonCommand = new RelayCommand(() => Export("json"), () => selectedRun is not null);
        OpenReportFolderCommand = new RelayCommand(() => { if (coordinator is not null) Ui.OpenFolder(selectedRun is null ? coordinator.Layout.Runs : coordinator.Layout.RunFolder(selectedRun.Id)); });
        BuildChoiceLists();
    }

    private void BuildChoiceLists()
    {
        var status = statusFilter?.Value;
        StatusFilters.ReplaceAll(new[] { new Choice<RecordingStatus?>(null, "All") }
            .Concat(Enum.GetValues<RecordingStatus>().Select(s => new Choice<RecordingStatus?>(s, "RecStatus" + s))));
        statusFilter = StatusFilters.First(c => c.Value == status);
        Raise(nameof(StatusFilter));
        var scope = archiveScopeChoice?.Value ?? Settings.ArchiveScope;
        ArchiveScopes.ReplaceAll([new Choice<ArchiveScope>(ArchiveScope.AllSelected, "ScopeAll"), new Choice<ArchiveScope>(ArchiveScope.ThisRunOnly, "ScopeRun")]);
        archiveScopeChoice = ArchiveScopes.First(c => c.Value == scope);
        Raise(nameof(ArchiveScopeChoice));
        var log = logFilter?.Value ?? "all";
        LogFilters.ReplaceAll([new("all", "FilterAll"), new("success", "FilterSuccess"), new("fail", "FilterFail"), new("warn", "FilterWarn")]);
        logFilter = LogFilters.First(c => c.Value == log);
        Raise(nameof(LogFilter));
    }

    private void OnPageChanged()
    {
        if (selectedPage == 1) ReloadRecordings();
        else if (selectedPage == 3) ReloadArchives();
        else if (selectedPage == 4) ReloadRuns();
    }

    private void ReloadAll()
    {
        BuildChoiceLists();
        ReloadRecordings();
        ReloadArchives();
        ReloadRuns();
        LoadSettingsEditor();
    }

    private void ReloadRecordings()
    {
        var selectedKey = selectedRecording?.Key;
        recordingRows.Clear();
        if (coordinator is not null) recordingRows.AddRange(coordinator.Store.All(coordinator.Provider.ProviderId).Select(e => new RecordingRow(e)));
        RecordingsView = CollectionViewSource.GetDefaultView(recordingRows.ToList());
        RecordingsView.Filter = FilterRecording;
        Raise(nameof(RecordingsView));
        var camera = cameraFilter;
        CameraFilters.ReplaceAll(new[] { Loc.T("All") }.Concat(recordingRows.Select(r => r.Camera).Distinct(StringComparer.OrdinalIgnoreCase).Order(StringComparer.OrdinalIgnoreCase)));
        cameraFilter = camera is not null && CameraFilters.Contains(camera) ? camera : CameraFilters[0];
        Raise(nameof(CameraFilter));
        SelectedRecording = selectedKey is null ? null : recordingRows.FirstOrDefault(r => r.Key == selectedKey);
    }

    private bool FilterRecording(object item)
    {
        if (item is not RecordingRow row) return false;
        if (statusFilter?.Value is { } status && row.StatusValue != status) return false;
        if (cameraFilter is not null && cameraFilter != CameraFilters.FirstOrDefault() && !string.Equals(row.Camera, cameraFilter, StringComparison.OrdinalIgnoreCase)) return false;
        return searchText.Length == 0 || row.SearchText.Contains(searchText.Trim().ToLowerInvariant(), StringComparison.Ordinal);
    }

    private static List<string> SelectedKeys(object? parameter) =>
        parameter is IList list ? list.OfType<RecordingRow>().Select(r => r.Key).ToList() : [];

    private async Task VerifyAsync(IReadOnlyList<string>? keys)
    {
        if (coordinator is null) return;
        var entries = keys is null ? coordinator.Store.All(coordinator.Provider.ProviderId) : keys.Select(coordinator.Store.Get).OfType<RecordingEntry>().ToList();
        var progress = new Progress<(int Done, int Total)>(p => VerifyProgressText = $"{Loc.T("VerifyAll")}: {p.Done}/{p.Total}");
        var report = await coordinator.VerifyExistingAsync(entries, IntegrityPolicy.Sha256, progress, CancellationToken.None);
        VerifyProgressText = $"{Loc.T("RecStatusVerified")}: {report.Verified} | {Loc.T("Missing")}: {report.Missing} | {Loc.T("Corrupt")}: {report.Corrupt}";
        RefreshInventory();
        ReloadRecordings();
    }

    private void UpdateTransfers(IReadOnlyList<TransferSnapshot> snapshots)
    {
        var keys = snapshots.Select(s => s.Key).ToHashSet(StringComparer.Ordinal);
        for (var i = Transfers.Count - 1; i >= 0; i--)
            if (!keys.Contains(Transfers[i].Key)) Transfers.RemoveAt(i);
        for (var index = 0; index < snapshots.Count; index++)
        {
            var snapshot = snapshots[index];
            var row = Transfers.FirstOrDefault(t => t.Key == snapshot.Key);
            if (row is null)
            {
                row = new TransferRow { Key = snapshot.Key };
                Transfers.Insert(Math.Min(index, Transfers.Count), row);
            }
            row.Apply(snapshot, DisplayName(snapshot.Key));
        }
        Raise(nameof(HasTransfers));
    }

    private void ReloadArchives()
    {
        if (coordinator is null) { Archives.Clear(); return; }
        var id = selectedArchive?.Record.Id;
        Archives.ReplaceAll(coordinator.Store.Archives().Select(a => new ArchiveRow(a)));
        SelectedArchive = Archives.FirstOrDefault(a => a.Record.Id == id);
    }

    private async Task CreateArchiveAsync()
    {
        if (coordinator is null) return;
        var progress = new Progress<ArchiveProgress>(p =>
        {
            ArchiveProgressPercent = p.TotalBytes > 0 ? p.Bytes * 100.0 / p.TotalBytes : 0;
            ArchiveProgressText = $"{p.Phase}: {p.Files}/{p.TotalFiles} | {Format.Size(p.Bytes)} / {Format.Size(p.TotalBytes)}";
        });
        ArchiveProgressText = Loc.T("StateArchiving");
        try
        {
            var scope = archiveScopeChoice?.Value ?? ArchiveScope.AllSelected;
            var runId = scope == ArchiveScope.ThisRunOnly ? coordinator.Store.Runs(1).FirstOrDefault()?.Id : null;
            var active = coordinator;
            var result = await Task.Run(() => active.CreateArchiveAsync(scope, runId, progress, CancellationToken.None));
            if (result.Status is "verified" or "reused" or "skipped") ShowInfo(result.Message + (result.Path is null ? "" : "\n" + result.Path));
            else ShowError(result.Message, null);
        }
        finally
        {
            ArchiveProgressText = "";
            ReloadArchives();
        }
    }

    private async Task VerifyArchiveAsync()
    {
        if (coordinator is null || selectedArchive is null) return;
        var progress = new Progress<ArchiveProgress>(p =>
        {
            ArchiveProgressPercent = p.TotalBytes > 0 ? p.Bytes * 100.0 / p.TotalBytes : 0;
            ArchiveProgressText = $"{p.Phase}: {Format.Size(p.Bytes)} / {Format.Size(p.TotalBytes)}";
        });
        ArchiveProgressText = Loc.T("VerifyArchive");
        try
        {
            var active = coordinator;
            var record = selectedArchive.Record;
            var result = await Task.Run(() => active.Archives.VerifyRecordedAsync(record, progress, CancellationToken.None));
            if (result.Ok) ShowInfo($"{Loc.T("VerifyArchive")}: OK ({result.EntriesChecked} {Loc.T("ColEntries")})");
            else ShowError($"{Loc.T("VerifyArchive")}: {string.Join("; ", result.Problems.Take(4))}", null);
        }
        finally
        {
            ArchiveProgressText = "";
            ReloadArchives();
        }
    }

    private void BrowseArchive()
    {
        if (selectedArchive is null) return;
        try
        {
            ArchiveEntries.ReplaceAll(ArchiveService.List(selectedArchive.Record.Path).Select(e => new ArchiveEntryRow(e)));
        }
        catch (Exception e) when (e is IOException or InvalidDataException or UnauthorizedAccessException)
        {
            ShowError(e.Message, e);
        }
    }

    private void ReloadRuns()
    {
        if (coordinator is null) { Runs.Clear(); return; }
        var id = selectedRun?.Id;
        Runs.ReplaceAll(coordinator.Store.Runs().Select(r => new RunRow(r)));
        selectedRun = Runs.FirstOrDefault(r => r.Id == id) ?? Runs.FirstOrDefault();
        Raise(nameof(SelectedRun));
        LoadRun();
    }

    private void LoadRun()
    {
        Raise(nameof(HasSelectedRun));
        runLogLines.Clear();
        FailedRows.Clear();
        RunSummaryText = "";
        if (coordinator is null || selectedRun is null) { ApplyLogFilter(); return; }
        var layout = coordinator.Layout;
        var id = selectedRun.Id;
        RunSummaryText = selectedRun.Record.Summary is { } summary ? SummaryText(summary) : Loc.T("Run" + selectedRun.Record.State);
        foreach (var line in ReadShared(layout.LogPath(id)).Split('\n', StringSplitOptions.RemoveEmptyEntries).TakeLast(5000))
            runLogLines.Add(ParseLogLine(line.TrimEnd('\r')));
        var failed = Csv.Parse(ReadShared(layout.FailedReportPath(id)));
        if (failed.Count > 1)
        {
            var header = failed[0];
            int Col(string name) => header.IndexOf(name);
            foreach (var row in failed.Skip(1).Where(r => r.Count == header.Count))
                FailedRows.Add(new FailedRow(row[Col("timestamp")], row[Col("filename")], row[Col("filesize")] is { Length: > 0 } s && long.TryParse(s, out var b) ? Format.Size(b) : Loc.T("Unknown"),
                    row[Col("message")], row[Col("url")], row[Col("http_status")], row[Col("attempts")], row[Col("recording_id")]));
        }
        ApplyLogFilter();
    }

    private static string ReadShared(string path)
    {
        if (!File.Exists(path)) return "";
        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }

    private static LogLineRow ParseLogLine(string line)
    {
        var match = System.Text.RegularExpressions.Regex.Match(line, @"^\[(?<t>[^\]]+)\] \[(?<l>[A-Z]+)\] (?<x>.*)$");
        return match.Success ? new LogLineRow(match.Groups["t"].Value, match.Groups["l"].Value, match.Groups["x"].Value) : new LogLineRow("", "", line);
    }

    private void AppendLiveLog(LogEntry entry)
    {
        var row = new LogLineRow(Format.LogTime(entry.Time), entry.Level, entry.Text);
        runLogLines.Add(row);
        if (Matches(row)) LogLines.Add(row);
    }

    private bool Matches(LogLineRow row) => logFilter?.Value switch
    {
        "success" => row.Level is "SUCCESS" or "SKIP",
        "fail" => row.Level == "FAIL",
        "warn" => row.Level is "WARN" or "RETRY",
        _ => true,
    };

    private void ApplyLogFilter() => LogLines.ReplaceAll(runLogLines.Where(Matches));

    private async Task RetryFailedAsync()
    {
        if (coordinator is null) return;
        var keys = FailedRows.Select(f => f.RecordingId).Distinct()
            .Select(id => coordinator.Store.All(coordinator.Provider.ProviderId).FirstOrDefault(e => e.RecordingId == id)?.Key)
            .OfType<string>().ToList();
        SelectedPage = 0;
        await StartBackupAsync(keys, null);
    }

    private void Export(string format)
    {
        if (coordinator is null || selectedRun is null) return;
        var layout = coordinator.Layout;
        var id = selectedRun.Id;
        var dialog = new SaveFileDialog
        {
            FileName = format == "csv" ? $"log_report_{id}.csv" : $"run_{id}.json",
            Filter = format == "csv" ? "CSV (*.csv)|*.csv" : "JSON (*.json)|*.json",
            OverwritePrompt = true,
        };
        if (dialog.ShowDialog() != true) return;
        if (format == "csv")
        {
            File.WriteAllText(dialog.FileName, ReadShared(layout.CsvReportPath(id)), Csv.Utf8WithBom);
        }
        else
        {
            var export = new
            {
                summary = selectedRun.Record.Summary,
                failed = FailedRows,
                log = runLogLines,
            };
            File.WriteAllText(dialog.FileName, JsonSerializer.Serialize(export, JsonFiles.Options));
        }
        ShowInfo(Loc.T("ExportDone") + " " + dialog.FileName);
    }
}
