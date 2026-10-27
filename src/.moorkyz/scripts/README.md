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
The [storage compatibility module](moorkyz-storage.cjs)
remains an internal re-export.
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
