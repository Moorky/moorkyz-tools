using System.Globalization;
using System.IO.Compression;
using System.Text;
using System.Text.RegularExpressions;
using MoorkszVisoryMediaDownloader.Core;
using MoorkszVisoryMediaDownloader.Core.Archive;
using MoorkszVisoryMediaDownloader.Core.Backup;
using MoorkszVisoryMediaDownloader.Core.Download;
using MoorkszVisoryMediaDownloader.Core.Infrastructure;
using MoorkszVisoryMediaDownloader.Core.Localization;
using MoorkszVisoryMediaDownloader.Core.Providers;
using MoorkszVisoryMediaDownloader.Core.Reporting;
using MoorkszVisoryMediaDownloader.Core.Storage;

namespace MoorkszVisoryMediaDownloader.Tests;

internal static partial class Program
{
    private static readonly Regex SuccessLine = new(@"^\[\d{4}-\d{2}-\d{2} \d{2}:\d{2}:\d{2}\.\d{3} [+-]\d{2}:\d{2}\] \[SUCCESS\] @(?<file>[^|]+) \| Time: \d+\.\d{2}s \| Filesize: (?<size>[\d.]+ (B|KB|MB|GB|TB)) \| Current Average: \d+\.\d{2}s / [\d.]+ (B|KB|MB|GB|TB) \| Avg Speed: [\d.]+ (B|KB|MB|GB|TB)/s \| Progress: (?<done>\d+)/(?<total>\d+) \| Run: [^|]+ \| Area: backup<a id=""L\d+""></a>\r?$", RegexOptions.Multiline);
    private static readonly Regex FailLine = new(@"^\[\d{4}-\d{2}-\d{2} \d{2}:\d{2}:\d{2}\.\d{3} [+-]\d{2}:\d{2}\] \[FAIL\] @(?<file>[^|]+) \| Added to failed report \| (?<message>[^\r\n]+?) \| Run: [^|]+ \| Area: backup<a id=""L\d+""></a>\r?$", RegexOptions.Multiline);

    private static IEnumerable<(string Name, Func<Task> Body)> AllTests() =>
    [
        ("01 fresh launch and initial configuration", FreshLaunch),
        ("02 successful mock cloud scan", MockScan),
        ("03 multiple recording pages (Visory/Firebase shapes)", MultiplePages),
        ("04 incomplete inventory detection", IncompleteInventory),
        ("05 new file download", NewDownload),
        ("06 pre-existing verified file skipped", ExistingSkipped),
        ("07 same filename, different recording ID", SameNameDifferentId),
        ("08 network interruption waits for Windows", NetworkInterruption),
        ("09 partial transfer resumed with validated range", PartialResume),
        ("10 server rejects or ignores Range", RangeRejected),
        ("11 HTTP 429 with Retry-After", RetryAfter),
        ("12 expired download URL refreshed", ExpiredUrl),
        ("13 HTTP 403 requiring renewed sign-in", ReauthRequired),
        ("14 HTTP 404 permanent failure", NotFound),
        ("15 HTTP 500 retry exhaustion", ServerErrorExhaustion),
        ("16 unknown file size", UnknownSize),
        ("17 unexpected connection termination", ConnectionTermination),
        ("18 corrupt file detected in a later run", CorruptLaterRun),
        ("19 no available disk space", DiskFull),
        ("20 pause and resume", PauseResume),
        ("21 app restart resumes the interrupted run", AppRestart),
        ("22 concurrent downloads", Concurrent),
        ("23 CSV escaping and report correctness", CsvReports),
        ("24 one final status entry per file per run", OneFinalPerRun),
        ("25 ZIP64 by entry count", Zip64Entries),
        ("25b ZIP64 file larger than 4 GiB (HEAVY, opt-in)", Zip64LargeFile),
        ("26 archive corruption detection", ArchiveCorruption),
        ("28 WebView2 missing-runtime handling", WebView2Missing),
        ("29 German and English UI strings", Localization),
        ("32 large inventory performance (100,000 recordings)", LargeInventory),
        ("S1 URL policy, redirects and redaction", UrlPolicyAndRedaction),
        ("S2 path safety and naming", PathSafetyAndNaming),
        ("S3 remote MD5 digest verification", RemoteDigest),
        ("S4 demo mode end-to-end with verified archive", DemoEndToEnd),
        ("S5 Windows BITS transfer against loopback server", BitsTransfer),
        ("S6 settings, run IDs and scheduling arguments", SettingsAndScheduling),
        ("D1 durable per-line Markdown diagnostics", DurableDiagnostics),
        ("D2 CSV report embedded in HTML and Markdown", ReportViews),
        ("D3 diagnostics survive abrupt process termination", AbruptDiagnostics),
        ("D4 legacy CSV resume and invalid report handling", LegacyReports),
        ("D5 archive errors and whole-run duration reports", ArchiveErrorReports),
        ("D6 daily report schema, statistics and exact log links", DailyReports),
    ];

    private static async Task FreshLaunch()
    {
        await using var f = Fixture.Create();
        var exeDir = f.Sub("portable");
        var localAppData = f.Sub("localappdata");
        var locations = DataLocations.Resolve(exeDir, localAppData);
        Assert.True(locations.Portable, "writable EXE folder enables portable mode");
        Assert.Equal(Path.Combine(exeDir, "VisoryBulkDownloader Data"), locations.AppRoot, "rename retains existing portable settings");
        Assert.Equal(Path.Combine(localAppData, "VisoryBulkDownloader", "Sessions", DataLocations.Hash(locations.AppRoot)), locations.SessionRoot, "rename retains the protected session location");
        Assert.Equal("VisoryBulkDownloader-" + DataLocations.Hash(locations.AppRoot), Scheduling.TaskName(locations.AppRoot), "rename retains existing scheduled-task keys");
        Assert.True(locations.AppRoot.StartsWith(exeDir, StringComparison.OrdinalIgnoreCase), "portable data beside EXE");
        Assert.True(locations.SessionRoot.StartsWith(localAppData, StringComparison.OrdinalIgnoreCase), "session never stored beside a portable EXE");
        var settingsStore = new SettingsStore(locations.SettingsPath);
        var defaults = settingsStore.Load();
        Assert.Equal(PriorityMode.SmartBackground, defaults.Priority, "default priority");
        Assert.True(defaults.AutoArchive && !defaults.StoreFullFailedUrls && defaults.Filter.IncludeUnknownDates, "safe defaults");
        Assert.Equal(4, defaults.MaxAttempts, "default attempts");
        settingsStore.Save(defaults with { MaxConcurrency = 99, Language = "de", ScheduleTime = "7:5" });
        var reloaded = settingsStore.Load();
        Assert.Equal(8, reloaded.MaxConcurrency, "concurrency clamped");
        Assert.Equal("de", reloaded.Language, "language persisted");
        File.WriteAllText(locations.SettingsPath, "{ broken");
        var recovered = settingsStore.Load();
        Assert.Equal(4, recovered.MaxAttempts, "invalid settings fall back to defaults");
        Assert.True(Directory.GetFiles(locations.AppRoot, "settings.json.invalid-*").Length == 1, "invalid settings preserved, not deleted");
        f.Layout.Ensure();
        foreach (var dir in new[] { f.Layout.Downloads, f.Layout.Archives, f.Layout.Runs, f.Layout.State }) Assert.True(Directory.Exists(dir), "layout folder " + dir);
        Assert.Throws<ArgumentException>(() => new BackupLayout(Path.GetPathRoot(f.Root)!), "drive root rejected");
    }

    private static async Task MockScan()
    {
        await using var f = Fixture.Create();
        f.Provider.Items.AddRange([f.Recording("a", "/a.mp4", "cam1"), f.Recording("b", "/b.mp4", "cam1"), f.Recording("c", "/c.mp4", "cam2"), f.Recording("d", "/d.mp4", "cam2", size: 10), f.Recording("e", "/e.mp4", "cam2")]);
        var coordinator = f.Coordinator();
        var inventory = await coordinator.ScanAsync(null, CancellationToken.None);
        Assert.Equal(5, inventory.Discovered, "discovered");
        Assert.Equal(2, inventory.Cameras, "cameras");
        Assert.Equal(5, inventory.New, "new recordings");
        Assert.Equal(DiscoveryCompleteness.Complete, inventory.Completeness, "completeness");
        Assert.Equal(4, inventory.UnknownSizeSelected, "unknown sizes are counted, not estimated as zero");
        f.Provider.Items[0] = f.Provider.Items[0] with { Size = 1234 };
        inventory = await coordinator.ScanAsync(null, CancellationToken.None);
        Assert.Equal(5, inventory.Discovered, "rescans deduplicate by recording ID");
        Assert.Equal(1234L, coordinator.Store.Get("mock:a")!.RemoteSize, "metadata updated on rescan");
    }

    private static Task MultiplePages()
    {
        var now = DateTimeOffset.UtcNow;
        var bucket = VisoryEndpoints.StorageBucket;
        var session = new DiscoverySession();
        var list = new Uri($"https://firebasestorage.googleapis.com/v0/b/{bucket}/o?prefix=recordings%2Fu1%2F");
        var page1 = $$"""{"prefixes":[],"items":[{"name":"recordings/u1/cameras/camA/2026-10-26_18-40-55.webm","bucket":"{{bucket}}"},{"name":"recordings/u1/cameras/camA/2026-10-26_18-45-00.webm","bucket":"{{bucket}}"},{"name":"recordings/u1/cameras/camA/thumb.jpg","bucket":"{{bucket}}"}],"nextPageToken":"tok2"}""";
        Assert.Equal(2, session.ObserveHttpJson(list, page1, now), "page 1 media items (images ignored)");
        Assert.Equal(DiscoveryCompleteness.Incomplete, session.ToResult(true).Completeness, "announced page not yet loaded");
        var page2 = $$"""{"items":[{"name":"recordings/u1/cameras/camA/2026-10-26_19-00-00.webm","bucket":"{{bucket}}","contentType":"video/webm","size":"4096","timeCreated":"2026-10-26T18:00:01.000Z","md5Hash":"abc==","downloadTokens":"tok-1,tok-2"}]}""";
        Assert.Equal(1, session.ObserveHttpJson(new Uri(list + "&pageToken=tok2"), page2, now), "page 2");
        var result = session.ToResult(true);
        Assert.Equal(DiscoveryCompleteness.Uncertain, result.Completeness, "all pages loaded but Visory gives no total");
        Assert.Equal(3, result.Recordings.Count, "three recordings");
        var first = result.Recordings.Single(r => r.RecordingId.EndsWith("18-40-55.webm", StringComparison.Ordinal));
        Assert.Equal("camA", first.CameraId, "camera from path");
        Assert.Equal("filename (time zone assumed local)", first.RecordedAtSource, "date provenance recorded");
        var withMeta = result.Recordings.Single(r => r.Size == 4096);
        Assert.True(withMeta.DownloadUrl!.Contains("token=tok-1", StringComparison.Ordinal) && withMeta.DownloadUrl.Contains("alt=media", StringComparison.Ordinal), "download URL from storage metadata");
        Assert.Equal("remote:storage.timeCreated (upload time)", withMeta.RecordedAtSource, "upload time labelled");

        var rtdb = new DiscoverySession();
        var frame = """{"t":"d","d":{"a":"d","b":{"p":"users/u1/cameras/camB/recordings","d":{"-NxA1":{"createdAt":1793039600000,"size":1234567,"url":"https://firebasestorage.googleapis.com/v0/b/BUCKET/o/rec%2FcamB%2Fa.mp4?alt=media&token=abc"},"-NxA2":{"createdAt":1793039700000,"url":"https://evil.example/x.mp4"}}}}}""".Replace("BUCKET", bucket, StringComparison.Ordinal);
        Assert.Equal(0, rtdb.ObserveWebSocketFrame("s1", "2", now), "multi-frame header");
        Assert.Equal(0, rtdb.ObserveWebSocketFrame("s1", frame[..40], now), "frame part 1");
        Assert.Equal(1, rtdb.ObserveWebSocketFrame("s1", frame[40..], now), "reassembled frame yields one safe recording");
        var rec = rtdb.Recordings.Single();
        Assert.Equal("-NxA1", rec.RecordingId, "RTDB key used as ID");
        Assert.Equal("camB", rec.CameraId, "camera from RTDB path");
        Assert.Equal(1234567L, rec.Size, "size");
        Assert.Equal(DateTimeOffset.FromUnixTimeMilliseconds(1793039600000), rec.RecordedAt, "epoch milliseconds");
        Assert.True(rtdb.Diagnostics.Any(d => d.Contains("evil.example", StringComparison.Ordinal) && !d.Contains("token", StringComparison.Ordinal)), "unsafe URL ignored and logged redacted");
        Assert.Equal(0, rtdb.ObserveMediaResponse(new Uri($"https://firebasestorage.googleapis.com/v0/b/{bucket}/o/rec%2FcamB%2Fa.mp4?alt=media&token=abc"), "video/mp4", 1234567, 200, now), "media response merged by object path");
        Assert.Equal(1, rtdb.Recordings.Count, "no duplicate from media response");

        var firestore = new DiscoverySession();
        var doc = """{"documents":[{"name":"projects/visory-54ab5/databases/(default)/documents/users/u1/recordings/rec9","fields":{"cameraName":{"stringValue":"Garden"},"timestamp":{"timestampValue":"2026-10-20T10:00:00Z"},"storagePath":{"stringValue":"recordings/u1/garden/rec9.mp4"},"fileSize":{"integerValue":"999"}}}]}""";
        Assert.Equal(1, firestore.ObserveHttpJson(new Uri("https://firestore.googleapis.com/v1/projects/visory-54ab5/databases/(default)/documents/users/u1/recordings"), doc, now), "firestore document");
        var fsRec = firestore.Recordings.Single();
        Assert.Equal("rec9", fsRec.RecordingId, "document ID");
        Assert.Equal("Garden", fsRec.CameraName, "camera name");
        Assert.Equal(999L, fsRec.Size, "integerValue size");
        Assert.True(!session.ShouldInspect(new Uri("https://eu.i.posthog.com/e/"), "application/json", 10), "analytics origin never inspected");
        Assert.True(session.ShouldInspect(new Uri($"https://{VisoryEndpoints.RealtimeDatabaseHost}/users/u1.json"), "application/json", 10), "RTDB REST inspected");
        Assert.True(!session.ShouldInspect(list, "application/json", 50_000_000), "oversized bodies not buffered");
        Assert.True(!session.ShouldInspect(new Uri($"https://firebasestorage.googleapis.com/v0/b/other-bucket/o"), "application/json", 10), "other buckets rejected");
        return Task.CompletedTask;
    }

    private static async Task IncompleteInventory()
    {
        var session = new DiscoverySession();
        session.ObserveHttpJson(new Uri($"https://firebasestorage.googleapis.com/v0/b/{VisoryEndpoints.StorageBucket}/o"),
            $$"""{"items":[{"name":"a/cameras/c1/x.mp4","bucket":"{{VisoryEndpoints.StorageBucket}}"}],"nextPageToken":"more"}""", DateTimeOffset.UtcNow);
        var result = session.ToResult(true);
        Assert.Equal(DiscoveryCompleteness.Incomplete, result.Completeness, "outstanding page token");
        Assert.True(result.Notes.Any(n => n.Contains("never loaded", StringComparison.Ordinal)), "explains incomplete scan");
        var empty = new DiscoverySession().ToResult(true);
        Assert.Equal(DiscoveryCompleteness.Incomplete, empty.Completeness, "no observed metadata is not a successful empty scan");
        var errored = new DiscoverySession();
        errored.ObserveError(new Uri($"https://{VisoryEndpoints.RealtimeDatabaseHost}/x.json"), 403);
        Assert.Equal(DiscoveryCompleteness.Incomplete, errored.ToResult(true).Completeness, "metadata errors mark incomplete");

        await using var f = Fixture.Create();
        var data = Data(50_000, 4);
        f.Server.MapMedia("/i.mp4", data);
        f.Provider.Items.Add(f.Recording("i", "/i.mp4"));
        f.Provider.Completeness = DiscoveryCompleteness.Incomplete;
        var summary = await f.ScanAndRunAsync(f.Coordinator());
        Assert.Equal(1, summary.Succeeded, "discovered recording downloaded");
        Assert.Equal(RunState.CompletedWithErrors, summary.State, "never a full success with incomplete inventory");
        Assert.True(f.LogText(summary).Contains("[WARN] Cloud inventory not confirmed complete", StringComparison.Ordinal), "warning logged");
        f.Provider.Completeness = DiscoveryCompleteness.Uncertain;
        var second = await f.ScanAndRunAsync(f.Coordinator());
        Assert.Equal(RunState.Completed, second.State, "uncertain inventory with no failures");
        Assert.True(second.Outcome.Contains("could not be confirmed", StringComparison.Ordinal), "outcome states the uncertainty");
    }

    private static async Task NewDownload()
    {
        await using var f = Fixture.Create();
        var data = Data(300_000, 1);
        f.Server.MapMedia("/new.mp4", data);
        f.Provider.Items.Add(f.Recording("new", "/new.mp4", "Front Door", new DateTimeOffset(2026, 10, 26, 17, 40, 55, TimeSpan.Zero)));
        var coordinator = f.Coordinator();
        var summary = await f.ScanAndRunAsync(coordinator);
        Assert.Equal(1, summary.Succeeded, "succeeded");
        var entry = coordinator.Store.Get("mock:new")!;
        Assert.Equal(RecordingStatus.Verified, entry.Status, "verified");
        var path = f.Layout.DestinationPath(entry.Destination!);
        Assert.Equal(Sha(data), ShaFile(path), "bytes intact");
        Assert.Equal(Sha(data), entry.Sha256, "hash recorded");
        Assert.True(Path.GetFileName(path).StartsWith("Front Door_2026-10-26_", StringComparison.Ordinal), "camera_time naming: " + Path.GetFileName(path));
        Assert.True(Directory.GetFiles(f.Layout.Downloads, "*.part", SearchOption.AllDirectories).Length == 0, "no partial files remain");
        var log = f.LogText(summary);
        var match = SuccessLine.Match(log);
        Assert.True(match.Success, "SUCCESS line follows the contract:\n" + log);
        Assert.Equal("292.97 KB", match.Groups["size"].Value, "binary units");
        Assert.Equal("1/1", match.Groups["done"].Value + "/" + match.Groups["total"].Value, "progress");
        var report = Csv.Parse(File.ReadAllText(summary.CsvReportPath!));
        Assert.Equal(RunLogger.ReportColumns.Length, report[0].Count, "report header");
        Assert.True(report.Any(r => r.Contains("SUCCESS") && r.Contains(Sha(data))), "CSV row with hash");
        Assert.True(File.Exists(summary.SummaryPath!) && File.Exists(summary.DebugLogPath!) && File.Exists(summary.FailedReportPath!), "all report files present");
        Assert.True(summary.ReportsFolder!.Contains(Path.Combine("runs", summary.RunId), StringComparison.Ordinal), "runs/<id> layout");
        Assert.True(Regex.IsMatch(summary.RunId, @"^\d{4}-\d{2}-\d{2}_\d{2}-\d{2}-\d{2}(_\d+)?$"), "run ID format");
    }

    private static async Task ExistingSkipped()
    {
        await using var f = Fixture.Create();
        var data = Data(120_000, 2);
        f.Server.MapMedia("/s.mp4", data);
        f.Provider.Items.Add(f.Recording("s", "/s.mp4"));
        var coordinator = f.Coordinator();
        await f.ScanAndRunAsync(coordinator);
        var gets = f.Server.RequestsFor("/s.mp4").Count(r => r.Method == "GET");
        var second = await f.ScanAndRunAsync(coordinator);
        Assert.Equal(0, second.Succeeded, "nothing downloaded");
        Assert.Equal(1, second.AlreadyVerified, "already verified");
        Assert.Equal(gets, f.Server.RequestsFor("/s.mp4").Count(r => r.Method == "GET"), "no new network request");
        Assert.True(f.LogText(second).Contains("[SKIP] @", StringComparison.Ordinal), "SKIP logged");
        Assert.True(second.RunId != Directory.GetDirectories(f.Layout.Runs).Select(Path.GetFileName).First(id => id != second.RunId), "distinct run IDs");
    }

    private static async Task SameNameDifferentId()
    {
        await using var f = Fixture.Create();
        var at = new DateTimeOffset(2026, 10, 26, 18, 0, 0, TimeSpan.Zero);
        var a = Data(40_000, 3);
        var b = Data(41_000, 4);
        f.Server.MapMedia("/x1.mp4", a);
        f.Server.MapMedia("/x2.mp4", b);
        f.Provider.Items.AddRange([f.Recording("id-1", "/x1.mp4", "cam", at), f.Recording("id-2", "/x2.mp4", "cam", at)]);
        var coordinator = f.Coordinator();
        var summary = await f.ScanAndRunAsync(coordinator);
        Assert.Equal(2, summary.Succeeded, "both downloaded");
        var e1 = coordinator.Store.Get("mock:id-1")!;
        var e2 = coordinator.Store.Get("mock:id-2")!;
        Assert.True(!string.Equals(e1.Destination, e2.Destination, StringComparison.OrdinalIgnoreCase), "distinct destinations");
        Assert.Equal(Sha(a), ShaFile(f.Layout.DestinationPath(e1.Destination!)), "first content");
        Assert.Equal(Sha(b), ShaFile(f.Layout.DestinationPath(e2.Destination!)), "second content");
    }

    private static async Task NetworkInterruption()
    {
        await using var f = Fixture.Create();
        var data = Data(60_000, 5);
        var hits = 0;
        f.Server.Map("/n.mp4", ctx =>
        {
            if (Interlocked.Increment(ref hits) == 1) { ctx.Abort(); return Task.CompletedTask; }
            return ctx.ServeMediaAsync(data, new MediaOptions());
        });
        f.Provider.Items.Add(f.Recording("n", "/n.mp4"));
        var networkChecks = 0;
        var options = Fixture.FastOptions() with { NetworkAvailable = () => Interlocked.Increment(ref networkChecks) > 3 };
        var summary = await f.ScanAndRunAsync(f.Coordinator(options));
        Assert.Equal(1, summary.Succeeded, "succeeded after network returned");
        var log = f.LogText(summary);
        Assert.True(log.Contains("[WARN] Network connection lost | Waiting for Windows", StringComparison.Ordinal), "waiting logged");
        Assert.True(log.Contains("Network connection restored", StringComparison.Ordinal), "restore logged");
        Assert.Equal(0, summary.RetryAttempts, "offline wait does not consume attempts");
    }

    private static async Task PartialResume()
    {
        await using var f = Fixture.Create();
        var data = Data(2_000_000, 6);
        var options = new MediaOptions { AbortAfterBytes = 700_000, ETag = "\"v1\"" };
        f.Server.MapMedia("/r.mp4", data, options);
        f.Provider.Items.Add(f.Recording("r", "/r.mp4"));
        var summary = await f.ScanAndRunAsync(f.Coordinator());
        Assert.Equal(1, summary.Succeeded, "resumed download succeeded");
        var gets = f.Server.RequestsFor("/r.mp4").Where(r => r.Method == "GET").ToList();
        Assert.True(gets.Count >= 2, "retried");
        var range = gets[1].Header("Range");
        Assert.True(range is not null && range.StartsWith("bytes=", StringComparison.Ordinal) && !range.StartsWith("bytes=0-", StringComparison.Ordinal), "second request continued from offset: " + range);
        Assert.Equal("\"v1\"", gets[1].Header("If-Range"), "If-Range validator sent");
        Assert.True(f.DebugText(summary).Contains("resumed validated byte range", StringComparison.Ordinal), "resume recorded in debug log");
        var entry = f.Coordinators[0].Store.Get("mock:r")!;
        Assert.Equal(Sha(data), entry.Sha256, "resumed file hash equals original");
    }

    private static async Task RangeRejected()
    {
        await using var f = Fixture.Create();
        var data = Data(900_000, 7);
        f.Server.MapMedia("/norange.mp4", data, new MediaOptions { AcceptRanges = false, AbortAfterBytes = 300_000 });
        f.Server.MapMedia("/ignored.mp4", data, new MediaOptions { IgnoreRange = true, AbortAfterBytes = 300_000 });
        f.Provider.Items.AddRange([f.Recording("nr", "/norange.mp4"), f.Recording("ig", "/ignored.mp4")]);
        var coordinator = f.Coordinator(Fixture.FastOptions(concurrency: 1));
        var summary = await f.ScanAndRunAsync(coordinator);
        Assert.Equal(2, summary.Succeeded, "both completed from zero");
        var noRange = f.Server.RequestsFor("/norange.mp4").Where(r => r.Method == "GET").ToList();
        Assert.True(noRange.Count >= 2 && noRange.All(r => r.Header("Range") is null), "no resume without Accept-Ranges");
        var ignored = f.Server.RequestsFor("/ignored.mp4").Where(r => r.Method == "GET").ToList();
        Assert.True(ignored[1].Header("Range") is not null, "range was requested");
        Assert.Equal(Sha(data), coordinator.Store.Get("mock:ig")!.Sha256, "200 response replaced partial data safely");
        Assert.Equal(Sha(data), coordinator.Store.Get("mock:nr")!.Sha256, "restart from zero intact");
    }

    private static async Task RetryAfter()
    {
        await using var f = Fixture.Create();
        var data = Data(30_000, 8);
        var hits = 0;
        f.Server.Map("/ra.mp4", ctx => Interlocked.Increment(ref hits) == 1
            ? ctx.StatusAsync(429, new() { ["Retry-After"] = "3" })
            : ctx.ServeMediaAsync(data, new MediaOptions()));
        f.Provider.Items.Add(f.Recording("ra", "/ra.mp4"));
        var delays = new List<TimeSpan>();
        var options = Fixture.FastOptions() with { RetryDelayObserver = (_, d) => { lock (delays) delays.Add(d); } };
        var summary = await f.ScanAndRunAsync(f.Coordinator(options));
        Assert.Equal(1, summary.Succeeded, "succeeded after throttling");
        Assert.Equal(TimeSpan.FromSeconds(3) * 0.01, delays.Single(), "Retry-After honored (scaled for tests)");
        Assert.True(f.LogText(summary).Contains("[RETRY] @", StringComparison.Ordinal) && f.LogText(summary).Contains("HTTP 429", StringComparison.Ordinal), "RETRY logged");
    }

    private static async Task ExpiredUrl()
    {
        await using var f = Fixture.Create();
        var data = Data(80_000, 9);
        f.Server.Map("/e.mp4", ctx => ctx.QueryValue("token") == "fresh" ? ctx.ServeMediaAsync(data, new MediaOptions()) : ctx.StatusAsync(403));
        f.Provider.Items.Add(f.Recording("e", "/e.mp4?token=stale") with { DownloadUrl = f.Server.Url("/e.mp4?token=stale").AbsoluteUri });
        f.Provider.Refresh = _ => f.Server.Url("/e.mp4?token=fresh");
        var coordinator = f.Coordinator();
        var summary = await f.ScanAndRunAsync(coordinator);
        Assert.Equal(1, summary.Succeeded, "succeeded with a refreshed URL");
        Assert.Equal(1, f.Provider.RefreshCount, "one refresh");
        Assert.True(f.LogText(summary).Contains("Refreshing authorization", StringComparison.Ordinal), "refresh logged");
        Assert.True(coordinator.Store.Get("mock:e")!.LastUrl!.Contains("fresh", StringComparison.Ordinal), "fresh URL persisted (DPAPI protected)");
        foreach (var dbFile in Directory.GetFiles(f.Layout.State, "visory-backup.db*"))
        {
            using var shared = new FileStream(dbFile, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
            using var reader = new StreamReader(shared, Encoding.Latin1);
            Assert.True(!reader.ReadToEnd().Contains("token=", StringComparison.Ordinal), "signed URL not stored in plaintext in " + Path.GetFileName(dbFile));
        }
    }

    private static async Task ReauthRequired()
    {
        await using var f = Fixture.Create();
        var data = Data(80_000, 10);
        f.Server.Map("/auth.mp4", ctx => ctx.QueryValue("token") == "renewed" ? ctx.ServeMediaAsync(data, new MediaOptions()) : ctx.StatusAsync(403));
        f.Provider.Items.Add(f.Recording("auth", "/auth.mp4") with { DownloadUrl = f.Server.Url("/auth.mp4?token=expired").AbsoluteUri });
        f.Provider.SessionValid = false;
        f.Provider.Refresh = _ => f.Server.Url("/auth.mp4?token=renewed");
        var coordinator = f.Coordinator();
        var expired = new TaskCompletionSource();
        coordinator.SessionExpired += _ => expired.TrySetResult();
        await coordinator.ScanAsync(null, CancellationToken.None);
        var run = coordinator.RunBackupAsync(new BackupRequest { CreateArchive = false }, null, CancellationToken.None);
        await expired.Task.WaitAsync(TimeSpan.FromSeconds(20));
        Assert.Equal(EngineState.PausedSignInRequired, coordinator.Engine!.State, "paused for sign-in instead of failing");
        f.Provider.SessionValid = true;
        coordinator.Resume();
        var summary = await run;
        Assert.Equal(1, summary.Succeeded, "continued after re-authentication");
        Assert.Equal(0, summary.Failed, "no failure recorded");
        Assert.True(f.LogText(summary).Contains("Visory sign-in required", StringComparison.Ordinal), "guidance logged");
    }

    private static async Task NotFound()
    {
        await using var f = Fixture.Create();
        f.Server.Map("/gone.mp4", ctx => ctx.StatusAsync(404));
        f.Provider.Items.Add(f.Recording("gone", "/gone.mp4") with { DownloadUrl = f.Server.Url("/gone.mp4?sig=SECRET123").AbsoluteUri, Size = 5000 });
        var summary = await f.ScanAndRunAsync(f.Coordinator());
        Assert.Equal(1, summary.Failed, "failed");
        Assert.Equal(1, f.Server.RequestsFor("/gone.mp4").Count(r => r.Method == "GET"), "404 is not retried");
        var match = FailLine.Match(f.LogText(summary));
        Assert.True(match.Success, "FAIL line follows the contract");
        Assert.Equal("HTTP 404: Recording not found on server", match.Groups["message"].Value.Trim(), "message");
        var failed = Csv.Parse(File.ReadAllText(summary.CsvReportPath!));
        Assert.Equal("5000", failed[1][7], "file size");
        Assert.True(failed[1][19].EndsWith("/gone.mp4?[REDACTED]", StringComparison.Ordinal), "URL redacted by default");
        Assert.True(File.ReadAllText(summary.FailedReportPath!).Contains("[FAIL]", StringComparison.Ordinal), "failure also saved to Markdown error log");
        foreach (var file in Directory.GetFiles(summary.ReportsFolder!, "*", SearchOption.AllDirectories))
            Assert.True(!File.ReadAllText(file).Contains("SECRET123", StringComparison.Ordinal), "secret absent from " + file);
    }

    private static async Task ServerErrorExhaustion()
    {
        await using var f = Fixture.Create();
        f.Server.Map("/err.mp4", ctx => ctx.StatusAsync(500));
        f.Provider.Items.Add(f.Recording("err", "/err.mp4"));
        var coordinator = f.Coordinator();
        var summary = await f.ScanAndRunAsync(coordinator);
        Assert.Equal(1, summary.Failed, "failed after exhausting retries");
        Assert.Equal(4, f.Server.RequestsFor("/err.mp4").Count(r => r.Method == "GET"), "four attempts");
        Assert.Equal(4, coordinator.Store.Attempts(summary.RunId).Count, "attempt history persisted");
        Assert.Equal(3, Regex.Matches(f.LogText(summary), @"\[RETRY\]").Count, "three RETRY lines");
        Assert.Equal(4, summary.RetryAttempts, "failed attempts counted");
        Assert.Equal(1, summary.FilesRequiringRetries, "files requiring retries");
        Assert.Equal(RunState.CompletedWithErrors, summary.State, "completed with errors");
    }

    private static async Task UnknownSize()
    {
        await using var f = Fixture.Create();
        var data = Data(150_000, 11);
        f.Server.MapMedia("/u.mp4", data, new MediaOptions { SendLength = false, AcceptRanges = false, ETag = null });
        f.Server.Map("/u-fail.mp4", ctx => ctx.StatusAsync(404));
        f.Provider.Items.AddRange([f.Recording("u", "/u.mp4"), f.Recording("uf", "/u-fail.mp4")]);
        var coordinator = f.Coordinator();
        var summary = await f.ScanAndRunAsync(coordinator);
        Assert.Equal(1, summary.Succeeded, "unknown-length download");
        Assert.Equal((long)data.Length, coordinator.Store.Get("mock:u")!.ActualSize, "actual size recorded");
        var log = f.LogText(summary);
        Assert.True(log.Contains("Filesize: 146.48 KB", StringComparison.Ordinal), "real size shown");
        Assert.True(!log.Contains("Filesize: 0 B", StringComparison.Ordinal), "no invented zero size");
        var failed = Csv.Parse(File.ReadAllText(summary.CsvReportPath!));
        Assert.Equal("", failed.Single(r => r[5] == "FAIL")[7], "unknown failed size is empty, not 0");
    }

    private static async Task ConnectionTermination()
    {
        await using var f = Fixture.Create();
        var data = Data(600_000, 12);
        f.Server.MapMedia("/t.mp4", data, new MediaOptions { AbortAfterBytes = 200_000, ETag = null });
        f.Provider.Items.Add(f.Recording("t", "/t.mp4"));
        var summary = await f.ScanAndRunAsync(f.Coordinator());
        Assert.Equal(1, summary.Succeeded, "succeeded after termination");
        Assert.True(f.DebugText(summary).Contains("Server closed connection unexpectedly", StringComparison.Ordinal), "termination classified");
        var gets = f.Server.RequestsFor("/t.mp4").Where(r => r.Method == "GET").ToList();
        Assert.True(gets.Skip(1).All(r => r.Header("Range") is null), "no unvalidated resume without an ETag/Last-Modified");
    }

    private static async Task CorruptLaterRun()
    {
        await using var f = Fixture.Create();
        var data = Data(70_000, 13);
        f.Server.MapMedia("/c.mp4", data);
        f.Provider.Items.Add(f.Recording("c", "/c.mp4"));
        var coordinator = f.Coordinator();
        await f.ScanAndRunAsync(coordinator);
        var path = f.Layout.DestinationPath(coordinator.Store.Get("mock:c")!.Destination!);
        var bytes = File.ReadAllBytes(path);
        bytes[1000] ^= 0xFF;
        File.WriteAllBytes(path, bytes);
        var second = await f.ScanAndRunAsync(coordinator);
        Assert.Equal(1, second.Succeeded, "corrupt file re-downloaded");
        Assert.Equal(Sha(data), ShaFile(path), "restored content at the original name");
        Assert.True(Directory.GetFiles(Path.GetDirectoryName(path)!, Path.GetFileName(path) + ".corrupt-*").Length == 1, "changed file preserved, not overwritten");
        Assert.True(f.LogText(second).Contains("no longer matches", StringComparison.Ordinal), "corruption logged");
    }

    private static async Task DiskFull()
    {
        await using var f = Fixture.Create();
        var data = Data(100_000, 14);
        f.Server.MapMedia("/d.mp4", data);
        f.Provider.Items.Add(f.Recording("d", "/d.mp4", size: data.Length));
        var disk = new FakeDisk(0);
        var coordinator = f.Coordinator(disk: disk);
        await coordinator.ScanAsync(null, CancellationToken.None);
        var run = coordinator.RunBackupAsync(new BackupRequest { CreateArchive = false }, null, CancellationToken.None);
        await Assert.Until(() => coordinator.Engine?.State == EngineState.PausedDiskFull, "paused for disk space");
        Assert.Equal(0, f.Server.RequestsFor("/d.mp4").Count(), "no request while disk is full");
        Interlocked.Exchange(ref disk.Available, long.MaxValue / 4);
        coordinator.Resume();
        var summary = await run;
        Assert.Equal(1, summary.Succeeded, "continued after space was freed");
        Assert.Equal(0, summary.Failed, "disk full is not a file failure");
        Assert.True(f.LogText(summary).Contains("[WARN] Insufficient disk space | Downloads paused", StringComparison.Ordinal), "warning logged");
    }

    private static async Task PauseResume()
    {
        await using var f = Fixture.Create();
        var data = Data(3_000_000, 15);
        f.Server.MapMedia("/p.mp4", data, new MediaOptions { ChunkDelayMs = 15, ChunkSize = 32 * 1024 });
        f.Provider.Items.Add(f.Recording("p", "/p.mp4"));
        var coordinator = f.Coordinator();
        await coordinator.ScanAsync(null, CancellationToken.None);
        var run = coordinator.RunBackupAsync(new BackupRequest { CreateArchive = false }, null, CancellationToken.None);
        await Assert.Until(() => coordinator.Engine?.Snapshot().Any(s => s.Bytes > 200_000) == true, "transfer in progress");
        coordinator.Pause();
        await Assert.Until(() => coordinator.Engine?.Snapshot().All(s => s.Status == "Queued") == true, "active transfer stopped");
        Assert.Equal(EngineState.Paused, coordinator.Engine!.State, "paused");
        var part = Directory.GetFiles(f.Layout.Downloads, "*.part", SearchOption.AllDirectories).Single();
        var pausedLength = new FileInfo(part).Length;
        await Task.Delay(300);
        Assert.Equal(pausedLength, new FileInfo(part).Length, "no writes while paused");
        coordinator.Resume();
        var summary = await run;
        Assert.Equal(1, summary.Succeeded, "completed after resume");
        Assert.True(f.Server.RequestsFor("/p.mp4").Last(r => r.Method == "GET").Header("Range") is not null, "resume used a validated range");
        Assert.Equal(Sha(data), coordinator.Store.Get("mock:p")!.Sha256, "intact");
    }

    private static async Task AppRestart()
    {
        await using var f = Fixture.Create();
        var data = Data(2_500_000, 16);
        f.Server.MapMedia("/ar.mp4", data, new MediaOptions { ChunkDelayMs = 15, ChunkSize = 32 * 1024 });
        f.Provider.Items.Add(f.Recording("ar", "/ar.mp4"));
        var first = f.Coordinator();
        await first.ScanAsync(null, CancellationToken.None);
        var run = first.RunBackupAsync(new BackupRequest { CreateArchive = false }, null, CancellationToken.None);
        await Assert.Until(() => first.Engine?.Snapshot().Any(s => s.Bytes > 300_000) == true, "transfer in progress");
        first.Stop(handOffBits: true);
        var interrupted = await run;
        Assert.Equal(RunState.Interrupted, interrupted.State, "run interrupted, not failed");
        first.Dispose();
        f.Coordinators.Remove(first);

        var second = f.Coordinator();
        await second.ReconcileAsync();
        var runId = second.LatestInterruptedRunId();
        Assert.Equal(interrupted.RunId, runId, "interrupted run found after restart");
        var summary = await second.RunBackupAsync(new BackupRequest { ResumeRunId = runId, CreateArchive = false }, null, CancellationToken.None);
        Assert.Equal(interrupted.RunId, summary.RunId, "same run continued");
        Assert.Equal(1, summary.Succeeded, "completed after restart");
        Assert.Equal(RunState.Completed, summary.State, "completed");
        Assert.Equal(1, second.Store.RunItems(runId!).Count(i => i.FinalStatus == "SUCCESS"), "single final result");
        Assert.Equal(1, Regex.Matches(f.LogText(summary), @"\[SUCCESS\]").Count, "single SUCCESS line across restart");
        Assert.True(f.Server.RequestsFor("/ar.mp4").Last(r => r.Method == "GET").Header("Range") is not null, "resumed after restart");
    }

    private static async Task Concurrent()
    {
        await using var f = Fixture.Create();
        for (var i = 0; i < 6; i++)
        {
            f.Server.MapMedia($"/k{i}.mp4", Data(400_000, 100 + i), new MediaOptions { ChunkDelayMs = 10, ChunkSize = 32 * 1024 });
            f.Provider.Items.Add(f.Recording($"k{i}", $"/k{i}.mp4"));
        }
        var summary = await f.ScanAndRunAsync(f.Coordinator(Fixture.FastOptions(concurrency: 3)));
        Assert.Equal(6, summary.Succeeded, "all succeeded");
        Assert.True(f.Server.MaxConcurrent is >= 2 and <= 4, $"concurrency honored (observed {f.Server.MaxConcurrent})");
        Assert.True(Regex.Matches(f.LogText(summary), @"Progress: (\d+)/6").Select(m => int.Parse(m.Groups[1].Value, CultureInfo.InvariantCulture)).SequenceEqual([1, 2, 3, 4, 5, 6]), "progress counter monotonic");
    }

    private static async Task CsvReports()
    {
        Assert.Equal("\"a,b\"", Csv.Text("a,b"), "comma quoted");
        Assert.Equal("\"say \"\"hi\"\"\"", Csv.Text("say \"hi\""), "quotes doubled");
        Assert.Equal("\"line1\nline2\"", Csv.Text("line1\nline2"), "newline quoted");
        Assert.Equal("'=SUM(A1)", Csv.Text("=SUM(A1)"), "formula neutralized");
        var parsed = Csv.Parse("a,\"b,c\",\"d\"\"e\"\r\n1,2,3\r\n");
        Assert.Equal("b,c", parsed[0][1], "parse quoted comma");
        Assert.Equal("d\"e", parsed[0][2], "parse escaped quote");
        await using var f = Fixture.Create();
        f.Layout.Ensure();
        var previous = CultureInfo.CurrentCulture;
        CultureInfo.CurrentCulture = new CultureInfo("de-DE");
        try
        {
            using var store = new StateStore(f.Layout.DatabasePath);
            store.AddRunItems("r1", ["k1", "k2"], "scheduled");
            using (var log = new RunLogger(f.Layout, "r1", store, storeFullUrls: false))
            {
                log.ScheduledTotal = 2;
                log.Success(new SuccessRecord("k1", "id,1", "cam \"A\", door.mp4", "cam;1", 12.5, 88405565, 88405565, 11.25, 1, TransferEngineKind.Http, @"C:\x\y.mp4", "ab"));
                log.Fail(new FailRecord("k2", "id2", "=evil.mp4", null, null, "HTTP 403: Download authorization expired, \"signed\"", 403, 4, TransferEngineKind.Http, new Uri("https://h.example/p.mp4?token=abc"), 3));
            }
            var lines = File.ReadAllLines(f.Layout.LogPath("r1"));
            Assert.True(lines[0].Contains("Filesize: 84.31 MB", StringComparison.Ordinal) && lines[0].Contains("Time: 12.50s", StringComparison.Ordinal), "invariant numbers in log: " + lines[0]);
            Assert.True(lines[0].Contains("Avg Speed: 7.49 MB/s", StringComparison.Ordinal), "cumulative average speed: " + lines[0]);
            var bytes = File.ReadAllBytes(f.Layout.CsvReportPath("r1"));
            Assert.True(bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF, "UTF-8 BOM for Excel");
            var report = Csv.Parse(Encoding.UTF8.GetString(bytes));
            Assert.True(report.All(r => r.Count == RunLogger.ReportColumns.Length), "every row has every column");
            Assert.Equal("cam \"A\", door.mp4", report[1][3], "filename round-trips");
            Assert.Equal("12.500", report[1][6], "invariant decimal in CSV under de-DE");
            var failed = report.Single(r => r[5] == "FAIL");
            Assert.Equal("'=evil.mp4", failed[3], "formula prefix neutralized");
            Assert.Equal("HTTP 403: Download authorization expired, \"signed\"", failed[18], "message round-trips");
            Assert.Equal("https://h.example/p.mp4?[REDACTED]", failed[19], "URL redacted");
            Assert.Equal("403", failed[20], "http status column");
        }
        finally
        {
            CultureInfo.CurrentCulture = previous;
        }
    }

    private static async Task OneFinalPerRun()
    {
        await using var f = Fixture.Create();
        f.Server.MapMedia("/ok.mp4", Data(20_000, 17));
        f.Server.Map("/nf.mp4", ctx => ctx.StatusAsync(404));
        f.Server.Map("/se.mp4", ctx => ctx.StatusAsync(503));
        f.Provider.Items.AddRange([f.Recording("ok", "/ok.mp4"), f.Recording("nf", "/nf.mp4"), f.Recording("se", "/se.mp4")]);
        var coordinator = f.Coordinator();
        var summary = await f.ScanAndRunAsync(coordinator);
        var items = coordinator.Store.RunItems(summary.RunId);
        Assert.True(items.All(i => i.FinalStatus is "SUCCESS" or "FAIL"), "every scheduled file has a final status");
        var log = f.LogText(summary);
        Assert.Equal(1, SuccessLine.Matches(log).Count, "one SUCCESS");
        Assert.Equal(2, FailLine.Matches(log).Count, "two FAIL");
        var files = SuccessLine.Matches(log).Concat(FailLine.Matches(log)).Select(m => m.Groups["file"].Value).ToList();
        Assert.Equal(files.Count, files.Distinct().Count(), "no file has two final results");
        Assert.True(!coordinator.Store.TryRecordFinal(summary.RunId, "mock:ok", "FAIL", DateTimeOffset.UtcNow), "database rejects a second final result");
        using var duplicate = new RunLogger(f.Layout, summary.RunId, coordinator.Store, false);
        Assert.True(!duplicate.Success(new SuccessRecord("mock:ok", "ok", "x.mp4", null, 1, 1, 1, 1, 1, TransferEngineKind.Http, "x", null)), "logger suppresses duplicates");
    }

    private static async Task Zip64Entries()
    {
        await using var f = Fixture.Create();
        var path = Path.Combine(f.Sub("zip"), "many.zip");
        var payload = Encoding.ASCII.GetBytes("x");
        var entries = Enumerable.Range(0, 70_000).Select(i => new ArchiveEntrySource($"videos/{i:D6}.bin", 1, Sha(payload), () => new MemoryStream(payload, false), true)).ToList();
        var mismatches = await ArchiveService.WriteAsync(path, entries, null, CancellationToken.None);
        Assert.Equal(0, mismatches.Count, "no mismatches");
        var bytes = File.ReadAllBytes(path);
        var zip64Eocd = bytes.AsSpan().IndexOf(new byte[] { 0x50, 0x4b, 0x06, 0x06 });
        Assert.True(zip64Eocd > 0, "ZIP64 end-of-central-directory record present");
        var verification = await ArchiveService.VerifyAsync(path, entries.Take(100).ToDictionary(e => e.EntryName, e => (1L, e.Sha256)), 70_000, null, CancellationToken.None);
        Assert.True(verification.Ok, "verified: " + string.Join(";", verification.Problems.Take(3)));
        Assert.Equal(70_000, ArchiveService.List(path).Count, "all entries listed");
    }

    private static async Task Zip64LargeFile()
    {
        if (Environment.GetEnvironmentVariable("VBD_HEAVY_TESTS") != "1") throw new SkipException("set VBD_HEAVY_TESTS=1 to write and verify a 4.2 GiB archive");
        await using var f = Fixture.Create();
        const long size = 4L * 1024 * 1024 * 1024 + 200L * 1024 * 1024;
        if (PathSafety.FreeSpace(f.Root) < size * 2 + (1L << 30)) throw new SkipException("insufficient free space");
        string sha;
        using (var hash = System.Security.Cryptography.IncrementalHash.CreateHash(System.Security.Cryptography.HashAlgorithmName.SHA256))
        {
            using var zeros = new ZeroStream(size);
            var buffer = new byte[1 << 20];
            int read;
            while ((read = zeros.Read(buffer, 0, buffer.Length)) > 0) hash.AppendData(buffer, 0, read);
            sha = Convert.ToHexString(hash.GetHashAndReset()).ToLowerInvariant();
        }
        var path = Path.Combine(f.Sub("zip"), "large.zip");
        var entries = new List<ArchiveEntrySource> { new("videos/large.bin", size, sha, () => new ZeroStream(size), true) };
        var mismatches = await ArchiveService.WriteAsync(path, entries, null, CancellationToken.None);
        Assert.Equal(0, mismatches.Count, "source hash");
        Assert.True(new FileInfo(path).Length > size, "archive larger than 4 GiB");
        var verification = await ArchiveService.VerifyAsync(path, new Dictionary<string, (long, string?)> { ["videos/large.bin"] = (size, sha) }, 1, null, CancellationToken.None);
        Assert.True(verification.Ok, "4.2 GiB entry verified: " + string.Join(";", verification.Problems));
        Assert.Equal(size, ArchiveService.List(path).Single().Length, "ZIP64 entry length");
    }

    private static async Task ArchiveCorruption()
    {
        await using var f = Fixture.Create();
        var data = Data(500_000, 18);
        f.Server.MapMedia("/z.mp4", data);
        f.Provider.Items.Add(f.Recording("z", "/z.mp4"));
        var coordinator = f.Coordinator();
        var summary = await f.ScanAndRunAsync(coordinator, archive: true);
        Assert.True(summary.ArchiveStatus is "verified" or "verified (incomplete)", "archive created and verified: " + summary.ArchiveStatus);
        var archivePath = summary.ArchivePath!;
        var entries = ArchiveService.List(archivePath);
        Assert.True(entries.Any(e => e.Name.StartsWith("videos/", StringComparison.Ordinal) && e.CompressedLength == e.Length), "video stored without recompression");
        Assert.True(entries.Any(e => e.Name == "manifest/recordings_manifest.csv") && entries.Any(e => e.Name.StartsWith("reports/report_", StringComparison.Ordinal)), "manifest and reports included");
        Assert.True(!entries.Any(e => e.Name.EndsWith(".part", StringComparison.Ordinal)), "no partial files");
        var record = coordinator.Store.Archives().First();
        Assert.True((await coordinator.Archives.VerifyRecordedAsync(record, null, CancellationToken.None)).Ok, "intact archive verifies");
        var again = await coordinator.CreateArchiveAsync(ArchiveScope.AllSelected, summary.RunId, null, CancellationToken.None);
        Assert.Equal("reused", again.Status, "unchanged archive is not recreated");
        var bytes = File.ReadAllBytes(archivePath);
        var marker = bytes.AsSpan().IndexOf(data.AsSpan(200_000, 64));
        Assert.True(marker > 0, "stored data located");
        bytes[marker + 10] ^= 0x5A;
        File.WriteAllBytes(archivePath, bytes);
        var verification = await coordinator.Archives.VerifyRecordedAsync(record, null, CancellationToken.None);
        Assert.True(!verification.Ok, "corruption detected");
        Assert.True(verification.Problems.Any(p => p.Contains("SHA-256", StringComparison.Ordinal) || p.Contains("CRC", StringComparison.OrdinalIgnoreCase) || p.Contains("corrupt", StringComparison.OrdinalIgnoreCase)), "problem explained: " + string.Join(";", verification.Problems));
        Assert.Equal("corrupt", coordinator.Store.Archives().First().Status, "status persisted");
    }

    private static Task WebView2Missing()
    {
        Assert.Equal(WebView2Status.Missing, WebView2Availability.Evaluate(() => null).Status, "null version");
        Assert.Equal(WebView2Status.Missing, WebView2Availability.Evaluate(() => throw new WebView2RuntimeNotFoundException()).Status, "runtime-not-found exception");
        Assert.Equal(WebView2Status.Missing, WebView2Availability.Evaluate(() => throw new DllNotFoundException("WebView2Loader.dll")).Status, "missing loader");
        Assert.Equal(WebView2Status.Error, WebView2Availability.Evaluate(() => throw new InvalidOperationException("boom")).Status, "other errors are reported, not hidden");
        var ok = WebView2Availability.Evaluate(() => "130.0.1.0");
        Assert.True(ok.Status == WebView2Status.Available && ok.Message.Contains("130.0.1.0", StringComparison.Ordinal), "version reported");
        Assert.True(WebView2Availability.InstallUrl.StartsWith("https://developer.microsoft.com/", StringComparison.Ordinal), "official install link");
        return Task.CompletedTask;
    }

    private static Task Localization()
    {
        Assert.Equal("Moorksz Visory Media Downloader", Strings.En["AppName"], "exact English product identity");
        Assert.Equal("Moorksz Visory Media Downloader", Strings.De["AppName"], "exact German product identity");
        Assert.True(Strings.En.Keys.ToHashSet().SetEquals(Strings.De.Keys), "identical key sets: " + string.Join(",", Strings.En.Keys.Except(Strings.De.Keys).Concat(Strings.De.Keys.Except(Strings.En.Keys))));
        Assert.True(Strings.En.Values.Concat(Strings.De.Values).All(v => !string.IsNullOrWhiteSpace(v)), "no empty strings");
        Assert.True(Strings.En.Count(kv => Strings.De[kv.Key] != kv.Value) > Strings.En.Count * 0.8, "German is actually translated");
        foreach (var key in Strings.En.Keys.Where(k => Strings.En[k].Contains("{0}", StringComparison.Ordinal)))
            Assert.True(Strings.De[key].Contains("{0}", StringComparison.Ordinal), "placeholder kept in German: " + key);
        var repo = Path.GetDirectoryName(FixtureRoot)!;
        var appDir = Path.Combine(repo, "src", "MoorkszVisoryMediaDownloader.App");
        if (Directory.Exists(appDir))
        {
            var used = new HashSet<string>(StringComparer.Ordinal);
            foreach (var file in Directory.EnumerateFiles(appDir, "*.*", SearchOption.AllDirectories).Where(p => (p.EndsWith(".xaml", StringComparison.Ordinal) || p.EndsWith(".cs", StringComparison.Ordinal)) && !p.Contains("\\obj\\", StringComparison.Ordinal) && !p.Contains("\\bin\\", StringComparison.Ordinal)))
            {
                var text = File.ReadAllText(file);
                if (file.EndsWith(".xaml", StringComparison.Ordinal))
                    foreach (Match m in Regex.Matches(text, @"\[(?<k>[A-Z][A-Za-z0-9]+)\], Source=\{x:Static app:Loc\.Instance\}")) used.Add(m.Groups["k"].Value);
                foreach (Match m in Regex.Matches(text, @"Loc\.[TF]\(""(?<k>[A-Za-z0-9]+)""[),]")) used.Add(m.Groups["k"].Value);
                foreach (Match m in Regex.Matches(text, @"new(?:\s*Choice<[^>]+>)?\((?:[^,()]+), ""(?<k>[A-Za-z0-9]+)""\)")) used.Add(m.Groups["k"].Value);
                foreach (Match m in Regex.Matches(text, @"new\(\d+, ""(?<k>[A-Za-z0-9]+)""")) used.Add(m.Groups["k"].Value);
            }
            foreach (var status in Enum.GetNames<RecordingStatus>()) used.Add("RecStatus" + status);
            foreach (var state in Enum.GetNames<RunState>()) used.Add("Run" + state);
            foreach (var completeness in Enum.GetNames<DiscoveryCompleteness>()) used.Add("Completeness" + completeness);
            var missing = used.Where(k => !Strings.En.ContainsKey(k)).ToList();
            Assert.True(missing.Count == 0, "UI references undefined strings: " + string.Join(", ", missing));
            Assert.True(used.Count > 50, $"UI text is localized through the string table ({used.Count} keys used)");
        }
        return Task.CompletedTask;
    }

    private static async Task LargeInventory()
    {
        await using var f = Fixture.Create();
        f.Layout.Ensure();
        using var store = new StateStore(f.Layout.DatabasePath);
        var recordings = Enumerable.Range(0, 100_000).Select(i => new DiscoveredRecording
        {
            RecordingId = $"rec-{i:D6}",
            CameraId = $"cam{i % 7}",
            RecordedAt = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero).AddMinutes(i),
            OriginalName = $"rec-{i:D6}.mp4",
            Size = 1_000_000 + i,
        }).ToList();
        var watch = System.Diagnostics.Stopwatch.StartNew();
        store.MergeDiscovery("visory", "scan-1", recordings, DateTimeOffset.UtcNow);
        var merge = watch.Elapsed;
        watch.Restart();
        var all = store.All("visory");
        var load = watch.Elapsed;
        watch.Restart();
        var filter = new RecordingFilter { StartDate = new DateTime(2026, 2, 1), EndDate = new DateTime(2026, 2, 28), Cameras = ["cam3"] };
        var selected = all.Count(filter.Matches);
        var filterTime = watch.Elapsed;
        watch.Restart();
        store.MergeDiscovery("visory", "scan-2", recordings, DateTimeOffset.UtcNow);
        var remerge = watch.Elapsed;
        Console.WriteLine($"        100k merge {merge.TotalSeconds:0.00}s, load {load.TotalSeconds:0.00}s, filter {filterTime.TotalMilliseconds:0}ms, re-merge {remerge.TotalSeconds:0.00}s, selected {selected}");
        Assert.Equal(100_000, all.Count, "all loaded");
        Assert.True(selected > 0, "filter applied");
        Assert.True(merge < TimeSpan.FromSeconds(60) && remerge < TimeSpan.FromSeconds(60), "merge time acceptable");
        Assert.True(load < TimeSpan.FromSeconds(10), "load time acceptable");
        Assert.True(filterTime < TimeSpan.FromSeconds(2), "filter time acceptable");
    }

    private static async Task UrlPolicyAndRedaction()
    {
        var media = VisoryEndpoints.MediaPolicy;
        Assert.True(media.IsAllowed(new Uri($"https://firebasestorage.googleapis.com/v0/b/{VisoryEndpoints.StorageBucket}/o/a%2Fb.mp4?alt=media&token=x"), out _), "Visory storage bucket allowed");
        Assert.True(!media.IsAllowed(new Uri("https://firebasestorage.googleapis.com/v0/b/someone-else.appspot.com/o/a.mp4"), out _), "other buckets rejected");
        Assert.True(!media.IsAllowed(new Uri("http://app.visory.cam/a.mp4"), out var r1) && r1.Contains("HTTPS", StringComparison.Ordinal), "HTTP rejected");
        Assert.True(!media.IsAllowed(new Uri("https://user:pw@app.visory.cam/a.mp4"), out _), "embedded credentials rejected");
        Assert.True(!media.IsAllowed(new Uri("https://app.visory.cam:8443/a.mp4"), out _), "non-standard port rejected");
        Assert.True(!media.IsAllowed(new Uri("https://93.184.216.34/a.mp4"), out _), "IP hosts rejected");
        Assert.True(!media.IsAllowed(new Uri("https://visory.cam.evil.example/a.mp4"), out _), "suffix spoofing rejected");
        Assert.True(!media.IsAllowed(new Uri("http://127.0.0.1/a.mp4"), out _), "loopback HTTP only allowed in explicit test policy");
        Assert.Equal("https://x.example/a.mp4?[REDACTED]", UrlPolicy.Redact(new Uri("https://u:p@x.example/a.mp4?token=1#f")), "redaction");
        var cleaned = Redactor.Clean("Authorization: Bearer abc.def.ghi Cookie: sid=1; x=2 eyJhbGciOiJIUzI1NiJ9.eyJzdWIiOiIxMjM0NTY3ODkwIn0.abcdefghijk https://h/x?token=SECRET");
        Assert.True(!cleaned.Contains("abc.def", StringComparison.Ordinal) && !cleaned.Contains("sid=1", StringComparison.Ordinal) && !cleaned.Contains("SECRET", StringComparison.Ordinal) && !cleaned.Contains("eyJzdWIi", StringComparison.Ordinal), "secrets redacted: " + cleaned);

        await using var f = Fixture.Create();
        f.Server.Map("/redirect.mp4", ctx => ctx.WriteHeadAsync(302, new() { ["Location"] = "https://evil.example/steal.mp4", ["Content-Length"] = "0" }));
        f.Provider.Items.Add(f.Recording("redir", "/redirect.mp4"));
        var summary = await f.ScanAndRunAsync(f.Coordinator());
        Assert.Equal(1, summary.Failed, "redirect to unknown origin refused");
        Assert.True(FailLine.Match(f.LogText(summary)).Groups["message"].Value.Contains("Rejected redirect", StringComparison.Ordinal), "reason logged");
    }

    private static async Task PathSafetyAndNaming()
    {
        Assert.Equal("_CON", PathSafety.SanitizeSegment("CON"), "reserved device name");
        Assert.Equal("a_b", PathSafety.SanitizeSegment("a/b"), "separator removed");
        Assert.Equal("recording", PathSafety.SanitizeSegment(".."), "dot segments replaced");
        Assert.Equal(".mp4", PathSafety.SanitizeExtension(".MP4"), "extension normalized");
        Assert.Equal("", PathSafety.SanitizeExtension(".mp4/../../x"), "malicious extension dropped");
        Assert.Throws<UnauthorizedAccessException>(() => PathSafety.Combine(@"C:\root", @"..\..\Windows\x.mp4"), "traversal rejected");
        Assert.Throws<UnauthorizedAccessException>(() => PathSafety.Combine(@"C:\root", @"C:\Windows\x.mp4"), "rooted path rejected");
        await using var f = Fixture.Create();
        f.Layout.Ensure();
        using var store = new StateStore(f.Layout.DatabasePath);
        store.MergeDiscovery("visory", "s", [new DiscoveredRecording { RecordingId = "../../evil", CameraName = "..\\..\\Cam:1", OriginalName = "x.mp4", RecordedAt = new DateTimeOffset(2026, 1, 2, 3, 4, 5, TimeSpan.Zero) }], DateTimeOffset.UtcNow);
        var entry = store.Get("visory:../../evil")!;
        var planner = new DestinationPlanner(store, f.Layout);
        foreach (var rule in Enum.GetValues<NamingRule>())
        {
            entry.Destination = null;
            var relative = planner.Assign(entry, rule);
            var full = f.Layout.DestinationPath(relative);
            Assert.True(full.StartsWith(f.Layout.Downloads + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase), $"{rule} stays inside downloads: {relative}");
        }
        var link = Path.Combine(f.Layout.Downloads, "link");
        try
        {
            Directory.CreateSymbolicLink(link, f.Root);
            Assert.Throws<UnauthorizedAccessException>(() => PathSafety.EnsureNoReparsePoints(f.Layout.Downloads, Path.Combine(link, "x")), "links not followed");
        }
        catch (IOException)
        {
            Console.WriteLine("        (symbolic link creation not permitted for this user; link check covered by attribute logic only)");
        }
        catch (UnauthorizedAccessException)
        {
            Console.WriteLine("        (symbolic link creation not permitted for this user; link check covered by attribute logic only)");
        }
    }

    private static async Task RemoteDigest()
    {
        await using var f = Fixture.Create();
        var data = Data(64_000, 19);
        var md5 = Convert.ToBase64String(System.Security.Cryptography.MD5.HashData(data));
        f.Server.MapMedia("/good.mp4", data, new MediaOptions { XGoogHashMd5 = md5 });
        f.Server.MapMedia("/bad.mp4", data, new MediaOptions { XGoogHashMd5 = Convert.ToBase64String(new byte[16]) });
        f.Provider.Items.AddRange([f.Recording("good", "/good.mp4"), f.Recording("bad", "/bad.mp4")]);
        var summary = await f.ScanAndRunAsync(f.Coordinator(Fixture.FastOptions(attempts: 2)));
        Assert.Equal(1, summary.Succeeded, "matching server digest accepted");
        Assert.Equal(1, summary.Failed, "mismatching server digest rejected");
        Assert.True(f.LogText(summary).Contains("Checksum mismatch with the server-provided MD5 digest", StringComparison.Ordinal), "reason logged");
        Assert.True(Directory.GetFiles(f.Layout.Downloads, "*", SearchOption.AllDirectories).Length == 1, "rejected bytes not kept as a recording");
    }

    private static async Task DemoEndToEnd()
    {
        await using var f = Fixture.Create();
        var demo = new DemoRecordingProvider(count: 6);
        var coordinator = f.Coordinator(provider: demo, options: Fixture.FastOptions(concurrency: 3) with { UseBits = true }, bits: new BitsClient());
        var inventory = await coordinator.ScanAsync(null, CancellationToken.None);
        Assert.Equal(6, inventory.Discovered, "demo recordings");
        var summary = await coordinator.RunBackupAsync(new BackupRequest { CreateArchive = true }, null, CancellationToken.None);
        Assert.Equal(6, summary.Succeeded, "all demo files");
        Assert.Equal("verified", summary.ArchiveStatus, "complete demo inventory gives a verified, complete archive");
        Assert.True(f.LogText(summary).Contains("DEMO - synthetic data, not your Visory account", StringComparison.Ordinal), "demo clearly labelled in reports");
        var entry = coordinator.Store.All().First();
        var bytes = File.ReadAllBytes(f.Layout.DestinationPath(entry.Destination!));
        Assert.True(bytes.Select((b, i) => (b, i)).Take(5000).All(t => t.b == SyntheticMediaHandler.ByteAt(entry.RecordingId, t.i)), "synthetic content intact");
        Assert.True(coordinator.Store.All().All(e => e.Engine == TransferEngineKind.Http), "custom transport never uses BITS");
    }

    private static async Task BitsTransfer()
    {
        var client = new BitsClient();
        if (!client.IsAvailable) throw new SkipException("BITS COM server unavailable");
        await using var f = Fixture.Create();
        var data = Data(1_500_000, 20);
        f.Server.MapMedia("/bits.mp4", data, new MediaOptions { ChunkSize = 256 * 1024 });
        f.Provider.Items.Add(f.Recording("bits", "/bits.mp4"));
        var options = Fixture.FastOptions() with { UseBits = true, Priority = PriorityMode.Fast, BitsPollInterval = TimeSpan.FromMilliseconds(200), IdleTimeout = TimeSpan.FromSeconds(30) };
        var coordinator = f.Coordinator(options, bits: client);
        var summary = await f.ScanAndRunAsync(coordinator);
        var entry = coordinator.Store.Get("mock:bits")!;
        Assert.Equal(1, summary.Succeeded, "BITS download succeeded: " + entry.LastError);
        Assert.Equal(TransferEngineKind.Bits, entry.Engine, "transferred by BITS");
        Assert.Equal(Sha(data), entry.Sha256, "intact");
        Assert.True(f.Server.RequestsFor("/bits.mp4").Any(r => r.Method == "HEAD"), "eligibility probe performed");
        Assert.True(entry.BitsJob is null, "job completed and released");
    }

    private static Task SettingsAndScheduling()
    {
        var options = EngineOptions.From(new AppSettings { Priority = PriorityMode.SmartBackground, MaxConcurrency = 6, BandwidthLimitKBps = 100 });
        Assert.Equal(2, options.MaxConcurrency, "smart background caps concurrency");
        Assert.Equal(100 * 1024L, options.BandwidthBytesPerSecond, "bandwidth limit");
        Assert.Equal(BitsPriority.Normal, options.BitsPriority, "background BITS priority");
        Assert.Equal(BitsPriority.Foreground, EngineOptions.From(new AppSettings { Priority = PriorityMode.Fast }).BitsPriority, "fast BITS priority");
        var existing = new HashSet<string> { "2026-10-26_18-30-02" };
        var local = new DateTimeOffset(2026, 10, 26, 18, 30, 2, TimeZoneInfo.Local.GetUtcOffset(new DateTime(2026, 10, 26, 18, 30, 2)));
        Assert.Equal("2026-10-26_18-30-02_2", RunIds.Create(local, existing.Contains), "collision suffix only when needed");
        Assert.Equal("2026-10-26_18-30-02", RunIds.Create(local, _ => false), "plain run ID");
        var args = Scheduling.DailyArguments("MoorkszVisoryMediaDownloader-x", @"C:\Tools\Visory Media\MoorkszVisoryMediaDownloader.exe", "03:00");
        Assert.True(args.Contains("DAILY") && args.Contains("03:00") && args.Contains("LIMITED") && args.Last() == "\"C:\\Tools\\Visory Media\\MoorkszVisoryMediaDownloader.exe\" --scheduled", "daily task arguments");
        Assert.True(Scheduling.LogonArguments("t", @"C:\a.exe").Contains("ONLOGON"), "logon task arguments");
        Assert.Equal("t", Scheduling.DeleteArguments("t").Last(), "delete arguments");
        var filter = new RecordingFilter { StartDate = new DateTime(2026, 10, 1), EndDate = new DateTime(2026, 10, 31) };
        var unknown = new RecordingEntry { Key = "k", Provider = "p", RecordingId = "r", OriginalName = "a.mp4" };
        Assert.True(filter.Matches(unknown), "unknown dates included by default");
        Assert.True(!(filter with { IncludeUnknownDates = false }).Matches(unknown), "unknown dates excluded only when asked");
        return Task.CompletedTask;
    }
}

internal sealed class WebView2RuntimeNotFoundException : Exception;

internal sealed class ZeroStream(long length) : Stream
{
    private long position;
    public override bool CanRead => true;
    public override bool CanSeek => false;
    public override bool CanWrite => false;
    public override long Length => length;
    public override long Position { get => position; set => throw new NotSupportedException(); }
    public override void Flush() { }
    public override int Read(byte[] buffer, int offset, int count)
    {
        var n = (int)Math.Min(count, length - position);
        Array.Clear(buffer, offset, n);
        position += n;
        return n;
    }
    public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
    public override void SetLength(long value) => throw new NotSupportedException();
    public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
}
