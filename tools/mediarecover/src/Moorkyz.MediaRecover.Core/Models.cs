using System.Text.Json;
using System.Text.RegularExpressions;

namespace Moorkyz.Tools.MediaRecover.Core;

public enum ItemStatus { Eligible, Excluded, Transferring, Verifying, Promoting, Verified, AlreadyVerified, Failed, Interrupted, LegacyUnverified, Quarantined }
public record SourceSpec(string Kind, string Id);
public record SourceCapabilities(bool StableIdentity, bool OffsetReads, bool ReadOnly);
public record DateValue(string Property, string Raw, DateTimeOffset? Value, string? Timezone);
public record SourceItem(string Key, SourceSpec Source, string ObjectId, string RelativePath, long? Size,
    string Version, List<DateValue> Dates, string MediaType, bool UnknownDate = false, bool DateConflict = false);
public record ScanIssue(string Path, string Code, string Message, string Remedy);
public record InventoryEntry(SourceItem? Item, ScanIssue? Issue);
public record DeviceInfo(string Id, string Name);
public record FileRecord(SourceItem Item, ItemStatus Status, string Target = "", string Staging = "",
    string Hash = "", long Bytes = 0, int Attempts = 0, string ErrorCode = "", string Error = "",
    string Remedy = "", string Verification = "", double Seconds = 0, string Quarantine = "",
    DateTimeOffset? VerifiedAt = null, bool Repaired = false);
public record RunRecord(string Id, DateTimeOffset Started, Settings Settings, string Status = "Scanned",
    bool Partial = false, long Discovered = 0, long Examined = 0, long Video = 0, long Audio = 0,
    long Inaccessible = 0, string Reports = "", string Archive = "", DateTimeOffset? Finished = null);
public record ProgressEvent(string Status, string Message, long Bytes = 0, double Speed = 0);

public sealed class Settings
{
    public List<SourceSpec> Sources { get; set; } = [];
    public string Destination { get; set; } = DefaultDestination();
    public string Language { get; set; } = "en";
    public string Theme { get; set; } = "Light";
    public bool Videos { get; set; } = true;
    public bool Audio { get; set; } = true;
    public bool AllTypes { get; set; }
    public bool IncludeHidden { get; set; }
    public string Extensions { get; set; } = VideoExtensions + " " + AudioExtensions;
    public string Exclusions { get; set; } = "";
    public bool DateFilter { get; set; } = true;
    public DateTime StartDate { get; set; } = new(2026, 10, 1);
    public DateTime? EndDate { get; set; }
    public bool IncludeUnknown { get; set; } = true;
    public string DatePolicy { get; set; } = "All filesystem/device dates; conflicts included";
    public bool HighAssurance { get; set; }
    public bool Retry { get; set; } = true;
    public string Layout { get; set; } = "Flat";
    public bool Zip { get; set; }
    public bool IntroductionSeen { get; set; }
    public long SpeedLimitBytes { get; set; }
    public const string VideoExtensions = ".mp4 .mkv .avi .mov .wmv .webm .m4v .flv .mpeg .mpg .3gp .3g2 .ts .mts .m2ts .vob";
    public const string AudioExtensions = ".mp3 .m4a .wav .aac .flac .ogg .oga .opus .wma .amr .3ga .aif .aiff .m4b .caf";
    public static string StateDirectory => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Moorkyz", "MediaRecover");
    private static string DefaultDestination()
    {
        var videos = Environment.GetFolderPath(Environment.SpecialFolder.MyVideos);
        var legacy = Path.Combine(videos, "S26-Backup");
        return Directory.Exists(legacy) ? legacy : Path.Combine(videos, "Moorkyz Media Recover");
    }
    public Settings Clone() => JsonSerializer.Deserialize<Settings>(JsonSerializer.Serialize(this))!;
    public void Validate()
    {
        if (!Path.IsPathFullyQualified(Destination)) throw new RecoveryException("INVALID_DESTINATION", "Choose an absolute destination.", "Use the destination folder picker.");
        if (Path.GetFullPath(Destination).TrimEnd('\\').Equals(Path.GetPathRoot(Destination)?.TrimEnd('\\'), StringComparison.OrdinalIgnoreCase))
            throw new RecoveryException("INVALID_DESTINATION", "A filesystem root cannot be the save folder.", "Choose a subfolder.");
        if (EndDate?.Date < StartDate.Date) throw new RecoveryException("INVALID_DATE", "End date precedes start date.", "Correct the date range.");
        if (SpeedLimitBytes < 0) throw new RecoveryException("INVALID_LIMIT", "Speed limit cannot be negative.", "Use zero for unlimited.");
        PathSafety.NoLinks(Destination);
        if (Sources.Count == 0) throw new RecoveryException("NO_SOURCE", "No source selected.", "Add a local folder or connected device.");
        foreach (var s in Sources.Where(s => s.Kind == "LocalFolder"))
        {
            PathSafety.NoLinks(s.Id);
            if (PathSafety.Within(s.Id, Destination)) throw new RecoveryException("SOURCE_IS_OUTPUT", "A source cannot be inside the destination.", "Choose a separate save folder (a destination inside a source is excluded safely).");
        }
    }
}

public sealed class RecoveryException(string code, string message, string remedy, bool transient = false, Exception? inner = null)
    : IOException(message, inner)
{
    public string Code { get; } = code;
    public string Remedy { get; } = remedy;
    public bool Transient { get; } = transient;
}

public static class Errors
{
    public static RecoveryException Describe(Exception error)
    {
        if (error is RecoveryException known) return known;
        if (error is TimeoutException) return new("SOURCE_TIMEOUT", "Source/driver did not respond within the managed timeout.", "Check USB cable, unlock the device and reconnect before retrying. A blocked OS driver call may finish later.", true, error);
        if (error is UnauthorizedAccessException) return new("ACCESS_DENIED", error.Message, "Choose an accessible folder; do not bypass permissions.");
        if (error is FileNotFoundException or DirectoryNotFoundException) return new("SOURCE_MISSING", error.Message, "Reconnect the source and scan again.", true);
        if ((error.HResult & 0xffff) is 112 or 39) return new("WAITING_FOR_SPACE", error.Message, "Free disk space or choose another destination.", false);
        if ((error.HResult & 0xffff) is 21 or 53 or 64 or 67) return new("STORAGE_UNAVAILABLE", error.Message, "Reconnect the selected drive/share and explicitly retry the run.", false);
        return new("IO_ERROR", error.Message, "Check cable, device unlock, File Transfer mode, drive access and file locks; retry.", error is IOException, error);
    }
}

public static class Filter
{
    public static string? Media(string path, Settings settings)
    {
        var extension = Path.GetExtension(path).ToLowerInvariant();
        if (settings.AllTypes) return VideoExtensions.Contains(extension) ? "Video" : AudioExtensions.Contains(extension) ? "Audio" : "Other";
        if (!settings.Extensions.Split([' ', ',', ';', '\r', '\n'], StringSplitOptions.RemoveEmptyEntries).Contains(extension, StringComparer.OrdinalIgnoreCase)) return null;
        if (VideoExtensions.Contains(extension)) return settings.Videos ? "Video" : null;
        if (AudioExtensions.Contains(extension)) return settings.Audio ? "Audio" : null;
        return "Other";
    }
    private static readonly HashSet<string> VideoExtensions = Settings.VideoExtensions.Split(' ').ToHashSet(StringComparer.OrdinalIgnoreCase);
    private static readonly HashSet<string> AudioExtensions = Settings.AudioExtensions.Split(' ').ToHashSet(StringComparer.OrdinalIgnoreCase);
    public static bool Excluded(string path, Settings settings) => settings.Exclusions.Split([';', '\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
        .Any(pattern => Regex.IsMatch(path.Replace('\\', '/'), "^" + Regex.Escape(pattern.Replace('\\', '/')).Replace("\\*", ".*").Replace("\\?", ".") + "$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, TimeSpan.FromSeconds(1)));
    public static (bool Eligible, bool Unknown, bool Conflict) Date(SourceItem item, Settings settings)
    {
        var dates = item.Dates.Where(d => d.Value is not null && d.Value.Value.Year is >= 1970 and <= 2200).Select(d => d.Value!.Value.Date).ToList();
        if (dates.Count == 0) return (!settings.DateFilter || settings.IncludeUnknown, true, false);
        var inRange = dates.Select(d => d >= settings.StartDate.Date && (settings.EndDate is null || d <= settings.EndDate.Value.Date)).ToList();
        var conflict = inRange.Distinct().Count() > 1;
        return (!settings.DateFilter || inRange.Any(v => v), false, conflict);
    }
}
