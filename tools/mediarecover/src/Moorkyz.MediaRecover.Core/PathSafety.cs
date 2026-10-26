using System.Security.Cryptography;
using System.Text;

namespace Moorkyz.Tools.MediaRecover.Core;

public static class PathSafety
{
    public static bool Within(string child, string parent)
    {
        var c = Path.GetFullPath(child).TrimEnd('\\');
        var p = Path.GetFullPath(parent).TrimEnd('\\');
        return c.Equals(p, StringComparison.OrdinalIgnoreCase) || c.StartsWith(p + "\\", StringComparison.OrdinalIgnoreCase);
    }
    public static void NoLinks(string path)
    {
        var full = Path.GetFullPath(path);
        for (string? current = full; !string.IsNullOrEmpty(current); current = Path.GetDirectoryName(current))
        {
            try
            {
                if ((File.GetAttributes(current) & FileAttributes.ReparsePoint) != 0)
                    throw new RecoveryException("LINK_BLOCKED", $"Link/reparse point blocked: {current}", "Choose a real folder; junctions and symbolic links are not traversed.");
            }
            catch (FileNotFoundException) { }
            catch (DirectoryNotFoundException) { }
        }
    }
    public static string Key(string value) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value))).ToLowerInvariant();
    public static string SafeName(string raw)
    {
        var name = new string(raw.Normalize(NormalizationForm.FormC).Select(c => c < 32 || Path.GetInvalidFileNameChars().Contains(c) ? '_' : c).ToArray()).TrimEnd('.', ' ');
        if (string.IsNullOrWhiteSpace(name)) name = "unnamed";
        var stem = Path.GetFileNameWithoutExtension(name);
        if (new[] { "CON", "PRN", "AUX", "NUL", "COM1", "COM2", "COM3", "COM4", "COM5", "COM6", "COM7", "COM8", "COM9", "LPT1", "LPT2", "LPT3", "LPT4", "LPT5", "LPT6", "LPT7", "LPT8", "LPT9" }.Contains(stem, StringComparer.OrdinalIgnoreCase)) name = "_" + name;
        if (name.Length > 110) name = name[..80] + "-" + Key(name)[..12] + Path.GetExtension(name)[..Math.Min(Path.GetExtension(name).Length, 12)];
        return name;
    }
    public static string Target(Settings settings, SourceItem item)
    {
        var relative = item.RelativePath.Replace('/', '\\');
        var name = SafeName(Path.GetFileNameWithoutExtension(relative)) + "-" + item.Key[..16] + SafeExtension(relative);
        var dir = settings.Destination;
        if (settings.Layout == "Type") dir = Path.Combine(dir, item.MediaType);
        if (settings.Layout == "Tree")
        {
            var pieces = (Path.GetDirectoryName(relative) ?? "").Split('\\', StringSplitOptions.RemoveEmptyEntries).Select(SafeName);
            foreach (var piece in pieces) dir = Path.Combine(dir, piece);
        }
        var result = Path.GetFullPath(Path.Combine(dir, name));
        if (!Within(result, settings.Destination)) throw new RecoveryException("INVALID_PATH", "Target escaped destination.", "Use a different destination.");
        NoLinks(result);
        return result;
    }
    private static string SafeExtension(string path)
    {
        var extension = Path.GetExtension(path);
        return extension.Length <= 16 ? SafeName("x" + extension)[1..] : ".bin";
    }
    public static string Unused(string path)
    {
        if (!File.Exists(path) && !Directory.Exists(path)) return path;
        return Path.Combine(Path.GetDirectoryName(path)!, Path.GetFileNameWithoutExtension(path) + "-" + Guid.NewGuid().ToString("N")[..12] + Path.GetExtension(path));
    }
}
