# Moorksz Visory Media Downloader

Identity: `Moorksz Visory Media Downloader`, portal slug `visory-media-downloader`. Canonical source
lives in **Moorky/moorkyz-tools** under `tools/visory-media-downloader`; it is an
independently usable Windows desktop app, not a portal runtime. Root `AGENTS.md` applies.

- Read README, src/docs/ARCHITECTURE, src/docs/VISORY_INTEGRATION and the latest audit before edits.
- Never invent Visory endpoints. Observe only allowlisted Visory/Firebase-project origins,
  never read media bodies for discovery, and label unverified behavior honestly.
- Never present demo or mock data as account data. Show "Discovered recordings" and a
  warning unless completeness is proven.
- No telemetry, uploads, plaintext credentials, or secrets in logs/reports. Tokens stay in
  memory; stored links are DPAPI-protected; report URLs are always redacted.
- Never overwrite, delete originals, follow links/junctions, or trust remote names as paths.
  Only verified bytes are finalized; partial files stay `.part`.
- One final SUCCESS/FAIL per file per run. Keep the exact logging contract.
- No new NuGet/native dependencies beyond Microsoft.Web.WebView2, no elevation, signing,
  publishing, scheduled-task creation in tests, or prerequisite installs without approval.
- Tests use isolated synthetic fixtures under `src/.fixtures` and the loopback server only.
- Start through `RUN.cmd` so terminal logging begins before checks/build/launch.
- Commands: `src\scripts\development\build.ps1`, `src\scripts\development\test.ps1`,
  `src\tests\startup\test-startup.ps1`. Functional modules live in `src/tools`,
  the shared non-UI engine in `src/core`, helpers in `src/utils`, automation in `src/scripts`.
  Implementation/generated directories stay under `src`, including third-party
  notices, artifacts and startup runtime output. User-facing metadata folders
  start capitalized: `Data/Portal/portal.json` and
  `Data/Source/{source-files.json,SOURCE-PROVENANCE.json}`. Keep the launcher and
  README/license files at the root; do not duplicate JSON manifests there.
  New logs/error logs are Markdown only; reports are CSV with HTML/Markdown views.
  The daily `reports/report.html` uses stable outcome IDs and exact Markdown line anchors.
  Its local hash-authorized viewer batches 50 rows, searches the full snapshot,
  offers task/statistics modes and bounded calendars, and compares measured
  successes with an unfiltered weighted all-time baseline. Never fetch fonts or
  report data from a server; DM Mono and its SIL-OFL notice stay bundled locally.
  Do not claim GUI, BITS hand-off or Visory account results that were not observed.
- Keep this project's `Data/Source/source-files.json` allowlist complete and
  text-only, then refresh provenance with `node src/scripts\sync-visory-source.mjs` from the
  repository root. Never include artifacts, bin/obj, `.fixtures`, state databases,
  sessions or recordings.
- Record actual PASS/FAIL/NOT RUN/N/A in a task audit under the repository root `.audits`.
  Never push automatically.
