# Internal workspace

User-facing launchers, the main readme/license and required platform discovery
files stay at the repository root. Implementation and developer tooling live
here. Independent, user-facing tools keep their canonical folders under
[`../tools`](../tools/README.md).

From the repository root:

```powershell
pnpm --dir src install --frozen-lockfile
pnpm --dir src build
pnpm --dir src check
node .\moorkyz help
node .\moorkyz gui
```

| Area | Responsibility |
|---|---|
| [packages/portal](packages/portal/README.md) | Next.js catalog and local tool workspaces |
| [.template](.template/README.md) | Shared CLI/checkup implementation |
| [.moorkyz/gui](.moorkyz/gui/README.md) | Optional native control GUI |
| [.moorkyz/config](.moorkyz/config) | Launcher configuration and examples |
| [.moorkyz/scripts](.moorkyz/scripts) | Explicit optional profile/shortcut setup |
| [scripts](scripts/README.md) | Indexing, source bundles and deployment automation |
| [tests](tests) | Repository, tool and browser regression tests |

Both command identities preserve their ports and repository-root runtime state.
The CLI also still accepts a legacy root configuration when explicitly supplied
in another project. A dependency install remains explicit; starting the portal
must not silently repair an out-of-sync installation.

See the [repository structure](docs/engineering/repository-structure.md) for
the ownership rules and retained root/container compatibility exceptions.
