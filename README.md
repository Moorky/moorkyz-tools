# Moorkyz Tools

A growing catalog of independent tools, with clear setup, readable source and
local-first workspaces. The former SDK and Studio have been removed;
the **Moorkyz Toolbar Builder** is now a small standalone HTML generator, not a framework.

## Tools

- **Moorkyz Toolbar Builder** - compact link editor, live preview, Copy and one-file HTML
  download. No SDK, adapters, service or installation required for the output.
- **Moorkyz Video Crunch** - optional Windows localhost video downloader/compressor;
  original source bytes remain preserved. Its complete source is included.
- **Moorkyz Directory Cleaner** - filename/relative-path patterns, creation-date bounds
  and file-size thresholds; Keep, Move and Delete (archive only). Available as a
  local service and a standalone script. Preview, then explicitly confirm changes.
- **Moorkyz Media Recover** - independent Windows desktop media backup/recovery
  development preview, with an explanation-first page at `/tools/mediarecover`.
  Its canonical desktop source lives in `tools/mediarecover`; no executable
  release is publicly available. The portal never scans device or local files.

Every local script tool has a source hierarchy, console-style viewer, per-file
Copy/Download and a complete ZIP. See the [portal guide](docs/portal/modular-tools.md)
and [Directory Cleaner guide](tools/directory-cleaner/README.md).
All tool directories and canonical sources live under [tools](tools/) in this
repository. Tools are independently usable; separate repositories are only
created when a concrete need is approved. Media Recover's local editable source
has an allowlisted, SHA-256-checked download manifest. See
[source provenance](tools/mediarecover/SOURCE-PROVENANCE.json) and the
[portal integration guide](docs/portal/modular-tools.md).

## Development

```powershell
pnpm install --frozen-lockfile
pnpm --filter @moorkyz-tools/portal dev --hostname 127.0.0.1
pnpm build
pnpm check
pnpm check:source
```

For a managed background portal, use `.\moorkyz.cmd start` (or the
repository-specific `.\tools.moorkyz.com.cmd start`), then `status`, `logs`,
`restart` or `stop` with the same launcher. The shared launcher prefers
`http://127.0.0.1:4320`; the repository-specific launcher prefers
`http://127.0.0.1:3001`. Both print the actual selected URL and wait for a
successful HTTP response. Do not run a manual and managed Next.js development
server in the same workspace at once. All portal scripts bind to loopback by
default. Dependencies that are out of sync produce an explicit error rather
than an automatic reinstall; run the frozen install above when needed.

Use Node.js 22+ and the pinned pnpm version. Browser tests use installed
Chromium/Firefox/WebKit; `pnpm exec playwright install` prepares them for development.

Repository management uses `.\tools.moorkyz.com.cmd help` on Windows or
`sh ./tools.moorkyz.com help` on macOS/Linux. See the
[CLI guide](docs/engineering/tools.moorkyz.com-cli.md). The workspace package is
`moorkyz-tools`; the portal package is `@moorkyz-tools/portal`.

Project links target [Moorky/moorkyz-tools](https://github.com/Moorky/moorkyz-tools).
Use the same repository and hostname in GitHub, Cloudflare and Coolify. See the
[deployment guide](docs/deployment/coolify.md).

## Explicit local services

```powershell
# Independent terminals, only for tools you want to run:
pnpm tool:videocrunch
pnpm tool:directory-cleaner
# Portal terminal:
$env:TOOLS_MOORKYZ_COM_VIDEOCRUNCH_ENABLED = "1"
$env:TOOLS_MOORKYZ_COM_DIRECTORY_CLEANER_ENABLED = "1"
pnpm --filter @moorkyz-tools/portal dev --hostname 127.0.0.1
```

Both connections are **off by default**. Nothing is installed or started by
opening a tool page. Do not expose either native service publicly or reverse-proxy
its operations. No accounts, tenant isolation, hosted storage or telemetry.

## Repository structure

The [structure guide](docs/engineering/repository-structure.md) records the
adaptation of [moorkyz-template](https://github.com/Moorky/moorkyz-template):
shared systems in [.template](.template/README.md), application code in
[packages/portal](packages/portal/README.md), independent [tools](tools/),
product [automation](scripts/README.md), and new [task audits](.audits/README.md).
Both root launchers now share one implementation; existing commands, ports,
configuration and runtime data remain compatible.

Use `pnpm check:repository` for read-only syntax/structure checks,
`pnpm test:unit` for tests without a rebuild, and `pnpm check` for the full gate.
Historical audits remain at their original paths.

## Governance

[AGENTS.md](AGENTS.md), [decisions](docs/decisions/decision-register.md) and
[testing](docs/engineering/testing.md) describe current behavior. Old specifications
remain in Git history; [audits](docs/audits/) remain historical evidence.
Production deployments follow the authenticated CI pipeline and the approved hosting configuration.

The root [Apache-2.0 license](LICENSE) covers first-party project code. The
imported VideoCrunch application's original no-selected-distribution-license
notice remains separate; do not assume it was relicensed. Self-hosted fonts
retain their SIL OFL notices.

## Repository operations

[Deployment and CI](docs/deployment/coolify.md), [security reporting](SECURITY.md) and [audit evidence](audits/) describe repository operations. [README.html](README.html) and [READM.html](READM.html) are offline HTML entry points.

The root `trash/`, `logs/`, `archive/` and `.moorkyz/` directories are local runtime data and ignored by Git and Docker. The shared `moorkyz` command manages project-scoped retention and saved ports; see its help and runtime documentation.
