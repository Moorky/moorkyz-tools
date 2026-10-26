using System.Collections.ObjectModel;
using System.Globalization;
using Microsoft.Win32;
using VisoryBulkDownloader.Core;
using VisoryBulkDownloader.Core.Infrastructure;

namespace VisoryBulkDownloader.App;

/// <summary>Editable settings, applied only when the user saves.</summary>
public sealed partial class MainViewModel
{
    private string outputDirectory = "";
    private DateTime? startDate, endDate;
    private bool includeUnknownDates = true, newOrMissingOnly, useBits = true, autoArchive = true, minimizeToTray = true, scheduleEnabled, runAfterLogin, notify = true, reduceMotion, storeFullUrls, diagnostics;
    private string camerasText = "", typesText = "", minSizeText = "", maxSizeText = "", scheduleTime = "03:00", settingsMessage = "";
    private int concurrency = 2, bandwidth, attempts = 4, timeoutSeconds = 60;
    private Choice<NamingRule>? naming;
    private Choice<PriorityMode>? priority;
    private Choice<IntegrityPolicy>? integrity;
    private Choice<ArchiveScope>? settingsArchiveScope;
    private Choice<ThemeChoice>? theme;
    private Choice<string>? language;

    public ObservableCollection<Choice<NamingRule>> NamingChoices { get; } = [];
    public ObservableCollection<Choice<PriorityMode>> PriorityChoices { get; } = [];
    public ObservableCollection<Choice<IntegrityPolicy>> IntegrityChoices { get; } = [];
    public ObservableCollection<Choice<ThemeChoice>> ThemeChoices { get; } = [];
    public ObservableCollection<Choice<string>> LanguageChoices { get; } = [];

    public string OutputDirectory { get => outputDirectory; set => Set(ref outputDirectory, value); }
    public string DataLocationText => (Locations.Portable ? Loc.T("DataPortable") : Loc.T("DataProfile")) + "\n" + Locations.AppRoot;
    public string SessionLocationText => Locations.SessionRoot;
    public DateTime? StartDate { get => startDate; set => Set(ref startDate, value); }
    public DateTime? EndDate { get => endDate; set => Set(ref endDate, value); }
    public bool IncludeUnknownDates { get => includeUnknownDates; set => Set(ref includeUnknownDates, value); }
    public string CamerasText { get => camerasText; set => Set(ref camerasText, value); }
    public string TypesText { get => typesText; set => Set(ref typesText, value); }
    public string MinSizeText { get => minSizeText; set => Set(ref minSizeText, value); }
    public string MaxSizeText { get => maxSizeText; set => Set(ref maxSizeText, value); }
    public bool NewOrMissingOnly { get => newOrMissingOnly; set => Set(ref newOrMissingOnly, value); }
    public Choice<NamingRule>? Naming { get => naming; set => Set(ref naming, value); }
    public Choice<PriorityMode>? Priority { get => priority; set => Set(ref priority, value); }
    public bool UseBits { get => useBits; set => Set(ref useBits, value); }
    public int Concurrency { get => concurrency; set => Set(ref concurrency, value); }
    public int Bandwidth { get => bandwidth; set => Set(ref bandwidth, value); }
    public int Attempts { get => attempts; set => Set(ref attempts, value); }
    public int TimeoutSeconds { get => timeoutSeconds; set => Set(ref timeoutSeconds, value); }
    public Choice<IntegrityPolicy>? Integrity { get => integrity; set => Set(ref integrity, value); }
    public bool AutoArchive { get => autoArchive; set => Set(ref autoArchive, value); }
    public Choice<ArchiveScope>? SettingsArchiveScope { get => settingsArchiveScope; set => Set(ref settingsArchiveScope, value); }
    public bool MinimizeToTray { get => minimizeToTray; set => Set(ref minimizeToTray, value); }
    public bool ScheduleEnabled { get => scheduleEnabled; set => Set(ref scheduleEnabled, value); }
    public string ScheduleTime { get => scheduleTime; set => Set(ref scheduleTime, value); }
    public bool RunAfterLogin { get => runAfterLogin; set => Set(ref runAfterLogin, value); }
    public bool NotifyOnCompletion { get => notify; set => Set(ref notify, value); }
    public Choice<ThemeChoice>? Theme { get => theme; set => Set(ref theme, value); }
    public Choice<string>? Language { get => language; set => Set(ref language, value); }
    public bool ReduceMotion { get => reduceMotion; set => Set(ref reduceMotion, value); }
    public bool StoreFullUrls { get => storeFullUrls; set => Set(ref storeFullUrls, value); }
    public bool DiagnosticsEnabled { get => diagnostics; set => Set(ref diagnostics, value); }
    public string SettingsMessage { get => settingsMessage; private set => Set(ref settingsMessage, value); }

    private RelayCommand? browseOutputCommand;
    private AsyncCommand? saveSettingsCommand, signOutCommand, deleteSessionCommand;
    public RelayCommand BrowseOutputCommand => browseOutputCommand ??= new(BrowseOutput);
    public AsyncCommand SaveSettingsCommand => saveSettingsCommand ??= new(SaveSettingsAsync);
    public AsyncCommand SignOutCommand => signOutCommand ??= new(() => SignOutAsync(deleteLinks: false), () => !isRunning && !isScanning);
    public AsyncCommand DeleteSessionCommand => deleteSessionCommand ??= new(() => SignOutAsync(deleteLinks: true), () => !isRunning && !isScanning);

    private void LoadSettingsEditor()
    {
        var s = Settings;
        NamingChoices.ReplaceAll([new(NamingRule.CameraAndTime, "NamingCameraTime"), new(NamingRule.CameraFolders, "NamingFolders"), new(NamingRule.OriginalName, "NamingOriginal"), new(NamingRule.RecordingId, "NamingId")]);
        PriorityChoices.ReplaceAll([new(PriorityMode.SmartBackground, "PrioritySmart"), new(PriorityMode.Balanced, "PriorityBalanced"), new(PriorityMode.Fast, "PriorityFast")]);
        IntegrityChoices.ReplaceAll([new(IntegrityPolicy.SizeOnly, "IntegritySize"), new(IntegrityPolicy.Sha256, "IntegritySha")]);
        ThemeChoices.ReplaceAll([new(ThemeChoice.System, "ThemeSystem"), new(ThemeChoice.Light, "ThemeLight"), new(ThemeChoice.Dark, "ThemeDark")]);
        LanguageChoices.ReplaceAll([new("system", "LanguageSystem"), new Choice<string>("en", "LanguageEnglish"), new Choice<string>("de", "LanguageGerman")]);
        OutputDirectory = s.OutputDirectory;
        StartDate = s.Filter.StartDate;
        EndDate = s.Filter.EndDate;
        IncludeUnknownDates = s.Filter.IncludeUnknownDates;
        CamerasText = string.Join(", ", s.Filter.Cameras);
        TypesText = string.Join(", ", s.Filter.FileTypes);
        MinSizeText = s.Filter.MinSize is { } min ? (min / 1048576.0).ToString("0.##", Loc.Culture) : "";
        MaxSizeText = s.Filter.MaxSize is { } max ? (max / 1048576.0).ToString("0.##", Loc.Culture) : "";
        NewOrMissingOnly = s.Filter.NewOrMissingOnly;
        Naming = NamingChoices.First(c => c.Value == s.Naming);
        Priority = PriorityChoices.First(c => c.Value == s.Priority);
        UseBits = s.UseBits;
        Concurrency = s.MaxConcurrency;
        Bandwidth = s.BandwidthLimitKBps;
        Attempts = s.MaxAttempts;
        TimeoutSeconds = s.TimeoutSeconds;
        Integrity = IntegrityChoices.First(c => c.Value == s.Integrity);
        AutoArchive = s.AutoArchive;
        SettingsArchiveScope = ArchiveScopes.FirstOrDefault(c => c.Value == s.ArchiveScope) ?? new Choice<ArchiveScope>(s.ArchiveScope, "ScopeAll");
        MinimizeToTray = s.MinimizeToTrayOnClose;
        ScheduleEnabled = s.ScheduleEnabled;
        ScheduleTime = s.ScheduleTime;
        RunAfterLogin = s.RunAfterLogin;
        NotifyOnCompletion = s.NotifyOnCompletion;
        Theme = ThemeChoices.First(c => c.Value == s.Theme);
        Language = LanguageChoices.First(c => c.Value == s.Language);
        ReduceMotion = s.ReduceMotion;
        StoreFullUrls = s.StoreFullFailedUrls;
        DiagnosticsEnabled = s.DiagnosticsEnabled;
        Raise(nameof(DataLocationText));
    }

    private void BrowseOutput()
    {
        var dialog = new OpenFolderDialog { Title = Loc.T("OutputFolder"), InitialDirectory = Directory.Exists(OutputDirectory) ? OutputDirectory : Environment.GetFolderPath(Environment.SpecialFolder.MyVideos) };
        if (dialog.ShowDialog() == true) OutputDirectory = Path.Combine(dialog.FolderName, Path.GetFileName(dialog.FolderName) == "Visory Backup" ? "" : "Visory Backup").TrimEnd('\\');
    }

    private async Task SaveSettingsAsync()
    {
        try
        {
            PathSafety.ValidateBackupRoot(OutputDirectory);
        }
        catch (ArgumentException e)
        {
            SettingsMessage = e.Message;
            return;
        }
        var previous = Settings;
        var next = new AppSettings
        {
            OutputDirectory = Path.GetFullPath(OutputDirectory),
            Theme = theme?.Value ?? ThemeChoice.System,
            Language = language?.Value ?? "system",
            ReduceMotion = ReduceMotion,
            Priority = priority?.Value ?? PriorityMode.SmartBackground,
            UseBits = UseBits,
            MaxConcurrency = Concurrency,
            BandwidthLimitKBps = Bandwidth,
            MaxAttempts = Attempts,
            TimeoutSeconds = TimeoutSeconds,
            Integrity = integrity?.Value ?? IntegrityPolicy.Sha256,
            Naming = naming?.Value ?? NamingRule.CameraAndTime,
            AutoArchive = AutoArchive,
            ArchiveScope = settingsArchiveScope?.Value ?? ArchiveScope.AllSelected,
            StoreFullFailedUrls = StoreFullUrls,
            MinimizeToTrayOnClose = MinimizeToTray,
            CloseBehaviorExplained = previous.CloseBehaviorExplained,
            ScheduleEnabled = ScheduleEnabled,
            ScheduleTime = ScheduleTime,
            RunAfterLogin = RunAfterLogin,
            NotifyOnCompletion = NotifyOnCompletion,
            DiagnosticsEnabled = DiagnosticsEnabled,
            Filter = new RecordingFilter
            {
                StartDate = StartDate,
                EndDate = EndDate,
                IncludeUnknownDates = IncludeUnknownDates,
                Cameras = Split(CamerasText),
                FileTypes = Split(TypesText).Select(t => t.TrimStart('.').ToUpperInvariant()).ToList(),
                MinSize = Megabytes(MinSizeText),
                MaxSize = Megabytes(MaxSizeText),
                NewOrMissingOnly = NewOrMissingOnly,
            },
        }.Normalize();
        settingsStore.Save(next);
        Settings = next;
        ThemeManager.ReduceMotion = next.ReduceMotion;
        ThemeManager.Apply(next.Theme);
        Loc.Instance.Apply(next.Language);
        var messages = new List<string> { Loc.T("SettingsSaved") };
        var executable = Environment.ProcessPath ?? Path.Combine(AppContext.BaseDirectory, "VisoryBulkDownloader.exe");
        if (next.ScheduleEnabled != previous.ScheduleEnabled || next.ScheduleTime != previous.ScheduleTime)
        {
            var (ok, message) = await Task.Run(() => Scheduling.ConfigureDaily(Locations.AppRoot, executable, next.ScheduleEnabled, next.ScheduleTime));
            if (!ok || next.ScheduleEnabled) messages.Add(message);
        }
        if (next.RunAfterLogin != previous.RunAfterLogin)
            messages.Add((await Task.Run(() => Scheduling.ConfigureLogon(Locations.AppRoot, executable, next.RunAfterLogin))).Message);
        if (coordinator is not null) coordinator.Settings = next;
        if (!string.Equals(next.OutputDirectory, previous.OutputDirectory, StringComparison.OrdinalIgnoreCase) && !isDemo)
        {
            if (isRunning) messages.Add("The new backup folder is used after the current run.");
            else await CreateCoordinatorAsync();
        }
        RefreshInventory();
        SettingsMessage = string.Join("\n", messages.Where(m => m.Length > 0));
    }

    public void MarkCloseExplained()
    {
        if (Settings.CloseBehaviorExplained) return;
        Settings = Settings with { CloseBehaviorExplained = true };
        settingsStore.Save(Settings);
    }

    private async Task SignOutAsync(bool deleteLinks)
    {
        var ok = await visorySession.SignOutAsync();
        if (deleteLinks && coordinator is not null)
        {
            foreach (var entry in coordinator.Store.All("visory").Where(e => e.LastUrl is not null))
            {
                entry.LastUrl = null;
                coordinator.Store.Update(entry);
            }
        }
        SetStatus("StateSignedOut", "Brush.TextSecondary");
        SettingsMessage = ok ? Loc.T(deleteLinks ? "DeleteSession" : "SignOut") + ": OK" : Loc.T("SignOutPending");
    }

    private static List<string> Split(string text) =>
        text.Split([',', ';'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).Distinct(StringComparer.OrdinalIgnoreCase).ToList();

    private static long? Megabytes(string text) =>
        double.TryParse(text, NumberStyles.Float, Loc.Culture, out var mb) && mb >= 0 ? (long)(mb * 1048576) : null;
}
