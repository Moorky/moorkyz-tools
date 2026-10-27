# Shared repository tooling

Adapted from [Moorky/moorkyz-template](https://github.com/Moorky/moorkyz-template)
at revision `1c413a3a0e7ea24c39fc995e1a947541a4efac01`.
This catalog separates reusable systems from the portal and independent tools.
It is not an automatic template updater.

| System                                     | Entrypoint                                        | Purpose                                                         |
| ------------------------------------------ | ------------------------------------------------- | --------------------------------------------------------------- |
| [Local CLI](../.moorkyz/scripts/README.md) | Root `moorkyz` / `moorkyz.cmd`                    | Explicit project lifecycle, diagnostics and recoverable storage |
| [Maintenance](maintenance/README.md)       | `node src/.template/maintenance/run-checkups.cjs` | Read-only runnable-source syntax checks                         |
| [Task audits](../../.audits/README.md)     | `node moorkyz audit "task title"`                 | Evidence from the [audit template](../docs/AUDIT_TEMPLATE.md)   |

Each system keeps its entrypoint and README together; implementation helpers
live under `helpers/`. Root launchers remain thin compatibility entrypoints.
Project-owned generation and deployment stay in [scripts](../scripts/README.md),
not here. GitHub workflows and skill discovery stay in their platform locations.

## Deliberate adaptations

- Keep the approved Node.js/pnpm pins and existing Next.js workspace. Do not
  install template stack presets or copy the template's showcase website.
- Share the repository's already-tested CLI and storage implementation rather
  than overwrite it with a different upstream version. Preserve automatic port
  selection, startup readiness, both launcher identities and their saved state.
- Preserve existing `logs/`, `archive/`, `trash/`, `.moorkyz/` and
  `.tools.moorkyz.com/` data in place. Do not run maintenance on existing data
  merely to reorganize folders, or adopt upstream's different retention policy.
- New audits go in `.audits/`; historical records remain at their original paths.
- Checkups use Node core, not new PowerShell/native prerequisites. They never
  start services, install packages, contact upstream, or modify user settings.

Review future template changes deliberately. Claiming a new template, changing
Copilot settings, adding presets, deployment and storage cleanup are not part of
normal verification.
