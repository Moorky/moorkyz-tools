using System.Net;

namespace VisoryBulkDownloader.Core.Infrastructure;

/// <summary>A host family permitted for metadata or media, optionally restricted to a path prefix.</summary>
public sealed record HostRule(string Host, bool IncludeSubdomains = false, string? PathPrefix = null)
{
    public bool Matches(Uri uri)
    {
        var host = uri.IdnHost.ToLowerInvariant();
        var hostMatches = host == Host || (IncludeSubdomains && host.EndsWith("." + Host, StringComparison.Ordinal));
        if (!hostMatches) return false;
        return PathPrefix is null || uri.AbsolutePath.StartsWith(PathPrefix, StringComparison.Ordinal);
    }
}

public sealed class UrlPolicy
{
    private readonly IReadOnlyList<HostRule> rules;

    public UrlPolicy(IEnumerable<HostRule> rules, bool allowLoopbackHttpForTests = false)
    {
        this.rules = rules.ToArray();
        AllowLoopbackHttpForTests = allowLoopbackHttpForTests;
    }

    public bool AllowLoopbackHttpForTests { get; }
    public IReadOnlyList<HostRule> Rules => rules;

    public bool IsAllowed(Uri? uri, out string reason)
    {
        reason = string.Empty;
        if (uri is null || !uri.IsAbsoluteUri) { reason = "URL is not absolute"; return false; }
        if (!string.IsNullOrEmpty(uri.UserInfo)) { reason = "URL contains embedded credentials"; return false; }
        var loopback = uri.IsLoopback;
        if (loopback && AllowLoopbackHttpForTests && uri.Scheme is "http" or "https") return true;
        if (uri.Scheme != Uri.UriSchemeHttps) { reason = "Only HTTPS media URLs are accepted"; return false; }
        if (!uri.IsDefaultPort) { reason = "Non-standard ports are rejected"; return false; }
        if (uri.HostNameType is UriHostNameType.IPv4 or UriHostNameType.IPv6 || IPAddress.TryParse(uri.Host, out _))
        { reason = "IP-address hosts are rejected"; return false; }
        if (rules.Any(rule => rule.Matches(uri))) return true;
        reason = $"Host is not an allowed Visory media/metadata origin: {uri.IdnHost}";
        return false;
    }

    public void Require(Uri uri)
    {
        if (!IsAllowed(uri, out var reason)) throw new UnsafeUrlException(reason);
    }

    /// <summary>Removes query, fragment and credentials; signed URLs keep only their non-secret path.</summary>
    public static string Redact(Uri? uri)
    {
        if (uri is null) return string.Empty;
        if (!uri.IsAbsoluteUri) return "[relative-url]";
        var text = $"{uri.Scheme}://{uri.Authority.Split('@')[^1]}{uri.AbsolutePath}";
        return string.IsNullOrEmpty(uri.Query) ? text : text + "?[REDACTED]";
    }

    public static string Redact(string? url) => Uri.TryCreate(url, UriKind.Absolute, out var uri) ? Redact(uri) : string.IsNullOrEmpty(url) ? string.Empty : "[invalid-url]";
}

public sealed class UnsafeUrlException(string message) : Exception(message);

/// <summary>Origins derived from the public Visory web bootstrap (Firebase project visory-54ab5).</summary>
public static class VisoryEndpoints
{
    public const string AppUrl = "https://app.visory.cam/";
    public const string FirebaseProject = "visory-54ab5";
    public const string StorageBucket = "visory-54ab5.appspot.com";
    public const string RealtimeDatabaseHost = "visory-54ab5-default-rtdb.europe-west1.firebasedatabase.app";

    public static readonly HostRule[] MetadataRules =
    [
        new("visory.cam", IncludeSubdomains: true),
        new("visory.app", IncludeSubdomains: true),
        new(RealtimeDatabaseHost),
        new("firebasestorage.googleapis.com", PathPrefix: $"/v0/b/{StorageBucket}/"),
        new("storage.googleapis.com", PathPrefix: $"/{StorageBucket}/"),
        new("storage.googleapis.com", PathPrefix: $"/download/storage/v1/b/{StorageBucket}/"),
        new("storage.googleapis.com", PathPrefix: $"/storage/v1/b/{StorageBucket}/"),
        new("firestore.googleapis.com", PathPrefix: $"/v1/projects/{FirebaseProject}/"),
        new("firestore.googleapis.com", PathPrefix: "/google.firestore.v1.Firestore/"),
        new(StorageBucket + ".storage.googleapis.com"),
        new(FirebaseProject + ".firebaseapp.com"),
        new(FirebaseProject + ".web.app"),
    ];

    public static readonly HostRule[] MediaRules =
    [
        new("visory.cam", IncludeSubdomains: true),
        new("visory.app", IncludeSubdomains: true),
        new("firebasestorage.googleapis.com", PathPrefix: $"/v0/b/{StorageBucket}/o/"),
        new("storage.googleapis.com", PathPrefix: $"/{StorageBucket}/"),
        new("storage.googleapis.com", PathPrefix: $"/download/storage/v1/b/{StorageBucket}/o/"),
        new(StorageBucket + ".storage.googleapis.com"),
    ];

    public static UrlPolicy MetadataPolicy { get; } = new(MetadataRules);
    public static UrlPolicy MediaPolicy { get; } = new(MediaRules);
}
