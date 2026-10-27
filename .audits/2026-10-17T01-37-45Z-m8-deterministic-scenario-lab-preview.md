# Development Audit: M8 deterministic Scenario Lab preview

> Fill in with observed evidence. Never mark an unexecuted check as PASS.

| Field                    | Value                                                                                       |
| ------------------------ | ------------------------------------------------------------------------------------------- |
| Audit ID                 | `2026-10-17T01-37-45Z-m8-deterministic-scenario-lab-preview`                                |
| Created (UTC)            | `2026-10-17T01:37:45.659Z`                                                                  |
| Task / issue             | M8 deterministic Scenario Lab preview                                                       |
| Auditor                  | Copilot self-audit; direct source, browser tests, and preview behavior inspection           |
| Branch / baseline commit | `master` / `15ef1ca`                                                                        |
| Final commit             | Report in task completion; do not edit this audit after committing                          |
| Scope reviewed           | Portal Scenario Lab, host-simulated outcomes, snapshot replay/export, browser/a11y coverage |
| Outcome                  | `PASS WITH RISKS`                                                                           |

## 1. Request and outcome

- Requested behavior: Continue M8 with deterministic, real-SDK scenario previews covering context, viewport, time, permission decisions, and failures.
- Implemented behavior: Added a page-local Scenario Lab that mounts the actual toolbar, maps selected context to actions, simulates compact/wide viewport, a controlled clock, host authorization denial, and host action failure. It captures/replays one in-memory versioned snapshot and copies JSON as data.
- Acceptance criteria and evidence: Browser test exercises denied actions, simulated host failures, time/viewport/context replay and accessibility; full suite passes (127 Node, 29 Chromium).
- Intentional exclusions: No real auth state, network calls, arbitrary code execution, persistent history, or production host state is connected.
- Relevant decisions/contracts: M8 Scenario Lab in `docs/portal/development-studio.md`; host-owned authority rules in `docs/architecture/security-and-trust.md`.

## 2. Change inventory

| Path                                                                                                         | Change                                                                           | Risk                                                          |
| ------------------------------------------------------------------------------------------------------------ | -------------------------------------------------------------------------------- | ------------------------------------------------------------- |
| `packages/portal/src/components/scenario-lab.tsx`                                                            | Deterministic local scenario controls, host callbacks, capture/replay, JSON copy | Medium: security simulations must remain clearly illustrative |
| `packages/portal/src/components/component-lab.tsx`, `packages/portal/src/components/visual-configurator.tsx` | Give embedded previews named regions for assistive-technology navigation         | Low                                                           |
| `packages/portal/src/app/page.tsx`                                                                           | Add the Scenario Lab portal section and link                                     | Low                                                           |
| `packages/portal/src/app/globals.css`                                                                        | Responsive scenario controls and preview frame                                   | Low                                                           |
| `tests/browser/portal.spec.js`                                                                               | Verify permission/fault simulation, replay, and axe                              | Low                                                           |
| `docs/portal/development-studio.md`, `docs/engineering/implementation-status.md`, `MANIFEST.json`            | Document local-only scenario behavior, M8 status, and portal spec digest         | Low                                                           |
| `docs/audits/2026-10-17T01-37-45Z-m8-deterministic-scenario-lab-preview.md`                                  | This evidence audit                                                              | Low                                                           |

## 3. Verification evidence

Use `PASS`, `FAIL`, `NOT RUN` (with reason), or `N/A`. Record exact commands and results.

| Check                        | Result          | Exact command / method                                                                                             | Evidence / notes                                                                                                                                            |
| ---------------------------- | --------------- | ------------------------------------------------------------------------------------------------------------------ | ----------------------------------------------------------------------------------------------------------------------------------------------------------- |
| Formatting / lint            | PASS            | `$env:TOOLS_MOORKYZ_COM_FIXTURE_PORT='48731'; $env:TOOLS_MOORKYZ_COM_PORTAL_PORT='48732'; pnpm check`                  | ESLint and workspace formatting passed.                                                                                                                     |
| Typecheck                    | PASS            | Same `pnpm check` invocation                                                                                       | Portal, SDK, and consumer typechecks passed.                                                                                                            |
| Unit / integration tests     | PASS            | Same `pnpm check` invocation                                                                                       | 127 Node tests passed; 0 failed.                                                                                                                            |
| Browser / accessibility      | PASS            | Same `pnpm check` invocation; targeted: `pnpm exec playwright test tests/browser/portal.spec.js -g "Scenario Lab"` | 29 Chromium tests passed; Scenario Lab denial/fault/replay scenario and axe assertions passed.                                                              |
| Build / consumer fixture     | PASS            | Same `pnpm check` invocation                                                                                       | Next.js production build and all consumer typechecks passed.                                                                                                |
| Security / dependency review | PASS WITH RISKS | Source/test review                                                                                                 | No dependencies added. Host decisions are simulated locally; no actual permissions, remote services, credentials, or generated code execution are involved. |
| Performance (if relevant)    | N/A             | Not measured                                                                                                       | Fixed local scenario state; no timers or polling.                                                                                                           |
| `git diff --check`           | PASS            | `git diff --check`                                                                                                 | No whitespace errors.                                                                                                                                       |
| Manifest integrity           | PASS            | PowerShell SHA-256 comparison over `MANIFEST.json`                                                                 | All 16 listed document hashes matched.                                                                                                                      |

## 4. Risk and quality review

- Public contract, feature-default, and compatibility impact: Portal-only demo; no SDK exports, feature defaults, or host policy change.
- Security, privacy, permissions, and secrets: Permission and fault behaviors are simulated by a host callback in the isolated preview. The Scenario Lab does not contact an app server or operate on real authorization state.
- Accessibility, keyboard, and reduced motion: Labeled native controls, status messages, actual toolbar keyboard behavior, and axe tests with the preview active pass.
- Disabled-feature work/resource/network behavior: Search remains off in the scenario toolbar; no polling or network calls. Preview lifecycle is effect-scoped.
- Error handling, cleanup, and recovery: Denials are visible and do not call the action; simulated failures are surfaced through `onActionError`; snapshots can replay only typed local scenario fields.
- Dependency, performance, and regression exposure: No new dependencies. Preview and scenario controls use existing package exports.

## 5. Findings and residual risks

| ID  | Severity | Finding and evidence                                                                        | Mitigation / owner                                                               | Status                 |
| --- | -------- | ------------------------------------------------------------------------------------------- | -------------------------------------------------------------------------------- | ---------------------- |
| R1  | LOW      | Scenario snapshots contain only one current configuration and are kept in component memory. | Current UI labels page-local behavior; broader replay/history is future M8 work. | Accepted limitation    |
| R2  | LOW      | Simulated permissions/faults do not exercise real host auth or network boundaries.          | Text explicitly identifies simulation; no production permissions are granted.    | Accepted demo boundary |

- Known gaps / checks not run: No regression fixture export/import, real browser/device matrix, manual assistive-technology validation, or full Scenario Lab simulation matrix.
- Required approvals: None; local simulation uses no actual host permissions or external service. No publishing, deployment, push, or external integration was performed.
- Rollback or recovery: Revert the focused Scenario Lab commit; no scenario state persists after leaving the page.
- Release readiness: `NOT READY`

## 6. Completion record

- Audit created before the task commit and included in that commit, or reason not committed: Created before staging; will be included in the focused local commit.
- Final commit hash: `PENDING`
- External actions: `NOT AUTHORIZED` unless separately approved.

**Limitations:** An agent self-audit is not an independent security review, accessibility certification, or release approval.
