using System.Net;
using System.Security.Cryptography;
using System.Text;

namespace MoorkszVisoryMediaDownloader.Core.Reporting;

public static class DocumentHtml
{
    private static readonly string Style = LoadStyle();
    private static readonly string StyleHash = Convert.ToBase64String(SHA256.HashData(Encoding.UTF8.GetBytes(Style)));

    private static string LoadStyle()
    {
        using var stream = typeof(DocumentHtml).Assembly.GetManifestResourceStream("Visory.Document.css")
            ?? throw new InvalidDataException("The embedded document stylesheet is missing.");
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd().Replace("\r\n", "\n", StringComparison.Ordinal);
    }

    public static string Start(string title) => "<!doctype html><html lang=\"en\"><head><meta charset=\"utf-8\">" +
        "<meta name=\"viewport\" content=\"width=device-width, initial-scale=1\">" +
        $"<meta http-equiv=\"Content-Security-Policy\" content=\"default-src 'none'; style-src 'sha256-{StyleHash}'; base-uri 'none'; form-action 'none'\">" +
        $"<title>{WebUtility.HtmlEncode(title)}</title><style>{Style}</style></head><body>" +
        "<a class=\"skip-link\" href=\"#main\">Skip to content</a>";

    public const string End = "</body></html>";
}
