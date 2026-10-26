# Release notes

## 0.1.0 — development preview (not published)

No public release, installer or signed binary exists. `scripts\publish.ps1` builds a local
portable package.

### Included

- Portable, self-contained single-file `MoorkszVisoryMediaDownloader.exe` (Windows x64, .NET 10.0.11).
- WPF hub with Dashboard, Recordings, Downloads, Archives, Reports and Settings; light/dark
  themes following Windows, high-contrast palette, English and German UI, reduced motion,
  keyboard shortcuts Ctrl+1…Ctrl+6.
- WebView2 sign-in and passive Visory/Firebase metadata discovery with completeness states
  (see VISORY_INTEGRATION.md), auto-scroll helper and redacted diagnostics.
- Hybrid download engine: Windows BITS for compatible links, HttpClient streaming otherwise;
  classified retries with jitter and Retry-After, authorization refresh, safe Range
  resume, disk-space pause, network wait, pause/resume/cancel, bandwidth limit.
- SQLite state with one final result per file per run, restart reconciliation and
  DPAPI-protected links; exact text/CSV/JSON reports with redaction.
- Streaming ZIP64 archives with read-back verification, manifests and reuse.
- Tray operation, single instance, optional daily schedule and quiet start after sign-in.
- Clearly labeled demo mode with synthetic recordings.

### Known limitations

- **Real Visory behavior is unverified**: discovery depends on the metadata the official
  web app loads; Visory exposes no total count, so completeness is reported as
  "not confirmable" at best.
- Download-link renewal works only for Firebase Storage objects; other links require a
  new scan.
- Scheduled unattended scans replay previously visited app routes and require a valid
  saved session; they are untested against the real app.
- The EXE is unsigned and may be blocked by SmartScreen, Smart App Control or WDAC.
- BITS is not used for links that need sign-in headers or while a bandwidth limit is set.
- Direct (HttpClient) downloads stop when the app exits and resume on the next start.
- The WebView2 Runtime is required for sign-in and is not bundled.
