# Project automation

These scripts belong to this tools portal, not the reusable template systems.

| Location                                             | Responsibility                                                                            |
| ---------------------------------------------------- | ----------------------------------------------------------------------------------------- |
| [prepare-tool-sources.mjs](prepare-tool-sources.mjs) | Generate allowlisted text/ZIP assets for portal builds                                    |
| [index-tools.mjs](index-tools.mjs)                   | Discover per-project manifests in `tools/`, validate and generate the shared portal index |
| [verify-source.mjs](verify-source.mjs)               | Bounded credential and unsafe-rendering source scan                                       |
| [deployment](deployment/README.md)                   | Approved CI deployment client and isolated tests                                          |

Source-generation entrypoint paths remain stable for workspace build scripts.
After editing canonical desktop sources under `tools/mediarecover`, run
`node src/scripts\sync-mediarecover-source.mjs` without arguments to refresh their
local SHA-256 provenance manifest. Despite its retained compatibility name, this
command never imports or copies a separate project.
`pnpm --dir src tools:index` discovers the self-contained project folders described in
[tools/README.md](../../tools/README.md). Portal dev/build run indexing before source
packaging; `pnpm --dir src tools:index:check` and typecheck reject stale generated indexes.
Manifest discovery supports root `portal.json` / `source-files.json` and the
capitalized `Data/Portal/portal.json` / `Data/Source/source-files.json` layout.
Duplicate locations and linked metadata ancestors fail explicitly. All source
allowlist entries stay relative to the project root.
After editing Visory, run `node src/scripts\sync-visory-source.mjs` to refresh
`tools/visory-media-downloader/Data/Source/SOURCE-PROVENANCE.json`.
No project commands, dependency installs, services or external connections run.
Do not put application workspaces, downloaded sources or runtime data here.
Reusable local lifecycle/storage and checkups live in
[.template](../.template/README.md).
