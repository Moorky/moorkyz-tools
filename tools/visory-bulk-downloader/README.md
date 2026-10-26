# Visory Bulk Downloader

**Open, connect, scan, back up, done.** A portable Windows desktop hub that backs up the
recordings your authenticated Visory web session can access, then leaves the transfer
running quietly in the background with resumable downloads, exact reports and a
verified ZIP.

> **Unofficial tool.** Not affiliated with, endorsed by or supported by Visory.
> Real-account Visory behavior is **unverified** in development; see
> [VISORY_INTEGRATION.md](VISORY_INTEGRATION.md) before relying on it for important
> footage. Version 0.1.0 is an unreleased development preview.

## What it does

1. Sign in to the official Visory web app inside the hub (Microsoft Edge WebView2).
2. Open your cloud recordings in Visory; the hub collects the recording metadata the app
   loads (or press **Auto-scroll list**). Press **Finish scan**.
3. Review the inventory: discovered recordings, cameras, already verified, missing,
   corrupt, needing retry, estimated size, free space and scan completeness.
4. **Start backup.** Minimize or close the window; the backup continues in the tray.
5. Return to verified files, an optional verified ZIP, a run summary and reports.

When the inventory cannot be confirmed complete, the hub says **Discovered recordings**
and shows a warning instead of claiming every recording was found.

**Demo mode** generates clearly labeled synthetic recordings in a separate demo folder,
so the full workflow can be tried without a Visory account. Demo data is never shown as
account data.

## Requirements

- Windows 10 or 11, x64.
- Microsoft Edge WebView2 Runtime for Visory sign-in (preinstalled on Windows 11 and most
  Windows 10 systems). If it is missing, the hub explains this and links to Microsoft's
  official download page; nothing is installed silently. Demo mode works without it.
- No .NET, Node.js, Python, PowerShell or other runtime is required to run the portable
  EXE. It is self-contained (.NET 10.0.11, WPF).

The EXE is **not code-signed**. Windows SmartScreen, Smart App Control or an organization's
application-control policy may block unsigned builds. On the development machine, Smart
App Control blocked some individual build outputs (see [TEST_REPORT.md](TEST_REPORT.md)).

## Data and privacy

Recordings are sensitive. Everything stays on your computer: no uploads, accounts on our
side, telemetry or third-party services.

| Data | Location |
|---|---|
| Settings, app log, demo backup | `VisoryBulkDownloader Data\` beside the EXE when that folder is writable (portable mode); otherwise `%LOCALAPPDATA%\VisoryBulkDownloader\` |
| Visory sign-in session (WebView2 profile) | Always `%LOCALAPPDATA%\VisoryBulkDownloader\Sessions\<id>\` |
| Backup (downloads, archives, runs, state) | Your chosen backup folder, default `Videos\Visory Backup` |

**Portability versus account security.** The settings folder can travel with the EXE.
The sign-in session deliberately does not: WebView2 protects cookies with Windows
encryption bound to your Windows account, so a copied profile would not work elsewhere,
and keeping it off USB sticks avoids leaking an account session on removable media.
Passwords are never stored by this app (WebView2 password saving is disabled).
Download links that carry access tokens are stored in the state database encrypted with
Windows DPAPI (current user). ID tokens are kept in memory only.

**Settings → Privacy** offers **Sign out** (deletes the WebView2 profile and in-memory
tokens) and **Delete local session data** (also removes stored download links). Failed
reports redact URL query strings unless you explicitly enable full URLs, which shows a
warning that signed URLs may grant temporary access. Authorization headers, cookies and
tokens are never written to logs or reports.

## Backup folder layout

```
Visory Backup/
├── downloads/                 verified recordings (in-progress files end with .part)
├── archives/                  Visory_<run-id>.zip (…_INCOMPLETE.zip when scope had failures)
├── runs/<yyyy-MM-dd_HH-mm-ss>/
│   ├── logs/log_<id>.txt              exact SUCCESS/FAIL lines, plus INFO/WARN/SKIP/RETRY
│   ├── logs/log_report_<id>.csv       one row per file result (UTF-8, Excel-safe)
│   ├── logs/debug_<id>.log            attempt-level diagnostics (redacted)
│   ├── logs/run_summary_<id>.json     completion summary
│   └── failed/failed_report_<id>.csv  timestamp, filename, filesize, message, url, …
└── state/visory-backup.db     SQLite (Windows-supplied winsqlite3) recording state
```

Each file receives exactly one final SUCCESS or FAIL per run, enforced in the database.
Already verified files receive one SKIP. A changed local file is preserved as
`<name>.corrupt-<timestamp>` and downloaded again; nothing is overwritten.

## Background behavior

- **Smart background** (default) prefers Windows BITS for compatible links (no sign-in
  headers, byte-range support, no bandwidth limit), lowers process priority and caps
  direct transfers at two. **Balanced** and **Fast** raise BITS priority and use the
  configured concurrency.
- Closing the window during a backup minimizes to the tray (explained once). The tray
  shows progress and speed and offers Pause/Resume, Open downloads, Open latest report
  and **Exit safely**.
- **Exit safely** hands compatible BITS jobs to Windows (they continue while you are
  signed in) and stops direct downloads with their `.part` data. The next start
  reconciles BITS jobs and offers **Resume interrupted run**. Transfers do not continue
  after Windows sign-out for direct downloads, and BITS continuity depends on Windows.
- Optional, user-enabled scheduling: a daily Task Scheduler task (`--scheduled`) and a
  quiet start after sign-in (`--background`, Task Scheduler with a per-user startup
  entry fallback). Scheduled scans reuse the routes you visited during an interactive
  scan and require a still-valid Visory session; otherwise the hub notifies you.

## Build and test (developers)

The canonical source is `tools/visory-bulk-downloader` in the
[Moorky/moorkyz-tools](https://github.com/Moorky/moorkyz-tools) repository. Requires the
.NET 10 SDK on Windows; run the scripts from this folder.

```powershell
.\scripts\build.ps1                 # Release build of all projects
.\scripts\test.ps1                  # automated suite (Debug), isolated .fixtures folder
.\scripts\test.ps1 -Heavy -Filter 25b   # opt-in 4.2 GiB ZIP64 archive test
.\scripts\publish.ps1               # artifacts\release\VisoryBulkDownloader-<version>-win-x64\
```

`publish.ps1` produces one self-contained single-file `VisoryBulkDownloader.exe`, copies
these documents and all required license notices, and writes `SHA256SUMS.txt`.
The app icon is stored as Base64 text (`app.ico.b64`) and decoded during the build;
`scripts\make-icon.ps1` regenerates it.

## Documentation

- [ARCHITECTURE.md](ARCHITECTURE.md) — components, data flow, state machine and safety rules.
- [VISORY_INTEGRATION.md](VISORY_INTEGRATION.md) — verified facts, observation contract,
  and what is unverified.
- [TEST_REPORT.md](TEST_REPORT.md) — automated scenarios and smoke evidence.
- [RELEASE_NOTES.md](RELEASE_NOTES.md) — version notes and known limitations.
- [THIRD-PARTY-NOTICES.md](THIRD-PARTY-NOTICES.md) — dependencies and redistributed components.

## License

MIT © 2026 Moorkyz contributors. Third-party components retain their own licenses.
