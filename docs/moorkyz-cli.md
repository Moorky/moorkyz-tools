# Moorkyz CLI, Local Project Management

> Portable and dependency-free JavaScript CLI for the approved Node.js runtime. No application code or project-specific architecture is bundled.

## Setup

Prerequisites: the project's approved Node.js runtime (at least Node.js 22.18; use its runtime pin),
the package-manager version in `package.json`, and a `package.json` containing the scripts you
intend to run. Maintenance and archival commands also work in repositories without a package
manifest. The root launchers now delegate to one implementation in
[.template/cli](../src/.template/cli/README.md). When copying the CLI, include that
entire folder and the chosen root launchers; the root compatibility files are
not a standalone distribution. Both launcher identities keep their existing
configuration and runtime namespaces.

This checkout keeps its developer workspace under `src/`. Launcher configuration
lives under `src/.moorkyz/config/`; run package commands with `pnpm --dir src`.
The thin user launchers remain at the repository root. Existing root
`<command>.config.json` files are still accepted when using the CLI in other
projects and take precedence over a source-folder configuration.

**Windows / PowerShell** (from the repository containing these starter files):

```powershell
.\moorkyz.cmd help
# Optional Windows control GUI (nothing runs until you choose an action):
.\moorkyz.cmd gui
# Optional Explorer shortcut with the supplied Moorkyz icon:
.\src\.moorkyz\scripts\create-moorkyz-shortcut.ps1
# Optional one-time installation of a shell function so `moorkyz` works in every project folder:
& .\src\.moorkyz\scripts\install-moorkyz.ps1
. $PROFILE
moorkyz help
```

The optional installer edits **only the current user's PowerShell profile**, creating a function pointing at this CLI file; it does not change the system PATH. Keep this CLI file in place after installing. To remove the function, run `& .\src\.moorkyz\scripts\install-moorkyz.ps1 -Uninstall`, then restart PowerShell. If PowerShell blocks scripts by policy, use `.\moorkyz.cmd` directly; don't weaken the system execution policy just to install the shortcut.

Windows batch files cannot carry an individual file icon. The optional shortcut
script creates `moorkyz.lnk` beside the launcher, starts an interactive command
prompt in this repository, and uses `src/.moorkyz/public/favicon.ico` as its
icon. It refuses to overwrite an existing shortcut; remove the generated
`moorkyz.lnk` yourself if you want to recreate it.

Two Windows details worth knowing:

- Downloaded starter files carry an internet zone marker, so `RemoteSigned` policy rejects the unsigned installer. Trust that one file instead of changing the policy: `Unblock-File .\src\.moorkyz\scripts\install-moorkyz.ps1`, then run the installer again.
- Each PowerShell host keeps its own profile. Install once per host you use, for example `powershell.exe -File .\src\.moorkyz\scripts\install-moorkyz.ps1` for Windows PowerShell 5.1 and `pwsh -File .\src\.moorkyz\scripts\install-moorkyz.ps1` for PowerShell 7+.

**macOS/Linux:**

```bash
chmod +x ./moorkyz
./moorkyz help
# Optional current-shell command for use from other project folders:
moorkyz() { node /absolute/path/to/moorkyz.cjs "$@"; }
```

To persist the function, put it in your appropriate shell initialization file and replace `/absolute/path/to` with the true path. The CLI always operates on the **current working directory**.

## Command reference

| Command | Action |
|---|---|
| `moorkyz help [command]` | Detailed command list |
| `moorkyz gui` | Open the opt-in native Windows control GUI using this launcher's configuration |
| `moorkyz start [name\|all]` | Start in background and open the project URL in the default browser |
| `moorkyz start [name\|all] --prod` | Start project's declared production script |
| `moorkyz stop [name\|all]` | Stop only tracked processes the CLI started |
| `moorkyz restart [name\|all] [--prod]` | Stop, start, and open the project URL in the default browser |
| `moorkyz status [name\|all]` | Show tracked process state |
| `moorkyz logs [name] --lines 80 --follow` | Read/follow logs; Ctrl+C exits log view |
| `moorkyz list` | Show configured project names |
| `moorkyz doctor` | Inspect Node, pnpm, scripts and the audit template |
| `moorkyz verify [name]` | Run `verify`, then `validate` if available, otherwise available format/lint/typecheck/test/build scripts |
| `moorkyz build [name]` | Run build |
| `moorkyz test [name]` | Run tests |
| `moorkyz lint [name]` | Run lint |
| `moorkyz typecheck [name]` | Run TypeScript checks |
| `moorkyz security [name]` | Read-only dependency vulnerability audit with the detected package manager |
| `moorkyz install [name]` | Reproducible install from the authoritative lockfile |
| `moorkyz run <script> [name]` | Run an existing `package.json` script |
| `moorkyz audit "task title"` | Create a task audit from the template |
| `moorkyz archive <path> [path ...]` | Compress a batch in one verified ZIP, then remove the selected originals |
| `moorkyz maintain` | Archive malformed/expired logs and staged trash, then apply free-space retention |

Lifecycle commands use installed dependencies. `moorkyz install` explicitly runs a reproducible
install; it requires a reviewed lockfile and never generates a replacement lockfile. Database
migrations, production deployment and Git operations remain separate project workflows.
This repository sets `verifyDepsBeforeRun: error` in `pnpm-workspace.yaml` so pnpm 11 reports
stale dependencies without starting an automatic install from a background service. This pnpm
setting belongs in the workspace YAML, not `.npmrc`. Run `moorkyz install` deliberately when
dependencies need synchronization; do not bypass the noninteractive module-purge prompt.

## Optional Windows control GUI

`moorkyz gui` opens the native dark-first control window, with the supplied Moorkyz
icon, shared deep-blue/violet components and an Interaction lab. It requires the
built-in Windows PowerShell/WPF runtime; it adds no mandatory package dependency,
local server, profile edit or execution-policy bypass.

The GUI delegates explicit project actions to the existing CLI and shows their
real output/exit status. Opening it does not start a service or run a check.
Only one command runs at a time; close is blocked while it finishes. Closing an
idle window does not stop existing services. Reduced motion follows Windows'
preference and can also be enabled in the window. Card order is session-only.

The same command on `tools.moorkyz.com` keeps that launcher's configuration/state.
See the [GUI guide](../src/.moorkyz/gui/README.md) for supported actions, three
presets per interaction, inertia/3D effects, verification and current limits.

## Package managers

The nearest ancestor `package.json` with an explicit `packageManager` is authoritative. Otherwise,
the CLI detects `pnpm-lock.yaml`, `package-lock.json` or `yarn.lock` in the project or its ancestors
inside the repository. Multiple competing lockfiles require an explicit `packageManager`.
Projects with no declaration or lockfile retain the pnpm default. The selected tool must already be
installed; `doctor` reports detected and declared versions. Tool/version and script lookups are
cached within a command invocation.

Reproducible installs use `pnpm --dir src install --frozen-lockfile`, `npm ci`, Yarn Classic's
`yarn install --frozen-lockfile`, or modern Yarn's `yarn install --immutable`. The install runs at
the selected manager's root; scripts run in the configured project's directory. npm script options
are passed after `--`. Security checks use the manager's native audit; modern Yarn includes all
workspaces and transitive dependencies. Audit severity/exit semantics differ across manager versions;
these checks report the tool's real exit status and never apply upgrades.

## Default single-project mode

With no `moorkyz.config.json`, one root project named `default` is assumed, running the detected
manager's `dev` script for `start` and `start` script for `start --prod`. The requested script must
exist in `package.json`; a documentation or coordination repository does not acquire a fabricated
web server. Production mode sets `NODE_ENV=production` as well as the selected `PORT`.

## Named projects / monorepos

Copy `moorkyz.config.example.json` to `moorkyz.config.json`, then update the project names, paths and existing script names. Example:

```json
{
  "defaultProject": "web",
  "projects": {
    "web": { "cwd": "apps/web", "dev": "dev", "prod": "start", "url": "http://localhost:3000" },
    "worker": { "cwd": "apps/worker", "dev": "dev", "prod": "start", "url": "http://localhost:3001" }
  }
}
```

- `moorkyz start` starts the configured `defaultProject`.
- `moorkyz start all` launches every configured project.
- After `start` or `restart` succeeds, each selected project's URL opens in the default browser.
  This is skipped in CI and non-interactive sessions.
- The selected port is saved in `.moorkyz/<name>.preferences.json`. Every start checks
  it again, then tries the configured URL's port, a project-specific candidate, and up to 64 random
  ports in 10000–49151. Both IPv4 and IPv6 loopback are checked in parallel. A global port lock
  and live state reservations separate simultaneous starts. No unprivileged CLI can reserve a port
  permanently: a process can still claim it between the probe and application bind. Detected
  `EADDRINUSE` startup failures retry up to three total attempts with another port.
- The runtime URL is saved and shown by lifecycle, list, and status output after stopping too.
  Configure `url` for a different default; changing it invalidates the previous preference.
- The CLI sets `PORT` and recognizes Vite/Astro scripts, passing their `--port` argument.
  Other servers can use `"portArgs": ["--port", "{port}"]` in their project settings. Arguments
  are constrained to safe CLI tokens. `startupTimeoutMs` defaults to 20000 (allowed: 1000–300000).
  Startup success requires a verified supervisor heartbeat and an HTTP 2xx/3xx response from
  the configured URL. A transient TCP listener or an HTTP error does not count as ready.
  Failed startup prints the final log lines and stops the CLI-managed instance.
  This local startup check does not replace application/container production health checks.
- Starting a project that is already running replaces its existing CLI-managed instance first;
  unrelated processes are left running.
- `moorkyz logs worker --follow` follows one project's output.
- If multiple projects are configured without a default, specify a project name.

## Safety and operational behavior

- Background processes are supervised and recorded under `.moorkyz/`, which must remain Git-ignored.
  Runtime state, preferences, lock files and log paths refuse symlinks/junctions. Corrupt tracking
  JSON stops the operation with an actionable error and preserves existing state.
- Lifecycle commands serialize operations for each project using
  `.moorkyz/<project>.lock.json`. State and heartbeats are published atomically;
  `stop` requires the recorded token, supervisor PID and fresh heartbeat to match.
  An alive but unverified supervisor keeps its tracking state and requires
  inspection before another service is started or stopped.
- A forcibly killed CLI can leave a lock. Inspect the recorded owner PID and
  confirm that no lifecycle command is active before manually removing that
  exact project lock; the CLI does not reclaim it automatically. If upgrading
  a running older CLI, stop its services with that version first: old heartbeats
  do not carry the PID required by the new verification.
- Unix stopping sends SIGTERM to the tracked process group. Windows stopping uses `taskkill /PID ... /T /F` on the verified supervisor tree; graceful shutdown behavior therefore differs by platform.
- If an application forks into an unrelated process group or daemonizes itself, the CLI may not control the detached descendants. Don't use the CLI as a production process manager.
- Running `start --prod` invokes an existing production script **locally**; it is not a deployment command.
- Each start creates `logs/log_yyyy-mm-dd-hh-mm-ss-mmm.log` using UTC and exclusive creation to
  avoid collisions. The preference file records the service's latest log. Older `.moorkyz/<name>.log`
  files remain readable for compatibility; the CLI does not rewrite their contents.
- `moorkyz security` checks known advisories but does not update dependencies. Dependabot handles automatic update PRs (see `docs/dependency-security.md`).
- `moorkyz audit` generates an **unfilled** template; the agent must record real verification evidence before committing.
- The PowerShell installer validates unique, ordered management markers before
  editing. It writes the profile atomically as UTF-8 with a BOM so Windows
  PowerShell 5.1 preserves non-ASCII text. Uninstalling an absent function leaves
  the file untouched. Regression tests use disposable profiles and never edit
  the developer's real profile.

## Logs, trash and compressed archives

Keep `/logs/`, `/archive/`, `/trash/` and `/.moorkyz/` ignored by Git and Docker. These directories
are local runtime data; do not add tracked `.gitkeep` files inside them. The CLI creates them when needed.

The root `logs/` directory retains at most 50 canonical log files (or the lower configured
`storage.maxLogs`). Invalid names and expired logs from one maintenance pass are compressed together
in one ZIP before removal. Running services' logs are protected; starting another service fails
with a clear message if active services occupy the entire cap. Maintenance runs before each start,
every 30 seconds while a supervised service runs, and with `moorkyz maintain`. A filesystem folder
cannot reject arbitrary writes by other programs: external writers must use the shared helper or
run maintenance, so the cap is eventually enforced between checks. Symlinks are refused and left
for inspection rather than followed into another folder.

Long-running application loggers should call `registerLog(projectRoot, logPath)` after creating a
canonical file with `createLog(projectRoot)` and retain the returned release callback. Shared
maintenance automatically protects live PID leases in `.moorkyz/log-leases/`, even when a different
CLI or application runs it. Call the callback after closing the log stream; dead process leases are
cleaned automatically. A reused PID conservatively keeps its old log protected until inspection.
Registering the same log twice is refused without replacing the first writer's lease.
Callers should run maintenance before opening a new log and periodically while running.
Use `maintainStorage(root, { reserveLogs: 1 })` before creating the next managed log to make room
without temporarily exceeding the cap.

Place disposable project files in `trash/`, then run `moorkyz maintain` to compress all staged
items and selected logs together in one verified ZIP. Routine background maintenance rotates logs; explicit maintenance also empties
staged trash. Files deleted by an editor, OS command or unrelated tool cannot be intercepted by this
CLI. Route deletions that need recovery through `moorkyz archive` or its code API:

```js
const { archivePaths, maintainStorage } = require("./src/.moorkyz/launchers/moorkyz-storage.cjs");
await archivePaths(process.cwd(), ["generated/report.json", "old-assets"]);
await maintainStorage(process.cwd(), { includeTrash: true });
```

`archivePaths` accepts `{ remove: false }` for copy-only backups. Use the CLI to serialize concurrent
operations, or serialize library calls in your application. Archive names are
`archive/log_yyyy-mm-dd-hh-mm-ss-mmm.zip`, even for one file. Entries preserve relative paths, UTF-8
names, file permissions and empty directories. Compression streams with bounded buffers; classic
ZIP limits (less than 4 GiB per file/archive and at most 65534 entries) fail safely before removal.
Every ZIP is flushed and its local headers, descriptors, central directory and end record are
verified before source removal. Nonempty entries are decompressed to verify CRC32 and SHA-256;
empty files and directories use stored entries with no file data. Source
identity and content are checked again before unlinking; directories are removed only when empty.
The project root, Git metadata, runtime state, archive directory, symlinks/junctions and special files
cannot be selected. Stop writers before archiving mutable data: simultaneous changes can leave
sources in place or a partly removed batch whose verified ZIP still contains the original bytes.
The helper is not a security sandbox against another process racing filesystem operations.
In-progress archive PID leases prevent a concurrent maintenance process from pruning a ZIP while
its source-removal step is still running.

Free-space checks use the archive directory's filesystem. Defaults trigger retention below the
greater of 1 GiB free or 5% free, then delete oldest canonical ZIPs until the greater of 2 GiB or
10% free is reached. Configure `minFreeBytes`, `targetFreeBytes`, `minFreePercent` and
`targetFreePercent` under `storage` in `moorkyz.config.json`; targets must meet or exceed their
minimums. Only regular canonical ZIP files in this project's `archive/` are eligible. Newly created
archives are protected for their current maintenance run. If removing eligible archives cannot
reach the threshold, maintenance reports low space; project source files and other folders are
never pruned. Archived recovery data may be permanently removed by this policy, so keep durable
backups outside the project archive. Archives are compressed, not encrypted.

The Node-core regression suite is portable: `node --test src/tests/tooling/moorkyz-runtime.node.cjs`.
The repository's existing tooling tests also exercise the supervised process lifecycle and installer.

Implementation references: [Node 24 net](https://nodejs.org/download/release/v24.20.0/docs/api/net.html),
[filesystem](https://nodejs.org/docs/latest-v24.x/api/fs.html),
[child processes](https://nodejs.org/download/release/v24.20.0/docs/api/child_process.html), and
[DEFLATE/CRC32](https://nodejs.org/download/release/v24.20.0/docs/api/zlib.html).
Package-manager references: [npm scripts](https://docs.npmjs.com/cli/v11/commands/npm-run/),
[npm ci](https://docs.npmjs.com/cli/v11/commands/npm-ci/),
[Yarn install](https://yarnpkg.com/cli/install), and [Yarn audit](https://yarnpkg.com/cli/npm/audit).
ZIP layout reference: [PKWARE APPNOTE 6.3.10](https://pkware.cachefly.net/webdocs/casestudies/APPNOTE.TXT).
