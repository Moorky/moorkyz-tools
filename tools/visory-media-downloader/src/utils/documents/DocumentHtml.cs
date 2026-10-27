using System.Net;
using System.Security.Cryptography;
using System.Text;

namespace MoorkszVisoryMediaDownloader.Core.Reporting;

public static class DocumentHtml
{
    private static readonly string Style = LoadStyle();
    private static readonly string StyleHash = Convert.ToBase64String(SHA256.HashData(Encoding.UTF8.GetBytes(Style)));

    public static string LoadResource(string name)
    {
        using var stream = typeof(DocumentHtml).Assembly.GetManifestResourceStream(name)
            ?? throw new InvalidDataException("An embedded document resource is missing: " + name);
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd().Replace("\r\n", "\n", StringComparison.Ordinal);
    }

    private static string LoadStyle() => LoadResource("Visory.Document.css")
        .Replace("__DM_MONO__", LoadResource("Visory.DMMono.b64").Trim(), StringComparison.Ordinal);

    public static string Start(string title, string? script = null) => "<!doctype html><html lang=\"en\"><head><meta charset=\"utf-8\">" +
        "<meta name=\"viewport\" content=\"width=device-width, initial-scale=1\">" +
        $"<meta http-equiv=\"Content-Security-Policy\" content=\"default-src 'none'; font-src data:; style-src 'sha256-{StyleHash}'; " +
        (script is null ? "" : $"script-src 'sha256-{Convert.ToBase64String(SHA256.HashData(Encoding.UTF8.GetBytes(script)))}'; ") +
        "base-uri 'none'; form-action 'none'\">" +
        $"<title>{WebUtility.HtmlEncode(title)}</title><style>{Style}</style></head><body>" +
        "<a class=\"skip-link\" href=\"#main\">Skip to content</a>";

    public const string End = "</body></html>";
}
