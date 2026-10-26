namespace VisoryBulkDownloader.Core;

public enum RecordingStatus { New, Queued, Downloading, Finalizing, Verified, Failed, Missing, Corrupt, Canceled }

public enum DiscoveryCompleteness { Complete, Incomplete, Uncertain }

public enum TransferEngineKind { None, Bits, Http }

public enum PriorityMode { SmartBackground, Balanced, Fast }

public enum IntegrityPolicy { SizeOnly, Sha256 }

public enum NamingRule { CameraAndTime, CameraFolders, OriginalName, RecordingId }

public enum ArchiveScope { AllSelected, ThisRunOnly }

public enum ThemeChoice { System, Light, Dark }

public enum RunState { Running, Interrupted, Completed, CompletedWithErrors, Canceled }

/// <summary>A recording as reported by a provider during discovery. All metadata is optional except the ID.</summary>
public sealed record DiscoveredRecording
{
    public required string RecordingId { get; init; }
    public bool IdIsFingerprint { get; init; }
    public string? CameraId { get; init; }
    public string? CameraName { get; init; }
    public DateTimeOffset? RecordedAt { get; init; }
    /// <summary>Provenance of <see cref="RecordedAt"/>, e.g. remote:createdAt or filename.</summary>
    public string? RecordedAtSource { get; init; }
    public string? OriginalName { get; init; }
    public string? MediaType { get; init; }
    public long? Size { get; init; }
    public string? DownloadUrl { get; init; }
    public string? ObjectPath { get; init; }
    public string? RemoteMd5 { get; init; }

    public string Extension
    {
        get
        {
            var fromName = Path.GetExtension(OriginalName ?? ObjectPath ?? string.Empty);
            if (!string.IsNullOrEmpty(fromName)) return fromName.ToLowerInvariant();
            if (Uri.TryCreate(DownloadUrl, UriKind.Absolute, out var uri))
            {
                var fromUrl = Path.GetExtension(Uri.UnescapeDataString(uri.AbsolutePath));
                if (!string.IsNullOrEmpty(fromUrl)) return fromUrl.ToLowerInvariant();
            }
            return MediaTypes.ExtensionFor(MediaType);
        }
    }

    public DiscoveredRecording Merge(DiscoveredRecording other) => this with
    {
        IdIsFingerprint = IdIsFingerprint && other.IdIsFingerprint,
        CameraId = CameraId ?? other.CameraId,
        CameraName = CameraName ?? other.CameraName,
        RecordedAt = RecordedAt ?? other.RecordedAt,
        RecordedAtSource = RecordedAt is null ? other.RecordedAtSource : RecordedAtSource,
        OriginalName = OriginalName ?? other.OriginalName,
        MediaType = MediaType ?? other.MediaType,
        Size = Size ?? other.Size,
        DownloadUrl = other.DownloadUrl ?? DownloadUrl,
        ObjectPath = ObjectPath ?? other.ObjectPath,
        RemoteMd5 = RemoteMd5 ?? other.RemoteMd5,
    };
}

public static class MediaTypes
{
    public static readonly string[] VideoExtensions = [".mp4", ".webm", ".mov", ".mkv", ".m4v", ".avi", ".ts", ".3gp"];
    public static readonly string[] AudioExtensions = [".m4a", ".mp3", ".aac", ".ogg", ".opus", ".wav"];

    public static bool IsMediaExtension(string? extension) =>
        extension is not null && (VideoExtensions.Contains(extension.ToLowerInvariant()) || AudioExtensions.Contains(extension.ToLowerInvariant()));

    public static bool IsMediaPath(string? path)
    {
        if (string.IsNullOrWhiteSpace(path)) return false;
        var clean = path.Split('?', '#')[0];
        return IsMediaExtension(Path.GetExtension(Uri.UnescapeDataString(clean)));
    }

    public static bool IsMediaContentType(string? contentType) =>
        contentType is not null && (contentType.StartsWith("video/", StringComparison.OrdinalIgnoreCase) || contentType.StartsWith("audio/", StringComparison.OrdinalIgnoreCase));

    public static string ExtensionFor(string? contentType) => contentType?.Split(';')[0].Trim().ToLowerInvariant() switch
    {
        "video/mp4" => ".mp4",
        "video/webm" => ".webm",
        "video/quicktime" => ".mov",
        "video/x-matroska" => ".mkv",
        "video/mp2t" => ".ts",
        "audio/mp4" => ".m4a",
        "audio/mpeg" => ".mp3",
        "audio/webm" => ".webm",
        "audio/ogg" => ".ogg",
        _ => ".bin",
    };

    public static string TypeLabel(string extension) => extension.TrimStart('.').ToUpperInvariant();
}

public sealed record DiscoveryResult(
    IReadOnlyList<DiscoveredRecording> Recordings,
    DiscoveryCompleteness Completeness,
    IReadOnlyList<string> Notes,
    IReadOnlyList<string> Errors)
{
    public int Cameras => Recordings.Select(r => r.CameraId ?? r.CameraName).Where(c => c is not null).Distinct().Count();
}

public sealed record DiscoveryProgress(int Recordings, int Cameras, string Message);

/// <summary>Persisted per-recording state.</summary>
public sealed class RecordingEntry
{
    public required string Key { get; init; }
    public required string Provider { get; init; }
    public required string RecordingId { get; init; }
    public bool IdIsFingerprint { get; set; }
    public string? CameraId { get; set; }
    public string? CameraName { get; set; }
    public DateTimeOffset? RecordedAt { get; set; }
    public string? RecordedAtSource { get; set; }
    public string? OriginalName { get; set; }
    public string? MediaType { get; set; }
    public long? RemoteSize { get; set; }
    public string? RemoteMd5 { get; set; }
    public string? ObjectPath { get; set; }
    public string? LastUrl { get; set; }
    public string? Destination { get; set; }
    public long? ActualSize { get; set; }
    public string? Sha256 { get; set; }
    public RecordingStatus Status { get; set; }
    public TransferEngineKind Engine { get; set; }
    public int Attempts { get; set; }
    public string? LastError { get; set; }
    public int? LastHttpStatus { get; set; }
    public DateTimeOffset? LastVerifiedAt { get; set; }
    public string? ResumeValidator { get; set; }
    public long? ResumeTotal { get; set; }
    public string? BitsJob { get; set; }
    public string? RunId { get; set; }
    public DateTimeOffset FirstSeenAt { get; set; }
    public DateTimeOffset LastSeenAt { get; set; }
    public string? LastScanId { get; set; }

    public string Camera => CameraName ?? CameraId ?? "unknown";
    public string Extension => new DiscoveredRecording { RecordingId = RecordingId, OriginalName = OriginalName, ObjectPath = ObjectPath, MediaType = MediaType }.Extension;
    public string DisplayName => Destination is null ? (OriginalName ?? RecordingId) : Path.GetFileName(Destination);
    public long? KnownSize => ActualSize ?? ResumeTotal ?? RemoteSize;
}

public sealed record MediaRequest(Uri Url, IReadOnlyDictionary<string, string> Headers)
{
    public static MediaRequest Plain(Uri url) => new(url, new Dictionary<string, string>());
    public bool RequiresHeaders => Headers.Count > 0;
}

public sealed record RecordingFilter
{
    public DateTime? StartDate { get; init; }
    public DateTime? EndDate { get; init; }
    public bool IncludeUnknownDates { get; init; } = true;
    public IReadOnlyList<string> Cameras { get; init; } = [];
    public IReadOnlyList<string> FileTypes { get; init; } = [];
    public long? MinSize { get; init; }
    public long? MaxSize { get; init; }
    public bool IncludeUnknownSizes { get; init; } = true;
    public bool NewOrMissingOnly { get; init; }

    public bool Matches(RecordingEntry entry)
    {
        if (entry.RecordedAt is { } recorded)
        {
            var local = recorded.ToLocalTime().DateTime;
            if (StartDate is { } start && local < start.Date) return false;
            if (EndDate is { } end && local >= end.Date.AddDays(1)) return false;
        }
        else if ((StartDate is not null || EndDate is not null) && !IncludeUnknownDates) return false;
        if (Cameras.Count > 0 && !Cameras.Contains(entry.Camera, StringComparer.OrdinalIgnoreCase)) return false;
        if (FileTypes.Count > 0 && !FileTypes.Contains(MediaTypes.TypeLabel(entry.Extension), StringComparer.OrdinalIgnoreCase)) return false;
        if (entry.KnownSize is { } size)
        {
            if (MinSize is { } min && size < min) return false;
            if (MaxSize is { } max && size > max) return false;
        }
        else if ((MinSize is not null || MaxSize is not null) && !IncludeUnknownSizes) return false;
        if (NewOrMissingOnly && entry.Status == RecordingStatus.Verified) return false;
        return true;
    }
}

public sealed record InventorySummary
{
    public int Discovered { get; init; }
    public int Cameras { get; init; }
    public int Verified { get; init; }
    public int Missing { get; init; }
    public int Corrupt { get; init; }
    public int NeedsRetry { get; init; }
    public int New { get; init; }
    public int Selected { get; init; }
    public int ToDownload { get; init; }
    public long KnownSelectedBytes { get; init; }
    public int UnknownSizeSelected { get; init; }
    public long AvailableBytes { get; init; }
    public DiscoveryCompleteness Completeness { get; init; } = DiscoveryCompleteness.Uncertain;
    public IReadOnlyList<string> Notes { get; init; } = [];
    public IReadOnlyList<string> Errors { get; init; } = [];
    public DateTimeOffset? ScannedAt { get; init; }
}

public sealed record RunSummary
{
    public required string RunId { get; init; }
    public DateTimeOffset StartedAt { get; init; }
    public DateTimeOffset? FinishedAt { get; init; }
    public double ElapsedSeconds { get; init; }
    public RunState State { get; init; }
    public string Outcome { get; init; } = "";
    public int Discovered { get; init; }
    public DiscoveryCompleteness InventoryCompleteness { get; init; }
    public int Selected { get; init; }
    public int AlreadyVerified { get; init; }
    public int Scheduled { get; init; }
    public int Succeeded { get; init; }
    public int Failed { get; init; }
    public int Skipped { get; init; }
    public int PendingOrCanceled { get; init; }
    public int RetryAttempts { get; init; }
    public int FilesRequiringRetries { get; init; }
    public long BytesTransferred { get; init; }
    public double? AverageFileSeconds { get; init; }
    public double? AverageFileBytes { get; init; }
    public double? AverageBytesPerSecond { get; init; }
    public double CompletionPercent { get; init; }
    public string? ArchivePath { get; init; }
    public string ArchiveStatus { get; init; } = "not created";
    public string? ReportsFolder { get; init; }
    public string? LogPath { get; init; }
    public string? CsvReportPath { get; init; }
    public string? FailedReportPath { get; init; }
    public string? DebugLogPath { get; init; }
    public string? SummaryPath { get; init; }
}
