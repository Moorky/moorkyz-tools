using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using MoorkszVisoryMediaDownloader.Core.Infrastructure;
using MoorkszVisoryMediaDownloader.Core.Storage;

namespace MoorkszVisoryMediaDownloader.Core.Providers;

/// <summary>
/// Accumulates recordings from network traffic observed while the user browses the official Visory web app.
/// Pure and deterministic so it can be tested with captured-shape fixtures; it never issues requests.
/// </summary>
public sealed partial class DiscoverySession
{
    public const int MaxMetadataBytes = 8 * 1024 * 1024;
    private const string ProviderId = "visory";
    private static readonly string[] IdFields = ["recordingId", "recording_id", "videoId", "video_id", "eventId", "event_id", "clipId", "clip_id", "id", "key"];
    private static readonly string[] CameraIdFields = ["cameraId", "camera_id", "cameraUid", "camera_uid", "camId", "deviceId", "device_id", "deviceUid", "sourceId"];
    private static readonly string[] CameraNameFields = ["cameraName", "camera_name", "deviceName", "device_name", "camera", "device", "sourceName"];
    private static readonly string[] TimeFields = ["recordedAt", "recorded_at", "startTime", "start_time", "startedAt", "started_at", "createdAt", "created_at", "timestamp", "time", "date", "dateTime", "timeCreated", "creationTime", "start"];
    private static readonly string[] SizeFields = ["size", "fileSize", "file_size", "sizeBytes", "size_bytes", "bytes", "contentLength"];
    private static readonly string[] NameFields = ["fileName", "filename", "file_name", "name", "title"];
    private static readonly string[] UrlFields = ["downloadUrl", "downloadURL", "download_url", "url", "videoUrl", "video_url", "mediaUrl", "media_url", "recordingUrl", "recording_url", "fileUrl", "file_url", "src", "link"];
    private static readonly string[] PathFields = ["storagePath", "storage_path", "fullPath", "full_path", "path", "filePath", "file_path", "videoPath", "video_path", "objectPath", "object", "file", "ref"];
    private static readonly string[] TypeFields = ["contentType", "mimeType", "mime_type", "mediaType", "media_type"];
    private static readonly string[] CameraSegments = ["cameras", "camera", "devices", "device"];
    private static readonly string[] IgnoredNameParts = ["thumb", "preview", "poster", "image", "snapshot", "avatar", "icon", "cover"];

    private readonly UrlPolicy metadataPolicy;
    private readonly UrlPolicy mediaPolicy;
    private readonly string bucket;
    private readonly Dictionary<string, DiscoveredRecording> recordings = new(StringComparer.Ordinal);
    private readonly Dictionary<string, string> objectIndex = new(StringComparer.Ordinal);
    private readonly HashSet<string> outstandingPageTokens = new(StringComparer.Ordinal);
    private readonly HashSet<string> resolvedPageTokens = new(StringComparer.Ordinal);
    private readonly List<string> errors = [];
    private readonly List<string> diagnostics = [];
    private readonly Dictionary<string, (int Remaining, StringBuilder Buffer)> frameAssembly = new(StringComparer.Ordinal);

    public DiscoverySession(UrlPolicy? metadataPolicy = null, UrlPolicy? mediaPolicy = null, string bucket = VisoryEndpoints.StorageBucket)
    {
        this.metadataPolicy = metadataPolicy ?? VisoryEndpoints.MetadataPolicy;
        this.mediaPolicy = mediaPolicy ?? VisoryEndpoints.MediaPolicy;
        this.bucket = bucket;
    }

    public IReadOnlyCollection<DiscoveredRecording> Recordings => recordings.Values;
    public int CameraCount => recordings.Values.Select(r => r.CameraId ?? r.CameraName).Where(c => c is not null).Distinct().Count();
    public IReadOnlyList<string> Errors => errors;
    public IReadOnlyList<string> Diagnostics => diagnostics;
    public int OutstandingPages => outstandingPageTokens.Count;
    public DateTimeOffset? LastNewRecordingAt { get; private set; }
    public int ObservedMetadataResponses { get; private set; }

    /// <summary>Only small JSON responses from Visory/Firebase origins of this project are inspected.</summary>
    public bool ShouldInspect(Uri uri, string? contentType, long? contentLength)
    {
        if (!metadataPolicy.IsAllowed(uri, out _)) return false;
        if (contentLength is > MaxMetadataBytes) return false;
        var type = contentType?.Split(';')[0].Trim().ToLowerInvariant();
        return type is "application/json" or "text/json" or "application/x-javascript" or "text/javascript" || (type is null or "text/plain" && uri.AbsolutePath.EndsWith(".json", StringComparison.OrdinalIgnoreCase));
    }

    public bool IsMediaCandidate(Uri uri, string? contentType) =>
        mediaPolicy.IsAllowed(uri, out _) && (MediaTypes.IsMediaContentType(contentType) || MediaTypes.IsMediaPath(uri.AbsolutePath));

    public void ObserveRequest(Uri uri)
    {
        var token = QueryValue(uri, "pageToken");
        if (token is null) return;
        outstandingPageTokens.Remove(token);
        resolvedPageTokens.Add(token);
    }

    public void ObserveError(Uri uri, int status)
    {
        if (!metadataPolicy.IsAllowed(uri, out _)) return;
        var message = $"HTTP {status} while the Visory app loaded {UrlPolicy.Redact(uri)}";
        if (!errors.Contains(message)) errors.Add(message);
    }

    /// <summary>Parses a JSON metadata response. Returns the number of newly discovered recordings.</summary>
    public int ObserveHttpJson(Uri source, string body, DateTimeOffset now)
    {
        if (!metadataPolicy.IsAllowed(source, out _) || body.Length > MaxMetadataBytes) return 0;
        ObserveRequest(source);
        JsonNode? node;
        try
        {
            node = JsonNode.Parse(body, documentOptions: new JsonDocumentOptions { MaxDepth = 64, AllowTrailingCommas = true });
        }
        catch (JsonException)
        {
            return 0;
        }
        if (node is null) return 0;
        ObservedMetadataResponses++;
        var path = new List<string>();
        if (source.IdnHost.Equals(VisoryEndpoints.RealtimeDatabaseHost, StringComparison.OrdinalIgnoreCase) || source.IdnHost.EndsWith(".firebasedatabase.app", StringComparison.OrdinalIgnoreCase))
        {
            var rtdbPath = Uri.UnescapeDataString(source.AbsolutePath).Trim('/');
            if (rtdbPath.EndsWith(".json", StringComparison.OrdinalIgnoreCase)) rtdbPath = rtdbPath[..^5];
            path.AddRange(rtdbPath.Split('/', StringSplitOptions.RemoveEmptyEntries));
        }
        return Walk(node, path.LastOrDefault(), path, 0, now);
    }

    /// <summary>Parses a Firebase Realtime Database WebSocket frame, reassembling multi-frame messages.</summary>
    public int ObserveWebSocketFrame(string socketId, string payload, DateTimeOffset now)
    {
        if (frameAssembly.TryGetValue(socketId, out var pending))
        {
            pending.Buffer.Append(payload);
            pending.Remaining--;
            if (pending.Remaining > 0) { frameAssembly[socketId] = pending; return 0; }
            frameAssembly.Remove(socketId);
            payload = pending.Buffer.ToString();
        }
        else if (payload.Length <= 6 && int.TryParse(payload, NumberStyles.None, CultureInfo.InvariantCulture, out var frames) && frames > 1)
        {
            frameAssembly[socketId] = (frames, new StringBuilder());
            return 0;
        }
        if (payload.Length > MaxMetadataBytes) return 0;
        JsonNode? message;
        try { message = JsonNode.Parse(payload); }
        catch (JsonException) { return 0; }
        if (message is not JsonObject || Str(message["t"]) != "d") return 0;
        var data = message["d"] as JsonObject;
        var body = data?["b"] as JsonObject;
        if (body is null) return 0;
        var action = Str(data?["a"]);
        if (action is "d" or "m" && body["p"] is JsonValue p && p.TryGetValue<string>(out var rtdbPath))
        {
            ObservedMetadataResponses++;
            var segments = rtdbPath.Split('/', StringSplitOptions.RemoveEmptyEntries).ToList();
            return body["d"] is { } payloadData ? Walk(payloadData, segments.LastOrDefault(), segments, 0, now) : 0;
        }
        if (body["s"] is JsonValue status && status.TryGetValue<string>(out var statusText) && statusText != "ok")
        {
            var message2 = $"Realtime Database request rejected: {statusText}";
            if (!errors.Contains(message2)) errors.Add(message2);
        }
        return 0;
    }

    /// <summary>Records a media response the Visory app itself loaded (for example while playing a recording).</summary>
    public int ObserveMediaResponse(Uri url, string? contentType, long? contentLength, int status, DateTimeOffset now)
    {
        if (!IsMediaCandidate(url, contentType)) return 0;
        var objectPath = FirebaseObjectName(url);
        var name = Path.GetFileName(Uri.UnescapeDataString(objectPath ?? url.AbsolutePath));
        var candidate = new DiscoveredRecording
        {
            RecordingId = objectPath ?? url.IdnHost + url.AbsolutePath,
            OriginalName = name,
            ObjectPath = objectPath,
            MediaType = contentType?.Split(';')[0].Trim(),
            Size = status == 200 ? contentLength : null,
            DownloadUrl = url.AbsoluteUri,
            CameraId = CameraFromSegments((objectPath ?? url.AbsolutePath).Split('/', StringSplitOptions.RemoveEmptyEntries)),
        };
        return Add(WithFilenameTime(candidate), now) ? 1 : 0;
    }

    public DiscoveryResult ToResult(bool scanFinishedNormally, bool allowComplete = false)
    {
        var notes = new List<string>
        {
            "Visory does not publish a recordings API or total count. Results reflect the metadata the official web app loaded during this scan.",
        };
        DiscoveryCompleteness completeness;
        if (!scanFinishedNormally)
        {
            completeness = DiscoveryCompleteness.Incomplete;
            notes.Add("The scan was stopped before it finished.");
        }
        else if (outstandingPageTokens.Count > 0)
        {
            completeness = DiscoveryCompleteness.Incomplete;
            notes.Add($"{outstandingPageTokens.Count} additional result page(s) were announced but never loaded. Scroll further or scan again.");
        }
        else if (errors.Count > 0)
        {
            completeness = DiscoveryCompleteness.Incomplete;
        }
        else if (ObservedMetadataResponses == 0)
        {
            completeness = DiscoveryCompleteness.Incomplete;
            notes.Add("No recording metadata was observed. Open the cloud recordings view in Visory before finishing the scan.");
        }
        else
        {
            completeness = allowComplete ? DiscoveryCompleteness.Complete : DiscoveryCompleteness.Uncertain;
        }
        var list = recordings.Values.OrderByDescending(r => r.RecordedAt ?? DateTimeOffset.MinValue).ThenBy(r => r.RecordingId, StringComparer.Ordinal).ToList();
        return new DiscoveryResult(list, completeness, notes, errors.ToList());
    }

    private int Walk(JsonNode node, string? keyInParent, List<string> path, int depth, DateTimeOffset now)
    {
        if (depth > 48) return 0;
        var added = 0;
        switch (node)
        {
            case JsonObject obj:
                if (TryStorageList(obj, now, ref added) || TryStorageMetadata(obj, now, ref added)) return added;
                if (obj["documents"] is JsonArray documents)
                {
                    foreach (var doc in documents.OfType<JsonObject>()) added += WalkFirestoreDocument(doc, path, depth, now);
                    if (obj["nextPageToken"] is JsonValue firestoreToken && firestoreToken.TryGetValue<string>(out var ft) && !resolvedPageTokens.Contains(ft)) outstandingPageTokens.Add(ft);
                    return added;
                }
                if (obj["fields"] is JsonObject && obj["name"] is JsonValue docName && docName.TryGetValue<string>(out var dn) && dn.StartsWith("projects/", StringComparison.Ordinal))
                    return WalkFirestoreDocument(obj, path, depth, now);
                if (TryRecording(obj, keyInParent, path, now, out var isNew))
                    return isNew ? 1 : 0;
                foreach (var (name, child) in obj.ToList())
                {
                    if (child is null) continue;
                    path.Add(name);
                    added += Walk(child, name, path, depth + 1, now);
                    path.RemoveAt(path.Count - 1);
                }
                return added;
            case JsonArray array:
                foreach (var child in array)
                    if (child is not null) added += Walk(child, keyInParent, path, depth + 1, now);
                return added;
            default:
                return 0;
        }
    }

    private int WalkFirestoreDocument(JsonObject document, List<string> path, int depth, DateTimeOffset now)
    {
        var name = Str(document["name"]) ?? "";
        var segments = name.Split("/documents/", 2) is { Length: 2 } parts ? parts[1].Split('/') : [];
        var flattened = FlattenFirestore(document["fields"]) as JsonObject ?? new JsonObject();
        var local = new List<string>(path);
        local.AddRange(segments);
        return Walk(flattened, segments.LastOrDefault(), local, depth + 1, now);
    }

    private static JsonNode? FlattenFirestore(JsonNode? value)
    {
        if (value is not JsonObject obj) return value?.DeepClone();
        if (obj.Count == 1)
        {
            var (type, inner) = obj.First();
            switch (type)
            {
                case "stringValue" or "timestampValue" or "referenceValue": return JsonValue.Create(Str(inner));
                case "integerValue": return long.TryParse(inner?.ToString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var l) ? JsonValue.Create(l) : null;
                case "doubleValue": return inner?.DeepClone();
                case "booleanValue": return inner?.DeepClone();
                case "nullValue": return null;
                case "mapValue": return FlattenFirestore(inner?["fields"]) ?? new JsonObject();
                case "arrayValue":
                    var array = new JsonArray();
                    foreach (var item in inner?["values"] as JsonArray ?? new JsonArray()) array.Add(FlattenFirestore(item));
                    return array;
            }
        }
        var result = new JsonObject();
        foreach (var (key, child) in obj) result[key] = FlattenFirestore(child);
        return result;
    }

    private bool TryStorageList(JsonObject obj, DateTimeOffset now, ref int added)
    {
        if (obj["items"] is not JsonArray items || (obj["prefixes"] is null && obj["nextPageToken"] is null && !items.OfType<JsonObject>().Any(i => i["bucket"] is not null))) return false;
        if (obj["nextPageToken"] is JsonValue tokenValue && tokenValue.TryGetValue<string>(out var token) && !resolvedPageTokens.Contains(token))
            outstandingPageTokens.Add(token);
        foreach (var item in items.OfType<JsonObject>())
        {
            if (!TryStorageMetadata(item, now, ref added) && Str(item["name"]) is { } name && MediaTypes.IsMediaPath(name) && (Str(item["bucket"]) ?? bucket) == bucket)
            {
                var candidate = new DiscoveredRecording
                {
                    RecordingId = name,
                    ObjectPath = name,
                    OriginalName = Path.GetFileName(name),
                    CameraId = CameraFromSegments(name.Split('/')),
                };
                if (Add(WithFilenameTime(candidate), now)) added++;
            }
        }
        return true;
    }

    private bool TryStorageMetadata(JsonObject obj, DateTimeOffset now, ref int added)
    {
        if (Str(obj["bucket"]) is not { } objBucket || Str(obj["name"]) is not { } name) return false;
        if (obj["contentType"] is null && obj["size"] is null && obj["downloadTokens"] is null) return false;
        if (objBucket != bucket) return true;
        var type = Str(obj["contentType"]);
        if (!MediaTypes.IsMediaPath(name) && !MediaTypes.IsMediaContentType(type)) return true;
        string? url = null;
        if (Str(obj["downloadTokens"]) is { } tokens && tokens.Split(',')[0].Trim() is { Length: > 0 } firstToken)
            url = $"https://firebasestorage.googleapis.com/v0/b/{bucket}/o/{Uri.EscapeDataString(name)}?alt=media&token={Uri.EscapeDataString(firstToken)}";
        var candidate = new DiscoveredRecording
        {
            RecordingId = name,
            ObjectPath = name,
            OriginalName = Path.GetFileName(name),
            MediaType = type,
            Size = Long(obj["size"]),
            RecordedAt = Time(obj["timeCreated"]),
            RecordedAtSource = obj["timeCreated"] is null ? null : "remote:storage.timeCreated (upload time)",
            RemoteMd5 = Str(obj["md5Hash"]),
            DownloadUrl = url,
            CameraId = CameraFromSegments(name.Split('/')),
        };
        if (Add(WithFilenameTime(candidate), now)) added++;
        return true;
    }

    private bool TryRecording(JsonObject obj, string? keyInParent, List<string> path, DateTimeOffset now, out bool isNew)
    {
        isNew = false;
        string? url = null;
        string? objectPath = null;
        foreach (var (name, value) in obj)
        {
            if (IgnoredNameParts.Any(p => name.Contains(p, StringComparison.OrdinalIgnoreCase))) continue;
            if (Str(value) is not { } text) continue;
            if (url is null && Uri.TryCreate(text, UriKind.Absolute, out var uri) && uri.Scheme is "https" or "http"
                && (MediaTypes.IsMediaPath(uri.AbsolutePath) || text.Contains("alt=media", StringComparison.Ordinal) || UrlFields.Contains(name)))
            {
                if (!MediaTypes.IsMediaPath(uri.AbsolutePath) && !text.Contains("alt=media", StringComparison.Ordinal)) continue;
                if (mediaPolicy.IsAllowed(uri, out var reason)) url = text;
                else AddDiagnostic($"Ignored media URL outside allowed origins ({reason}): {UrlPolicy.Redact(uri)}");
            }
            else if (objectPath is null && PathFields.Contains(name) && !text.Contains("://", StringComparison.Ordinal) && MediaTypes.IsMediaPath(text))
            {
                objectPath = text.TrimStart('/');
            }
        }
        if (url is null && objectPath is null) return false;
        if (url is not null && objectPath is null && Uri.TryCreate(url, UriKind.Absolute, out var parsed)) objectPath = FirebaseObjectName(parsed);

        string? id = FirstScalar(obj, IdFields);
        if (id is null && keyInParent is not null && LooksLikeId(keyInParent)) id = keyInParent;
        id ??= objectPath;
        if (id is null && url is not null && Uri.TryCreate(url, UriKind.Absolute, out var u)) id = u.IdnHost + u.AbsolutePath;

        string? cameraId = FirstScalar(obj, CameraIdFields);
        string? cameraName = null;
        foreach (var field in CameraNameFields)
        {
            if (obj[field] is JsonObject cameraObject)
            {
                cameraName ??= Str(cameraObject["name"]) ?? Str(cameraObject["title"]);
                cameraId ??= FirstScalar(cameraObject, ["id", "uid", "deviceId"]);
            }
            else if (Str(obj[field]) is { } text && !text.Contains("://", StringComparison.Ordinal)) cameraName ??= text;
        }
        cameraId ??= CameraFromSegments(path) ?? (objectPath is null ? null : CameraFromSegments(objectPath.Split('/')));

        DateTimeOffset? recordedAt = null;
        string? source = null;
        foreach (var field in TimeFields)
        {
            if (obj[field] is { } t && Time(t) is { } parsedTime) { recordedAt = parsedTime; source = "remote:" + field; break; }
        }
        long? size = null;
        foreach (var field in SizeFields)
            if (Long(obj[field]) is { } s && s >= 0) { size = s; break; }
        var originalName = NameFields.Select(f => Str(obj[f])).FirstOrDefault(n => n is not null && MediaTypes.IsMediaPath(n))
            ?? (objectPath is not null ? Path.GetFileName(objectPath) : url is not null && Uri.TryCreate(url, UriKind.Absolute, out var nameUri) ? Path.GetFileName(Uri.UnescapeDataString(nameUri.AbsolutePath)) : null);
        var mediaType = TypeFields.Select(f => Str(obj[f])).FirstOrDefault(v => v is not null && v.Contains('/', StringComparison.Ordinal));

        var fingerprint = false;
        if (id is null)
        {
            id = StateStore.Fingerprint(ProviderId, cameraId ?? cameraName, recordedAt, originalName, size);
            fingerprint = true;
        }
        var candidate = new DiscoveredRecording
        {
            RecordingId = id,
            IdIsFingerprint = fingerprint,
            CameraId = cameraId,
            CameraName = cameraName,
            RecordedAt = recordedAt,
            RecordedAtSource = source,
            OriginalName = originalName,
            MediaType = mediaType,
            Size = size,
            DownloadUrl = url,
            ObjectPath = objectPath,
            RemoteMd5 = Str(obj["md5Hash"]) ?? Str(obj["md5"]),
        };
        isNew = Add(WithFilenameTime(candidate), now);
        return true;
    }

    private bool Add(DiscoveredRecording candidate, DateTimeOffset now)
    {
        if (candidate.ObjectPath is { } objectPath && objectIndex.TryGetValue(objectPath, out var knownId) && knownId != candidate.RecordingId)
            candidate = candidate with { RecordingId = knownId, IdIsFingerprint = recordings[knownId].IdIsFingerprint };
        if (recordings.TryGetValue(candidate.RecordingId, out var existing))
        {
            var merged = existing.Merge(candidate);
            recordings[candidate.RecordingId] = merged;
            if (merged.ObjectPath is not null) objectIndex.TryAdd(merged.ObjectPath, candidate.RecordingId);
            return false;
        }
        recordings[candidate.RecordingId] = candidate;
        if (candidate.ObjectPath is not null) objectIndex.TryAdd(candidate.ObjectPath, candidate.RecordingId);
        LastNewRecordingAt = now;
        return true;
    }

    private void AddDiagnostic(string message)
    {
        if (diagnostics.Count < 500 && !diagnostics.Contains(message)) diagnostics.Add(message);
    }

    private static DiscoveredRecording WithFilenameTime(DiscoveredRecording r)
    {
        if (r.RecordedAt is not null) return r;
        var name = r.OriginalName ?? r.ObjectPath;
        if (name is null) return r;
        var epoch = EpochInName().Match(name);
        if (epoch.Success && long.TryParse(epoch.Groups[1].Value, NumberStyles.None, CultureInfo.InvariantCulture, out var ms))
            return r with { RecordedAt = DateTimeOffset.FromUnixTimeMilliseconds(ms), RecordedAtSource = "filename:epoch-milliseconds" };
        var stamp = StampInName().Match(name);
        if (stamp.Success)
        {
            var text = string.Concat(stamp.Groups.Values.Skip(1).Select(g => g.Value));
            if (DateTime.TryParseExact(text, "yyyyMMddHHmmss", CultureInfo.InvariantCulture, DateTimeStyles.AssumeLocal, out var local))
                return r with { RecordedAt = new DateTimeOffset(local), RecordedAtSource = "filename (time zone assumed local)" };
        }
        return r;
    }

    private static string? CameraFromSegments(IReadOnlyList<string> segments)
    {
        for (var i = 0; i < segments.Count - 1; i++)
            if (CameraSegments.Contains(segments[i], StringComparer.OrdinalIgnoreCase) && LooksLikeId(segments[i + 1]))
                return segments[i + 1];
        return null;
    }

    private string? FirebaseObjectName(Uri url)
    {
        var prefix = $"/v0/b/{bucket}/o/";
        if (url.IdnHost == "firebasestorage.googleapis.com" && url.AbsolutePath.StartsWith(prefix, StringComparison.Ordinal))
            return Uri.UnescapeDataString(url.AbsolutePath[prefix.Length..]);
        if (url.IdnHost == "storage.googleapis.com" && url.AbsolutePath.StartsWith($"/{bucket}/", StringComparison.Ordinal))
            return Uri.UnescapeDataString(url.AbsolutePath[(bucket.Length + 2)..]);
        return null;
    }

    private static bool LooksLikeId(string value) =>
        value.Length is >= 4 and <= 128 && !value.Contains(' ', StringComparison.Ordinal) && !CameraSegments.Contains(value, StringComparer.OrdinalIgnoreCase)
        && value.Any(char.IsLetterOrDigit) && !IsCollectionWord(value);

    private static bool IsCollectionWord(string value) => value.ToLowerInvariant() is "recordings" or "videos" or "events" or "clips" or "items" or "data" or "users" or "cloud" or "files";

    private static string? QueryValue(Uri uri, string name)
    {
        foreach (var part in uri.Query.TrimStart('?').Split('&', StringSplitOptions.RemoveEmptyEntries))
        {
            var pair = part.Split('=', 2);
            if (pair.Length == 2 && Uri.UnescapeDataString(pair[0]) == name) return Uri.UnescapeDataString(pair[1].Replace('+', ' '));
        }
        return null;
    }

    private static string? Str(JsonNode? node) => node is JsonValue value && value.TryGetValue<string>(out var text) && !string.IsNullOrWhiteSpace(text) ? text : null;

    private static string? FirstScalar(JsonObject obj, IEnumerable<string> fields)
    {
        foreach (var field in fields)
        {
            if (obj[field] is not JsonValue value) continue;
            if (value.TryGetValue<string>(out var text) && !string.IsNullOrWhiteSpace(text) && text.Length <= 256) return text;
            if (value.TryGetValue<long>(out var number)) return number.ToString(CultureInfo.InvariantCulture);
        }
        return null;
    }

    private static long? Long(JsonNode? node)
    {
        if (node is not JsonValue value) return null;
        if (value.TryGetValue<long>(out var number)) return number;
        if (value.TryGetValue<double>(out var d) && d >= 0 && d < long.MaxValue && Math.Abs(d % 1) < double.Epsilon) return (long)d;
        if (value.TryGetValue<string>(out var text) && long.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out var parsed)) return parsed;
        return null;
    }

    private static DateTimeOffset? Time(JsonNode? node)
    {
        if (node is not JsonValue value) return null;
        double? number = value.TryGetValue<double>(out var d) ? d : value.TryGetValue<long>(out var l) ? l : null;
        if (number is null && value.TryGetValue<string>(out var text))
        {
            if (double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsedNumber)) number = parsedNumber;
            else if (DateTimeOffset.TryParse(text, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var parsed)) return Plausible(parsed);
        }
        if (number is not { } n) return null;
        try
        {
            var time = n > 1e14 ? DateTimeOffset.FromUnixTimeMilliseconds((long)(n / 1000))
                : n > 1e11 ? DateTimeOffset.FromUnixTimeMilliseconds((long)n)
                : n > 1e8 ? DateTimeOffset.FromUnixTimeSeconds((long)n)
                : (DateTimeOffset?)null;
            return time is { } t ? Plausible(t) : null;
        }
        catch (ArgumentOutOfRangeException)
        {
            return null;
        }
    }

    private static DateTimeOffset? Plausible(DateTimeOffset value) => value.Year is >= 2000 and <= 2100 ? value : null;

    [GeneratedRegex(@"(?<!\d)(1[5-9]\d{11})(?!\d)")]
    private static partial Regex EpochInName();

    [GeneratedRegex(@"(20\d{2})[-_]?(\d{2})[-_]?(\d{2})[T_ -]?(\d{2})[-_:.]?(\d{2})[-_:.]?(\d{2})")]
    private static partial Regex StampInName();
}
