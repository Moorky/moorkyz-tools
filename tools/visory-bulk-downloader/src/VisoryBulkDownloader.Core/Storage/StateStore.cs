using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using VisoryBulkDownloader.Core.Infrastructure;

namespace VisoryBulkDownloader.Core.Storage;

public sealed class StateStore : IDisposable
{
    private const string Columns = "key,provider,recording_id,id_is_fingerprint,camera_id,camera_name,recorded_at,recorded_at_source,original_name,media_type,remote_size,remote_md5,object_path,last_url,destination,actual_size,sha256,status,engine,attempts,last_error,last_http_status,last_verified_at,resume_validator,resume_total,bits_job,run_id,first_seen_at,last_seen_at,last_scan_id";
    private readonly SqliteConnection db;

    public StateStore(string path)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
        db = new SqliteConnection(path);
        db.ExecuteScript("""
            PRAGMA journal_mode=WAL;
            PRAGMA synchronous=FULL;
            CREATE TABLE IF NOT EXISTS meta(key TEXT PRIMARY KEY, value TEXT NOT NULL);
            CREATE TABLE IF NOT EXISTS recordings(
              key TEXT PRIMARY KEY, provider TEXT NOT NULL, recording_id TEXT NOT NULL, id_is_fingerprint INTEGER NOT NULL DEFAULT 0,
              camera_id TEXT, camera_name TEXT, recorded_at INTEGER, recorded_at_source TEXT, original_name TEXT, media_type TEXT,
              remote_size INTEGER, remote_md5 TEXT, object_path TEXT, last_url TEXT, destination TEXT, actual_size INTEGER, sha256 TEXT,
              status TEXT NOT NULL, engine TEXT NOT NULL DEFAULT 'None', attempts INTEGER NOT NULL DEFAULT 0, last_error TEXT, last_http_status INTEGER,
              last_verified_at INTEGER, resume_validator TEXT, resume_total INTEGER, bits_job TEXT, run_id TEXT,
              first_seen_at INTEGER NOT NULL, last_seen_at INTEGER NOT NULL, last_scan_id TEXT);
            CREATE INDEX IF NOT EXISTS ix_recordings_status ON recordings(status);
            CREATE INDEX IF NOT EXISTS ix_recordings_recorded ON recordings(recorded_at);
            CREATE UNIQUE INDEX IF NOT EXISTS ux_recordings_destination ON recordings(destination COLLATE NOCASE) WHERE destination IS NOT NULL;
            CREATE TABLE IF NOT EXISTS attempts(id INTEGER PRIMARY KEY, run_id TEXT NOT NULL, key TEXT NOT NULL, attempt INTEGER NOT NULL,
              started_at INTEGER NOT NULL, finished_at INTEGER NOT NULL, engine TEXT NOT NULL, outcome TEXT NOT NULL, http_status INTEGER, bytes INTEGER, message TEXT);
            CREATE INDEX IF NOT EXISTS ix_attempts_run ON attempts(run_id, key);
            CREATE TABLE IF NOT EXISTS runs(id TEXT PRIMARY KEY, started_at INTEGER NOT NULL, finished_at INTEGER, state TEXT NOT NULL, summary_json TEXT, selection_json TEXT);
            CREATE TABLE IF NOT EXISTS run_items(run_id TEXT NOT NULL, key TEXT NOT NULL, role TEXT NOT NULL, final_status TEXT, final_at INTEGER, PRIMARY KEY(run_id, key));
            CREATE TABLE IF NOT EXISTS scans(id TEXT PRIMARY KEY, started_at INTEGER NOT NULL, finished_at INTEGER NOT NULL, provider TEXT NOT NULL,
              completeness TEXT NOT NULL, discovered INTEGER NOT NULL, cameras INTEGER NOT NULL, notes_json TEXT, errors_json TEXT);
            CREATE TABLE IF NOT EXISTS archives(id INTEGER PRIMARY KEY, run_id TEXT, path TEXT NOT NULL, scope TEXT NOT NULL, created_at INTEGER NOT NULL,
              fingerprint TEXT NOT NULL, entries INTEGER NOT NULL, bytes INTEGER NOT NULL, status TEXT NOT NULL, verified_at INTEGER, incomplete INTEGER NOT NULL, message TEXT);
            """);
        db.Execute("INSERT OR IGNORE INTO meta(key,value) VALUES('schema','1')");
    }

    public static string KeyFor(string provider, string recordingId) => provider + ":" + recordingId;

    /// <summary>Stable fallback identity when a provider has no recording ID.</summary>
    public static string Fingerprint(string provider, string? camera, DateTimeOffset? recordedAt, string? name, long? size)
    {
        var text = string.Join('|', provider, camera ?? "", recordedAt?.ToUnixTimeMilliseconds().ToString(Format.Invariant) ?? "", name ?? "", size?.ToString(Format.Invariant) ?? "");
        return "fp-" + Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(text)))[..24].ToLowerInvariant();
    }

    public sealed record MergeResult(int Added, int Updated, IReadOnlyList<string> Keys);

    public MergeResult MergeDiscovery(string provider, string scanId, IReadOnlyList<DiscoveredRecording> recordings, DateTimeOffset now)
    {
        var added = 0;
        var updated = 0;
        var keys = new List<string>(recordings.Count);
        db.Transaction(() =>
        {
            using var exists = db.Prepare("SELECT 1 FROM recordings WHERE key=?1");
            using var insert = db.Prepare("""
                INSERT INTO recordings(key,provider,recording_id,id_is_fingerprint,camera_id,camera_name,recorded_at,recorded_at_source,original_name,media_type,remote_size,remote_md5,object_path,last_url,status,first_seen_at,last_seen_at,last_scan_id)
                VALUES(?1,?2,?3,?4,?5,?6,?7,?8,?9,?10,?11,?12,?13,?14,'New',?15,?15,?16)
                ON CONFLICT(key) DO UPDATE SET
                  camera_id=coalesce(excluded.camera_id,camera_id), camera_name=coalesce(excluded.camera_name,camera_name),
                  recorded_at=coalesce(excluded.recorded_at,recorded_at), recorded_at_source=CASE WHEN excluded.recorded_at IS NULL THEN recorded_at_source ELSE excluded.recorded_at_source END,
                  original_name=coalesce(excluded.original_name,original_name), media_type=coalesce(excluded.media_type,media_type),
                  remote_size=coalesce(excluded.remote_size,remote_size), remote_md5=coalesce(excluded.remote_md5,remote_md5),
                  object_path=coalesce(excluded.object_path,object_path), last_url=coalesce(excluded.last_url,last_url),
                  last_seen_at=excluded.last_seen_at, last_scan_id=excluded.last_scan_id
                """);
            foreach (var r in recordings)
            {
                var key = KeyFor(provider, r.RecordingId);
                keys.Add(key);
                exists.Bind(key);
                var known = exists.Step();
                if (known) updated++; else added++;
                insert.Run(key, provider, r.RecordingId, r.IdIsFingerprint, r.CameraId, r.CameraName, r.RecordedAt, r.RecordedAtSource,
                    r.OriginalName, r.MediaType, r.Size, r.RemoteMd5, r.ObjectPath, r.DownloadUrl is null ? null : Dpapi.Protect(r.DownloadUrl), now, scanId);
            }
        });
        return new MergeResult(added, updated, keys);
    }

    public void SaveScan(string scanId, string provider, DateTimeOffset started, DateTimeOffset finished, DiscoveryResult result) =>
        db.Execute("INSERT OR REPLACE INTO scans VALUES(?1,?2,?3,?4,?5,?6,?7,?8,?9)", scanId, started, finished, provider, result.Completeness,
            result.Recordings.Count, result.Cameras, JsonSerializer.Serialize(result.Notes), JsonSerializer.Serialize(result.Errors));

    public sealed record ScanRecord(string Id, DateTimeOffset StartedAt, DateTimeOffset FinishedAt, string Provider, DiscoveryCompleteness Completeness, int Discovered, int Cameras, IReadOnlyList<string> Notes, IReadOnlyList<string> Errors);

    public ScanRecord? LatestScan(string provider) => db.Query("SELECT id,started_at,finished_at,provider,completeness,discovered,cameras,notes_json,errors_json FROM scans WHERE provider=?1 ORDER BY finished_at DESC LIMIT 1",
        row => new ScanRecord(row.Text(0)!, row.Time(1)!.Value, row.Time(2)!.Value, row.Text(3)!, Enum.Parse<DiscoveryCompleteness>(row.Text(4)!), row.Int32(5), row.Int32(6),
            JsonSerializer.Deserialize<string[]>(row.Text(7) ?? "[]") ?? [], JsonSerializer.Deserialize<string[]>(row.Text(8) ?? "[]") ?? []), provider).FirstOrDefault();

    public RecordingEntry? Get(string key) => db.Query($"SELECT {Columns} FROM recordings WHERE key=?1", Map, key).FirstOrDefault();

    public List<RecordingEntry> All(string? provider = null) => provider is null
        ? db.Query($"SELECT {Columns} FROM recordings ORDER BY recorded_at DESC, key", Map)
        : db.Query($"SELECT {Columns} FROM recordings WHERE provider=?1 ORDER BY recorded_at DESC, key", Map, provider);

    public List<RecordingEntry> ByStatus(params RecordingStatus[] statuses)
    {
        var list = string.Join(',', statuses.Select(s => $"'{s}'"));
        return db.Query($"SELECT {Columns} FROM recordings WHERE status IN ({list}) ORDER BY key", Map);
    }

    public int Count() => (int)db.Scalar<long>("SELECT count(*) FROM recordings");

    public void Update(RecordingEntry e) => db.Execute("""
        UPDATE recordings SET id_is_fingerprint=?2,camera_id=?3,camera_name=?4,recorded_at=?5,recorded_at_source=?6,original_name=?7,media_type=?8,remote_size=?9,remote_md5=?10,
          object_path=?11,last_url=?12,destination=?13,actual_size=?14,sha256=?15,status=?16,engine=?17,attempts=?18,last_error=?19,last_http_status=?20,last_verified_at=?21,
          resume_validator=?22,resume_total=?23,bits_job=?24,run_id=?25,last_seen_at=?26,last_scan_id=?27 WHERE key=?1
        """, e.Key, e.IdIsFingerprint, e.CameraId, e.CameraName, e.RecordedAt, e.RecordedAtSource, e.OriginalName, e.MediaType, e.RemoteSize, e.RemoteMd5,
        e.ObjectPath, e.LastUrl is null ? null : Dpapi.Protect(e.LastUrl), e.Destination, e.ActualSize, e.Sha256, e.Status, e.Engine, e.Attempts, e.LastError, e.LastHttpStatus,
        e.LastVerifiedAt, e.ResumeValidator, e.ResumeTotal, e.BitsJob, e.RunId, e.LastSeenAt, e.LastScanId);

    public void SetStatus(string key, RecordingStatus status, string? error = null) =>
        db.Execute("UPDATE recordings SET status=?2,last_error=coalesce(?3,last_error) WHERE key=?1", key, status, error);

    public bool DestinationTaken(string relative, string exceptKey) =>
        db.Scalar<long>("SELECT count(*) FROM recordings WHERE destination=?1 COLLATE NOCASE AND key<>?2", relative, exceptKey) > 0;

    public void AddAttempt(string runId, string key, int attempt, DateTimeOffset started, DateTimeOffset finished, TransferEngineKind engine, string outcome, int? httpStatus, long? bytes, string? message) =>
        db.Execute("INSERT INTO attempts(run_id,key,attempt,started_at,finished_at,engine,outcome,http_status,bytes,message) VALUES(?1,?2,?3,?4,?5,?6,?7,?8,?9,?10)",
            runId, key, attempt, started, finished, engine, outcome, httpStatus, bytes, message);

    public sealed record AttemptRecord(string RunId, string Key, int Attempt, DateTimeOffset StartedAt, DateTimeOffset FinishedAt, string Engine, string Outcome, int? HttpStatus, long? Bytes, string? Message);

    public List<AttemptRecord> Attempts(string runId, string? key = null) => db.Query(
        "SELECT run_id,key,attempt,started_at,finished_at,engine,outcome,http_status,bytes,message FROM attempts WHERE run_id=?1 AND (?2 IS NULL OR key=?2) ORDER BY id",
        row => new AttemptRecord(row.Text(0)!, row.Text(1)!, row.Int32(2), row.Time(3)!.Value, row.Time(4)!.Value, row.Text(5)!, row.Text(6)!, (int?)row.NullableInt64(7), row.NullableInt64(8), row.Text(9)), runId, key);

    public bool RunExists(string runId) => db.Scalar<long>("SELECT count(*) FROM runs WHERE id=?1", runId) > 0;

    public void CreateRun(string runId, DateTimeOffset started, string selectionJson) =>
        db.Execute("INSERT INTO runs(id,started_at,state,selection_json) VALUES(?1,?2,'Running',?3)", runId, started, selectionJson);

    public void UpdateRun(string runId, RunState state, DateTimeOffset? finished, RunSummary? summary) =>
        db.Execute("UPDATE runs SET state=?2,finished_at=?3,summary_json=coalesce(?4,summary_json) WHERE id=?1", runId, state, finished,
            summary is null ? null : JsonSerializer.Serialize(summary, JsonFiles.Options));

    public sealed record RunRecord(string Id, DateTimeOffset StartedAt, DateTimeOffset? FinishedAt, RunState State, RunSummary? Summary, string? SelectionJson);

    public List<RunRecord> Runs(int limit = 200) => db.Query("SELECT id,started_at,finished_at,state,summary_json,selection_json FROM runs ORDER BY started_at DESC LIMIT ?1",
        row => new RunRecord(row.Text(0)!, row.Time(1)!.Value, row.Time(2), Enum.Parse<RunState>(row.Text(3)!),
            row.Text(4) is { } json ? JsonSerializer.Deserialize<RunSummary>(json, JsonFiles.Options) : null, row.Text(5)), limit);

    public RunRecord? Run(string runId) => Runs(10000).FirstOrDefault(r => r.Id == runId);

    public void AddRunItems(string runId, IEnumerable<string> keys, string role)
    {
        db.Transaction(() =>
        {
            using var insert = db.Prepare("INSERT OR IGNORE INTO run_items(run_id,key,role) VALUES(?1,?2,?3)");
            foreach (var key in keys) insert.Run(runId, key, role);
        });
    }

    /// <summary>Atomically records the single final outcome for a file in a run. Returns false if one already exists.</summary>
    public bool TryRecordFinal(string runId, string key, string status, DateTimeOffset at) =>
        db.Execute("UPDATE run_items SET final_status=?3, final_at=?4 WHERE run_id=?1 AND key=?2 AND final_status IS NULL", runId, key, status, at) == 1;

    public sealed record RunItem(string Key, string Role, string? FinalStatus);

    public List<RunItem> RunItems(string runId) =>
        db.Query("SELECT key,role,final_status FROM run_items WHERE run_id=?1 ORDER BY rowid", row => new RunItem(row.Text(0)!, row.Text(1)!, row.Text(2)), runId);

    public sealed record ArchiveRecord(long Id, string? RunId, string Path, ArchiveScope Scope, DateTimeOffset CreatedAt, string Fingerprint, int Entries, long Bytes, string Status, DateTimeOffset? VerifiedAt, bool Incomplete, string? Message);

    public long AddArchive(ArchiveRecord record)
    {
        db.Execute("INSERT INTO archives(run_id,path,scope,created_at,fingerprint,entries,bytes,status,verified_at,incomplete,message) VALUES(?1,?2,?3,?4,?5,?6,?7,?8,?9,?10,?11)",
            record.RunId, record.Path, record.Scope, record.CreatedAt, record.Fingerprint, record.Entries, record.Bytes, record.Status, record.VerifiedAt, record.Incomplete, record.Message);
        return db.Scalar<long>("SELECT last_insert_rowid()");
    }

    public void UpdateArchiveVerification(long id, string status, DateTimeOffset? verifiedAt, string? message) =>
        db.Execute("UPDATE archives SET status=?2, verified_at=?3, message=?4 WHERE id=?1", id, status, verifiedAt, message);

    public List<ArchiveRecord> Archives() => db.Query("SELECT id,run_id,path,scope,created_at,fingerprint,entries,bytes,status,verified_at,incomplete,message FROM archives ORDER BY created_at DESC",
        row => new ArchiveRecord(row.Int64(0), row.Text(1), row.Text(2)!, Enum.Parse<ArchiveScope>(row.Text(3)!), row.Time(4)!.Value, row.Text(5)!, row.Int32(6), row.Int64(7), row.Text(8)!, row.Time(9), row.Bool(10), row.Text(11)));

    private static RecordingEntry Map(SqliteRow r) => new()
    {
        Key = r.Text(0)!,
        Provider = r.Text(1)!,
        RecordingId = r.Text(2)!,
        IdIsFingerprint = r.Bool(3),
        CameraId = r.Text(4),
        CameraName = r.Text(5),
        RecordedAt = r.Time(6),
        RecordedAtSource = r.Text(7),
        OriginalName = r.Text(8),
        MediaType = r.Text(9),
        RemoteSize = r.NullableInt64(10),
        RemoteMd5 = r.Text(11),
        ObjectPath = r.Text(12),
        LastUrl = Dpapi.TryUnprotect(r.Text(13)),
        Destination = r.Text(14),
        ActualSize = r.NullableInt64(15),
        Sha256 = r.Text(16),
        Status = Enum.Parse<RecordingStatus>(r.Text(17)!),
        Engine = Enum.Parse<TransferEngineKind>(r.Text(18)!),
        Attempts = r.Int32(19),
        LastError = r.Text(20),
        LastHttpStatus = (int?)r.NullableInt64(21),
        LastVerifiedAt = r.Time(22),
        ResumeValidator = r.Text(23),
        ResumeTotal = r.NullableInt64(24),
        BitsJob = r.Text(25),
        RunId = r.Text(26),
        FirstSeenAt = r.Time(27)!.Value,
        LastSeenAt = r.Time(28)!.Value,
        LastScanId = r.Text(29),
    };

    public void Dispose() => db.Dispose();
}
