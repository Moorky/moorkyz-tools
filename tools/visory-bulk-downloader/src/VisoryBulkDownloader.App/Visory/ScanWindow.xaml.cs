using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Windows;
using Microsoft.Web.WebView2.Core;
using VisoryBulkDownloader.Core;
using VisoryBulkDownloader.Core.Infrastructure;
using VisoryBulkDownloader.Core.Providers;

namespace VisoryBulkDownloader.App.Visory;

public enum ScanMode { Interactive, Unattended, TokenOnly }

/// <summary>
/// Hosts the official Visory web app in WebView2 and passively observes the metadata it loads.
/// Only Visory/Firebase-project origins are inspected; media bodies are never read.
/// </summary>
public partial class ScanWindow : Window
{
    private const string TokenScript = "(async () => { try { if (!window.firebase || !firebase.auth) return null; const u = firebase.auth().currentUser; return u ? await u.getIdToken() : null; } catch (e) { return null; } })()";
    private readonly DiscoverySession session = new();
    private readonly TaskCompletionSource<DiscoveryResult> completion = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly VisorySession visory;
    private readonly IProgress<DiscoveryProgress>? progress;
    private readonly ScanMode mode;
    private readonly IReadOnlyList<string> routes;
    private readonly HashSet<string> sockets = new(StringComparer.Ordinal);
    private CancellationTokenSource? autoScroll;
    private bool finished;

    public ScanWindow(VisorySession visory, IProgress<DiscoveryProgress>? progress, ScanMode mode, bool diagnostics)
    {
        InitializeComponent();
        this.visory = visory;
        this.progress = progress;
        this.mode = mode;
        routes = visory.LoadRoutes();
        DiagnosticsButton.Visibility = diagnostics ? Visibility.Visible : Visibility.Collapsed;
        Icon = MainWindow.WindowIcon();
        if (mode != ScanMode.Interactive)
        {
            ShowInTaskbar = false;
            ShowActivated = false;
            WindowStartupLocation = WindowStartupLocation.Manual;
            Left = -32000;
            Top = -32000;
        }
        Loaded += OnLoaded;
        Closed += OnClosed;
        UpdateCounter();
    }

    public Task<DiscoveryResult> Completion => completion.Task;
    public HashSet<string> VisitedRoutes { get; } = new(StringComparer.Ordinal);

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        try
        {
            Directory.CreateDirectory(visory.UserDataFolder);
            var environment = await CoreWebView2Environment.CreateAsync(null, visory.UserDataFolder, new CoreWebView2EnvironmentOptions { Language = Loc.Instance.Language == "de" ? "de-DE" : "en-US" });
            await Web.EnsureCoreWebView2Async(environment);
        }
        catch (WebView2RuntimeNotFoundException)
        {
            ShowMissing(null);
            return;
        }
        catch (Exception ex) when (ex is COMException or InvalidOperationException or IOException or UnauthorizedAccessException)
        {
            ShowMissing(ex.Message);
            return;
        }
        var core = Web.CoreWebView2;
        visory.TrackBrowserProcess((int)core.BrowserProcessId);
        core.Settings.AreDevToolsEnabled = false;
        core.Settings.IsStatusBarEnabled = false;
        core.Settings.IsPasswordAutosaveEnabled = false;
        core.Settings.IsGeneralAutofillEnabled = false;
        core.WebResourceResponseReceived += OnResponse;
        core.SourceChanged += (_, _) => { if (mode == ScanMode.Interactive && session.Recordings.Count > 0) VisitedRoutes.Add(core.Source); };
        await core.CallDevToolsProtocolMethodAsync("Network.enable", "{}");
        core.GetDevToolsProtocolEventReceiver("Network.webSocketCreated").DevToolsProtocolEventReceived += OnSocketCreated;
        core.GetDevToolsProtocolEventReceiver("Network.webSocketFrameReceived").DevToolsProtocolEventReceived += OnFrameReceived;
        core.GetDevToolsProtocolEventReceiver("Network.webSocketFrameSent").DevToolsProtocolEventReceived += OnFrameSent;
        core.Navigate(mode == ScanMode.Unattended && routes.Count > 0 ? routes[0] : VisoryEndpoints.AppUrl);
        if (mode != ScanMode.Interactive) _ = RunUnattendedAsync();
    }

    private void ShowMissing(string? detail)
    {
        MissingPanel.Visibility = Visibility.Visible;
        if (detail is not null) MissingText.Text = Loc.T("WebView2Missing") + "\n\n" + detail;
        FinishButton.IsEnabled = false;
        if (mode != ScanMode.Interactive) completion.TrySetException(new InvalidOperationException(Loc.T("WebView2Missing")));
    }

    private async void OnResponse(object? sender, CoreWebView2WebResourceResponseReceivedEventArgs args)
    {
        if (!Uri.TryCreate(args.Request.Uri, UriKind.Absolute, out var uri)) return;
        try
        {
            CaptureAuthorization(uri, args.Request.Headers);
            session.ObserveRequest(uri);
            var status = args.Response.StatusCode;
            var contentType = Header(args.Response.Headers, "Content-Type");
            long? length = long.TryParse(Header(args.Response.Headers, "Content-Length"), out var parsed) ? parsed : null;
            if (status >= 400)
            {
                session.ObserveError(uri, status);
                UpdateCounter();
                return;
            }
            if (session.IsMediaCandidate(uri, contentType))
            {
                if (session.ObserveMediaResponse(uri, contentType, length, status, DateTimeOffset.UtcNow) > 0) Changed();
                return;
            }
            if (!session.ShouldInspect(uri, contentType, length)) return;
            using var stream = await args.Response.GetContentAsync();
            if (stream is null) return;
            using var memory = new MemoryStream();
            var chunk = new byte[64 * 1024];
            int read;
            while ((read = await stream.ReadAsync(chunk)) > 0)
            {
                memory.Write(chunk, 0, read);
                if (memory.Length > DiscoverySession.MaxMetadataBytes) return;
            }
            var text = Encoding.UTF8.GetString(memory.GetBuffer(), 0, (int)memory.Length);
            if (session.ObserveHttpJson(uri, text, DateTimeOffset.UtcNow) > 0) Changed();
            else UpdateCounter();
        }
        catch (Exception ex) when (ex is COMException or IOException or InvalidOperationException or JsonException)
        {
        }
    }

    private void CaptureAuthorization(Uri uri, CoreWebView2HttpRequestHeaders headers)
    {
        if (uri.IdnHost is not ("firebasestorage.googleapis.com" or "firestore.googleapis.com") || !VisoryEndpoints.MetadataPolicy.IsAllowed(uri, out _)) return;
        var authorization = Header(headers, "Authorization");
        if (authorization is null) return;
        var parts = authorization.Split(' ', 2);
        if (parts.Length == 2 && parts[0] is "Firebase" or "Bearer") visory.OfferToken(parts[1]);
    }

    private void OnSocketCreated(object? sender, CoreWebView2DevToolsProtocolEventReceivedEventArgs args)
    {
        using var json = JsonDocument.Parse(args.ParameterObjectAsJson);
        var id = json.RootElement.GetProperty("requestId").GetString();
        var url = json.RootElement.TryGetProperty("url", out var u) ? u.GetString() : null;
        if (id is null || url is null || !Uri.TryCreate(url.Replace("wss://", "https://", StringComparison.OrdinalIgnoreCase), UriKind.Absolute, out var https)) return;
        if (VisoryEndpoints.MetadataPolicy.IsAllowed(new UriBuilder(https) { Query = "" }.Uri, out _)) sockets.Add(id);
    }

    private void OnFrameReceived(object? sender, CoreWebView2DevToolsProtocolEventReceivedEventArgs args)
    {
        using var json = JsonDocument.Parse(args.ParameterObjectAsJson);
        var root = json.RootElement;
        var id = root.GetProperty("requestId").GetString();
        if (id is null || !sockets.Contains(id)) return;
        var response = root.GetProperty("response");
        if (response.TryGetProperty("opcode", out var opcode) && opcode.GetInt32() != 1) return;
        var payload = response.GetProperty("payloadData").GetString();
        if (payload is not null && session.ObserveWebSocketFrame(id, payload, DateTimeOffset.UtcNow) > 0) Changed();
    }

    private void OnFrameSent(object? sender, CoreWebView2DevToolsProtocolEventReceivedEventArgs args)
    {
        using var json = JsonDocument.Parse(args.ParameterObjectAsJson);
        var id = json.RootElement.GetProperty("requestId").GetString();
        if (id is null || !sockets.Contains(id)) return;
        var payload = json.RootElement.GetProperty("response").GetProperty("payloadData").GetString();
        if (payload is null || !payload.Contains("\"cred\"", StringComparison.Ordinal)) return;
        var match = Regex.Match(payload, "\"cred\"\\s*:\\s*\"(?<token>[A-Za-z0-9._-]{20,})\"");
        if (match.Success) visory.OfferToken(match.Groups["token"].Value);
    }

    private void Changed()
    {
        if (mode == ScanMode.Interactive && Web.CoreWebView2 is { } core) VisitedRoutes.Add(core.Source);
        UpdateCounter();
        progress?.Report(new DiscoveryProgress(session.Recordings.Count, session.CameraCount, CounterText.Text));
    }

    private void UpdateCounter()
    {
        CounterText.Text = Loc.F("ScanFound", session.Recordings.Count, session.CameraCount);
        var preview = session.ToResult(scanFinishedNormally: true);
        NoteText.Text = preview.Completeness == DiscoveryCompleteness.Incomplete && session.ObservedMetadataResponses > 0
            ? string.Join(" ", preview.Notes.Skip(1).Concat(preview.Errors.Take(2)))
            : string.Empty;
    }

    private async void OnAutoScroll(object sender, RoutedEventArgs e)
    {
        if (autoScroll is not null)
        {
            autoScroll.Cancel();
            return;
        }
        AutoScrollButton.IsChecked = true;
        try { await AutoScrollAsync(maxIdleRounds: 12); }
        finally { AutoScrollButton.IsChecked = false; }
    }

    /// <summary>Sends mouse-wheel input to the Flutter canvas until no new recordings appear.</summary>
    private async Task AutoScrollAsync(int maxIdleRounds)
    {
        if (Web.CoreWebView2 is not { } core) return;
        autoScroll = new CancellationTokenSource();
        var token = autoScroll.Token;
        try
        {
            var idle = 0;
            for (var round = 0; round < 600 && idle < maxIdleRounds && !token.IsCancellationRequested; round++)
            {
                var before = session.Recordings.Count;
                var x = Math.Max(10, (int)(Web.ActualWidth / 2));
                var y = Math.Max(10, (int)(Web.ActualHeight * 0.6));
                var payload = JsonSerializer.Serialize(new { type = "mouseWheel", x, y, deltaX = 0, deltaY = 720 });
                await core.CallDevToolsProtocolMethodAsync("Input.dispatchMouseEvent", payload);
                await Task.Delay(900, token);
                idle = session.Recordings.Count > before ? 0 : idle + 1;
            }
        }
        catch (OperationCanceledException)
        {
        }
        catch (COMException)
        {
        }
        finally
        {
            autoScroll.Dispose();
            autoScroll = null;
        }
    }

    private async Task RunUnattendedAsync()
    {
        try
        {
            await Task.Delay(TimeSpan.FromSeconds(8));
            await CaptureTokenAsync();
            if (mode == ScanMode.TokenOnly)
            {
                Finish(true);
                return;
            }
            var targets = routes.Count > 0 ? routes : [VisoryEndpoints.AppUrl];
            foreach (var route in targets)
            {
                if (finished) return;
                Web.CoreWebView2.Navigate(route);
                await Task.Delay(TimeSpan.FromSeconds(6));
                await AutoScrollAsync(maxIdleRounds: 6);
            }
            Finish(true);
        }
        catch (Exception ex) when (ex is COMException or InvalidOperationException or TaskCanceledException)
        {
            completion.TrySetException(ex);
            Close();
        }
    }

    private async Task CaptureTokenAsync()
    {
        if (Web.CoreWebView2 is not { } core) return;
        for (var attempt = 0; attempt < 10 && !finished; attempt++)
        {
            try
            {
                var result = await core.CallDevToolsProtocolMethodAsync("Runtime.evaluate", JsonSerializer.Serialize(new { expression = TokenScript, awaitPromise = true, returnByValue = true }));
                using var json = JsonDocument.Parse(result);
                if (json.RootElement.TryGetProperty("result", out var r) && r.TryGetProperty("value", out var value) && value.ValueKind == JsonValueKind.String)
                {
                    visory.OfferToken(value.GetString()!);
                    return;
                }
            }
            catch (Exception ex) when (ex is COMException or JsonException or InvalidOperationException)
            {
            }
            if (visory.CurrentToken() is not null) return;
            await Task.Delay(1500);
        }
    }

    private async void OnFinish(object sender, RoutedEventArgs e)
    {
        FinishButton.IsEnabled = false;
        autoScroll?.Cancel();
        await CaptureTokenAsync();
        Finish(true);
    }

    private void Finish(bool normally)
    {
        if (finished) return;
        finished = true;
        if (mode == ScanMode.Interactive && VisitedRoutes.Count > 0) visory.SaveRoutes(VisitedRoutes);
        completion.TrySetResult(session.ToResult(normally));
        Close();
    }

    private void OnCancel(object sender, RoutedEventArgs e)
    {
        finished = true;
        completion.TrySetCanceled();
        Close();
    }

    private void OnDiagnostics(object sender, RoutedEventArgs e)
    {
        var lines = new List<string> { $"Observed metadata responses: {session.ObservedMetadataResponses}", $"Recordings: {session.Recordings.Count}", $"Outstanding pages: {session.OutstandingPages}", $"Session token in memory: {(visory.CurrentToken() is null ? "no" : "yes")}" };
        lines.AddRange(session.Errors.Select(x => "Error: " + x));
        lines.AddRange(session.Diagnostics.Take(40));
        lines.AddRange(session.Recordings.Take(20).Select(r => $"{r.RecordingId} | {r.CameraId ?? r.CameraName} | {r.RecordedAt:O} ({r.RecordedAtSource}) | {r.Size} | {UrlPolicy.Redact(r.DownloadUrl)}"));
        System.Windows.MessageBox.Show(this, Core.Reporting.Redactor.Clean(string.Join("\n", lines)), Loc.T("Diagnostics"));
    }

    private void OnInstallWebView2(object sender, RoutedEventArgs e) => Ui.OpenUrl(WebView2Availability.InstallUrl);

    private void OnClosed(object? sender, EventArgs e)
    {
        autoScroll?.Cancel();
        if (!completion.Task.IsCompleted)
        {
            if (mode == ScanMode.Interactive) completion.TrySetCanceled();
            else completion.TrySetResult(session.ToResult(false));
        }
        Web.Dispose();
    }

    private static string? Header(CoreWebView2HttpResponseHeaders headers, string name)
    {
        try { return headers.Contains(name) ? headers.GetHeader(name) : null; }
        catch (ArgumentException) { return null; }
    }

    private static string? Header(CoreWebView2HttpRequestHeaders headers, string name)
    {
        try { return headers.Contains(name) ? headers.GetHeader(name) : null; }
        catch (ArgumentException) { return null; }
    }
}
