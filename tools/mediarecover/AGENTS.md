# Moorkyz Media Recover

Identity: `Moorkyz Media Recover`, canonical directory `tools/mediarecover` in
`Moorky/moorkyz-tools`. This independently usable Windows desktop app and its
portal are maintained in the same repository. Follow the root AGENTS.md.
Do not create a separate repository, nested Git root or external-source sync
without a concrete need and explicit approval.

- Read README, docs and the latest implementation audit before edits.
- Sources are always read-only. No source write/delete/rename, Shell.CopyHere,
  encryption bypass, unapproved network, telemetry, account or background server.
- Never overwrite unrelated targets or traverse source/destination links.
  Recovery stages unique `.part` files and only promotes positively verified bytes.
- Preserve all legacy, incomplete and quarantined files. No automatic deletion.
- Unknown dates default to included; date conflicts include and flag.
- No new mandatory NuGet/native dependencies, prerequisite installers, elevated
  privilege, remote creation, publishing, signing or deployment without approval.
- Tests use isolated synthetic fixtures under `.fixtures`; never personal media.
- `scripts\build.ps1`, `scripts\test.ps1`, `scripts\publish.ps1`,
  `scripts\smoke.ps1` are local operations. Don't claim hardware/VM tests passed.
- Keep the text-only `source-files.json` allowlist complete. Never include
  artifacts, private data, state, fixture output, Git, bin/obj or runtime caches.
- After source edits, run `node src/scripts\sync-mediarecover-source.mjs` from the
  repository root to refresh local source provenance before the portal build.
- Record actual PASS/FAIL/NOT RUN/N/A in a task audit. No Git initialization or
  forced commit when there is no authorized baseline. Never push automatically.
