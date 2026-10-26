# Moorksz Visory Media Downloader

Identity: `Moorksz Visory Media Downloader`, portal slug `visory-media-downloader`. Canonical source
lives in **Moorky/moorkyz-tools** under `tools/visory-media-downloader`; it is an
independently usable Windows desktop app, not a portal runtime. Root `AGENTS.md` applies.

- Read README, ARCHITECTURE, VISORY_INTEGRATION and the latest audit before edits.
- Never invent Visory endpoints. Observe only allowlisted Visory/Firebase-project origins,
  never read media bodies for discovery, and label unverified behavior honestly.
- Never present demo or mock data as account data. Show "Discovered recordings" and a
  warning unless completeness is proven.
- No telemetry, uploads, plaintext credentials, or secrets in logs/reports. Tokens stay in
  memory; stored links are DPAPI-protected; failed-report URLs are redacted by default.
- Never overwrite, delete originals, follow links/junctions, or trust remote names as paths.
  Only verified bytes are finalized; partial files stay `.part`.
- One final SUCCESS/FAIL per file per run. Keep the exact logging contract.
- No new NuGet/native dependencies beyond Microsoft.Web.WebView2, no elevation, signing,
  publishing, scheduled-task creation in tests, or prerequisite installs without approval.
- Tests use isolated synthetic fixtures under `.fixtures` and the loopback server only.
- Commands: `scripts\build.ps1`, `scripts\test.ps1`, `scripts\publish.ps1`.
  Do not claim GUI, BITS hand-off or Visory account results that were not observed.
- Keep this project's `source-files.json` allowlist complete and
  text-only, then refresh provenance with `node scripts\sync-visory-source.mjs` from the
  repository root. Never include artifacts, bin/obj, `.fixtures`, state databases,
  sessions or recordings.
- Record actual PASS/FAIL/NOT RUN/N/A in a task audit under the repository root `.audits`.
  Never push automatically.
