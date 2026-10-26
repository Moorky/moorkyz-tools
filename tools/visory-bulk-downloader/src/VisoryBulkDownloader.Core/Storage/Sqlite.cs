using System.Runtime.InteropServices;

namespace VisoryBulkDownloader.Core.Storage;

/// <summary>Minimal binding to the SQLite engine shipped with Windows (winsqlite3.dll); no NuGet dependency.</summary>
public sealed class SqliteConnection : IDisposable
{
    private const int OpenReadWrite = 0x2, OpenCreate = 0x4, OpenFullMutex = 0x10000;
    internal const int Row = 100, Done = 101;
    private IntPtr handle;
    private readonly object gate = new();

    public SqliteConnection(string path)
    {
        var code = Native.sqlite3_open_v2(path, out handle, OpenReadWrite | OpenCreate | OpenFullMutex, IntPtr.Zero);
        if (code != 0)
        {
            var message = handle == IntPtr.Zero ? $"SQLite error {code}" : ErrorMessage();
            if (handle != IntPtr.Zero) Native.sqlite3_close_v2(handle);
            handle = IntPtr.Zero;
            throw new IOException($"Cannot open state database: {message}");
        }
        Native.sqlite3_extended_result_codes(handle, 1);
        Native.sqlite3_busy_timeout(handle, 10000);
    }

    internal IntPtr Handle => handle != IntPtr.Zero ? handle : throw new ObjectDisposedException(nameof(SqliteConnection));

    internal string ErrorMessage() => Marshal.PtrToStringUTF8(Native.sqlite3_errmsg(handle)) ?? "unknown SQLite error";

    public void ExecuteScript(string sql)
    {
        lock (gate)
        {
            var code = Native.sqlite3_exec(Handle, sql, IntPtr.Zero, IntPtr.Zero, out var error);
            if (code != 0)
            {
                var message = Marshal.PtrToStringUTF8(error) ?? $"SQLite error {code}";
                Native.sqlite3_free(error);
                throw new IOException(message);
            }
        }
    }

    public SqliteStatement Prepare(string sql)
    {
        lock (gate)
        {
            var code = Native.sqlite3_prepare_v2(Handle, sql, -1, out var statement, IntPtr.Zero);
            if (code != 0) throw new IOException($"SQLite prepare failed: {ErrorMessage()}");
            return new SqliteStatement(this, statement);
        }
    }

    public int Execute(string sql, params object?[] args)
    {
        lock (gate)
        {
            using var statement = Prepare(sql);
            statement.Bind(args);
            while (statement.Step()) { }
            return Native.sqlite3_changes(Handle);
        }
    }

    public List<T> Query<T>(string sql, Func<SqliteRow, T> map, params object?[] args)
    {
        lock (gate)
        {
            using var statement = Prepare(sql);
            statement.Bind(args);
            var result = new List<T>();
            while (statement.Step()) result.Add(map(new SqliteRow(statement.Pointer)));
            return result;
        }
    }

    public T? Scalar<T>(string sql, params object?[] args)
    {
        var values = Query(sql, row => row.IsNull(0) ? default : (T?)row.Value(0), args);
        return values.Count == 0 ? default : values[0];
    }

    public void Transaction(Action action)
    {
        lock (gate)
        {
            ExecuteScript("BEGIN IMMEDIATE");
            try
            {
                action();
                ExecuteScript("COMMIT");
            }
            catch
            {
                try { ExecuteScript("ROLLBACK"); } catch (IOException) { }
                throw;
            }
        }
    }

    public void Dispose()
    {
        lock (gate)
        {
            if (handle == IntPtr.Zero) return;
            Native.sqlite3_close_v2(handle);
            handle = IntPtr.Zero;
        }
    }

    internal object Gate => gate;

    internal static class Native
    {
        private const string Library = "winsqlite3.dll";
        [DllImport(Library, CallingConvention = CallingConvention.Cdecl)]
        internal static extern int sqlite3_open_v2([MarshalAs(UnmanagedType.LPUTF8Str)] string path, out IntPtr db, int flags, IntPtr vfs);
        [DllImport(Library, CallingConvention = CallingConvention.Cdecl)]
        internal static extern int sqlite3_close_v2(IntPtr db);
        [DllImport(Library, CallingConvention = CallingConvention.Cdecl)]
        internal static extern int sqlite3_exec(IntPtr db, [MarshalAs(UnmanagedType.LPUTF8Str)] string sql, IntPtr callback, IntPtr data, out IntPtr error);
        [DllImport(Library, CallingConvention = CallingConvention.Cdecl)]
        internal static extern void sqlite3_free(IntPtr pointer);
        [DllImport(Library, CallingConvention = CallingConvention.Cdecl)]
        internal static extern int sqlite3_busy_timeout(IntPtr db, int milliseconds);
        [DllImport(Library, CallingConvention = CallingConvention.Cdecl)]
        internal static extern int sqlite3_extended_result_codes(IntPtr db, int onoff);
        [DllImport(Library, CallingConvention = CallingConvention.Cdecl)]
        internal static extern IntPtr sqlite3_errmsg(IntPtr db);
        [DllImport(Library, CallingConvention = CallingConvention.Cdecl)]
        internal static extern int sqlite3_changes(IntPtr db);
        [DllImport(Library, CallingConvention = CallingConvention.Cdecl)]
        internal static extern int sqlite3_prepare_v2(IntPtr db, [MarshalAs(UnmanagedType.LPUTF8Str)] string sql, int bytes, out IntPtr statement, IntPtr tail);
        [DllImport(Library, CallingConvention = CallingConvention.Cdecl)]
        internal static extern int sqlite3_step(IntPtr statement);
        [DllImport(Library, CallingConvention = CallingConvention.Cdecl)]
        internal static extern int sqlite3_reset(IntPtr statement);
        [DllImport(Library, CallingConvention = CallingConvention.Cdecl)]
        internal static extern int sqlite3_clear_bindings(IntPtr statement);
        [DllImport(Library, CallingConvention = CallingConvention.Cdecl)]
        internal static extern int sqlite3_finalize(IntPtr statement);
        [DllImport(Library, CallingConvention = CallingConvention.Cdecl)]
        internal static extern int sqlite3_bind_null(IntPtr statement, int index);
        [DllImport(Library, CallingConvention = CallingConvention.Cdecl)]
        internal static extern int sqlite3_bind_int64(IntPtr statement, int index, long value);
        [DllImport(Library, CallingConvention = CallingConvention.Cdecl)]
        internal static extern int sqlite3_bind_double(IntPtr statement, int index, double value);
        [DllImport(Library, CallingConvention = CallingConvention.Cdecl)]
        internal static extern int sqlite3_bind_text(IntPtr statement, int index, byte[] value, int bytes, IntPtr destructor);
        [DllImport(Library, CallingConvention = CallingConvention.Cdecl)]
        internal static extern int sqlite3_column_count(IntPtr statement);
        [DllImport(Library, CallingConvention = CallingConvention.Cdecl)]
        internal static extern int sqlite3_column_type(IntPtr statement, int index);
        [DllImport(Library, CallingConvention = CallingConvention.Cdecl)]
        internal static extern long sqlite3_column_int64(IntPtr statement, int index);
        [DllImport(Library, CallingConvention = CallingConvention.Cdecl)]
        internal static extern double sqlite3_column_double(IntPtr statement, int index);
        [DllImport(Library, CallingConvention = CallingConvention.Cdecl)]
        internal static extern IntPtr sqlite3_column_text(IntPtr statement, int index);
        [DllImport(Library, CallingConvention = CallingConvention.Cdecl)]
        internal static extern int sqlite3_column_bytes(IntPtr statement, int index);
    }
}

public sealed class SqliteStatement : IDisposable
{
    private static readonly IntPtr Transient = new(-1);
    private readonly SqliteConnection connection;
    internal IntPtr Pointer { get; private set; }

    internal SqliteStatement(SqliteConnection connection, IntPtr pointer)
    {
        this.connection = connection;
        Pointer = pointer;
    }

    public void Bind(params object?[] args)
    {
        SqliteConnection.Native.sqlite3_reset(Pointer);
        SqliteConnection.Native.sqlite3_clear_bindings(Pointer);
        for (var i = 0; i < args.Length; i++)
        {
            var index = i + 1;
            var code = args[i] switch
            {
                null => SqliteConnection.Native.sqlite3_bind_null(Pointer, index),
                string text => BindText(index, text),
                bool flag => SqliteConnection.Native.sqlite3_bind_int64(Pointer, index, flag ? 1 : 0),
                int number => SqliteConnection.Native.sqlite3_bind_int64(Pointer, index, number),
                long number => SqliteConnection.Native.sqlite3_bind_int64(Pointer, index, number),
                double number => SqliteConnection.Native.sqlite3_bind_double(Pointer, index, number),
                DateTimeOffset time => SqliteConnection.Native.sqlite3_bind_int64(Pointer, index, time.ToUnixTimeMilliseconds()),
                Enum value => BindText(index, value.ToString()),
                _ => throw new ArgumentException($"Unsupported SQLite parameter type {args[i]!.GetType().Name}"),
            };
            if (code != 0) throw new IOException($"SQLite bind failed: {connection.ErrorMessage()}");
        }
    }

    private int BindText(int index, string text)
    {
        var bytes = System.Text.Encoding.UTF8.GetBytes(text);
        return SqliteConnection.Native.sqlite3_bind_text(Pointer, index, bytes, bytes.Length, Transient);
    }

    /// <summary>Returns true while a row is available.</summary>
    public bool Step()
    {
        var code = SqliteConnection.Native.sqlite3_step(Pointer);
        if (code == SqliteConnection.Row) return true;
        if (code == SqliteConnection.Done) return false;
        throw new IOException($"SQLite step failed ({code}): {connection.ErrorMessage()}");
    }

    public void Run(params object?[] args)
    {
        lock (connection.Gate)
        {
            Bind(args);
            while (Step()) { }
        }
    }

    public void Dispose()
    {
        if (Pointer == IntPtr.Zero) return;
        SqliteConnection.Native.sqlite3_finalize(Pointer);
        Pointer = IntPtr.Zero;
    }
}

public readonly struct SqliteRow
{
    private readonly IntPtr statement;
    internal SqliteRow(IntPtr statement) => this.statement = statement;

    public bool IsNull(int index) => SqliteConnection.Native.sqlite3_column_type(statement, index) == 5;
    public long Int64(int index) => SqliteConnection.Native.sqlite3_column_int64(statement, index);
    public long? NullableInt64(int index) => IsNull(index) ? null : Int64(index);
    public int Int32(int index) => (int)Int64(index);
    public bool Bool(int index) => Int64(index) != 0;
    public double Double(int index) => SqliteConnection.Native.sqlite3_column_double(statement, index);

    public string? Text(int index)
    {
        if (IsNull(index)) return null;
        var pointer = SqliteConnection.Native.sqlite3_column_text(statement, index);
        var length = SqliteConnection.Native.sqlite3_column_bytes(statement, index);
        return pointer == IntPtr.Zero ? null : Marshal.PtrToStringUTF8(pointer, length);
    }

    public DateTimeOffset? Time(int index) => IsNull(index) ? null : DateTimeOffset.FromUnixTimeMilliseconds(Int64(index));

    public object? Value(int index) => SqliteConnection.Native.sqlite3_column_type(statement, index) switch
    {
        1 => Int64(index),
        2 => Double(index),
        3 => Text(index),
        5 => null,
        _ => Text(index),
    };
}
