# Local project CLI

The root [moorkyz.cjs](../../../moorkyz) and
[tools.moorkyz.com.cjs](tools.moorkyz.com.cjs) both load
[one implementation](moorkyz.cjs). The root shell/CMD launchers and optional
PowerShell installer remain supported. Read the
[command and safety guide](../../docs/moorkyz-cli.md) before real operations.

```powershell
node .\moorkyz help
node .\src\.moorkyz\scripts\tools.moorkyz.com.cjs doctor
```

The invoked root entrypoint selects its existing configuration and state:

| Launcher            | Configuration                                       | State                 | Preferred portal port |
| ------------------- | --------------------------------------------------- | --------------------- | --------------------- |
| `moorkyz`           | `src/.moorkyz/config/moorkyz.config.json`           | `.moorkyz/`           | 4320                  |
| `tools.moorkyz.com` | `src/.moorkyz/config/tools.moorkyz.com.config.json` | `.tools.moorkyz.com/` | 3001                  |

Supervisors re-enter through the same launcher, so restart, status, logs and stop
use the correct namespace. Both share the tested
[storage/path helper](helpers/storage.cjs). `moorkyz` keeps its canonical `logs/`
files, live-log protection and existing automatic retention.
`tools.moorkyz.com` keeps append-only `.tools.moorkyz.com/<project>.log` files
with no automatic storage maintenance. Consolidation does not opt it into
archive pruning or rotate its existing logs.
The [storage implementation](helpers/storage.cjs) is shared directly.
No logs, archives, preferences or active processes are migrated by this refactor.

When copying this CLI, copy this entire folder and the chosen root launchers,
not just the root compatibility files. Configuration and package manifests belong
to the receiving project. This tooling needs only the approved Node runtime;
lifecycle/package commands additionally require the selected package manager.

Both launchers write new reports to `.audits/` when
`docs/AUDIT_TEMPLATE.md` exists. Old template locations remain supported for
existing consumers, retaining their old `docs/audits/` output location.

`gui` optionally opens the [Windows Moorkyz control GUI](../gui/README.md).
It is a separate native front end, not the portal, and does nothing until an
action is selected. Copy `src/.moorkyz/gui` and `src/.moorkyz/public` as well if
the receiving checkout should support this optional command. No GUI package is
required for ordinary CLI commands.

## Shared startup

`RUN.cmd`, bare `node .\moorkyz`, and the optional
[icon-bearing EXE](../app/README.md) open the Windows GUI first. `moorkyz start`
and `moorkyz launch` do the same in this checkout. No console action menu is
opened. The same numbered startup flow runs inside the GUI's Logs & Errors view:

1. Validate required launch/configuration/GUI files, all discovered JavaScript
   syntax, repository structure and maintained documentation links. Missing
   application source is reported, not invented or downloaded.
2. Read the actual tech stack from the workspace and portal manifests. Validate
   Node against the declared minimum and pnpm against its exact package-manager
   pin. Current declarations are Node 22.18.0+, pnpm 11.8.0, Next.js 16.4.1,
   React/React DOM ^19.3.1 and TypeScript ^6.0.3. No stack upgrade is implied.
3. Check pnpm's dependency/lockfile state and the presence of declared workspace
   packages. Missing/stale dependencies trigger `pnpm install --frozen-lockfile`,
   then revalidation. A failed stage is retried once, for **two attempts total**;
   dependency repair therefore runs at most twice. Unrelated script errors never
   trigger installation.

On success, GUI project actions become available; no service starts until
selected. Failed preflight leaves actions gated and offers an explicit Retry
button. Logs/errors and duration reports are saved while checks run. Bare
`moorkyz`, `start` and `launch` resolve this checkout from the
root entrypoint, even when invoked by absolute path from another working directory.
Set `MOORKYZ_HEADLESS=1` deliberately for console automation (including legacy
start/service semantics and the old text menu), or use `launch --check` for
read-only console preflight. GUI children set headless mode themselves so Start
does not recursively open another window. Explicit `gui` stays idle for
inspection; `doctor`, `run`, help and other management commands retain their
console behavior. Outside this Windows checkout, portable CLI behavior remains.

```powershell
node .\moorkyz launch --check
node .\moorkyz tasks
node .\moorkyz tasks retry dependencies
```

`--check` skips installation and the action prompt, but **does write a task report**
if a check fails twice. Each failed pair appends a JSONL entry to
`.moorkyz/tasks/startup-<stage>.jsonl`, including both attempts, status, timestamp,
tech stack when readable, a suggested fix, a verification command and a link to
the previous entry. Retrying a failed task checks its prerequisites too. Another
failed pair appends another entry to the same task; success appends a resolved
entry without deleting history. There is no recursive retry/report loop.
`tasks` lists the latest state and retry command.

Reports are local ignored runtime data. URLs and common credential fields are
redacted, output is bounded, and symlink/junction report destinations are rejected.
Other filenames/paths can still be private. A report-write failure is an explicit
fatal error, not a false success or a recursive fallback.

Node must already be available to execute the shared checker/reporter. Missing
Node or corrupt bootstrap code can only produce a native/CLI bootstrap error,
not run a Node-based task reporter. Node, pnpm activation, Git, native compilers,
WPF, FFmpeg and per-tool runtimes are **not** auto-installed, and no global/profile,
execution-policy or optional-service changes occur. Optional tools remain
independently enabled. This flow does not certify binaries, accessibility,
all native toolchains or a production build; normal quality gates remain required.
