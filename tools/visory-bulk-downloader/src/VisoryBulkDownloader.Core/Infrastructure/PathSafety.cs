using System.Text;

namespace VisoryBulkDownloader.Core.Infrastructure;

public static class PathSafety
{
    private static readonly HashSet<string> ReservedNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "CON", "PRN", "AUX", "NUL", "CONIN$", "CONOUT$",
        "COM1", "COM2", "COM3", "COM4", "COM5", "COM6", "COM7", "COM8", "COM9",
        "LPT1", "LPT2", "LPT3", "LPT4", "LPT5", "LPT6", "LPT7", "LPT8", "LPT9",
    };

    /// <summary>Converts untrusted remote text into one safe Windows path segment.</summary>
    public static string SanitizeSegment(string? value, string fallback = "recording", int maxLength = 96)
    {
        var builder = new StringBuilder();
        foreach (var c in (value ?? string.Empty).Normalize(NormalizationForm.FormC))
        {
            if (char.IsControl(c) || Path.GetInvalidFileNameChars().Contains(c) || c is '/' or '\\' or ':' or '*' or '?' or '"' or '<' or '>' or '|')
                builder.Append('_');
            else
                builder.Append(c);
        }
        var text = builder.ToString().Trim().TrimEnd('.', ' ');
        while (text.Contains("__", StringComparison.Ordinal)) text = text.Replace("__", "_", StringComparison.Ordinal);
        if (text is "" or "." or ".." || text.All(c => c is '_' or '.')) text = fallback;
        var stem = text.Split('.')[0];
        if (ReservedNames.Contains(stem)) text = "_" + text;
        if (text.Length > maxLength) text = text[..maxLength].TrimEnd('.', ' ');
        return text;
    }

    public static string SanitizeExtension(string? extension)
    {
        if (string.IsNullOrWhiteSpace(extension)) return string.Empty;
        var clean = extension.TrimStart('.');
        return clean.Length is > 0 and <= 8 && clean.All(char.IsAsciiLetterOrDigit) ? "." + clean.ToLowerInvariant() : string.Empty;
    }

    /// <summary>Resolves a relative path beneath a root and rejects traversal or rooted input.</summary>
    public static string Combine(string root, string relative)
    {
        if (Path.IsPathRooted(relative) || relative.Contains(':', StringComparison.Ordinal))
            throw new UnauthorizedAccessException("Rooted paths are not allowed in backup destinations.");
        var fullRoot = Path.TrimEndingDirectorySeparator(Path.GetFullPath(root));
        var full = Path.GetFullPath(Path.Combine(fullRoot, relative));
        if (!full.StartsWith(fullRoot + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
            throw new UnauthorizedAccessException("Path escapes the backup folder.");
        return full;
    }

    /// <summary>Rejects reparse points (symlinks/junctions) between root and target.</summary>
    public static void EnsureNoReparsePoints(string root, string target)
    {
        var fullRoot = Path.TrimEndingDirectorySeparator(Path.GetFullPath(root));
        var current = Path.GetFullPath(target);
        while (current.Length >= fullRoot.Length)
        {
            if (File.Exists(current) || Directory.Exists(current))
            {
                var attributes = File.GetAttributes(current);
                if (attributes.HasFlag(FileAttributes.ReparsePoint))
                    throw new UnauthorizedAccessException($"Links and junctions are not followed: {current}");
            }
            if (string.Equals(current, fullRoot, StringComparison.OrdinalIgnoreCase)) break;
            var parent = Path.GetDirectoryName(current);
            if (parent is null || parent == current) break;
            current = parent;
        }
    }

    public static void ValidateBackupRoot(string root)
    {
        if (string.IsNullOrWhiteSpace(root) || !Path.IsPathFullyQualified(root))
            throw new ArgumentException("Choose an absolute output folder.");
        var full = Path.GetFullPath(root);
        if (Path.GetPathRoot(full) is { } drive && string.Equals(Path.TrimEndingDirectorySeparator(drive), Path.TrimEndingDirectorySeparator(full), StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException("A drive root cannot be used as the backup folder. Choose a subfolder.");
        if (full.StartsWith(@"\\?\", StringComparison.Ordinal) || full.StartsWith(@"\\.\", StringComparison.Ordinal))
            throw new ArgumentException("Device paths are not supported.");
    }

    public static long FreeSpace(string path)
    {
        var root = Path.GetPathRoot(Path.GetFullPath(path));
        return root is null ? long.MaxValue : new DriveInfo(root).AvailableFreeSpace;
    }
}

public interface IDiskSpaceProbe
{
    long AvailableBytes(string path);
}

public sealed class DriveDiskSpaceProbe : IDiskSpaceProbe
{
    public long AvailableBytes(string path) => PathSafety.FreeSpace(path);
}
