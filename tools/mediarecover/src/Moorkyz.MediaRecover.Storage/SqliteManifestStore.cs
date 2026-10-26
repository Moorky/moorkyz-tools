using System.Runtime.InteropServices;
using System.Text.Json;
using Moorkyz.Tools.MediaRecover.Core;

namespace Moorkyz.Tools.MediaRecover.Storage;

public sealed class SqliteManifestStore : IManifestStore
{
    private IntPtr database;
    private readonly object gate = new();
    public SqliteManifestStore(string path)
    {
        PathSafety.NoLinks(path);
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
        if (Native.sqlite3_open_v2(path, out database, 2 | 4 | 0x10000, IntPtr.Zero) != 0) throw new IOException("Cannot open SQLite ledger.");
        Execute("PRAGMA journal_mode=WAL; PRAGMA synchronous=FULL; PRAGMA busy_timeout=5000;" +
            "CREATE TABLE IF NOT EXISTS runs(id TEXT PRIMARY KEY,json TEXT NOT NULL);" +
            "CREATE TABLE IF NOT EXISTS inventory(run TEXT NOT NULL,key TEXT NOT NULL,json TEXT NOT NULL,PRIMARY KEY(run,key));" +
            "CREATE TABLE IF NOT EXISTS files(key TEXT PRIMARY KEY,json TEXT NOT NULL);" +
            "CREATE TABLE IF NOT EXISTS issues(id INTEGER PRIMARY KEY,run TEXT NOT NULL,json TEXT NOT NULL);");
    }
    private static string Q(string value) => "'" + value.Replace("'", "''") + "'";
    private static string Json<T>(T value) => Q(JsonSerializer.Serialize(value));
    public void SaveRun(RunRecord run) => Execute($"INSERT OR REPLACE INTO runs VALUES({Q(run.Id)},{Json(run)})");
    public RunRecord? Run(string id) => Read<RunRecord>($"SELECT json FROM runs WHERE id={Q(id)}").FirstOrDefault();
    public IEnumerable<RunRecord> Runs() => Read<RunRecord>("SELECT json FROM runs ORDER BY id DESC LIMIT 500");
    public void SaveInventory(string run, FileRecord record) => Execute($"INSERT OR REPLACE INTO inventory VALUES({Q(run)},{Q(record.Item.Key)},{Json(record)})");
    public IEnumerable<FileRecord> Inventory(string run) => Paged<FileRecord>($"SELECT json FROM inventory WHERE run={Q(run)} ORDER BY key");
    public void SaveFile(FileRecord record) => Execute($"INSERT OR REPLACE INTO files VALUES({Q(record.Item.Key)},{Json(record)})");
    public FileRecord? File(string key) => Read<FileRecord>($"SELECT json FROM files WHERE key={Q(key)}").FirstOrDefault();
    public IEnumerable<FileRecord> Files() => Paged<FileRecord>("SELECT json FROM files ORDER BY key");
    public void AddIssue(string run, ScanIssue issue) => Execute($"INSERT INTO issues(run,json) VALUES({Q(run)},{Json(issue)})");
    public IEnumerable<ScanIssue> Issues(string run) => Paged<ScanIssue>($"SELECT json FROM issues WHERE run={Q(run)} ORDER BY id");
    private IEnumerable<T> Paged<T>(string query)
    {
        for (var page = 0; ; page++)
        {
            var batch = Read<T>(query + $" LIMIT 100 OFFSET {page * 100}");
            foreach (var item in batch) yield return item;
            if (batch.Count < 100) break;
        }
    }
    private List<T> Read<T>(string sql)
    {
        lock (gate)
        {
            var result = new List<T>();
            Exception? callbackError = null;
            Native.Callback callback = (_, count, values, _) =>
            {
                try
                {
                    var json = Marshal.PtrToStringUTF8(Marshal.ReadIntPtr(values))!;
                    result.Add(JsonSerializer.Deserialize<T>(json)!);
                    return 0;
                }
                catch (Exception e) { callbackError = e; return 1; }
            };
            Execute(sql, callback);
            if (callbackError is not null) throw new InvalidDataException("Invalid ledger data.", callbackError);
            GC.KeepAlive(callback);
            return result;
        }
    }
    private void Execute(string sql, Native.Callback? callback = null)
    {
        lock (gate)
        {
            ObjectDisposedException.ThrowIf(database == IntPtr.Zero, this);
            var code = Native.sqlite3_exec(database, sql, callback, IntPtr.Zero, out var error);
            if (code != 0)
            {
                var message = Marshal.PtrToStringUTF8(error) ?? $"SQLite error {code}";
                Native.sqlite3_free(error);
                throw new IOException(message);
            }
        }
    }
    public void Dispose() { lock (gate) { if (database != IntPtr.Zero) { Native.sqlite3_close_v2(database); database = IntPtr.Zero; } } }
    private static class Native
    {
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)] internal delegate int Callback(IntPtr data, int count, IntPtr values, IntPtr names);
        [DllImport("winsqlite3.dll", CallingConvention = CallingConvention.Cdecl)]
        internal static extern int sqlite3_open_v2([MarshalAs(UnmanagedType.LPUTF8Str)] string path, out IntPtr db, int flags, IntPtr vfs);
        [DllImport("winsqlite3.dll", CallingConvention = CallingConvention.Cdecl)]
        internal static extern int sqlite3_exec(IntPtr db, [MarshalAs(UnmanagedType.LPUTF8Str)] string sql, Callback? callback, IntPtr data, out IntPtr error);
        [DllImport("winsqlite3.dll", CallingConvention = CallingConvention.Cdecl)] internal static extern void sqlite3_free(IntPtr pointer);
        [DllImport("winsqlite3.dll", CallingConvention = CallingConvention.Cdecl)] internal static extern int sqlite3_close_v2(IntPtr db);
    }
}
