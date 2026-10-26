using System.Text.Json;
using Moorkyz.Tools.MediaRecover.Core;

namespace Moorkyz.Tools.MediaRecover.Storage;

public sealed class JsonSettingsStore(string path) : ISettingsStore
{
    public Settings Load()
    {
        PathSafety.NoLinks(path);
        return File.Exists(path) ? JsonSerializer.Deserialize<Settings>(File.ReadAllText(path)) ?? throw new InvalidDataException("Settings are empty.") : new();
    }
    public void Save(Settings settings)
    {
        PathSafety.NoLinks(path);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var staging = path + "." + Guid.NewGuid().ToString("N") + ".part";
        using (var stream = new FileStream(staging, FileMode.CreateNew, FileAccess.Write, FileShare.None))
        {
            JsonSerializer.Serialize(stream, settings, new JsonSerializerOptions { WriteIndented = true });
            stream.Flush(true);
        }
        File.Move(staging, path, true);
    }
}
