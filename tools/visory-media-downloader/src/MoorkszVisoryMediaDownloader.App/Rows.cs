using MoorkszVisoryMediaDownloader.Core;
using MoorkszVisoryMediaDownloader.Core.Archive;
using MoorkszVisoryMediaDownloader.Core.Download;
using MoorkszVisoryMediaDownloader.Core.Infrastructure;
using MoorkszVisoryMediaDownloader.Core.Storage;

namespace MoorkszVisoryMediaDownloader.App;

public sealed record NavPage(int Index, string Key, string Glyph)
{
    public string Title => Loc.T(Key);
}

public sealed record ActivityItem(string Time, string Level, string Text);

public sealed class RecordingRow(RecordingEntry entry)
{
    public RecordingEntry Entry { get; } = entry;
    public string Key => Entry.Key;
    public string Camera => Entry.Camera;
    public DateTimeOffset? RecordedSort => Entry.RecordedAt;
    public string Recorded => Entry.RecordedAt?.ToLocalTime().ToString("g", Loc.Culture) ?? Loc.T("Unknown");
    public string Name => Entry.DisplayName;
    public long SizeSort => Entry.KnownSize ?? -1;
    public string SizeText => Entry.KnownSize is { } size ? Format.Size(size) : Loc.T("Unknown");
    public string Status => Loc.T("RecStatus" + Entry.Status);
    public RecordingStatus StatusValue => Entry.Status;
    public string Type => MediaTypes.TypeLabel(Entry.Extension);
    public string Id => Entry.IdIsFingerprint ? Entry.RecordingId + " (fingerprint)" : Entry.RecordingId;
    public string DateSource => Entry.RecordedAtSource ?? Loc.T("Unknown");
    public string Original => Entry.OriginalName ?? Loc.T("Unknown");
    public string Destination => Entry.Destination ?? "-";
    public string Sha256 => Entry.Sha256 ?? "-";
    public string LastError => Entry.LastError ?? "-";
    public string SearchText { get; } = $"{entry.Camera} {entry.DisplayName} {entry.RecordingId} {entry.OriginalName}".ToLowerInvariant();
}

public sealed class TransferRow : ObservableObject
{
    private string fileName = "", engine = "", attempt = "", bytes = "", speed = "", elapsed = "", status = "";
    private double percent;
    private bool waiting;

    public required string Key { get; init; }
    public string FileName { get => fileName; set => Set(ref fileName, value); }
    public string Engine { get => engine; set => Set(ref engine, value); }
    public string Attempt { get => attempt; set => Set(ref attempt, value); }
    public double Percent { get => percent; set => Set(ref percent, value); }
    public string Bytes { get => bytes; set => Set(ref bytes, value); }
    public string Speed { get => speed; set => Set(ref speed, value); }
    public string Elapsed { get => elapsed; set => Set(ref elapsed, value); }
    public string Status { get => status; set => Set(ref status, value); }
    public bool WaitingForRetry { get => waiting; set => Set(ref waiting, value); }

    public void Apply(TransferSnapshot s, string displayName)
    {
        FileName = displayName;
        Engine = s.Engine == TransferEngineKind.None ? "" : s.Engine == TransferEngineKind.Bits ? "BITS" : "HTTP";
        Attempt = s.Attempt == 0 ? "" : $"{s.Attempt}/{s.MaxAttempts}";
        Percent = s.Total is > 0 ? Math.Min(100, s.Bytes * 100.0 / s.Total.Value) : 0;
        Bytes = s.Status == "Queued" ? "" : $"{Format.Size(s.Bytes)} / {(s.Total is { } t ? Format.Size(t) : Loc.T("Unknown"))}";
        Speed = s.BytesPerSecond > 0 ? Format.Speed(s.BytesPerSecond) : "";
        Elapsed = s.Status == "Queued" ? "" : Format.Duration(s.Elapsed);
        Status = s.Message ?? Loc.TransferStatus(s.Status);
        WaitingForRetry = s.WaitingForRetry;
    }
}

public sealed record ArchiveRow(StateStore.ArchiveRecord Record)
{
    public string Created => Record.CreatedAt.ToLocalTime().ToString("g", Loc.Culture);
    public string FileName => Path.GetFileName(Record.Path);
    public string Scope => Loc.T(Record.Scope == ArchiveScope.ThisRunOnly ? "ScopeRun" : "ScopeAll");
    public int Entries => Record.Entries;
    public string Size => Format.Size(Record.Bytes);
    public string Status => Loc.ArchiveStatus(Record.Status) + (Record.Incomplete && !Record.Status.Contains("incomplete", StringComparison.Ordinal) ? $" ({Loc.T("ArchiveIncomplete")})" : "");
    public string Message => Record.Message ?? "";
}

public sealed record RunRow(StateStore.RunRecord Record)
{
    public string Id => Record.Id;
    public string Started => Record.StartedAt.ToLocalTime().ToString("g", Loc.Culture);
    public string State => Loc.T("Run" + Record.State);
    public string Counts => Record.Summary is { } s ? $"✓ {s.Succeeded + s.AlreadyVerified}  ✕ {s.Failed}" : "";
}

public sealed record LogLineRow(string Time, string Level, string Text);

public sealed record FailedRow(string Time, string FileName, string Size, string Message, string Url, string HttpStatus, string Attempts, string RecordingId);

public sealed record ArchiveEntryRow(ArchiveEntryInfo Info)
{
    public string Name => Info.Name;
    public string Size => Format.Size(Info.Length);
}
