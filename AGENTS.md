# AGENTS.md - tools.moorkyz.com

## Product and authority

This repository is a modular tools portal with independently usable local tools.
The user explicitly replaced the former SDK product on 2026-10-23: retain only
a small standalone Toolbar Builder, remove the legacy SDK/Studio/examples and
active specifications, and preserve Git history and existing audit records.
See [current decisions](docs/decisions/decision-register.md). Historical audits
describe older revisions and are not current product requirements.

## Required practice

- Inspect the existing implementation and relevant documentation before edits.
- Keep tools independent. Portal navigation must not depend on an individual tool.
- Keep every tool's canonical source under `tools/` in this repository. Tools
  remain independently usable, not separate repositories by default. A repository
  split requires a concrete need and explicit approval; do not create nested Git
  roots, submodules or sibling-source synchronization.
- Preserve accessibility, keyboard operation, reduced motion, strict typing,
  CSP-safe rendering, local-first data handling and explicit error reporting.
- Local services are opt-in, loopback-only and inactive while disabled. Do not
  introduce hosted file processing, accounts, telemetry or background connections.
- Directory Cleaner always archives "deleted" files. Preview is non-mutating;
  changes require explicit confirmation, revalidation and a recoverable receipt.
  Never overwrite files, follow symlinks/junctions, or clean a filesystem root.
- Use isolated temporary fixtures, never real personal files, for tests. Do not
  auto-install native tools or alter shared machine prerequisites.
- No new mandatory dependencies, permission/data handling changes, public hosting,
  publishing, deployment, signing, spending or external integration without approval.
- Preserve unrelated work. Stage only task-owned paths. After applicable checks
  pass, create a filled task-specific audit in `.audits/` from `docs/AUDIT_TEMPLATE.md` and one
  focused local commit including it. Never push or rewrite history without approval.
- Report exact commands and observed `PASS`, `FAIL`, `NOT RUN` or `N/A`; a
  self-audit is not independent security or accessibility certification.

## Structure and commands

- Keep the root minimal: user launchers/readme/license, this discovery contract
  and required Git/container discovery files only. Developer workspace files
  belong under `src/`; do not recreate root manifests, loose helpers or configs.
- `src/packages/portal`: Next.js/React catalog, shared detail/source views and small
  per-tool workspaces. `tools/`: standalone scripts/services and explicit source
  bundle allowlists. `src/tests/`: generic tooling and current tool tests.
- `src/.template/`: shared CLI/checkups, with a README and entrypoint per system
  and supporting `helpers/`. `src/scripts/`: product automation; deployment lives
  in `src/scripts/deployment/`. `src/tests/` owns repository/browser tests.
  `src/.moorkyz/` owns GUI assets, optional installers and launcher configuration.
  Root launchers delegate to one shared implementation.
- Run `pnpm --dir src check:repository` for read-only syntax/structure checks and
  `pnpm --dir src test:unit` for tests without rebuilding. See
  [repository structure](docs/engineering/repository-structure.md).
- Drag/add tool projects into `tools/<project>/`, never sibling repositories.
  Each indexed project owns `portal.json` and, for downloads, `source-files.json`.
  Run `pnpm --dir src tools:index`; portal dev/build also discover and index these manifests.
  Do not hand-edit the generated `src/packages/portal/src/data/tool-index.ts`.
  Source JSON/ZIP assets use explicit allowlists; never include history, media,
  secrets or caches. Indexing never executes project code or starts services.
- `pnpm --dir src build`, `pnpm --dir src lint`, `pnpm --dir src format:check`, `pnpm --dir src typecheck`,
  `pnpm --dir src test`, `pnpm --dir src test:browser`, `pnpm --dir src check:source`, `pnpm --dir src check`.
- `pnpm --dir src tool:videocrunch`, `pnpm --dir src tool:directory-cleaner` start explicit local
  services. Read their setup pages before enabling portal connections.
- `.\tools.moorkyz.com.cmd help`, `.\tools.moorkyz.com.cmd doctor`, `.\tools.moorkyz.com.cmd audit "task title"`
  remain generic repository management commands.

See [testing](docs/engineering/testing.md), [coding standards](docs/engineering/coding-standards.md),
[workflow](docs/engineering/agent-workflow.md) and
[security/dependencies](docs/engineering/security-and-dependencies.md).
