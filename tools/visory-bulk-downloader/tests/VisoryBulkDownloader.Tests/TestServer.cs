using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using System.Text;

namespace VisoryBulkDownloader.Tests;

/// <summary>Minimal loopback HTTP/1.1 server giving tests byte-level control (ranges, aborts, missing lengths).</summary>
internal sealed class TestServer : IAsyncDisposable
{
    private readonly TcpListener listener = new(IPAddress.Loopback, 0);
    private readonly CancellationTokenSource stop = new();
    private readonly ConcurrentDictionary<string, Func<HttpContext, Task>> routes = new(StringComparer.Ordinal);
    private readonly Task acceptLoop;
    private int concurrent;

    public TestServer()
    {
        listener.Start();
        acceptLoop = Task.Run(AcceptAsync);
    }

    public int Port => ((IPEndPoint)listener.LocalEndpoint).Port;
    public string BaseUrl => $"http://127.0.0.1:{Port}";
    public ConcurrentQueue<RecordedRequest> Requests { get; } = new();
    public int MaxConcurrent;

    public Uri Url(string pathAndQuery) => new(BaseUrl + pathAndQuery);

    public void Map(string path, Func<HttpContext, Task> handler) => routes[path] = handler;

    public void MapMedia(string path, byte[] data, MediaOptions? options = null)
    {
        options ??= new MediaOptions();
        Map(path, ctx => ctx.ServeMediaAsync(data, options));
    }

    public IEnumerable<RecordedRequest> RequestsFor(string path) => Requests.Where(r => r.Path == path);

    private async Task AcceptAsync()
    {
        while (!stop.IsCancellationRequested)
        {
            TcpClient client;
            try { client = await listener.AcceptTcpClientAsync(stop.Token); }
            catch (Exception) { return; }
            _ = Task.Run(() => HandleAsync(client));
        }
    }

    private async Task HandleAsync(TcpClient client)
    {
        var now = Interlocked.Increment(ref concurrent);
        InterlockedMax(ref MaxConcurrent, now);
        try
        {
            using (client)
            {
                client.NoDelay = true;
                var stream = client.GetStream();
                var head = await ReadHeadAsync(stream);
                if (head is null) return;
                var lines = head.Split("\r\n");
                var parts = lines[0].Split(' ');
                if (parts.Length < 2) return;
                var target = parts[1];
                var path = target.Split('?')[0];
                var query = target.Contains('?') ? target[(target.IndexOf('?') + 1)..] : "";
                var headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                foreach (var line in lines.Skip(1))
                {
                    var colon = line.IndexOf(':');
                    if (colon > 0) headers[line[..colon].Trim()] = line[(colon + 1)..].Trim();
                }
                var ctx = new HttpContext(client, stream, parts[0], path, query, headers);
                Requests.Enqueue(new RecordedRequest(parts[0], path, query, headers));
                if (routes.TryGetValue(path, out var handler))
                {
                    try { await handler(ctx); }
                    catch (IOException) { }
                    catch (SocketException) { }
                    catch (ObjectDisposedException) { }
                }
                else
                {
                    await ctx.WriteHeadAsync(404, new() { ["Content-Length"] = "0" });
                }
            }
        }
        finally
        {
            Interlocked.Decrement(ref concurrent);
        }
    }

    private static void InterlockedMax(ref int target, int value)
    {
        int current;
        while ((current = Volatile.Read(ref target)) < value && Interlocked.CompareExchange(ref target, value, current) != current) { }
    }

    private static async Task<string?> ReadHeadAsync(NetworkStream stream)
    {
        var buffer = new List<byte>(1024);
        var one = new byte[1];
        while (buffer.Count < 32 * 1024)
        {
            var read = await stream.ReadAsync(one);
            if (read == 0) return null;
            buffer.Add(one[0]);
            var n = buffer.Count;
            if (n >= 4 && buffer[n - 4] == '\r' && buffer[n - 3] == '\n' && buffer[n - 2] == '\r' && buffer[n - 1] == '\n')
                return Encoding.ASCII.GetString(buffer.ToArray(), 0, n - 4);
        }
        return null;
    }

    public async ValueTask DisposeAsync()
    {
        stop.Cancel();
        listener.Stop();
        try { await acceptLoop; } catch (Exception) { }
        stop.Dispose();
    }
}

internal sealed record RecordedRequest(string Method, string Path, string Query, IReadOnlyDictionary<string, string> Headers)
{
    public string? Header(string name) => Headers.TryGetValue(name, out var value) ? value : null;
}

internal sealed class MediaOptions
{
    public string? ETag { get; set; } = "\"v1\"";
    public bool AcceptRanges { get; set; } = true;
    public bool IgnoreRange { get; set; }
    public bool SendLength { get; set; } = true;
    /// <summary>Abort the connection after this many body bytes (once per value, then cleared).</summary>
    public long? AbortAfterBytes { get; set; }
    public int ChunkDelayMs { get; set; }
    public int ChunkSize { get; set; } = 64 * 1024;
    public string ContentType { get; set; } = "video/mp4";
    public string? XGoogHashMd5 { get; set; }
}

internal sealed class HttpContext(TcpClient client, NetworkStream stream, string method, string path, string query, IReadOnlyDictionary<string, string> headers)
{
    public string Method { get; } = method;
    public string Path { get; } = path;
    public string Query { get; } = query;
    public IReadOnlyDictionary<string, string> Headers { get; } = headers;

    public string? QueryValue(string name) => Query.Split('&').Select(p => p.Split('=', 2)).Where(p => p.Length == 2 && p[0] == name).Select(p => Uri.UnescapeDataString(p[1])).FirstOrDefault();

    public async Task WriteHeadAsync(int status, Dictionary<string, string> responseHeaders)
    {
        var text = new StringBuilder($"HTTP/1.1 {status} {Reason(status)}\r\n");
        responseHeaders.TryAdd("Connection", "close");
        foreach (var (name, value) in responseHeaders) text.Append(name).Append(": ").Append(value).Append("\r\n");
        text.Append("\r\n");
        var bytes = Encoding.ASCII.GetBytes(text.ToString());
        await stream.WriteAsync(bytes);
    }

    public async Task StatusAsync(int status, Dictionary<string, string>? extra = null)
    {
        var body = Encoding.UTF8.GetBytes($"status {status}");
        var h = extra ?? new Dictionary<string, string>();
        h["Content-Length"] = body.Length.ToString(System.Globalization.CultureInfo.InvariantCulture);
        h["Content-Type"] = "text/plain";
        await WriteHeadAsync(status, h);
        if (Method != "HEAD") await stream.WriteAsync(body);
    }

    public void Abort()
    {
        client.LingerState = new LingerOption(true, 0);
        client.Client.Close(0);
    }

    public async Task ServeMediaAsync(byte[] data, MediaOptions options)
    {
        long from = 0;
        var to = data.LongLength - 1;
        var status = 200;
        var range = Headers.TryGetValue("Range", out var r) ? r : null;
        var ifRange = Headers.TryGetValue("If-Range", out var ir) ? ir : null;
        if (range is not null && options.AcceptRanges && !options.IgnoreRange && (ifRange is null || ifRange == options.ETag))
        {
            var spec = range.Replace("bytes=", "", StringComparison.Ordinal).Split('-');
            from = long.Parse(spec[0], System.Globalization.CultureInfo.InvariantCulture);
            if (spec.Length > 1 && spec[1].Length > 0) to = long.Parse(spec[1], System.Globalization.CultureInfo.InvariantCulture);
            if (from >= data.LongLength)
            {
                await WriteHeadAsync(416, new() { ["Content-Range"] = $"bytes */{data.LongLength}", ["Content-Length"] = "0" });
                return;
            }
            to = Math.Min(to, data.LongLength - 1);
            status = 206;
        }
        var length = to - from + 1;
        var h = new Dictionary<string, string> { ["Content-Type"] = options.ContentType };
        if (options.SendLength) h["Content-Length"] = length.ToString(System.Globalization.CultureInfo.InvariantCulture);
        if (options.AcceptRanges) h["Accept-Ranges"] = "bytes";
        if (options.ETag is not null) h["ETag"] = options.ETag;
        if (options.XGoogHashMd5 is not null) h["x-goog-hash"] = "crc32c=AAAAAA==,md5=" + options.XGoogHashMd5;
        if (status == 206) h["Content-Range"] = $"bytes {from}-{to}/{data.LongLength}";
        await WriteHeadAsync(status, h);
        if (Method == "HEAD") return;
        long sent = 0;
        var abortAt = options.AbortAfterBytes;
        if (abortAt is not null) options.AbortAfterBytes = null;
        while (sent < length)
        {
            var count = (int)Math.Min(options.ChunkSize, length - sent);
            if (abortAt is { } limit && sent + count > limit) count = (int)Math.Max(0, limit - sent);
            if (count > 0) await stream.WriteAsync(data.AsMemory((int)(from + sent), count));
            sent += count;
            if (abortAt is { } stopAt && sent >= stopAt)
            {
                await stream.FlushAsync();
                await Task.Delay(30);
                Abort();
                return;
            }
            if (options.ChunkDelayMs > 0) await Task.Delay(options.ChunkDelayMs);
        }
        await stream.FlushAsync();
    }

    private static string Reason(int status) => status switch
    {
        200 => "OK", 206 => "Partial Content", 403 => "Forbidden", 404 => "Not Found", 416 => "Range Not Satisfiable",
        429 => "Too Many Requests", 500 => "Internal Server Error", 503 => "Service Unavailable", 401 => "Unauthorized", _ => "Status",
    };
}
