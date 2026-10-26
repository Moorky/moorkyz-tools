using System.Buffers;
using System.Net;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using VisoryBulkDownloader.Core.Infrastructure;

namespace VisoryBulkDownloader.Core.Download;

public enum FailureKind { Transient, Network, AuthExpired, Permanent, Integrity, DiskFull, Paused, Stopped, Canceled }

public sealed class TransferException(FailureKind kind, string message, int? httpStatus = null, TimeSpan? retryAfter = null, Exception? inner = null)
    : Exception(message, inner)
{
    public FailureKind Kind { get; } = kind;
    public int? HttpStatus { get; } = httpStatus;
    public TimeSpan? RetryAfter { get; } = retryAfter;
    public long BytesTransferred { get; init; }

    public static TransferException FromStatus(HttpStatusCode code, TimeSpan? retryAfter, string? reason = null)
    {
        var status = (int)code;
        return status switch
        {
            401 => new(FailureKind.AuthExpired, "HTTP 401: Authorization required", status),
            403 => new(FailureKind.AuthExpired, "HTTP 403: Download authorization expired", status),
            404 => new(FailureKind.Permanent, "HTTP 404: Recording not found on server", status),
            410 => new(FailureKind.Permanent, "HTTP 410: Recording is no longer available", status),
            408 => new(FailureKind.Transient, "HTTP 408: Server timed out waiting for the request", status, retryAfter),
            429 => new(FailureKind.Transient, "HTTP 429: Rate limited by server", status, retryAfter),
            >= 500 and <= 599 => new(FailureKind.Transient, $"HTTP {status}: Server error{(string.IsNullOrWhiteSpace(reason) ? "" : " (" + reason + ")")}", status, retryAfter),
            _ => new(FailureKind.Permanent, $"HTTP {status}: {(string.IsNullOrWhiteSpace(reason) ? "Unexpected response" : reason)}", status),
        };
    }

    public static bool IsDiskFull(IOException e) => (e.HResult & 0xFFFF) is 112 or 39;
}

/// <summary>Shared token bucket limiting aggregate direct-download throughput.</summary>
public sealed class Throttle(long bytesPerSecond)
{
    private readonly object gate = new();
    private double tokens = bytesPerSecond;
    private long lastTicks = Environment.TickCount64;

    public long BytesPerSecond { get; set; } = bytesPerSecond;

    public async ValueTask ConsumeAsync(int bytes, CancellationToken cancellationToken)
    {
        while (BytesPerSecond > 0)
        {
            TimeSpan wait;
            lock (gate)
            {
                var now = Environment.TickCount64;
                tokens = Math.Min(BytesPerSecond, tokens + (now - lastTicks) / 1000.0 * BytesPerSecond);
                lastTicks = now;
                if (tokens >= bytes || tokens >= BytesPerSecond) { tokens -= bytes; return; }
                wait = TimeSpan.FromMilliseconds(Math.Max(10, (bytes - tokens) / BytesPerSecond * 1000));
            }
            await Task.Delay(wait, cancellationToken).ConfigureAwait(false);
        }
    }
}

public sealed record ResumeInfo(string Validator, long Total);

public sealed record TransferOutcome(long Length, string Sha256, long? DeclaredTotal, string? ExpectedMd5, string? ActualMd5,
    long BytesThisAttempt, double Seconds, bool Resumed, TransferEngineKind Engine);

public sealed record ProbeResult(Uri FinalUrl, int Status, long? Length, bool AcceptsRanges, bool Encoded);

public sealed class ProgressCounter
{
    private long bytes;
    public long Bytes => Interlocked.Read(ref bytes);
    public long? Total { get; set; }
    public void Set(long value) => Interlocked.Exchange(ref bytes, value);
    public void Add(long value) => Interlocked.Add(ref bytes, value);
}

/// <summary>Streams one HTTP(S) response to a .part file with validated, optional byte-range continuation.</summary>
public sealed class HttpTransfer(HttpClient client, UrlPolicy policy, Throttle throttle, TimeSpan idleTimeout)
{
    private const int BufferSize = 256 * 1024;
    private const int MaxRedirects = 5;

    public static HttpClient CreateClient(HttpMessageHandler? custom, TimeSpan connectTimeout)
    {
        var handler = custom ?? new SocketsHttpHandler
        {
            AllowAutoRedirect = false,
            AutomaticDecompression = DecompressionMethods.None,
            UseCookies = false,
            ConnectTimeout = connectTimeout,
            PooledConnectionIdleTimeout = TimeSpan.FromMinutes(1),
            PooledConnectionLifetime = TimeSpan.FromMinutes(10),
        };
        var http = new HttpClient(handler, disposeHandler: true) { Timeout = Timeout.InfiniteTimeSpan };
        http.DefaultRequestHeaders.UserAgent.ParseAdd("VisoryBulkDownloader/0.1");
        http.DefaultRequestHeaders.AcceptEncoding.ParseAdd("identity");
        return http;
    }

    public async Task<ProbeResult> ProbeAsync(MediaRequest request, CancellationToken cancellationToken)
    {
        using var response = await SendAsync(HttpMethod.Head, request, null, null, cancellationToken).ConfigureAwait(false);
        var accepts = response.Headers.AcceptRanges.Contains("bytes", StringComparer.OrdinalIgnoreCase);
        var encoded = response.Content.Headers.ContentEncoding.Count > 0;
        return new ProbeResult(response.RequestMessage?.RequestUri ?? request.Url, (int)response.StatusCode, response.Content.Headers.ContentLength, accepts, encoded);
    }

    public async Task<TransferOutcome> DownloadAsync(MediaRequest request, string partPath, ResumeInfo? resume, string? expectedMd5,
        Action<ResumeInfo?> persistResume, ProgressCounter progress, CancellationToken cancellationToken)
    {
        var started = Environment.TickCount64;
        var existing = File.Exists(partPath) ? new FileInfo(partPath).Length : 0;
        if (existing > 0 && resume is null)
        {
            Truncate(partPath);
            existing = 0;
        }
        var rangeFrom = existing > 0 ? existing : (long?)null;
        var response = await SendAsync(HttpMethod.Get, request, rangeFrom, resume?.Validator, cancellationToken).ConfigureAwait(false);
        try
        {
            var resumed = false;
            long? total;
            string? remoteMd5 = null;
            if (rangeFrom is not null && response.StatusCode == HttpStatusCode.PartialContent)
            {
                var range = response.Content.Headers.ContentRange;
                if (range?.From == existing && range.Length == resume!.Total)
                {
                    resumed = true;
                    total = range.Length;
                }
                else
                {
                    response.Dispose();
                    Truncate(partPath);
                    existing = 0;
                    persistResume(null);
                    response = await SendAsync(HttpMethod.Get, request, null, null, cancellationToken).ConfigureAwait(false);
                    EnsureSuccess(response);
                    total = response.Content.Headers.ContentLength;
                }
            }
            else if (rangeFrom is not null && response.StatusCode == HttpStatusCode.RequestedRangeNotSatisfiable && existing == resume?.Total)
            {
                progress.Total = existing;
                progress.Set(existing);
                var (sha, md5) = await HashFileAsync(partPath, expectedMd5 is not null, cancellationToken).ConfigureAwait(false);
                return new TransferOutcome(existing, sha, existing, expectedMd5, md5, 0, Seconds(started), true, TransferEngineKind.Http);
            }
            else
            {
                if (rangeFrom is not null && response.StatusCode == HttpStatusCode.RequestedRangeNotSatisfiable)
                {
                    response.Dispose();
                    response = await SendAsync(HttpMethod.Get, request, null, null, cancellationToken).ConfigureAwait(false);
                }
                EnsureSuccess(response);
                if (response.StatusCode == HttpStatusCode.PartialContent)
                    throw new TransferException(FailureKind.Transient, "Server returned an unexpected partial response");
                if (rangeFrom is not null)
                {
                    // The server ignored the range or the object changed: continuing would corrupt the file.
                    Truncate(partPath);
                    existing = 0;
                    persistResume(null);
                }
                total = response.Content.Headers.ContentLength;
                remoteMd5 = RemoteMd5(response);
                var validator = response.Headers.ETag is { IsWeak: false } etag ? etag.ToString()
                    : response.Content.Headers.LastModified is { } modified ? modified.ToString("R") : null;
                var rangeable = response.Headers.AcceptRanges.Contains("bytes", StringComparer.OrdinalIgnoreCase);
                persistResume(rangeable && validator is not null && total is > 0 ? new ResumeInfo(validator, total.Value) : null);
            }
            expectedMd5 ??= remoteMd5;
            progress.Total = total;
            progress.Set(existing);

            using var sha256 = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
            using var md5Hash = expectedMd5 is null ? null : IncrementalHash.CreateHash(HashAlgorithmName.MD5);
            if (existing > 0) await HashPrefixAsync(partPath, existing, sha256, md5Hash, cancellationToken).ConfigureAwait(false);

            long written = 0;
            var buffer = ArrayPool<byte>.Shared.Rent(BufferSize);
            try
            {
                await using var file = new FileStream(partPath, FileMode.OpenOrCreate, FileAccess.Write, FileShare.Read, 1, FileOptions.Asynchronous | FileOptions.SequentialScan);
                file.Seek(existing, SeekOrigin.Begin);
                file.SetLength(existing);
                using var idle = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                idle.CancelAfter(idleTimeout);
                await using var body = await response.Content.ReadAsStreamAsync(idle.Token).ConfigureAwait(false);
                while (true)
                {
                    int read;
                    try
                    {
                        idle.CancelAfter(idleTimeout);
                        read = await body.ReadAsync(buffer.AsMemory(0, BufferSize), idle.Token).ConfigureAwait(false);
                    }
                    catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
                    {
                        throw new TransferException(FailureKind.Transient, $"No data received for {idleTimeout.TotalSeconds:0}s (timeout)") { BytesTransferred = written };
                    }
                    catch (Exception e) when (e is IOException or HttpRequestException && !cancellationToken.IsCancellationRequested)
                    {
                        throw new TransferException(FailureKind.Transient, "Server closed connection unexpectedly", inner: e) { BytesTransferred = written };
                    }
                    if (read == 0) break;
                    try
                    {
                        await file.WriteAsync(buffer.AsMemory(0, read), cancellationToken).ConfigureAwait(false);
                    }
                    catch (IOException e) when (TransferException.IsDiskFull(e))
                    {
                        throw new TransferException(FailureKind.DiskFull, "Insufficient disk space", inner: e) { BytesTransferred = written };
                    }
                    sha256.AppendData(buffer, 0, read);
                    md5Hash?.AppendData(buffer, 0, read);
                    written += read;
                    progress.Add(read);
                    await throttle.ConsumeAsync(read, cancellationToken).ConfigureAwait(false);
                }
                try
                {
                    file.Flush(flushToDisk: true);
                }
                catch (IOException e) when (TransferException.IsDiskFull(e))
                {
                    throw new TransferException(FailureKind.DiskFull, "Insufficient disk space", inner: e) { BytesTransferred = written };
                }
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(buffer);
            }
            var length = new FileInfo(partPath).Length;
            if (total is { } declared && length != declared)
                throw new TransferException(FailureKind.Integrity, $"Size mismatch: expected {declared} bytes, received {length}") { BytesTransferred = written };
            var actualMd5 = md5Hash is null ? null : Convert.ToBase64String(md5Hash.GetHashAndReset());
            return new TransferOutcome(length, Convert.ToHexString(sha256.GetHashAndReset()).ToLowerInvariant(), total, expectedMd5, actualMd5, written, Seconds(started), resumed, TransferEngineKind.Http);
        }
        finally
        {
            response.Dispose();
        }
    }

    private async Task<HttpResponseMessage> SendAsync(HttpMethod method, MediaRequest request, long? rangeFrom, string? ifRange, CancellationToken cancellationToken)
    {
        var url = request.Url;
        var headers = request.Headers;
        for (var hop = 0; hop <= MaxRedirects; hop++)
        {
            if (!policy.IsAllowed(url, out var reason))
                throw new TransferException(FailureKind.Permanent, hop == 0 ? $"Rejected unsafe URL: {reason}" : $"Rejected redirect: {reason}");
            using var message = new HttpRequestMessage(method, url);
            foreach (var (name, value) in headers) message.Headers.TryAddWithoutValidation(name, value);
            if (rangeFrom is not null)
            {
                message.Headers.Range = new RangeHeaderValue(rangeFrom, null);
                if (ifRange is not null)
                {
                    if (ifRange.StartsWith('"')) message.Headers.IfRange = new RangeConditionHeaderValue(EntityTagHeaderValue.Parse(ifRange));
                    else if (DateTimeOffset.TryParse(ifRange, Format.Invariant, System.Globalization.DateTimeStyles.AssumeUniversal, out var date)) message.Headers.IfRange = new RangeConditionHeaderValue(date);
                }
            }
            HttpResponseMessage response;
            using var connect = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            connect.CancelAfter(idleTimeout);
            try
            {
                response = await client.SendAsync(message, HttpCompletionOption.ResponseHeadersRead, connect.Token).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                throw new TransferException(FailureKind.Network, $"No response within {idleTimeout.TotalSeconds:0}s (timeout)");
            }
            catch (HttpRequestException e)
            {
                throw new TransferException(FailureKind.Network, "Network error: " + (e.InnerException?.Message ?? e.Message), inner: e);
            }
            var status = (int)response.StatusCode;
            if (status is 301 or 302 or 303 or 307 or 308 && response.Headers.Location is { } location)
            {
                var next = location.IsAbsoluteUri ? location : new Uri(url, location);
                response.Dispose();
                if (!string.Equals(next.IdnHost, url.IdnHost, StringComparison.OrdinalIgnoreCase)) headers = new Dictionary<string, string>();
                url = next;
                continue;
            }
            return response;
        }
        throw new TransferException(FailureKind.Permanent, "Too many redirects");
    }

    private static void EnsureSuccess(HttpResponseMessage response)
    {
        if (response.IsSuccessStatusCode) return;
        TimeSpan? retryAfter = response.Headers.RetryAfter switch
        {
            { Delta: { } delta } => delta,
            { Date: { } date } => date - DateTimeOffset.UtcNow,
            _ => null,
        };
        if (retryAfter < TimeSpan.Zero) retryAfter = TimeSpan.Zero;
        throw TransferException.FromStatus(response.StatusCode, retryAfter, response.ReasonPhrase);
    }

    private static string? RemoteMd5(HttpResponseMessage response)
    {
        if (response.Content.Headers.ContentMD5 is { Length: 16 } contentMd5) return Convert.ToBase64String(contentMd5);
        if (response.Headers.TryGetValues("x-goog-hash", out var values))
        {
            foreach (var part in values.SelectMany(v => v.Split(',')))
            {
                var trimmed = part.Trim();
                if (trimmed.StartsWith("md5=", StringComparison.OrdinalIgnoreCase)) return trimmed[4..];
            }
        }
        return null;
    }

    private static void Truncate(string path)
    {
        if (File.Exists(path)) using (var stream = new FileStream(path, FileMode.Truncate, FileAccess.Write)) { }
    }

    private static double Seconds(long startedTicks) => Math.Max(0.001, (Environment.TickCount64 - startedTicks) / 1000.0);

    private static async Task HashPrefixAsync(string path, long length, IncrementalHash sha, IncrementalHash? md5, CancellationToken cancellationToken)
    {
        var buffer = ArrayPool<byte>.Shared.Rent(BufferSize);
        try
        {
            await using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite, 1, FileOptions.Asynchronous | FileOptions.SequentialScan);
            var remaining = length;
            while (remaining > 0)
            {
                var read = await stream.ReadAsync(buffer.AsMemory(0, (int)Math.Min(BufferSize, remaining)), cancellationToken).ConfigureAwait(false);
                if (read == 0) throw new TransferException(FailureKind.Integrity, "Partial file shrank during resume");
                sha.AppendData(buffer, 0, read);
                md5?.AppendData(buffer, 0, read);
                remaining -= read;
            }
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer);
        }
    }

    public static async Task<(string Sha256, string? Md5)> HashFileAsync(string path, bool includeMd5, CancellationToken cancellationToken)
    {
        using var sha = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        using var md5 = includeMd5 ? IncrementalHash.CreateHash(HashAlgorithmName.MD5) : null;
        await HashPrefixAsync(path, new FileInfo(path).Length, sha, md5, cancellationToken).ConfigureAwait(false);
        return (Convert.ToHexString(sha.GetHashAndReset()).ToLowerInvariant(), md5 is null ? null : Convert.ToBase64String(md5.GetHashAndReset()));
    }
}
