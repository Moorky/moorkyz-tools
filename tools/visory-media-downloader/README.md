# Moorksz Visory Media Downloader

A local Windows tool for recordings available through your own Visory session.
This unreleased, unofficial project is currently rebuilding its structure,
logging and reports. Real-account discovery remains unverified; this phase does
not claim to repair missing recordings.

## Start

```powershell
.\CMD.cmd
.\CMD.cmd -CheckOnly
```

`CMD.cmd` is the single normal entry point. Windows uses `.cmd` for batch
launchers, so the requested command entry is not named `CMD.run`.
Logging starts in the terminal before prerequisite checks, build or app launch.
The terminal remains attached while the desktop hub is in the tray.
Exit through the app for orderly completion. Failure keeps the console visible;
automated callers can set `VISORY_NONINTERACTIVE=1` to disable the pause.

Requirements: Windows 10/11 x64, Windows PowerShell 5.1 and .NET 10 SDK.
The existing WebView2 NuGet package is restored by the build when necessary;
unchanged build outputs are reused. WebView2 Runtime is checked before sign-in.
Missing native prerequisites are reported, not automatically installed.
Application-control policies are never bypassed.
`-CheckOnly` does not build, start the UI or contact Visory.

## Structure

```text
visory-media-downloader/
  CMD.cmd                         one terminal entry point
  README.html                     local introduction and guide
  README.md                       canonical documentation source
  LICENSE                         existing project license
  src/
    tools/
      desktop/                    WPF hub and WebView2 adapter
      engine/
        Providers/                discovery and synthetic demo provider
        Download/                 transfers, resume and retry
        Archive/                  verified ZIP creation
        Backup/                   orchestration and result state
        Storage/                  SQLite state
        Scheduling.cs             explicit Windows scheduling feature
    utils/
      diagnostics/                per-line logging, redaction and daily reports
      documents/                  shared local document presentation
      system/                     path, formatting, URL and Windows helpers
    config/                       runtime settings and output layout
    localization/                 English/German text
    scripts/
      startup/                    launch checks and terminal capture
      development/                build, test, publish, icon and README tooling
    tests/                        synthetic and startup regression tests
    docs/                         architecture and integration details
  runtime/runs/                   generated startup/app logs and stage reports
```

These are responsibilities inside one tool, not separate apps or repositories.
Runtime features belong under `src/tools`; shared helpers are utilities;
one-off automation is a script. Arbitrary `scripts_a`/`scripts_b` groups are
unnecessary when names describe their jobs. Project metadata and explicit source
allowlists also remain at the root. No separate website or landing page is added.

## Daily report

Open `reports/report.html` in the selected backup folder. It groups every recorded
local day and shows SUCCESS, FAIL and ERROR counts, known processing time,
successful byte totals and success ratio. The same outcomes are in `report.csv`
and `report.md`; HTML and Markdown embed the CSV data rather than only linking it.

```text
ID | Timestamp | Status | Filename | Full Path | Time Spent (s)
File Size (bytes) | Length (s) | Log | Error | Success Ratio | Message
```

- ID: Unix milliseconds plus a unique suffix, stable once written.
- Timestamp: recorded local time, `yyyy-MM-dd HH:mm:ss`; detailed run CSV retains the UTC offset too.
- Status: SUCCESS, FAIL or ERROR. Already-verified SKIP entries and run summaries stay in detailed run records.
- Full Path: absolute saved destination, or the planned destination for a failed file. It does not imply the file exists.
- Time Spent: processing seconds, distinct from video/audio Length.
- File Size and Length: bytes and media seconds. Unknown values stay blank, never invented as zero.
- Log and Error: clickable relative Markdown file links with exact physical line numbers and same-line anchors.
- Success Ratio: `SUCCESS / (SUCCESS + FAIL + ERROR)` for the whole recorded day, across runs.
- Message: redacted result or diagnostic context.

Metadata-only errors may have no filename, destination, size or duration.
Media length is currently only shown when supplied by a result; no codec probe,
external program or extra download is introduced to guess it.
Rendered Markdown viewers honor log anchors; plain-text browsers may open the
file without scrolling, so the link label also gives its exact line number.

## Logs and output

```text
runtime/runs/<timestamp_guid>/
  logs/startup.md                  terminal/check/build/app output
  logs/app.md                      lifecycle and discovery diagnostics
  failed/startup-errors.md
  failed/app-errors.md
  reports/startup.csv              startup stage results and durations
  reports/startup.html
  reports/startup.md

<selected backup folder>/
  downloads/                      verified recordings; active files end in .part
  archives/                       optional verified ZIPs
  runs/<run-id>/
    logs/log_<run-id>.md
    failed/errors_<run-id>.md
    reports/report_<run-id>.csv    durable detailed results
    reports/report_<run-id>.html
    reports/report_<run-id>.md
  reports/report.csv              combined daily outcomes
  reports/report.html             daily statistics and exact log links
  reports/report.md
```

Regular logs and error logs are Markdown only. Each physical line has a
timestamp with milliseconds and offset, severity, run and area, and is
appended/flushed to disk immediately. Multiline errors get individually
timestamped lines. DEBUG is part of the main Markdown log.
CSV rows also flush immediately. HTML/Markdown reports are snapshots regenerated
at initialization, summary and orderly disposal, not background connections.
After a crash, views can be stale; completed run CSV rows remain authoritative.
Cross-file/database writes are not one atomic transaction.
Legacy artifacts are preserved; schema upgrades retain an original CSV copy.
No automatic log deletion or rotation is introduced.

## Privacy

No hosted processing, telemetry or logging network traffic. Passwords, tokens,
cookies and URL query/fragment credentials are redacted before display/storage.
Full signed-URL logging is not offered. No response/media bodies are logged.
Filenames, paths and metadata may still identify people; treat reports as private.
Generated state, sessions, build files and media are excluded from source downloads.
Sign-in remains opt-in; tokens stay in memory and stored links use Windows DPAPI.
Existing settings/session/scheduler identities are retained.

## Developer checks

```powershell
.\src\scripts\development\build.ps1
.\src\scripts\development\test.ps1 -Filter D1,D2,D3,D4,D5,D6
.\src\scripts\development\test.ps1
powershell.exe -NoProfile -File .\src\tests\startup\test-startup.ps1
node .\src\scripts\development\readme.mjs --check
```

Run the README command without `--check` after editing this Markdown or the
shared stylesheet. HTML is generated locally using a quiet documentation layout
inspired by the main moorkyz-template README, not a marketing website.
Tests use isolated `.fixtures` and loopback data, never personal recordings.
No EXE, installer or signed release is distributed by this phase.
After source changes, run `node scripts\sync-visory-source.mjs` and
`pnpm tools:index` from the repository root.

## Further reading

- [Architecture and module boundaries](src/docs/ARCHITECTURE.md)
- [Visory integration and unverified behavior](src/docs/VISORY_INTEGRATION.md)
- [Historical test evidence](src/docs/TEST_REPORT.md)
- [Release notes](src/docs/RELEASE_NOTES.md)
- [Third-party notices](src/docs/THIRD-PARTY-NOTICES.md)
- [Existing MIT license](LICENSE)
