using System.Security.Cryptography;

namespace Moorkyz.Tools.MediaRecover.Core;

public sealed class IntegrityService : IIntegrityService
{
    public async Task<string> HashAsync(string path, CancellationToken token)
    {
        PathSafety.NoLinks(path);
        await using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 1024 * 1024, FileOptions.Asynchronous | FileOptions.SequentialScan);
        return Convert.ToHexString(await SHA256.HashDataAsync(stream, token)).ToLowerInvariant();
    }
}
