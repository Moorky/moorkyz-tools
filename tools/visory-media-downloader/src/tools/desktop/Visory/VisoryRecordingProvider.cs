using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Windows;
using MoorkszVisoryMediaDownloader.Core;
using MoorkszVisoryMediaDownloader.Core.Infrastructure;
using MoorkszVisoryMediaDownloader.Core.Providers;

namespace MoorkszVisoryMediaDownloader.App.Visory;

/// <summary>In-memory authorization state for the signed-in Visory web session. Tokens are never persisted.</summary>
public sealed class VisorySession(string userDataFolder, string routesPath, Action<string, string> diagnostic)
{
    private readonly object gate = new();
    private string? token;
    private DateTimeOffset expires;

    public string UserDataFolder { get; } = userDataFolder;
    public void Log(string level, string message) => diagnostic(level, message);
    public bool HasSavedSession => !DeletionPending && Directory.Exists(UserDataFolder) && Directory.EnumerateFileSystemEntries(UserDataFolder).Any();

    public void OfferToken(string candidate)
    {
        var exp = ExpiryOf(candidate);
        if (exp is null) return;
        lock (gate)
        {
            if (token is not null && exp <= expires) return;
            token = candidate;
            expires = exp.Value;
        }
    }

    public string? CurrentToken(TimeSpan? margin = null)
    {
        lock (gate) return token is not null && expires - (margin ?? TimeSpan.FromMinutes(3)) > DateTimeOffset.UtcNow ? token : null;
    }

    public void ForgetToken()
    {
        lock (gate) token = null;
    }

    public IReadOnlyList<string> LoadRoutes()
    {
        try
        {
            var routes = JsonFiles.Read<List<string>>(routesPath) ?? [];
            return routes.Where(r => Uri.TryCreate(r, UriKind.Absolute, out var u) && VisoryEndpoints.MetadataPolicy.IsAllowed(u, out _)).Take(20).ToList();
        }
        catch (JsonException e)
        {
            Log("ERROR", "Saved Visory routes could not be parsed: " + e.Message);
            return [];
        }
    }

    public void SaveRoutes(IEnumerable<string> routes) =>
        JsonFiles.WriteAtomic(routesPath, routes.Where(r => Uri.TryCreate(r, UriKind.Absolute, out var u) && VisoryEndpoints.MetadataPolicy.IsAllowed(u, out _)).Distinct().Take(20).ToList());

    /// <summary>Removes cookies, IndexedDB tokens and cache of the embedded browser profile.</summary>
    public async Task<bool> SignOutAsync()
    {
        ForgetToken();
        // Release WebView2 environment/controller wrappers so the browser process can exit and unlock its profile.
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
        int[] processes;
        lock (gate) processes = [.. browserProcesses];
        foreach (var id in processes)
        {
            try
            {
                using var process = System.Diagnostics.Process.GetProcessById(id);
                using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(15));
                await process.WaitForExitAsync(timeout.Token);
            }
            catch (Exception e) when (e is ArgumentException or InvalidOperationException or OperationCanceledException)
            {
            }
        }
        lock (gate) browserProcesses.Clear();
        for (var attempt = 0; attempt < 20; attempt++)
        {
            try
            {
                if (Directory.Exists(UserDataFolder)) Directory.Delete(UserDataFolder, recursive: true);
                // A closing browser process can still flush state; confirm the profile stays gone.
                await Task.Delay(1500);
                if (!Directory.Exists(UserDataFolder)) return true;
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException)
            {
                await Task.Delay(500);
            }
        }
        if (!Directory.Exists(UserDataFolder)) return true;
        File.WriteAllText(PendingDeletionMarker, "Delete this sign-in profile before the next WebView2 use.");
        return false;
    }

    private string PendingDeletionMarker => UserDataFolder + ".delete-pending";

    /// <summary>Finishes a sign-out whose profile files were still locked by an exiting browser process.</summary>
    public void CompletePendingDeletion()
    {
        if (!File.Exists(PendingDeletionMarker)) return;
        try
        {
            if (Directory.Exists(UserDataFolder)) Directory.Delete(UserDataFolder, recursive: true);
            File.Delete(PendingDeletionMarker);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            Log("ERROR", "Pending sign-out cleanup failed: " + e.Message);
        }
    }

    public bool DeletionPending => File.Exists(PendingDeletionMarker);

    private readonly HashSet<int> browserProcesses = [];

    public void TrackBrowserProcess(int processId)
    {
        lock (gate) browserProcesses.Add(processId);
    }

    private static DateTimeOffset? ExpiryOf(string jwt)
    {
        var parts = jwt.Split('.');
        if (parts.Length != 3) return null;
        try
        {
            var payload = parts[1].Replace('-', '+').Replace('_', '/');
            payload = payload.PadRight(payload.Length + (4 - payload.Length % 4) % 4, '=');
            using var json = JsonDocument.Parse(Encoding.UTF8.GetString(Convert.FromBase64String(payload)));
            return json.RootElement.TryGetProperty("exp", out var exp) && exp.TryGetInt64(out var seconds) ? DateTimeOffset.FromUnixTimeSeconds(seconds) : null;
        }
        catch (Exception e) when (e is FormatException or JsonException or ArgumentException)
        {
            return null;
        }
    }
}

/// <summary>
/// Visory provider built on observed behavior of the official web app (Firebase project visory-54ab5).
/// Real-account behavior is unverified in development; see VISORY_INTEGRATION.md.
/// </summary>
public sealed class VisoryRecordingProvider(VisorySession session, Func<Window?> owner, Func<bool> diagnostics) : IRecordingProvider
{
    private static readonly HttpClient Metadata = CreateMetadataClient();

    public string ProviderId => "visory";
    public string DisplayName => "Visory";
    public bool IsSynthetic => false;
    public UrlPolicy MediaPolicy => VisoryEndpoints.MediaPolicy;
    public bool Unattended { get; set; }

    public async Task<DiscoveryResult> DiscoverAsync(IProgress<DiscoveryProgress>? progress, CancellationToken cancellationToken)
    {
        var dispatcher = Application.Current.Dispatcher;
        var window = await dispatcher.InvokeAsync(() =>
        {
            var w = new ScanWindow(session, progress, Unattended ? ScanMode.Unattended : ScanMode.Interactive, diagnostics());
            if (!Unattended && owner() is { IsVisible: true } o) w.Owner = o;
            w.Show();
            return w;
        });
        await using var registration = cancellationToken.Register(() => dispatcher.InvokeAsync(window.Close));
        return await window.Completion.ConfigureAwait(false);
    }

    public async Task<MediaRequest> GetMediaRequestAsync(RecordingEntry recording, bool refresh, CancellationToken cancellationToken)
    {
        if (!refresh && recording.LastUrl is { } known && HasEmbeddedAccess(known) && Uri.TryCreate(known, UriKind.Absolute, out var direct))
            return MediaRequest.Plain(direct);
        if (recording.ObjectPath is { } objectPath)
        {
            var token = session.CurrentToken() ?? await RefreshTokenAsync(cancellationToken).ConfigureAwait(false)
                ?? throw new ReauthenticationRequiredException("Visory sign-in required: the session expired or was signed out");
            var objectUrl = $"https://firebasestorage.googleapis.com/v0/b/{VisoryEndpoints.StorageBucket}/o/{Uri.EscapeDataString(objectPath)}";
            if (refresh)
            {
                var fresh = await FreshDownloadUrlAsync(objectUrl, token, cancellationToken).ConfigureAwait(false);
                if (fresh is not null) return MediaRequest.Plain(fresh);
            }
            return new MediaRequest(new Uri(objectUrl + "?alt=media"), new Dictionary<string, string> { ["Authorization"] = "Firebase " + token });
        }
        if (recording.LastUrl is { } url && Uri.TryCreate(url, UriKind.Absolute, out var plain))
        {
            if (refresh) throw new ProviderUnavailableException("The download link expired and cannot be renewed without a new scan. Scan Visory again to collect fresh links.");
            return MediaRequest.Plain(plain);
        }
        throw new ProviderUnavailableException("No download location was observed for this recording. Open it in Visory during a scan.");
    }

    public HttpMessageHandler? CreateMediaHandler() => null;

    private async Task<string?> RefreshTokenAsync(CancellationToken cancellationToken)
    {
        if (!session.HasSavedSession) return null;
        var dispatcher = Application.Current.Dispatcher;
        var window = await dispatcher.InvokeAsync(() =>
        {
            var w = new ScanWindow(session, null, ScanMode.TokenOnly, false);
            w.Show();
            return w;
        });
        await using var registration = cancellationToken.Register(() => dispatcher.InvokeAsync(window.Close));
        try { await window.Completion.WaitAsync(TimeSpan.FromSeconds(60), cancellationToken).ConfigureAwait(false); }
        catch (Exception e) when (e is TimeoutException or InvalidOperationException) { await dispatcher.InvokeAsync(window.Close); }
        return session.CurrentToken();
    }

    private static async Task<Uri?> FreshDownloadUrlAsync(string objectUrl, string token, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, objectUrl);
        request.Headers.Authorization = new AuthenticationHeaderValue("Firebase", token);
        using var response = await Metadata.SendAsync(request, cancellationToken).ConfigureAwait(false);
        if ((int)response.StatusCode is 401 or 403) throw new ReauthenticationRequiredException("Visory storage rejected the session; sign in again");
        if (!response.IsSuccessStatusCode) return null;
        var length = response.Content.Headers.ContentLength;
        if (length > DiscoverySession.MaxMetadataBytes) return null;
        var parser = new DiscoverySession();
        parser.ObserveHttpJson(new Uri(objectUrl), await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false), DateTimeOffset.UtcNow);
        var url = parser.Recordings.FirstOrDefault()?.DownloadUrl;
        return url is not null && Uri.TryCreate(url, UriKind.Absolute, out var fresh) && VisoryEndpoints.MediaPolicy.IsAllowed(fresh, out _) ? fresh : null;
    }

    private static bool HasEmbeddedAccess(string url) =>
        url.Contains("token=", StringComparison.Ordinal) || url.Contains("X-Goog-Signature=", StringComparison.OrdinalIgnoreCase) || url.Contains("Signature=", StringComparison.Ordinal);

    private static HttpClient CreateMetadataClient()
    {
        var client = new HttpClient(new SocketsHttpHandler { AllowAutoRedirect = false, UseCookies = false }) { Timeout = TimeSpan.FromSeconds(30) };
        client.DefaultRequestHeaders.UserAgent.ParseAdd("MoorkszVisoryMediaDownloader/0.1");
        return client;
    }
}
