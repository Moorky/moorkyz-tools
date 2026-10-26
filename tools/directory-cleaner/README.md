# Moorky Directory Cleaner

An independent Node.js 22+ local tool. No runtime dependencies, automatic
downloads, permanent deletion, scheduled cleanup or public service.

## Rules

Each rule has a filename or relative-path wildcard (`*`, `?`, `**`) and an action:
**Keep**, **Move**, or **Delete (archive)**. Matching is case-insensitive. Patterns
without slashes match basenames; patterns with slashes match relative paths.
All conditions inside a rule combine with AND. Keep rules always win, regardless
of order; otherwise the first matching rule applies. Unmatched files stay put.

Optional `createdBefore` and `createdAfter` use real `YYYY-MM-DD` dates at UTC
midnight. Optional `smallerThanBytes` and `largerThanBytes` are non-negative whole
byte counts. All bounds are strict, not inclusive. Missing creation metadata
with date rules fails explicitly. Recursion is off by default.

Delete **always archives** into `.directory-cleaner-archive\<preview-id>\files`
under the source, or a separately selected archive folder. Move preserves the
relative hierarchy under its chosen destination. Existing files are never
overwritten. Filesystem roots, the home folder itself, Windows system folders,
network paths and symlink/junction traversal are rejected. Output/archive folders
are excluded from scans. Previews are bounded to 10,000 directory entries.

## Standalone script

Extract the complete ZIP, edit `settings.example.json`, then:

```powershell
node .\clean.mjs --settings .\settings.example.json --plan .\preview.json
# Inspect preview.json. Nothing has moved yet.
node .\clean.mjs --apply .\preview.json --confirm "APPLY <preview-id>"
```

Use the actual ID printed by the preview. Existing plan files are not replaced.
The portal can download your settings; use the same script alongside them.
Do not put plan/settings output inside the scanned folder: adding files after
preview intentionally makes that preview stale.

## Local web service

Run `node .\server.mjs` or double-click `Start-DirectoryCleaner.cmd`. From the
parent repository, `pnpm tool:directory-cleaner` does the same thing. The service
binds only to `127.0.0.1:8766`; `--port 8767` selects another local port.
In the portal terminal, set `TOOLS_MOORKYZ_COM_DIRECTORY_CLEANER_ENABLED=1` and, if needed,
`TOOLS_MOORKYZ_COM_DIRECTORY_CLEANER_PORT` to the same port. Bind the portal to loopback.

The browser connects only when explicitly requested. Preview reads metadata but
does not create folders, receipts or file copies. Apply uses a process-held
preview (10-minute lifetime, at most 10 retained previews), process-local request
token and exact `APPLY <id>` confirmation. A fresh scan must match the entire
preview before any action. The service never accepts an arbitrary action list.

## Preservation, receipts and limits

Each run first writes and syncs an exclusive `receipt.jsonl` inside the archive
run folder, listing intended source/target paths. It appends completed/failed
actions as they occur. Same-filesystem moves preserve via a no-overwrite hard
link before removing the source name. Cross-filesystem/unsupported-link moves use
an exclusive copy and streaming SHA-256 verification first. Modification times
are preserved on copies; creation times can change across volumes.

Source identity is checked against preview metadata before and after preservation.
On a failure processing stops; completed files and copies remain, and the result
explicitly reports partial/failed status and pending work. Nothing is silently
rolled back or retried. Use the receipt to locate files; manually verify the
original path is empty before restoring a copy. Never overwrite a newer file.

Active programs, abrupt termination, unusual network-mounted filesystems and
malicious concurrent filesystem changes are not a supported multi-user boundary.
Close writers before applying. A failed preservation can retain both source and
destination. There is no arbitrary filesystem browser, remote hosting, account
system or automatic archive deletion.

First-party code follows the parent Apache-2.0 license. Test only disposable
fixtures, never personal directories.
