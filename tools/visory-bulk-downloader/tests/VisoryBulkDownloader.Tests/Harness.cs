using System.Diagnostics;
using System.Globalization;
using System.Security.Cryptography;
using VisoryBulkDownloader.Core;
using VisoryBulkDownloader.Core.Backup;
using VisoryBulkDownloader.Core.Download;
using VisoryBulkDownloader.Core.Infrastructure;
using VisoryBulkDownloader.Core.Providers;
using VisoryBulkDownloader.Core.Storage;

namespace VisoryBulkDownloader.Tests;

internal static class Assert
{
    public static void True(bool condition, string message)
    {
        if (!condition) throw new Exception("Assertion failed: " + message);
    }

    public static void Equal<T>(T expected, T actual, string message)
    {
        if (!EqualityComparer<T>.Default.Equals(expected, actual)) throw new Exception($"Assertion failed: {message} (expected '{expected}', actual '{actual}')");
    }

    public static async Task<T> Throws<T>(Func<Task> action, string message) where T : Exception
    {
        try { await action(); }
        catch (T e) { return e; }
        throw new Exception("Assertion failed: expected " + typeof(T).Name + ": " + message);
    }

    public static T Throws<T>(Action action, string message) where T : Exception
    {
        try { action(); }
        catch (T e) { return e; }
        throw new Exception("Assertion failed: expected " + typeof(T).Name + ": " + message);
    }

    public static async Task Until(Func<bool> condition, string message, int timeoutMs = 20000)
    {
        var watch = Stopwatch.StartNew();
        while (!condition())
        {
            if (watch.ElapsedMilliseconds > timeoutMs) throw new Exception("Timed out waiting: " + message);
            await Task.Delay(20);
        }
    }
}

internal sealed class FakeDisk(long available) : IDiskSpaceProbe
{
    public long Available = available;
    public long AvailableBytes(string path) => Interlocked.Read(ref Available);
}

/// <summary>Deterministic mock provider for isolated tests. Synthetic; never presented as real data.</summary>
internal sealed class MockProvider : IRecordingProvider
{
    public List<DiscoveredRecording> Items { get; } = [];
    public DiscoveryCompleteness Completeness { get; set; } = DiscoveryCompleteness.Complete;
    public Func<RecordingEntry, Uri>? Refresh { get; set; }
    public bool SessionValid { get; set; } = true;
    public int RefreshCount;
    public string ProviderId => "mock";
    public string DisplayName => "Mock (tests)";
    public bool IsSynthetic => true;
    public UrlPolicy MediaPolicy { get; } = new([], allowLoopbackHttpForTests: true);

    public Task<DiscoveryResult> DiscoverAsync(IProgress<DiscoveryProgress>? progress, CancellationToken cancellationToken) =>
        Task.FromResult(new DiscoveryResult(Items.ToList(), Completeness, ["mock"], Completeness == DiscoveryCompleteness.Incomplete ? ["page 3 not loaded"] : []));

    public Task<MediaRequest> GetMediaRequestAsync(RecordingEntry recording, bool refresh, CancellationToken cancellationToken)
    {
        if (refresh)
        {
            Interlocked.Increment(ref RefreshCount);
            if (!SessionValid) throw new ReauthenticationRequiredException("Visory session expired");
            if (Refresh is not null) return Task.FromResult(MediaRequest.Plain(Refresh(recording)));
        }
        return Task.FromResult(MediaRequest.Plain(new Uri(recording.LastUrl ?? throw new ProviderUnavailableException("No URL"))));
    }

    public HttpMessageHandler? CreateMediaHandler() => null;
}

internal sealed class Fixture : IAsyncDisposable
{
    private Fixture(string root)
    {
        Root = root;
        Layout = new BackupLayout(Path.Combine(root, "Visory Backup"));
    }

    public string Root { get; }
    public BackupLayout Layout { get; }
    public TestServer Server { get; } = new();
    public MockProvider Provider { get; } = new();
    public List<BackupCoordinator> Coordinators { get; } = [];

    public static Fixture Create()
    {
        var baseDir = Program.FixtureRoot;
        var root = Path.Combine(baseDir, Guid.NewGuid().ToString("N")[..12]);
        Directory.CreateDirectory(root);
        return new Fixture(root);
    }

    public string Sub(string name)
    {
        var path = Path.Combine(Root, name);
        Directory.CreateDirectory(path);
        return path;
    }

    public static EngineOptions FastOptions(int concurrency = 2, int attempts = 4) => new()
    {
        MaxConcurrency = concurrency,
        MaxAttempts = attempts,
        DelayScale = 0.01,
        IdleTimeout = TimeSpan.FromSeconds(5),
        UseBits = false,
        DiskReserveBytes = 0,
        NetworkPollInterval = TimeSpan.FromMilliseconds(50),
    };

    public BackupCoordinator Coordinator(EngineOptions? options = null, IDiskSpaceProbe? disk = null, IRecordingProvider? provider = null, AppSettings? settings = null, IBitsClient? bits = null)
    {
        var coordinator = new BackupCoordinator(Layout, provider ?? Provider, settings ?? new AppSettings { Integrity = IntegrityPolicy.Sha256 }, bits, disk ?? new FakeDisk(long.MaxValue / 4), null, options ?? FastOptions());
        Coordinators.Add(coordinator);
        return coordinator;
    }

    public DiscoveredRecording Recording(string id, string path, string camera = "cam1", DateTimeOffset? at = null, long? size = null, string name = "clip.mp4") => new()
    {
        RecordingId = id,
        CameraId = camera,
        CameraName = camera,
        RecordedAt = at ?? new DateTimeOffset(2026, 10, 26, 18, 40, 55, TimeSpan.Zero).AddMinutes(id.Sum(c => (int)c) % 1000),
        RecordedAtSource = "remote:createdAt",
        OriginalName = name,
        MediaType = "video/mp4",
        Size = size,
        DownloadUrl = Server.Url(path).AbsoluteUri,
    };

    public async Task<RunSummary> ScanAndRunAsync(BackupCoordinator coordinator, bool archive = false, CancellationToken cancellationToken = default)
    {
        await coordinator.ScanAsync(null, cancellationToken);
        return await coordinator.RunBackupAsync(new BackupRequest { CreateArchive = archive }, null, cancellationToken);
    }

    public string LogText(RunSummary summary) => File.ReadAllText(summary.LogPath!);
    public string DebugText(RunSummary summary) => File.ReadAllText(summary.DebugLogPath!);

    public async ValueTask DisposeAsync()
    {
        foreach (var c in Coordinators) c.Dispose();
        await Server.DisposeAsync();
        for (var i = 0; i < 10; i++)
        {
            try { if (Directory.Exists(Root)) Directory.Delete(Root, recursive: true); break; }
            catch (IOException) { await Task.Delay(100); }
            catch (UnauthorizedAccessException) { await Task.Delay(100); }
        }
    }
}

internal static partial class Program
{
    public static string FixtureRoot { get; private set; } = Path.Combine(Path.GetTempPath(), "vbd-tests");

    public static byte[] Data(int length, int seed)
    {
        var data = new byte[length];
        new Random(seed).NextBytes(data);
        return data;
    }

    public static string Sha(byte[] data) => Convert.ToHexString(SHA256.HashData(data)).ToLowerInvariant();

    public static string ShaFile(string path) => Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path))).ToLowerInvariant();

    private static async Task<int> Main(string[] args)
    {
        var repo = FindRepoRoot();
        FixtureRoot = Path.Combine(repo ?? Path.GetTempPath(), ".fixtures");
        Directory.CreateDirectory(FixtureRoot);
        var filter = args.FirstOrDefault(a => !a.StartsWith("--", StringComparison.Ordinal));
        var tests = AllTests().Where(t => filter is null || t.Name.Contains(filter, StringComparison.OrdinalIgnoreCase)).ToList();
        var failures = 0;
        var total = Stopwatch.StartNew();
        foreach (var (name, body) in tests)
        {
            var watch = Stopwatch.StartNew();
            try
            {
                var run = body();
                var finished = await Task.WhenAny(run, Task.Delay(TimeSpan.FromMinutes(name.Contains("HEAVY", StringComparison.Ordinal) ? 30 : 2)));
                if (finished != run) throw new TimeoutException("Test exceeded its time limit");
                await run;
                Console.WriteLine($"PASS  {name} ({watch.ElapsedMilliseconds} ms)");
            }
            catch (SkipException skip)
            {
                Console.WriteLine($"SKIP  {name}: {skip.Message}");
            }
            catch (Exception e)
            {
                failures++;
                Console.WriteLine($"FAIL  {name} ({watch.ElapsedMilliseconds} ms): {e.GetType().Name}: {e.Message}");
                Console.WriteLine(e.StackTrace?.Split('\n').Take(6).Aggregate("", (a, b) => a + "        " + b.Trim() + "\n"));
            }
        }
        Console.WriteLine($"{tests.Count - failures}/{tests.Count} passed in {total.Elapsed.TotalSeconds:0.0}s");
        try { if (Directory.Exists(FixtureRoot) && !Directory.EnumerateFileSystemEntries(FixtureRoot).Any()) Directory.Delete(FixtureRoot); } catch (IOException) { }
        return failures == 0 ? 0 : 1;
    }

    private static string? FindRepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "VisoryBulkDownloader.sln"))) dir = dir.Parent;
        return dir?.FullName;
    }
}

internal sealed class SkipException(string message) : Exception(message);
