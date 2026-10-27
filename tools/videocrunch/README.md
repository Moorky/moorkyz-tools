# Moorkyz Video Crunch

A private, Windows-first video workspace: paste an authorized video link, download the original, make a browser-friendly MP4, and watch it offline. Charcoal/violet UI, live PowerShell-blue terminal, no account, no hosted uploads.

## One-click start, including first use

This copy is integrated into the tools.moorkyz.com tool catalog. From the parent repository
root, `pnpm --dir src tool:videocrunch` starts this same localhost service; explicitly enable
the portal connection and open `/tools/videocrunch` to use it inside the portal.
Source browsing and ZIP downloads do not require a running service. Native
prerequisites and all local-only limitations below still apply.

1. Download/extract the complete repository onto your Windows PC.
2. Double-click **[Start-WebVideoCrunch.cmd](Start-WebVideoCrunch.cmd)**.
3. Your browser opens at **http://127.0.0.1:8765**. Keep the terminal open.

On first use the launcher installs missing **Node.js 22+**, **yt-dlp**, and **FFmpeg** through Windows Package Manager (`winget`), accepts their package/source agreements, refreshes PATH, and starts the app. Internet access, Windows App Installer, and any Windows installation approval are still required. It does not bypass administrator or organizational policies. No `npm install` is needed to run the application.

If your PC already has the tools, launch is entirely local. If installation is blocked, use the [troubleshooting guide](docs/TROUBLESHOOTING.md).

Alternative startup:

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\Start-WebVideoCrunch.ps1
# Another port, or without opening the browser:
.\Start-WebVideoCrunch.ps1 -Port 8766 -NoBrowser
# When prerequisites are already installed:
npm start
```

## Use it

Paste a public video-page URL or direct HTTP(S) media/stream URL. Choose **Balanced**, **Crunch**, or **Extreme**, or select **Just the original**. Click **Download & compress**, follow live logs, then open the player or download the files.

The default export root is **`C:\Users\marvi\Desktop\VideoExports`** on this PC (the current user's `Desktop\VideoExports` on other PCs). You can change it in the form. One export runs at a time; history retains the last 50 entries across restarts.

```text
VideoExports/
  example.com-20261022-223000-123-a1b2c3d4/
    index.html
    page-url.txt
    page-source.html
    detected-urls.txt
    resolved-stream-urls.txt
    manifest.json
    original/
      source.mp4
    compressed/
      video.mp4
```

`page-source.html` exists only when HTML is retrievable. `source.*` uses the downloaded format's extension. `compressed/` is absent when compression is skipped. Failed attempts may retain files in `.downloads/`; successful temporary downloads are moved unchanged into `original/`.

## Important limits

- Download only media you own or are authorized to save. No DRM, authentication, or paywall bypass.
- Compression is lossy and can produce a larger file. Originals remain unchanged.
- Website changes, anti-bot protections, unavailable videos, and missing codecs can cause explicit export failures.
- Original formats are not always playable in a browser; use a compatible desktop player or compress to MP4.
- History, logs, and stream URLs can contain sensitive temporary tokens. Keep them and your output folders private.
- The app binds only to `127.0.0.1`. It is not a hosted/multi-user service; do not reverse-proxy or expose it.

## Documentation

| Resource | Purpose |
|---|---|
| [CLI guide](Get-WebVideoCrunch-README.md) | Standalone PowerShell command and compression details |
| [Architecture](docs/ARCHITECTURE.md) | Backend, frontend, job lifecycle, files, trust boundaries |
| [Testing](docs/TESTING.md) | Regression tests, real-media integration, manual browser checks |
| [Troubleshooting](docs/TROUBLESHOOTING.md) | Setup, ports, download failures, recovery |
| [Dependencies](docs/DEPENDENCIES.md) | Runtime requirements and third-party licenses |
| [Contributing](CONTRIBUTING.md) | Development, verification, audit, commit workflow |
| [Security](SECURITY.md) | Privacy, responsible reporting, operational limits |
| [Guidelines](guidelines/README.md) | Engineering, design, and audit rules |
| [Ideas](ideas/README.md) | An idea template and an archive for proposals |
| [Tools](tools/README.md) | Doctor, verification, and audit commands |
| [Audits](audits/README.md) | Evidence-based audit records and limitations |
| [Changelog](CHANGELOG.md) | User-visible changes |

## Development checks

```powershell
.\tools\Doctor.ps1
.\tools\Verify.ps1
.\tools\Verify.ps1 -Media
# Optional browser tests (development-only dependency):
npm ci
.\tools\Verify.ps1 -Browser
.\tools\Audit.ps1
```

No application runtime npm dependencies are required. Tests use Node's built-in test runner and Windows PowerShell's Pester 3.4/4.x. Optional Playwright browser tests use the installed Microsoft Edge and require `npm ci` for development packages only. Media and browser tests generate synthetic video locally, not third-party copyrighted footage.

## License status

No distribution license has been selected by the repository owner. Do not assume an open-source license. External tools keep their own licenses; review [dependency notes](docs/DEPENDENCIES.md) before redistribution.
