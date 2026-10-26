# tools.moorkyz.com CLI for Local Repository Work

The repository includes a dependency-free Node.js helper for local project lifecycle, package scripts, and task-audit file creation. It is maintainer tooling, not a public tool or SDK consumer CLI.

## Commands available now

From the repository root in PowerShell:

```powershell
.\tools.moorkyz.com.cmd help
.\tools.moorkyz.com.cmd doctor
.\tools.moorkyz.com.cmd start
.\tools.moorkyz.com.cmd status
.\tools.moorkyz.com.cmd logs
.\tools.moorkyz.com.cmd stop
.\tools.moorkyz.com.cmd audit "task title"
```

On macOS/Linux, use `sh ./tools.moorkyz.com`. The `audit` command creates an unfilled report in `.audits/` from [the audit template](../AUDIT_TEMPLATE.md); fill in observed evidence before considering the task complete. Historical audit records remain at their original paths.

This launcher and `moorkyz` now use [one shared implementation](../../.template/cli/README.md), not separate copies. The command names, configuration files, state namespaces and preferred ports remain distinct.

`tools.moorkyz.com.config.json` selects `packages/portal` as the default project, using its `dev` script for `start` and its `start` script for `start --prod`. The portal prefers `http://127.0.0.1:3001`; if that port is occupied, the CLI selects an available port and records the runtime URL. The production command requires a prior portal build and sets `NODE_ENV=production`.

Startup waits for an HTTP 2xx/3xx response and a verified supervisor heartbeat,
not just a spawned process or open TCP port. The portal allows 60 seconds;
other configurations default to 20 seconds and can set `startupTimeoutMs`
from 1000 to 300000. A failed startup prints recent log lines and stops only
its own tracked instance. The portal's dev and production scripts bind to
`127.0.0.1` by default.

pnpm dependency verification fails explicitly when the installation is out of
sync. `pnpm-workspace.yaml` sets `verifyDepsBeforeRun: error` for both root and
workspace commands; pnpm 11 does not read this setting from `.npmrc`.
Run `pnpm install --frozen-lockfile` deliberately; starting a service must not
automatically reinstall dependencies. Do not set `CI=true` or disable module-purge
confirmation just to let a background startup reinstall packages.
An already-running manual Next.js
dev server in the same workspace must be stopped by its owner before using a
managed dev instance. The launcher does not kill an unrelated server or remove
its development lock.

After `start` or `restart` succeeds, the CLI opens the actual runtime URL in the default browser in
an interactive terminal. Lifecycle, list, and status output show the recorded runtime URL. Browser
launching is skipped in CI and non-interactive sessions.
Starting a project that is already running replaces its existing CLI-managed instance first;
unrelated processes are left running.

The CLI also supports `restart`, `verify`, `build`, `test`, `lint`, `typecheck`, `security`, `install`, `archive`, `maintain`, and `run`. Package-check commands require the selected package to define the requested script. Explicit storage commands follow the [shared recovery and retention rules](../moorkyz-cli.md#logs-trash-and-compressed-archives).

The workspace configuration is intentionally scoped to the portal. Use the named-project argument only if further projects are later added to this configuration.

## Safety

- Run commands from the intended repository root. The helper operates on the current working directory.
- It does not automatically install packages, modify package manifests, deploy, publish, or commit. `install` is a separate, explicit frozen-lockfile operation.
- Managed process state, saved preferences and append-only `<project>.log` files live in `.tools.moorkyz.com/`, ignored by `.gitignore`. This launcher does not opt into `moorkyz`'s automatic canonical-log retention. The helper will stop only verified processes it started; inspect an unverified stale state instead of deleting it blindly.
- `start --prod` runs an existing local production script; it is not deployment.
- Use the explicit repository-local launcher. The optional shared `install-moorkyz.ps1` profile installer requires its own deliberate invocation; neither launcher changes user settings automatically.
- `security` is a read-only package advisory check when a compatible package manifest and package manager are available; it does not prove dependencies safe or update them.
