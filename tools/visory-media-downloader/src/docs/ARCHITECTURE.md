# Architecture

Three projects, one external package (Microsoft.Web.WebView2). Everything else uses .NET
or Windows APIs.

| Project | Responsibility |
|---|---|
| `MoorkszVisoryMediaDownloader.Core` | Provider contract, Visory/Firebase observation parser, SQLite state, download engine (BITS + HttpClient), reporting, ZIP64 archives, coordinator, settings, localization tables, demo provider. No UI, no package references. |
| `MoorkszVisoryMediaDownloader.App` | WPF hub (MVVM), WebView2 sign-in/scan window and `VisoryRecordingProvider`, tray icon, single-instance activation, themes, scheduling UI. Output: `MoorkszVisoryMediaDownloader.exe`. |
| `MoorkszVisoryMediaDownloader.Tests` | Console test runner with a raw loopback HTTP/1.1 server and a mock provider. No test-framework package. |

## Data flow

```
WebView2 (official Visory web app)
   │  WebResourceResponseReceived (JSON ≤ 8 MB from allowed origins)
   │  DevTools Network.webSocketFrame* (Realtime Database frames)
   ▼
DiscoverySession ──► DiscoveryResult (recordings + Complete/Incomplete/Uncertain + notes)
   ▼
BackupCoordinator.ScanAsync ──► StateStore.MergeDiscovery (UPSERT by provider:recording_id)
   ▼
BackupCoordinator.RunBackupAsync
   ├─ verify existing files (size or SHA-256) → SKIP / quarantine changed files
   ├─ DownloadEngine (N workers) ── IRecordingProvider.GetMediaRequestAsync
   │     ├─ BITS (COM)  when header-free + HEAD shows length + Accept-Ranges + no limit
   │     └─ HttpClient   streaming to .part, validated Range resume, redirect re-checks
   │     → verify size / server MD5 → atomic rename → Verified
   ├─ RunLogger (durable Markdown log/errors, CSV report with HTML/Markdown views)
   └─ ArchiveService (optional ZIP, temp file → read-back verification → rename)
```

## Provider contract

`IRecordingProvider` supplies discovery, a `MediaRequest` (URL + in-memory headers) per
recording, an optional custom transport (demo only) and the media URL policy.
`refresh: true` asks for fresh authorization after HTTP 401/403. A provider throws
`ReauthenticationRequiredException` when the session must be renewed; the engine then
pauses in **Sign-in required** instead of failing every file.

Implementations: `VisoryRecordingProvider` (App, WebView2), `DemoRecordingProvider`
(Core, synthetic in-memory media), `MockProvider` (tests).

## Download engine

- **Queue and workers.** Configurable concurrency (Smart background caps direct work at 2).
  Pause cancels active transfers with a *pause* reason and requeues them at the front;
  BITS jobs are suspended. Stop either hands BITS jobs off (exit) or cancels them (Stop).
- **Classified failures.** Network (wait for Windows connectivity without consuming an
  attempt), transient (timeouts, resets, 408/429/5xx; backoff 2/5/15/45 s with ±20 %
  jitter or `Retry-After`, capped at 15 min), authorization (401/403 → refresh, at most
  twice), permanent (404/410/other 4xx, unsafe URL or redirect), integrity (size or
  server-MD5 mismatch → discard partial data and retry), disk full (pause the queue).
- **Resume.** A `.part` file is continued only when the first response carried a strong
  ETag or Last-Modified, `Accept-Ranges: bytes` and a known length. The request sends
  `Range` + `If-Range`; a 206 must start at the current length and match the stored total.
  Any 200, mismatched Content-Range or 416 with the wrong length restarts from zero.
  The SHA-256 of existing bytes is rehashed before appending.
- **Finalize.** State becomes *Finalizing* with the hash and size, the file is moved
  without overwrite (same directory, atomic), then *Verified*. Startup reconciliation
  completes or requeues interrupted finalizations by rehashing.
- **Security.** HTTPS only (loopback HTTP exists only in an explicit test policy), no
  credentials in URLs, no IP-literal hosts or non-standard ports, host+path allowlist,
  manual redirects re-validated per hop with headers dropped across hosts. BITS jobs
  disallow redirects and never receive headers.

## State and integrity

`winsqlite3.dll` (part of Windows) through a small P/Invoke wrapper with prepared
statements, WAL and `synchronous=FULL`. Tables: `recordings`, `attempts`, `runs`,
`run_items` (one final status per run and file, guarded by `UPDATE … WHERE final_status IS NULL`),
`scans`, `archives`. Destinations are unique (case-insensitive partial index).
Recording identity is the provider ID; without one, a SHA-256 fingerprint of camera,
timestamp, name and size. A local SHA-256 proves the file still matches what was
received; only a server digest (Firebase/GCS MD5) is checked against the origin.

## Reports (logging foundation, 2026-10-27)

`RUN.cmd` delegates to `src/scripts/startup/run.ps1`. The terminal remains attached until
the app exits, including when its hub is hidden in the tray. Startup output is
redacted before display and persisted line by line with an OS disk flush, before
prerequisite checks, build or launch. Missing native prerequisites fail explicitly;
no SDK or WebView2 installation is attempted.

The existing App/Core assembly split is retained to avoid changing runtime boundaries.
`src/tools/desktop` is the standalone WPF app; `src/core` is its shared non-UI
application engine and owns discovery, downloads,
archives, coordination, scheduling and SQLite state. Shared diagnostics/document
rendering and platform helpers are in `src/utils`, configuration in `src/config`
and translations in `src/localization`. These remain one application, not a new
SDK or separately hosted services. Namespaces are retained for source compatibility.
`src/scripts/startup` contains bootstrap orchestration; `src/scripts/development`
contains build/test/publish/icon/README development automation. Tests belong under
`src/tests`, not in runtime modules.
Implementation and generated directories live under `src`, including redistribution notices in
`src/third-party`, generated releases in `src/artifacts`, bootstrap runs in
`src/runtime/runs` and isolated test data in `src/.fixtures`. Previously generated
root test outputs are retained in `src/artifacts/legacy-test-build` without
overwriting current builds. User-selected backup folders and profile data locations
are unchanged. User-facing metadata is the capitalized `Data` exception:
`Data/Portal/portal.json`, `Data/Source/source-files.json` and
`Data/Source/SOURCE-PROVENANCE.json`. Source allowlist paths remain relative to
the project root; portal tooling also supports the existing root manifests of
other tools and rejects duplicate root/Data manifests.

New logs (including DEBUG) are Markdown only, with a separate Markdown error log
for ERROR/FATAL/FAIL. Each physical line has a timestamp, severity, run and area.
CSV result rows are flushed immediately. Reports use `reports/report_<id>.csv`
and matching HTML/Markdown tables, rendered initially, on summary and on orderly
disposal. After a hard kill, Markdown log lines and CSV rows already flushed
remain; the companion tables may be stale. Resume regenerates them from CSV.
There is no new JSONL, TXT/debug log, failed CSV or summary JSON file.

Internal SQLite summaries remain structured application state, not log exports.
Reports never contain full signed URLs, even if an old setting requests them.

The chosen backup root's `reports/report.{csv,html,md}` aggregates canonical run
CSVs, grouping outcomes by recorded local day. Only SUCCESS/FAIL/ERROR contribute
to the daily ratio; SKIP/RUN rows remain in detailed run records. Each outcome has
a unique millisecond-plus-GUID ID, human timestamp, filename, source camera, full path, time spent,
byte size, nullable media length, log/error links, daily success ratio and message.
Media length is independent of transfer duration and remains empty unless known.
Camera is read from the existing per-run `camera` field and included in CSV,
HTML, Markdown and full-snapshot search. Historical reports without that field
produce an empty camera cell; aggregation never rewrites those run reports.
Failed paths are planned destinations, not evidence that a file exists.
Markdown log lines carry a same-line `L<number>` anchor; links contain the real
file and physical line number and survive resume/appends. Plain-text browsers
may open the file without scrolling; a rendered Markdown viewer honors anchors.
Aggregate reports are snapshots regenerated at summary/disposal, not a background
watcher. A hard exit can leave them stale; flushed run CSVs remain authoritative.

The HTML viewer embeds local `src/utils/documents/report.mjs` as a CSP-hashed
module, with JSON-escaped snapshot data. It creates table nodes with textContent
and constrained local links, not HTML injection sinks. Only 50 task/day rows are
rendered per batch; scroll and a keyboard button append batches. Search covers
the whole snapshot. Full Statistics aggregates inclusive recorded-date spans.
An accessible local calendar disables out-of-snapshot days (including month
navigation), with explicit typed-range validation instead of silent clamping.
Two native radio options avoid WebKit's CSP-incompatible native select styling.

Compare performance uses successful, known-time/positive-size rows only:
all-time seconds per MiB = total qualifying seconds / total qualifying MiB.
The speed is the reciprocal when measured seconds are positive; unknown or
zero denominators stay neutral/empty. Daily and task colors compare with this
constant all-time weighted baseline, unaffected by search/date selection.
The initial HTML has a complete-data CSV/Markdown fallback and a 50-row preview.
DM Mono is embedded from the repository's existing SIL-OFL font, not fetched.
Markdown keeps physical line anchors on the same styled log line, and the app
strips generated markup before parsing old/new lines. HTML-sanitizing Markdown
viewers may retain text but ignore the requested colors/font.

The existing SUCCESS/FAIL fields are retained with precise timestamps and run/area context:

```
[2026-10-27 18:41:32.123 +01:00] [SUCCESS] @camera1.mp4 | Time: 12.84s | Filesize: 84.31 MB | Current Average: 10.12s / 76.45 MB | Avg Speed: 7.55 MB/s | Progress: 37/142 | Run: 2026-10-27_18-40-00 | Area: backup
[2026-10-27 18:45:17.456 +01:00] [FAIL] @camera2.mp4 | Added to failed report | HTTP 403: Download authorization expired | Run: 2026-10-27_18-40-00 | Area: backup
```

Sizes use binary units labeled KB/MB/GB as in the contract. *Current Average* covers
successful downloads only; *Avg Speed* is cumulative (bytes moved by successful transfers
divided by their transfer time), distinct from the live speed in the hub. CSV files are
UTF-8 with BOM, RFC 4180 quoted, invariant numbers, and text cells starting with
`= + - @` are prefixed with `'` against spreadsheet formula injection. All text passes a
redactor that removes JWTs, Authorization/Cookie values and URL queries.

## Archives

`System.IO.Compression` streams entries into a temporary `.zip.partial` (ZIP64 is used
automatically above 4 GiB or 65,535 entries). Video entries are stored without
compression, hashed while writing and compared with their verified SHA-256; the finished
archive is reopened and every entry re-read and checked before the atomic rename. The
manifest lists every recording in scope with `included=false` and a reason when a file
could not be included. An unchanged scope (same fingerprint) reuses the existing verified
archive. Originals are never deleted.

## Lifecycle

- Single instance per data folder (named mutex); a second launch focuses the hub or
  forwards a scheduled request through named events.
- `--background` starts in the tray and resumes an interrupted run; `--scheduled` resumes or
  rescans and backs up new/missing recordings, then exits if the hub was never opened.
- Themes: WPF Fluent (`ThemeMode`) for standard controls plus light/dark/high-contrast
  palettes for custom surfaces, following Windows unless overridden. Page transitions
  (180 ms) are disabled with *Reduce motion*, Windows animation settings or high contrast.
- Localization: English/German tables in Core, parity-tested; formatting uses the
  selected language's culture.
