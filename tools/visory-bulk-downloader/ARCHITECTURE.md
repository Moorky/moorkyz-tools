# Architecture

Three projects, one external package (Microsoft.Web.WebView2). Everything else uses .NET
or Windows APIs.

| Project | Responsibility |
|---|---|
| `VisoryBulkDownloader.Core` | Provider contract, Visory/Firebase observation parser, SQLite state, download engine (BITS + HttpClient), reporting, ZIP64 archives, coordinator, settings, localization tables, demo provider. No UI, no package references. |
| `VisoryBulkDownloader.App` | WPF hub (MVVM), WebView2 sign-in/scan window and `VisoryRecordingProvider`, tray icon, single-instance activation, themes, scheduling UI. Output: `VisoryBulkDownloader.exe`. |
| `VisoryBulkDownloader.Tests` | Console test runner with a raw loopback HTTP/1.1 server and a mock provider. No test-framework package. |

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
   ├─ RunLogger (text, CSV, failed CSV, debug, summary JSON)
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

## Reports

Text log lines follow the contract exactly, e.g.

```
[2026-10-26 18:41:32] [SUCCESS] @camera1_2026-10-26_18-40-55.mp4 | Time: 12.84s | Filesize: 84.31 MB | Current Average: 10.12s / 76.45 MB | Avg Speed: 7.55 MB/s | Progress: 37/142
[2026-10-26 18:45:17] [FAIL] @camera1_2026-10-26_18-44-10.mp4 | Added to failed report | HTTP 403: Download authorization expired
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
