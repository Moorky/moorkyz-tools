# Development Audit: M8 explicit local draft recovery

> Fill in with observed evidence. Never mark an unexecuted check as PASS.

| Field                    | Value                                                                           |
| ------------------------ | ------------------------------------------------------------------------------- |
| Audit ID                 | `2026-10-17T01-19-39Z-m8-explicit-local-draft-recovery`                         |
| Created (UTC)            | `2026-10-17T01:19:39.236Z`                                                      |
| Task / issue             | M8 explicit local draft recovery                                                |
| Auditor                  | Copilot self-audit; direct source, tests, storage boundary, and diff inspection |
| Branch / baseline commit | `master` / `9fa2e3d`                                                            |
| Final commit             | Report in task completion; do not edit this audit after committing              |
| Scope reviewed           | Explicit browser-local configurator save/restore/delete and validation recovery |
| Outcome                  | `PASS WITH RISKS`                                                               |

## 1. Request and outcome

- Requested behavior: Continue M8 local-first draft recovery without silently retaining drafts or sending them to a server.
- Implemented behavior: Added a versioned localStorage envelope, explicit save/restore/delete controls, confirmation before overwrite/deletion, schema validation on restore, and visible storage/validation errors.
- Acceptance criteria and evidence: Browser tests prove no draft is written on page load or ordinary editing; save persists version 1, restore applies validated data, overwrite/delete wait for confirmation, unsupported/invalid drafts leave current state unchanged, and reload does not auto-restore. Axe passes on valid and invalid states.
- Intentional exclusions: No automatic save, IndexedDB fallback, import/export, migration beyond version 1, or draft sharing.
- Relevant decisions/contracts: `docs/portal/development-studio.md`; M8 local-first Studio in `docs/engineering/roadmap.md`; shared config contract and validator in `packages/sdk/src/configuration.ts`.

## 2. Change inventory

| Path                                                                   | Change                                                                                                    | Risk                                |
| ---------------------------------------------------------------------- | --------------------------------------------------------------------------------------------------------- | ----------------------------------- |
| `packages/portal/src/components/visual-configurator.tsx`               | Versioned local draft encoding, user-initiated save/restore/delete, confirmations, fail-closed validation | Medium: browser-local data handling |
| `packages/portal/src/app/globals.css`                                  | Confirmation and storage status styling                                                                   | Low                                 |
| `tests/browser/portal.spec.js`                                         | Verify no autosave, explicit save, confirmed overwrite/delete, malformed/versioned restore rejection      | Low                                 |
| `docs/portal/development-studio.md`                                    | State retention, consent, and remaining recovery scope                                                    | Low                                 |
| `docs/engineering/implementation-status.md`, `MANIFEST.json`           | Record M8 evidence and refresh portal spec hash                                                           | Low                                 |
| `docs/audits/2026-10-17T01-19-39Z-m8-explicit-local-draft-recovery.md` | This evidence audit                                                                                       | Low                                 |

## 3. Verification evidence

Use `PASS`, `FAIL`, `NOT RUN` (with reason), or `N/A`. Record exact commands and results.

| Check                        | Result          | Exact command / method                                                                                                    | Evidence / notes                                                                                                                                                                      |
| ---------------------------- | --------------- | ------------------------------------------------------------------------------------------------------------------------- | ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| Formatting / lint            | PASS            | `$env:TOOLS_MOORKYZ_COM_FIXTURE_PORT='48731'; $env:TOOLS_MOORKYZ_COM_PORTAL_PORT='48732'; pnpm check`                         | ESLint and repository format checks passed.                                                                                                                                           |
| Typecheck                    | PASS            | Same `pnpm check` invocation; `pnpm --filter @tools.moorkyz.com/portal build`                                                       | Portal/SDK and consumer typechecks passed.                                                                                                                                            |
| Unit / integration tests     | PASS            | Same `pnpm check` invocation                                                                                              | 127 Node tests passed; 0 failed.                                                                                                                                                      |
| Browser / accessibility      | PASS            | Same `pnpm check` invocation; targeted: `pnpm exec playwright test tests/browser/portal.spec.js -g "visual configurator"` | 27 Chromium tests passed. Targeted flows cover explicit storage, invalid-version/schema recovery, and axe checks.                                                                     |
| Build / consumer fixture     | PASS            | Same `pnpm check` invocation                                                                                              | Next.js production build and all consumer typechecks passed.                                                                                                                          |
| Security / dependency review | PASS WITH RISKS | Source/diff inspection                                                                                                    | No dependencies changed. Stored values are limited to validated local configuration fields; all reads/writes/deletes are user-triggered and errors are surfaced. No remote reporting. |
| Performance (if relevant)    | N/A             | Not measured                                                                                                              | Small localStorage record; synchronous access only on explicit user actions.                                                                                                          |
| `git diff --check`           | PASS            | `git diff --check`                                                                                                        | No whitespace errors.                                                                                                                                                                 |
| Manifest integrity           | PASS            | PowerShell SHA-256 comparison over `MANIFEST.json`                                                                        | All 16 listed document hashes matched.                                                                                                                                                |

## 4. Risk and quality review

- Public contract, feature-default, and compatibility impact: Portal-only behavior; no SDK contract/default changes. Draft schema is stored under an explicit versioned portal namespace.
- Security, privacy, permissions, and secrets: Data includes only configuration fields supported by the schema, not callbacks, credentials, host context, or commands. No upload, URL sharing, telemetry, or code execution.
- Accessibility, keyboard, and reduced motion: Native controls, explicit confirmation buttons, live status, validation alerts, and axe checks for valid/invalid drafts passed.
- Disabled-feature work/resource/network behavior: No storage read/write occurs on initial render or ordinary draft edits; localStorage is touched only after explicit save/restore/delete actions. No network activity is introduced.
- Error handling, cleanup, and recovery: Storage exceptions produce visible messages; unsupported versions, malformed envelopes, and schema-invalid drafts fail closed without replacing the active draft or preview.
- Dependency, performance, and regression exposure: No dependency change. Portal build and full test suite passed.

## 5. Findings and residual risks

| ID  | Severity | Finding and evidence                                                                                                   | Mitigation / owner                                                             | Status                        |
| --- | -------- | ---------------------------------------------------------------------------------------------------------------------- | ------------------------------------------------------------------------------ | ----------------------------- |
| R1  | LOW      | Saved drafts remain in browser localStorage until the user deletes them; no expiry or automatic cleanup is configured. | UI explains browser-local retention and provides explicit delete confirmation. | Accepted local-first behavior |
| R2  | LOW      | Only draft envelope version 1 is supported; future schema migrations and recovery workflows are not implemented.       | Unsupported versions fail closed and preserve current state; future M8 work.   | Open                          |

- Known gaps / checks not run: No automatic draft recovery, migrations, import/export, storage quota simulation, or manual assistive-technology testing.
- Required approvals: None; explicit browser-local draft recovery is in the approved M8 portal scope. No publishing, deployment, push, or external integration was performed.
- Rollback or recovery: Revert the focused local-draft commit; stored records are not automatically deleted and remain user-removable.
- Release readiness: `NOT READY`

## 6. Completion record

- Audit created before the task commit and included in that commit, or reason not committed: Created before staging; will be included in the focused local commit.
- Final commit hash: `PENDING`
- External actions: `NOT AUTHORIZED` unless separately approved.

**Limitations:** An agent self-audit is not an independent security review, accessibility certification, or release approval.
