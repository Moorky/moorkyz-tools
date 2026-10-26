using System.Text.Json;
using System.Text.Json.Serialization;

namespace MoorkszVisoryMediaDownloader.Core.Infrastructure;

public sealed record AppSettings
{
    public string OutputDirectory { get; init; } = DefaultOutput();
    public ThemeChoice Theme { get; init; } = ThemeChoice.System;
    public string Language { get; init; } = "system";
    public bool ReduceMotion { get; init; }
    public PriorityMode Priority { get; init; } = PriorityMode.SmartBackground;
    public bool UseBits { get; init; } = true;
    public int MaxConcurrency { get; init; } = 2;
    public int BandwidthLimitKBps { get; init; }
    public int MaxAttempts { get; init; } = 4;
    public int TimeoutSeconds { get; init; } = 60;
    public IntegrityPolicy Integrity { get; init; } = IntegrityPolicy.Sha256;
    public NamingRule Naming { get; init; } = NamingRule.CameraAndTime;
    public bool AutoArchive { get; init; } = true;
    public ArchiveScope ArchiveScope { get; init; } = ArchiveScope.AllSelected;
    public bool StoreFullFailedUrls { get; init; }
    public bool MinimizeToTrayOnClose { get; init; } = true;
    public bool CloseBehaviorExplained { get; init; }
    public bool ScheduleEnabled { get; init; }
    public string ScheduleTime { get; init; } = "03:00";
    public bool RunAfterLogin { get; init; }
    public bool NotifyOnCompletion { get; init; } = true;
    public bool DiagnosticsEnabled { get; init; }
    public RecordingFilter Filter { get; init; } = new();

    public static string DefaultOutput() =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyVideos), "Visory Backup");

    public AppSettings Normalize() => this with
    {
        MaxConcurrency = Math.Clamp(MaxConcurrency, 1, 8),
        MaxAttempts = Math.Clamp(MaxAttempts, 1, 10),
        TimeoutSeconds = Math.Clamp(TimeoutSeconds, 10, 600),
        BandwidthLimitKBps = Math.Max(0, BandwidthLimitKBps),
        Language = Language is "en" or "de" ? Language : "system",
        ScheduleTime = TimeOnly.TryParse(ScheduleTime, Format.Invariant, out var t) ? t.ToString("HH:mm", Format.Invariant) : "03:00",
    };
}

public static class JsonFiles
{
    public static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new JsonStringEnumConverter() },
    };

    public static T? Read<T>(string path)
    {
        if (!File.Exists(path)) return default;
        using var stream = File.OpenRead(path);
        return JsonSerializer.Deserialize<T>(stream, Options);
    }

    /// <summary>Writes through a temporary file and an atomic replace so readers never observe partial JSON.</summary>
    public static void WriteAtomic<T>(string path, T value)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
        var temp = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        using (var stream = new FileStream(temp, FileMode.CreateNew, FileAccess.Write, FileShare.None))
        {
            JsonSerializer.Serialize(stream, value, Options);
            stream.Flush(flushToDisk: true);
        }
        File.Move(temp, path, overwrite: true);
    }
}

public sealed class SettingsStore(string path)
{
    public string Path { get; } = path;

    public AppSettings Load()
    {
        try
        {
            return (JsonFiles.Read<AppSettings>(Path) ?? new AppSettings()).Normalize();
        }
        catch (JsonException)
        {
            var preserved = Path + ".invalid-" + DateTime.Now.ToString("yyyyMMddHHmmss", Format.Invariant);
            File.Move(Path, preserved);
            return new AppSettings();
        }
    }

    public void Save(AppSettings settings) => JsonFiles.WriteAtomic(Path, settings.Normalize());
}

/// <summary>Where application state lives: portable beside the EXE when writable, otherwise %LOCALAPPDATA%.</summary>
public sealed record DataLocations(string AppRoot, bool Portable, string SessionRoot, string Reason)
{
    // Keep persisted settings, sessions and scheduled jobs compatible with earlier builds.
    public const string StorageIdentity = "VisoryBulkDownloader";
    public const string PortableFolderName = StorageIdentity + " Data";
    public string SettingsPath => System.IO.Path.Combine(AppRoot, "settings.json");
    public string AppLogPath => System.IO.Path.Combine(AppRoot, "app.log");
    public string DemoBackupRoot => System.IO.Path.Combine(AppRoot, "demo-backup");

    public static DataLocations Resolve(string executableDirectory, string? localAppData = null)
    {
        localAppData ??= Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        var userRoot = System.IO.Path.Combine(localAppData, StorageIdentity);
        var portableRoot = System.IO.Path.Combine(executableDirectory, PortableFolderName);
        string appRoot;
        bool portable;
        string reason;
        if (IsProtectedInstallLocation(executableDirectory))
        {
            appRoot = userRoot; portable = false; reason = "The EXE is in a protected program folder, so data is stored in your user profile.";
        }
        else if (CanWrite(executableDirectory))
        {
            appRoot = portableRoot; portable = true; reason = "Portable mode: settings are stored beside the EXE.";
        }
        else
        {
            appRoot = userRoot; portable = false; reason = "The EXE folder is not writable, so data is stored in your user profile.";
        }
        Directory.CreateDirectory(appRoot);
        // Sign-in sessions never travel with a portable folder: they stay DPAPI-protected in this Windows profile.
        var sessionRoot = System.IO.Path.Combine(userRoot, "Sessions", Hash(appRoot));
        return new DataLocations(appRoot, portable, sessionRoot, reason);
    }

    public static string Hash(string value)
    {
        var bytes = System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(value.ToUpperInvariant()));
        return Convert.ToHexString(bytes)[..16].ToLowerInvariant();
    }

    private static bool IsProtectedInstallLocation(string directory)
    {
        var full = System.IO.Path.GetFullPath(directory);
        foreach (var folder in new[] { Environment.SpecialFolder.ProgramFiles, Environment.SpecialFolder.ProgramFilesX86, Environment.SpecialFolder.Windows })
        {
            var root = Environment.GetFolderPath(folder);
            if (!string.IsNullOrEmpty(root) && full.StartsWith(root, StringComparison.OrdinalIgnoreCase)) return true;
        }
        return false;
    }

    private static bool CanWrite(string directory)
    {
        try
        {
            var probe = System.IO.Path.Combine(directory, ".vbd-write-" + Guid.NewGuid().ToString("N"));
            using (new FileStream(probe, FileMode.CreateNew, FileAccess.Write, FileShare.None, 1, FileOptions.DeleteOnClose)) { }
            return true;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            return false;
        }
    }
}

/// <summary>Folder structure under the user's chosen backup root.</summary>
public sealed class BackupLayout
{
    public BackupLayout(string root)
    {
        PathSafety.ValidateBackupRoot(root);
        Root = System.IO.Path.GetFullPath(root);
        Downloads = System.IO.Path.Combine(Root, "downloads");
        Archives = System.IO.Path.Combine(Root, "archives");
        Runs = System.IO.Path.Combine(Root, "runs");
        State = System.IO.Path.Combine(Root, "state");
    }

    public string Root { get; }
    public string Downloads { get; }
    public string Archives { get; }
    public string Runs { get; }
    public string State { get; }
    public string DatabasePath => System.IO.Path.Combine(State, "visory-backup.db");

    public void Ensure()
    {
        foreach (var dir in new[] { Root, Downloads, Archives, Runs, State })
        {
            Directory.CreateDirectory(dir);
            PathSafety.EnsureNoReparsePoints(System.IO.Path.GetDirectoryName(Root) ?? Root, dir);
        }
    }

    public string RunFolder(string runId) => System.IO.Path.Combine(Runs, PathSafety.SanitizeSegment(runId));
    public string LogFolder(string runId) => System.IO.Path.Combine(RunFolder(runId), "logs");
    public string FailedFolder(string runId) => System.IO.Path.Combine(RunFolder(runId), "failed");
    public string LogPath(string runId) => System.IO.Path.Combine(LogFolder(runId), $"log_{runId}.txt");
    public string CsvReportPath(string runId) => System.IO.Path.Combine(LogFolder(runId), $"log_report_{runId}.csv");
    public string DebugLogPath(string runId) => System.IO.Path.Combine(LogFolder(runId), $"debug_{runId}.log");
    public string SummaryPath(string runId) => System.IO.Path.Combine(LogFolder(runId), $"run_summary_{runId}.json");
    public string FailedReportPath(string runId) => System.IO.Path.Combine(FailedFolder(runId), $"failed_report_{runId}.csv");
    public string DestinationPath(string relative) => PathSafety.Combine(Downloads, relative);
}
