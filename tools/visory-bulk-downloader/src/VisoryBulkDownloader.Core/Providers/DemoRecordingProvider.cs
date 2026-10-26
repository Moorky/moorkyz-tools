using System.Net;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using VisoryBulkDownloader.Core.Infrastructure;

namespace VisoryBulkDownloader.Core.Providers;

/// <summary>
/// Clearly labeled demonstration provider. Generates deterministic synthetic "recordings" served by an in-memory
/// transport; nothing is fetched from the network and nothing represents a real Visory account.
/// </summary>
public sealed class DemoRecordingProvider : IRecordingProvider
{
    public const string DemoHost = "demo.visory-bulk-downloader.invalid";
    private readonly int count;
    private readonly DateTimeOffset origin;

    public DemoRecordingProvider(int count = 24, DateTimeOffset? origin = null)
    {
        this.count = count;
        this.origin = origin ?? new DateTimeOffset(2026, 10, 1, 8, 0, 0, TimeSpan.Zero);
        MediaPolicy = new UrlPolicy([new HostRule(DemoHost)]);
    }

    public string ProviderId => "demo";
    public string DisplayName => "Demo (synthetic data)";
    public bool IsSynthetic => true;
    public UrlPolicy MediaPolicy { get; }

    public async Task<DiscoveryResult> DiscoverAsync(IProgress<DiscoveryProgress>? progress, CancellationToken cancellationToken)
    {
        var list = new List<DiscoveredRecording>();
        for (var i = 0; i < count; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var camera = $"demo-camera-{i % 3 + 1}";
            var at = origin.AddMinutes(i * 37);
            var id = $"demo-{i:D4}";
            list.Add(new DiscoveredRecording
            {
                RecordingId = id,
                CameraId = camera,
                CameraName = $"Demo camera {i % 3 + 1}",
                RecordedAt = at,
                RecordedAtSource = "synthetic",
                OriginalName = $"{id}.webm",
                MediaType = "video/webm",
                Size = SyntheticMediaHandler.SizeFor(id),
                DownloadUrl = $"https://{DemoHost}/media/{id}.webm",
            });
            if (i % 8 == 7)
            {
                progress?.Report(new DiscoveryProgress(list.Count, Math.Min(3, list.Count), "Generating demo recordings"));
                await Task.Delay(40, cancellationToken).ConfigureAwait(false);
            }
        }
        return new DiscoveryResult(list, DiscoveryCompleteness.Complete,
            ["DEMO MODE: these are synthetic recordings generated locally. They are not from your Visory account."], []);
    }

    public Task<MediaRequest> GetMediaRequestAsync(RecordingEntry recording, bool refresh, CancellationToken cancellationToken) =>
        Task.FromResult(MediaRequest.Plain(new Uri($"https://{DemoHost}/media/{recording.RecordingId}.webm")));

    public HttpMessageHandler? CreateMediaHandler() => new SyntheticMediaHandler(TimeSpan.FromMilliseconds(4));
}

/// <summary>Serves deterministic bytes with HEAD, Content-Length, ETag and byte-range support entirely in memory.</summary>
public sealed class SyntheticMediaHandler(TimeSpan chunkDelay) : HttpMessageHandler
{
    public static long SizeFor(string id) => 512 * 1024 + BitConverter.ToUInt32(SHA256.HashData(Encoding.UTF8.GetBytes(id)), 0) % (3 * 1024 * 1024);

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var path = request.RequestUri?.AbsolutePath ?? "";
        var id = Path.GetFileNameWithoutExtension(path);
        if (request.RequestUri?.IdnHost != DemoRecordingProvider.DemoHost || !path.StartsWith("/media/", StringComparison.Ordinal))
            return new HttpResponseMessage(HttpStatusCode.NotFound) { RequestMessage = request };
        var size = SizeFor(id);
        long from = 0;
        var status = HttpStatusCode.OK;
        if (request.Headers.Range?.Ranges.FirstOrDefault() is { From: { } start } && start < size)
        {
            from = start;
            status = HttpStatusCode.PartialContent;
        }
        var length = size - from;
        var response = new HttpResponseMessage(status) { RequestMessage = request };
        response.Headers.AcceptRanges.Add("bytes");
        response.Headers.ETag = new EntityTagHeaderValue($"\"{id}-v1\"");
        HttpContent content = request.Method == HttpMethod.Head
            ? new ByteArrayContent([])
            : new StreamContent(new SyntheticStream(id, from, length, chunkDelay));
        content.Headers.ContentLength = length;
        content.Headers.ContentType = new MediaTypeHeaderValue("video/webm");
        if (status == HttpStatusCode.PartialContent) content.Headers.ContentRange = new ContentRangeHeaderValue(from, size - 1, size);
        response.Content = content;
        await Task.Yield();
        return response;
    }

    public static byte ByteAt(string id, long position) => (byte)((position * 31 + id.Length * 7 + (position >> 9)) & 0xFF);

    private sealed class SyntheticStream(string id, long start, long length, TimeSpan delay) : Stream
    {
        private long position;
        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => length;
        public override long Position { get => position; set => throw new NotSupportedException(); }
        public override void Flush() { }
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();

        public override int Read(byte[] buffer, int offset, int count) => Fill(buffer.AsSpan(offset, count));

        public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        {
            if (delay > TimeSpan.Zero) await Task.Delay(delay, cancellationToken).ConfigureAwait(false);
            return Fill(buffer.Span[..Math.Min(buffer.Length, 64 * 1024)]);
        }

        private int Fill(Span<byte> span)
        {
            var count = (int)Math.Min(span.Length, length - position);
            for (var i = 0; i < count; i++) span[i] = ByteAt(id, start + position + i);
            position += count;
            return count;
        }
    }
}
