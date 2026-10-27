# Read-only maintenance checkups

Run from any working directory:

```powershell
node src/.template/maintenance/run-checkups.cjs
node src/.template/maintenance/run-checkups.cjs --dry-run
```

The entrypoint resolves its repository from its own location. The example paths
above are relative to the repository root.

The default command discovers tracked and non-ignored JavaScript files through
Git, then runs `node --check` on each regular file. Deleted worktree files are
excluded. Invalid syntax, linked source files, escaped paths, failed Git
discovery and failed subprocesses are explicit failures with a nonzero exit.
It does not execute the checked sources.

`--dry-run` only describes the command: no discovery, validation or writes.
It is not evidence that real syntax checks passed. `--help` shows usage;
unrecognized arguments fail.

`pnpm --dir src check:repository` adds the project's structural regression tests.
`pnpm --dir src check` also runs lint, formatting, production build, unit/integration,
browser/accessibility, type and bounded source checks. Checkups never install
packages, start services, rotate logs, prune archives, access the network or
change the worktree. Tests of real CLI/storage actions use isolated fixtures.
