using System.Security.Cryptography;
using System.Text;
using VisoryBulkDownloader.Core.Infrastructure;
using VisoryBulkDownloader.Core.Storage;

namespace VisoryBulkDownloader.Core.Download;

/// <summary>Assigns stable, collision-free relative destinations under downloads/.</summary>
public sealed class DestinationPlanner(StateStore store, BackupLayout layout)
{
    public string Assign(RecordingEntry entry, NamingRule rule)
    {
        lock (gate) return AssignLocked(entry, rule);
    }

    private readonly object gate = new();

    private string AssignLocked(RecordingEntry entry, NamingRule rule)
    {
        if (entry.Destination is not null) return entry.Destination;
        var extension = PathSafety.SanitizeExtension(entry.Extension);
        if (extension.Length == 0) extension = ".bin";
        var shortId = ShortId(entry.RecordingId);
        var camera = PathSafety.SanitizeSegment(entry.CameraName ?? entry.CameraId, "camera", 48);
        var time = entry.RecordedAt?.ToLocalTime().ToString("yyyy-MM-dd_HH-mm-ss", Format.Invariant);
        var original = Path.GetFileNameWithoutExtension(entry.OriginalName ?? string.Empty);
        var stem = rule switch
        {
            NamingRule.CameraFolders => Path.Combine(camera, time ?? shortId),
            NamingRule.OriginalName => PathSafety.SanitizeSegment(original.Length > 0 ? original : null, shortId),
            NamingRule.RecordingId => PathSafety.SanitizeSegment(entry.IdIsFingerprint ? shortId : entry.RecordingId.Replace('/', '_'), shortId),
            _ => time is null ? $"{camera}_{shortId}" : $"{camera}_{time}",
        };
        foreach (var candidate in Candidates(stem, shortId, extension))
        {
            var full = layout.DestinationPath(candidate);
            if (store.DestinationTaken(candidate, entry.Key) || File.Exists(full) || File.Exists(full + ".part") || Directory.Exists(full)) continue;
            entry.Destination = candidate;
            try
            {
                store.Update(entry);
            }
            catch (IOException e) when (e.Message.Contains("UNIQUE", StringComparison.Ordinal))
            {
                entry.Destination = null;
                continue;
            }
            return candidate;
        }
        throw new IOException("Could not allocate a unique destination name.");
    }

    private static IEnumerable<string> Candidates(string stem, string shortId, string extension)
    {
        yield return stem + extension;
        yield return $"{stem}_{shortId}{extension}";
        for (var i = 2; i < 1000; i++) yield return $"{stem}_{shortId}_{i}{extension}";
    }

    public static string ShortId(string recordingId) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(recordingId)))[..8].ToLowerInvariant();
}
