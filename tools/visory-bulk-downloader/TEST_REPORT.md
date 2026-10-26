# Test report — 0.1.0 development preview

Date: 2026-10-26. Machine: Windows 11 (10.0.26200) x64, .NET SDK 10.0.303,
WebView2 Runtime 155.0.4283.62, Smart App Control **enforcing**.

**Automated tests use a loopback HTTP server and synthetic mock/demo providers.
No test signed in to Visory or touched real recordings. Real-account Visory
end-to-end behavior is NOT RUN** (no authorized test session was available).

## Commands and results

| Check | Command | Result |
|---|---|---|
| Release build (3 projects) | `dotnet build VisoryBulkDownloader.sln -c Release` | **PASS**, 0 warnings, 0 errors (warnings are errors) |
| Debug build | `dotnet build VisoryBulkDownloader.sln -c Debug` | **PASS**, 0 warnings, 0 errors |
| Automated suite | `dotnet run --project tests\VisoryBulkDownloader.Tests -c Debug` | **PASS** 35/35 executed, 1 opt-in skipped (15.4 s) |
| 4.2 GiB ZIP64 | `VBD_HEAVY_TESTS=1`, filter `25b` | **PASS** (10.7 s, entry written, re-read and SHA-256 verified) |
| Automated suite, Release binaries | `dotnet run … -c Release` | **BLOCKED by environment**: Application Control refused the rebuilt `VisoryBulkDownloader.Tests.dll` (`0x800711C7`, Code Integrity event 3077). The same source passed in Debug; earlier Release builds of the suite ran 35/36 before the UI-key test existed. No policy was changed or bypassed. |
| Portable single-file publish | `dotnet publish … -r win-x64 --self-contained -p:PublishSingleFile=true -p:RuntimeFrameworkVersion=10.0.11` | **PASS**, one EXE (~70 MB, compressed bundle) |

## Scenario coverage

| # | Scenario | Evidence | Result |
|---|---|---|---|
| 1 | Fresh launch and initial configuration | test 01 (portable/profile locations, defaults, invalid settings preserved, layout, drive-root rejection); GUI first-run welcome screen | PASS |
| 2 | Successful mock cloud scan | test 02 | PASS |
| 3 | Multiple recording pages | test 03 (Storage list pages + tokens, RTDB multi-frame, Firestore REST, media merge, origin filtering) | PASS |
| 4 | Incomplete inventory detection | test 04 (outstanding page, no metadata, metadata error; run ends *Completed with errors*; uncertain wording) | PASS |
| 5 | New file download | test 05 (exact SUCCESS line, CSV, hash, naming, no `.part`) | PASS |
| 6 | Pre-existing verified file skipped | test 06 (no network request, SKIP) | PASS |
| 7 | Same filename, different recording ID | test 07 | PASS |
| 8 | Network interruption | test 08 (waits for Windows connectivity without consuming attempts) | PASS |
| 9 | Partial transfer resumed | test 09 (`Range` + `If-Range`, 206 append, final hash) | PASS |
| 10 | Server rejects Range | test 10 (no `Accept-Ranges` → restart; ignored range → 200 replaces data) | PASS |
| 11 | HTTP 429 with Retry-After | test 11 (exact delay honored, scaled ×0.01 in tests) | PASS |
| 12 | Expired download URL | test 12 (403 → refreshed URL; links DPAPI-encrypted at rest) | PASS |
| 13 | 403 requiring renewed authorization | test 13 (pauses *Sign-in required*, resumes, no failure) | PASS |
| 14 | HTTP 404 permanent failure | test 14 (one request, FAIL line, failed CSV with redacted URL, no secret in any report) | PASS |
| 15 | HTTP 500 and retry exhaustion | test 15 (4 attempts, 3 RETRY lines, attempt history) | PASS |
| 16 | Unknown file size | test 16 (close-delimited body, real size reported, empty size for failures — never `0 B`) | PASS |
| 17 | Unexpected connection termination | test 17 (no unvalidated resume without ETag/Last-Modified) | PASS |
| 18 | Corrupt file in a later run | test 18 (SHA-256 mismatch, file preserved as `.corrupt-*`, re-downloaded) | PASS |
| 19 | No available disk space | test 19 (simulated free-space probe; queue pauses, no FAIL, resumes) | PASS (real full volume NOT RUN) |
| 20 | Pause and resume | test 20 (no writes while paused, validated range on resume) | PASS |
| 21 | App restart | test 21 (stop/hand-off, new coordinator, reconcile, same run ID continues, one SUCCESS) | PASS (in-process restart) |
| 22 | Concurrent downloads | test 22 (concurrency 3 observed at the server, monotonic progress) | PASS |
| 23 | CSV escaping and report correctness | test 23 (quotes, commas, newlines, formula neutralization, BOM, invariant numbers under de-DE) | PASS |
| 24 | One final status per file per run | test 24 (DB guard + logger guard) | PASS |
| 25 | ZIP64 large-file support | test 25 (70,000 entries, ZIP64 EOCD) and 25b (4.2 GiB entry) | PASS |
| 26 | Archive corruption detection | test 26 (stored video, manifest/reports, reuse of unchanged archive, flipped byte detected) | PASS |
| 27 | Portable EXE without SDK | Self-contained single-file EXE launched and ran the full demo workflow. The machine has the SDK installed, so a clean machine without SDK is NOT RUN. | PARTIAL |
| 28 | WebView2 missing runtime | test 28 (detection logic and official link); real missing-runtime machine NOT RUN | PARTIAL |
| 29 | German and English UI | test 29 (key parity, placeholders, every UI key defined) and GUI screenshots in both languages | PASS |
| 30 | Light/dark, resizing, high-DPI | GUI captures in dark (system) and light, at 125 % scaling, normal and maximized | PASS; high-contrast and minimum-size layout NOT RUN |
| 31 | Safe background exit | test 21 (hand-off stop) and GUI close → clean `Exit` in app log | PARTIAL: tray *Exit safely* during an active GUI run and BITS continuation after real app exit NOT RUN |
| 32 | Large inventory performance | test 32: 100,000 recordings merge 0.66 s, re-merge 0.55 s, load 0.35 s, filter 16 ms | PASS (GUI grid with 100k rows NOT RUN) |
| S1 | URL policy, redirects, redaction | HTTPS-only, bucket path, spoofed suffix, IP/port/userinfo, cross-host redirect refused | PASS |
| S2 | Path safety and naming | reserved names, traversal, rooted paths, every naming rule stays in `downloads`, symlink not followed | PASS |
| S3 | Server MD5 digest | matching accepted, mismatch rejected and discarded | PASS |
| S4 | Demo mode end-to-end | 6 synthetic recordings, verified ZIP, DEMO labels, custom transport never uses BITS | PASS |
| S5 | Windows BITS | real BITS job against the loopback server (HEAD probe, transfer, Complete, hash) | PASS |
| S6 | Settings, run IDs, scheduling arguments | clamping, collision suffix, `schtasks` argument construction | PASS (tasks not created: would change machine state) |

## GUI smoke (published EXE and Debug build)

Driven with Windows UI Automation; captures were taken of the app window only.

- Published single-file EXE (builds 1 and 2): cold start to window ≈ 4.0–4.1 s.
  Dark theme, German UI: welcome screen → **Demo-Modus testen** → scan (36 synthetic
  recordings, 3 cameras, *Vollständig*) → **Sicherung starten** → 36/36 downloaded,
  ZIP created and verified (41 entries, 76.29 MiB), reports and run summary present.
  Light theme, English UI: dashboard, recordings grid (en-US dates), settings, reports,
  archives rendered correctly. Window close when idle → clean `Exit` in the app log.
- Issues found and fixed during smoke: stale live progress after a run, unlocalized
  transfer/archive status, culture formatting after async startup, clipped tagline,
  collapsed star-sized grid columns, sign-out racing the WebView2 browser process
  (now waits for it, verifies the profile stays deleted, and completes a pending deletion
  at the next start; re-verified: 0 session folders after 25 s and after restart).
- WebView2 sign-in window (Debug build of the same source): the official Visory
  onboarding/login page rendered inside the hub; the scan was canceled without signing in.
- Later published EXE builds were **blocked by Application Control** (`0x800711C7`) on this
  machine. The release EXE built before relocation
  (`SHA-256 60930b37c2a72ef86c78a72936fdcd223d53f54ee3ed54c327065d39b2754420`) was
  **reproducible** (two clean `scripts\publish.ps1` runs produced identical bytes), but its
  launch was **blocked** here, so no GUI run of that exact binary is claimed. Builds of the
  same source with the earlier UI defects ran; the final UI changes were verified with the
  Debug build. Unsigned binaries may be blocked by Smart App Control or WDAC on other
  machines too; code signing was not performed (requires a certificate and approval).

## Relocation into Moorky/moorkyz-tools

The project was then moved byte-for-byte (69 files, SHA-256 verified) to its canonical
location `tools/visory-bulk-downloader`. Only Markdown changed afterwards.

- Release and Debug builds at the new location: **PASS** (0 warnings, 0 errors).
- Automated suite at the new location: **BLOCKED by environment**. Debug binaries embed
  their build path, so the relocated `VisoryBulkDownloader.Core.dll` has a new hash that
  Application Control refused (`0x800711C7`, Code Integrity 3077) before any test ran. The
  35/35 Debug result above was obtained on the identical code.
- Inside a Git repository the SDK embeds the commit in the product version
  (`0.1.0+<commit>`), so the release EXE hash depends on the commit. Two clean publishes at
  the new location produced identical bytes (**PASS**, reproducible).

## Not run

Real Visory sign-in, discovery, downloads and URL refresh; Task Scheduler task creation and
scheduled/after-login starts; clean VM without SDK or WebView2; real disk-full and power
loss; screen reader, keyboard-only walkthrough and high-contrast runtime checks; BITS
continuation after process exit and logoff.
