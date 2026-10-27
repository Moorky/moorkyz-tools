using MoorkszVisoryMediaDownloader.Core.Infrastructure;

namespace MoorkszVisoryMediaDownloader.Core.Providers;

public interface IRecordingProvider
{
    string ProviderId { get; }
    string DisplayName { get; }
    /// <summary>True for demo/mock providers whose data must never be presented as real account data.</summary>
    bool IsSynthetic { get; }
    UrlPolicy MediaPolicy { get; }

    Task<DiscoveryResult> DiscoverAsync(IProgress<DiscoveryProgress>? progress, CancellationToken cancellationToken);

    /// <summary>Returns a request for the media bytes. When <paramref name="refresh"/> is true, a fresh authorization must be obtained.</summary>
    Task<MediaRequest> GetMediaRequestAsync(RecordingEntry recording, bool refresh, CancellationToken cancellationToken);

    /// <summary>Optional custom transport (synthetic demo media). BITS is never used when a custom transport is supplied.</summary>
    HttpMessageHandler? CreateMediaHandler();
}

/// <summary>The authorized session is missing or expired; the user must sign in again.</summary>
public sealed class ReauthenticationRequiredException(string message) : Exception(message);

/// <summary>A provider could not supply a usable download location.</summary>
public sealed class ProviderUnavailableException(string message) : Exception(message);
