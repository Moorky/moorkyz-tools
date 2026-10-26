using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Text.Json;
using Moorkyz.Tools.MediaRecover.Core;
using Moorkyz.Tools.MediaRecover.Providers.Local;
using Moorkyz.Tools.MediaRecover.Providers.Wpd;
using Moorkyz.Tools.MediaRecover.Reporting;
using Moorkyz.Tools.MediaRecover.Storage;

namespace Moorkyz.Tools.MediaRecover.Tests;

internal static class Program
{
    private static int passed;
    private static readonly string Root = Path.GetFullPath(Path.Combine(".fixtures", "tests-" + Guid.NewGuid().ToString("N")));
    public static async Task<int> Main()
    {
        Directory.CreateDirectory(Root);
        try
        {
            await Check("date cutoff inclusive, unknown included, conflicts flagged; invalid dates unknown", Dates);
            await Check("recursive local discovery, Unicode/deep/hidden, exclusion, destination loop, read-only", LocalInventory);
            await Check("local no-overwrite collisions, hash skip, source changes, corruption and quarantine", LocalRecovery);
            await Check("unknown source size copied; zero-byte never verified; partial inventory", UnknownAndZero);
            await Check("transient disconnect retry with fresh stream, permanent access/space failures", RetryAndFailures);
            await Check("source mutation rejected", SourceChanged);
            await Check("independent source hash mismatch quarantined; no false verified output", HashMismatch);
            await Check("pause, resume and safe cancellation; interrupted run restarted", PauseAndStop);
            await Check("promotion-before-DB crash reconciliation; interrupted staging never promoted", CrashReconciliation);
            await Check("SQLite durable reopen, settings persisted, malformed settings fail explicitly", Persistence);
            await Check("legacy CSV / incomplete / damaged import is non-destructive and untrusted", Legacy);
            await Check("CSV newline/quote/formula safety, reports/count reconciliation and ZIP hashes", ReportsAndZip);
            await Check("actual Windows WPD COM manager enumeration (no phone resource reads)", WpdManager);
            Console.WriteLine($"PASS {passed} groups. Synthetic fixtures only. Phone transfer / >4 GiB / physical disk failure / screen reader NOT RUN.");
            return 0;
        }
        catch (Exception e) { Console.Error.WriteLine("FAIL: " + e); return 1; }
        finally
        {
            // Never recurse into a junction during cleanup; fixtures contain only our own files.
            foreach (var path in Junctions) if (Directory.Exists(path)) Directory.Delete(path);
            if (Directory.Exists(Root)) Directory.Delete(Root, true);
        }
    }
    private static async Task Check(string name, Func<Task> test) { await test(); passed++; Console.WriteLine("PASS " + name); }
    private static void Assert(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
    private static string Dir(string name) { var path = Path.Combine(Root, name); Directory.CreateDirectory(path); return path; }
    private static string Media(string root, string relative, byte value = 42, DateTime? date = null, int bytes = 32769)
    {
        var path = Path.Combine(root, relative); Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllBytes(path, Enumerable.Repeat(value, bytes).ToArray());
        var utc = date ?? new DateTime(2026, 10, 12, 12, 0, 0, DateTimeKind.Utc);
        File.SetCreationTimeUtc(path, utc); File.SetLastWriteTimeUtc(path, utc); return path;
    }
    private static Settings Configuration(string name, string? source = null) => new()
    {
        Destination = Path.Combine(Dir(name), "saved"),
        Retry = false,
        Sources = [new(source is null ? "Fake" : "LocalFolder", source ?? name)]
    };
    private static SqliteManifestStore Store(string name) => new(Path.Combine(Dir(name), "state.db"));
    private static RecoveryCoordinator Engine(IManifestStore store, params ISourceProvider[] providers) => new(store, providers, new IntegrityService(), new ReportWriter());
    private static SourceItem FakeItem(string name, long? size, List<DateValue>? dates = null) =>
        new(PathSafety.Key(name), new("Fake", name), name, name + ".mp4", size, "v1", dates ?? [], "Video");
    private static Task Dates()
    {
        var settings = new Settings();
        var old = FakeItem("old", 1, [new("date", "2026-09-30", new(2026, 9, 30, 23, 59, 59, TimeSpan.Zero), "UTC")]);
        var cutoff = old with { Dates = [new("date", "2026-10-01", new(2026, 10, 1, 0, 0, 0, TimeSpan.Zero), "UTC")] };
        Assert(!Filter.Date(old, settings).Eligible, "Old date must be excluded.");
        Assert(Filter.Date(cutoff, settings).Eligible, "Inclusive cutoff lost.");
        Assert(Filter.Date(FakeItem("unknown", null), settings) == (true, true, false), "Unknown dates must be included.");
        var conflict = old with { Dates = [.. old.Dates, .. cutoff.Dates] };
        Assert(Filter.Date(conflict, settings) == (true, false, true), "Conflicts must include and flag.");
        Assert(Filter.Date(old with { Dates = [new("bad", "1601", new(1601, 1, 1, 0, 0, 0, TimeSpan.Zero), "UTC")] }, settings).Unknown, "Bogus time must be unknown.");
        settings.EndDate = new(2026, 10, 10);
        Assert(!Filter.Date(cutoff with { Dates = [new("later", "", new(2026, 10, 11, 0, 0, 0, TimeSpan.Zero), null)] }, settings).Eligible, "End bound not honored.");
        Assert(Filter.Media("SOUND.3GA", settings) == "Audio" && Filter.Media("VIDEO.M2TS", settings) == "Video", "Default media extensions missing.");
        return Task.CompletedTask;
    }
    private static readonly List<string> Junctions = [];
    private static async Task LocalInventory()
    {
        var root = Dir("inventory");
        var nested = string.Join("\\", Enumerable.Repeat("deep-ä", 48));
        var path = Media(root, nested + "\\音声.M4A");
        Media(root, "empty\\..\\normal.MP4");
        var hidden = Media(root, "hidden.mp3"); File.SetAttributes(hidden, FileAttributes.Hidden);
        Media(root, "ignore\\excluded.mp4"); Media(root, "other.txt");
        Directory.CreateDirectory(Path.Combine(root, "empty"));
        var destination = Path.Combine(root, "saved"); Media(destination, "do-not-scan.mp4");
        var settings = Configuration("inventory-config", root); settings.Destination = destination; settings.Exclusions = "ignore*";
        var link = Path.Combine(root, "cycle");
        using (var process = Process.Start(new ProcessStartInfo("cmd.exe") { Arguments = $"/d /c mklink /J \"{link}\" \"{root}\"", UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true, CreateNoWindow = true })!)
        { await process.WaitForExitAsync(); Assert(process.ExitCode == 0, "Fixture junction creation failed: " + await process.StandardError.ReadToEndAsync()); }
        Junctions.Add(link);
        var provider = new LocalFolderProvider();
        var entries = new List<InventoryEntry>();
        await foreach (var entry in provider.InventoryAsync(settings.Sources[0], settings, default)) entries.Add(entry);
        Assert(entries.Count(e => e.Item is not null) == 3, "Deep scan counts incorrect.");
        Assert(entries.Any(e => e.Item?.ObjectId == path), "Deep Unicode file missing.");
        Assert(entries.Any(e => e.Issue?.Code == "LINK_SKIPPED"), "Cycle not reported.");
        settings.IncludeHidden = true;
        var withHidden = 0; await foreach (var entry in provider.InventoryAsync(settings.Sources[0], settings, default)) if (entry.Item is not null) withHidden++;
        Assert(withHidden == 4, "Hidden toggle ineffective.");
        var before = SHA256.HashData(File.ReadAllBytes(path));
        await using (var stream = await provider.OpenReadAsync(entries.First(e => e.Item?.ObjectId == path).Item!, default))
            Assert(stream.CanRead && !stream.CanWrite, "Source handle not read-only.");
        Assert(before.SequenceEqual(SHA256.HashData(File.ReadAllBytes(path))), "Source changed.");
        var blocked = false; try { PathSafety.NoLinks(Path.Combine(link, "normal.MP4")); } catch (RecoveryException e) { blocked = e.Code == "LINK_BLOCKED"; }
        Assert(blocked, "Ancestor junction not blocked.");
    }
    private static async Task LocalRecovery()
    {
        var root = Dir("local-source");
        var a = Media(root, "one\\same.mp4", 21);
        Media(root, "two\\same.mp4", 22);
        Media(root, "old.mp3", date: new(2026, 9, 20, 0, 0, 0, DateTimeKind.Utc));
        var settings = Configuration("local-recovery", root); settings.HighAssurance = true;
        using var store = Store("local-recovery"); using var engine = Engine(store, new LocalFolderProvider());
        var scan = await engine.ScanAsync(settings, default);
        Assert(store.Inventory(scan.Id).Count(r => r.Status == ItemStatus.Eligible) == 2, "Eligibility wrong.");
        var candidate = store.Inventory(scan.Id).First(r => r.Status == ItemStatus.Eligible);
        var preexisting = PathSafety.Target(settings, candidate.Item); Directory.CreateDirectory(Path.GetDirectoryName(preexisting)!); File.WriteAllText(preexisting, "unrelated");
        var completed = await engine.RecoverAsync(scan, default);
        Assert(completed.Status.StartsWith("Completed", StringComparison.Ordinal), "Recovery not completed.");
        var copied = store.Inventory(scan.Id).Where(r => r.Status == ItemStatus.Verified).ToArray();
        Assert(copied.Length == 2 && copied.Select(r => r.Target).Distinct().Count() == 2, "Collision lost a file.");
        Assert(File.ReadAllText(preexisting) == "unrelated", "Existing target overwritten.");
        Assert(copied.All(r => r.Verification.StartsWith("Independent", StringComparison.Ordinal)), "High assurance not recorded.");
        var rescan = await engine.ScanAsync(settings, default);
        Assert(store.Inventory(rescan.Id).Count(r => r.Status == ItemStatus.AlreadyVerified) == 2, "Verified files redownloaded.");
        var damaged = copied[0]; File.WriteAllText(damaged.Target, "corrupt");
        Assert((await engine.VerifyAsync(default)).Count == 1, "Corruption not detected.");
        var repairScan = await engine.ScanAsync(settings, default); await engine.RecoverAsync(repairScan, default);
        var repair = store.File(damaged.Item.Key)!;
        Assert(repair.Status == ItemStatus.Verified && repair.Repaired && File.Exists(repair.Quarantine), "Repair/quarantine not durable.");
        Assert(File.ReadAllText(damaged.Target) == "corrupt", "Suspect original was removed.");
        Media(root, "one\\same.mp4", 91, bytes: 65539);
        var changed = await engine.ScanAsync(settings, default);
        Assert(store.Inventory(changed.Id).Any(r => r.Item.ObjectId == a && r.Status == ItemStatus.Eligible), "Changed source was skipped.");
        await engine.RecoverAsync(changed, default);
        Assert(File.ReadAllBytes(a).All(b => b == 91), "Source modified.");
        var current = store.Files().First(r => r.Item.ObjectId == a); File.Delete(current.Target);
        var missing = await engine.ScanAsync(settings, default);
        Assert(store.Inventory(missing.Id).First(r => r.Item.ObjectId == a).Status == ItemStatus.Eligible, "Missing target skipped.");
    }
    private static async Task UnknownAndZero()
    {
        var provider = new FakeProvider([FakeItem("unknown", null), FakeItem("zero", 0), FakeItem("short", 999999)]);
        provider.Issue = true;
        using var store = Store("unknown"); using var engine = Engine(store, provider);
        var scan = await engine.ScanAsync(Configuration("unknown"), default);
        Assert(scan.Partial && scan.Inaccessible == 1, "Partial inventory falsely fully scanned.");
        var done = await engine.RecoverAsync(scan, default);
        var rows = store.Inventory(scan.Id).ToArray();
        Assert(rows.Single(r => r.Item.ObjectId == "unknown").Bytes == 65537, "Unknown size not streamed.");
        Assert(rows.Single(r => r.Item.ObjectId == "zero").ErrorCode == "ZERO_BYTE", "Zero bytes false success.");
        Assert(rows.Single(r => r.Item.ObjectId == "short").ErrorCode == "LENGTH_MISMATCH", "Truncation not detected.");
        Assert(done.Status == "Completed with warnings", "Errors falsely green.");
    }
    private static async Task RetryAndFailures()
    {
        var fake = new FakeProvider([FakeItem("retry", null), FakeItem("denied", null)]);
        fake.DisconnectOnce = true;
        using var store = Store("retry"); using var engine = Engine(store, fake);
        var settings = Configuration("retry"); settings.Retry = true;
        var run = await engine.ScanAsync(settings, default); await engine.RecoverAsync(run, default);
        var rows = store.Inventory(run.Id).ToArray();
        Assert(rows.Single(r => r.Item.ObjectId == "retry").Attempts == 2, "Retry count wrong.");
        Assert(rows.Single(r => r.Item.ObjectId == "retry").Status == ItemStatus.Verified, "Reconnect stream did not restart.");
        Assert(rows.Single(r => r.Item.ObjectId == "retry").ErrorCode == "", "Recovered retry retained a failure code.");
        Assert(rows.Single(r => r.Item.ObjectId == "denied").ErrorCode == "ACCESS_DENIED", "Access denied not classified.");
        var pending = Enumerable.Range(0, 100).Select(n => "pending" + n).First(n => string.CompareOrdinal(PathSafety.Key(n), PathSafety.Key("space")) > 0);
        using var spaceStore = Store("space"); using var spaceEngine = Engine(spaceStore, new FakeProvider([FakeItem("space", null), FakeItem(pending, null)]));
        var spaceRun = await spaceEngine.ScanAsync(Configuration("space"), default);
        var waiting = await spaceEngine.RecoverAsync(spaceRun, default);
        Assert(waiting.Status == "Waiting for space" && spaceStore.Inventory(spaceRun.Id).Single(r => r.Item.ObjectId == "space").ErrorCode == "WAITING_FOR_SPACE", "Disk full not classified.");
        Assert(spaceStore.Inventory(spaceRun.Id).Single(r => r.Item.ObjectId == pending).Status == ItemStatus.Eligible, "Unavailable storage kept hammering queued files.");
    }
    private static async Task SourceChanged()
    {
        var fake = new FakeProvider([FakeItem("mutated", null)]) { ChangeDuringRead = true };
        using var store = Store("mutated"); using var engine = Engine(store, fake);
        var run = await engine.ScanAsync(Configuration("mutated"), default); await engine.RecoverAsync(run, default);
        Assert(store.Inventory(run.Id).Single().ErrorCode == "SOURCE_CHANGED", "Mutation falsely verified.");
        Assert(!Directory.EnumerateFiles(run.Settings.Destination, "*.mp4").Any(), "Partial promoted.");
    }
    private static async Task HashMismatch()
    {
        var fake = new FakeProvider([FakeItem("hash-mismatch", null)]) { DifferentSecondRead = true };
        using var store = Store("hash-mismatch"); using var engine = Engine(store, fake);
        var settings = Configuration("hash-mismatch"); settings.HighAssurance = true;
        var run = await engine.ScanAsync(settings, default); await engine.RecoverAsync(run, default);
        var row = store.Inventory(run.Id).Single();
        Assert(row.ErrorCode == "SOURCE_HASH_MISMATCH" && row.Status == ItemStatus.Failed, "Independent mismatch falsely trusted.");
        Assert(File.Exists(row.Quarantine) && File.Exists(row.Quarantine + ".json") && File.Exists(row.Staging), "Suspect bytes/provenance not retained.");
        Assert(!File.Exists(row.Target), "Suspect result promoted.");
    }
    private static async Task PauseAndStop()
    {
        var fake = new FakeProvider([FakeItem("slow", null)]) { Slow = true };
        using var store = Store("pause"); using var engine = Engine(store, fake);
        var run = await engine.ScanAsync(Configuration("pause"), default);
        using var stop = new CancellationTokenSource();
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        engine.Progress += p => { if (p.Status == "Transferring") { engine.Pause(); started.TrySetResult(); } };
        var task = Task.Run(() => engine.RecoverAsync(run, stop.Token));
        await started.Task.WaitAsync(TimeSpan.FromSeconds(10));
        await Task.Delay(100); Assert(!task.IsCompleted, "Pause ineffective.");
        engine.Resume(); stop.Cancel(); var interrupted = await task;
        Assert(interrupted.Status == "Interrupted", "Stop false completion.");
        Assert(store.Inventory(run.Id).Single().Status == ItemStatus.Interrupted, "Interrupted file not recorded.");
        using var restarted = Engine(store, new FakeProvider([FakeItem("slow", null)]));
        await restarted.RecoverAsync(interrupted, default);
        Assert(store.Inventory(run.Id).Single().Status == ItemStatus.Verified, "Restart did not resume queued file.");
    }
    private static async Task CrashReconciliation()
    {
        var dir = Dir("crash"); var target = Media(dir, "promoted.mp4");
        var item = FakeItem("promoted", new FileInfo(target).Length);
        var integrity = new IntegrityService(); var hash = await integrity.HashAsync(target, default);
        using var store = Store("crash"); using var engine = Engine(store, new FakeProvider([]));
        store.SaveFile(new(item, ItemStatus.Promoting, target, Hash: hash, Bytes: new FileInfo(target).Length));
        var partial = Media(dir, "abandoned.part");
        var second = FakeItem("partial", 1);
        store.SaveFile(new(second, ItemStatus.Transferring, Path.Combine(dir, "never.mp4"), partial));
        await engine.ReconcileAsync(default);
        Assert(store.File(item.Key)!.Status == ItemStatus.Verified, "Promotion reconciliation failed.");
        Assert(store.File(second.Key)!.Status == ItemStatus.Interrupted && !File.Exists(Path.Combine(dir, "never.mp4")) && File.Exists(partial), "Unsafe partial promotion/cleanup.");
    }
    private static async Task Persistence()
    {
        var path = Path.Combine(Dir("persist"), "durable.db");
        var item = FakeItem("persist", null);
        using (var store = new SqliteManifestStore(path)) store.SaveFile(new(item, ItemStatus.Interrupted, Error: "persisted"));
        using (var store = new SqliteManifestStore(path)) Assert(store.File(item.Key)?.Error == "persisted", "SQLite state not durable.");
        var settingsPath = Path.Combine(Dir("persist"), "settings.json");
        var settingsStore = new JsonSettingsStore(settingsPath);
        settingsStore.Save(new() { Language = "de", Theme = "Dark", Zip = true });
        Assert(settingsStore.Load().Language == "de" && settingsStore.Load().Zip, "Settings not persisted.");
        await File.WriteAllTextAsync(settingsPath, "{broken");
        bool rejected = false; try { settingsStore.Load(); } catch (JsonException) { rejected = true; }
        Assert(rejected, "Malformed settings silently discarded.");
    }
    private static async Task Legacy()
    {
        var root = Dir("legacy-root"); var media = Media(root, "older.mp4");
        Media(root, "left.incomplete"); Media(root, "bad.damaged");
        var csv = Path.Combine(root, "S26-Backup-Index.csv");
        File.WriteAllText(csv, "\"FileName\",\"SHA256\"\r\n\"older.mp4\",\"not-a-hash\"\r\n\"..\\outside.mp4\",\"\"\r\n");
        var before = Directory.GetFiles(root).ToDictionary(p => p, p => Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(p))));
        using var store = Store("legacy");
        var receipt = await new LegacyImporter(store, new IntegrityService()).ImportAsync(root, Dir("legacy-receipt"), default);
        Assert(File.Exists(receipt) && File.ReadAllText(receipt).Contains("CSV_PATH_REJECTED", StringComparison.Ordinal), "Import receipt missing/rejected traversal absent.");
        Assert(store.Files().Count() == 3 && store.Files().All(r => r.Status == ItemStatus.LegacyUnverified), "Legacy trust misrepresented.");
        Assert(before.All(p => Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(p.Key))) == p.Value), "Legacy input modified.");
        Assert(File.Exists(media), "Legacy original removed.");
    }
    private static async Task ReportsAndZip()
    {
        var values = new[] { "a,b", "a\"b", "line1\r\nline2", "😀", "=cmd" };
        var roundtrip = Csv.Parse(new StringReader(Csv.Row(values))).Single();
        Assert(roundtrip.Take(4).SequenceEqual(values.Take(4)) && roundtrip[4] == "'=cmd", "CSV safety/quoting failed.");
        var fake = new FakeProvider([FakeItem("archive", null), FakeItem("zero", 0)]);
        using var store = Store("reports"); using var engine = Engine(store, fake);
        var run = await engine.ScanAsync(Configuration("reports"), default); run = await engine.RecoverAsync(run, default);
        var summary = JsonDocument.Parse(File.ReadAllText(Path.Combine(run.Reports, $"summary_{run.Id}.json")));
        Assert(summary.RootElement.GetProperty("ReconciledInventoryCount").GetInt64() == 2, "Summary counters do not reconcile.");
        Assert(File.ReadAllText(Path.Combine(run.Reports, $"failed_report_{run.Id}.csv")).Contains("ZERO_BYTE", StringComparison.Ordinal), "Failed report incomplete.");
        var zip = await new ReportWriter().ArchiveAsync(run, store, default);
        Assert(File.Exists(zip) && store.Inventory(run.Id).Where(r => r.Status == ItemStatus.Verified).All(r => File.Exists(r.Target)), "Archive lost originals.");
    }
    private static async Task WpdManager()
    {
        var devices = await new WpdProvider().DevicesAsync();
        Console.WriteLine($"  WPD manager returned {devices.Count} device(s); no source opened.");
    }
    private sealed class FakeProvider(SourceItem[] items) : ISourceProvider
    {
        private int opens;
        private readonly Dictionary<string, int> itemOpens = [];
        public string Kind => "Fake";
        public SourceCapabilities Capabilities => new(true, false, true);
        public bool Issue { get; set; }
        public bool DisconnectOnce { get; set; }
        public bool ChangeDuringRead { get; set; }
        public bool Slow { get; set; }
        public bool DifferentSecondRead { get; set; }
        public async IAsyncEnumerable<InventoryEntry> InventoryAsync(SourceSpec source, Settings settings, [EnumeratorCancellation] CancellationToken cancellationToken)
        {
            foreach (var item in items) { cancellationToken.ThrowIfCancellationRequested(); yield return new(item, null); await Task.Yield(); }
            if (Issue) yield return new(null, new("unreadable subtree", "ACCESS_DENIED", "Synthetic denied folder", "Choose an accessible source."));
        }
        public Task<Stream> OpenReadAsync(SourceItem item, CancellationToken cancellationToken)
        {
            Interlocked.Increment(ref opens);
            var attempt = itemOpens.GetValueOrDefault(item.ObjectId) + 1; itemOpens[item.ObjectId] = attempt;
            if (item.ObjectId == "retry" && DisconnectOnce && attempt == 1) throw new RecoveryException("DEVICE_DISCONNECTED", "Synthetic cable drop", "Reconnect.", true);
            if (item.ObjectId == "denied") throw new UnauthorizedAccessException("Synthetic permission denial.");
            if (item.ObjectId == "space") throw new RecoveryException("WAITING_FOR_SPACE", "Synthetic disk full", "Free space.");
            var bytes = item.Size == 0 ? Array.Empty<byte>() : Enumerable.Repeat((byte)(DifferentSecondRead && attempt > 1 ? 14 : 13), 65537).ToArray();
            return Task.FromResult<Stream>(Slow ? new SlowStream(bytes) : new MemoryStream(bytes, false));
        }
        public Task<bool> IsUnchangedAsync(SourceItem item, CancellationToken cancellationToken) => Task.FromResult(!ChangeDuringRead || opens == 0);
    }
    private sealed class SlowStream(byte[] bytes) : MemoryStream(bytes, false)
    {
        public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        { await Task.Delay(100, cancellationToken); return await base.ReadAsync(buffer[..Math.Min(buffer.Length, 1024)], cancellationToken); }
    }
}
