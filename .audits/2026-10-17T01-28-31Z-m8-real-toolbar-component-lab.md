# Development Audit: M8 real toolbar Component Lab

> Fill in with observed evidence. Never mark an unexecuted check as PASS.

| Field                    | Value                                                                                    |
| ------------------------ | ---------------------------------------------------------------------------------------- |
| Audit ID                 | `2026-10-17T01-28-31Z-m8-real-toolbar-component-lab`                                     |
| Created (UTC)            | `2026-10-17T01:28:31.842Z`                                                               |
| Task / issue             | M8 real toolbar Component Lab                                                            |
| Auditor                  | Copilot self-audit; direct component, browser tests, and diff inspection                 |
| Branch / baseline commit | `c96d989`                                                                                |
| Final commit             | Report in task completion; do not edit this audit after committing                       |
| Scope reviewed           | Portal Component Lab with actual adapter-mounted toolbar and reproducible state controls |
| Outcome                  | `PASS WITH RISKS`                                                                        |

## 1. Request and outcome

- Requested behavior: Continue M8 component exploration with real, reproducible toolbar states rather than a visual mock.
- Implemented behavior: Added default, narrow-overflow, and local-search scenarios plus manual collapse/expand for a real toolbar instance; action results return only to the portal host.
- Acceptance criteria and evidence: Browser test switches scenarios, observes overflow controls, opens the actual search panel, collapses/restores, and runs axe. Full suite passes (127 Node, 28 Chromium).
- Intentional exclusions: No simulated authorization, remote search provider, task/notification state, arbitrary code, or persistence.
- Relevant decisions/contracts: M8 Component Lab in `docs/portal/development-studio.md`; host-only action authority and real SDK preview requirements in `docs/architecture/security-and-trust.md`.

## 2. Change inventory

| Path                                                                                              | Change                                                                   | Risk |
| ------------------------------------------------------------------------------------------------- | ------------------------------------------------------------------------ | ---- |
| `packages/portal/src/components/component-lab.tsx`                                                | Real toolbar scenario controls, narrow frame, search and manual collapse | Low  |
| `packages/portal/src/app/page.tsx`                                                                | Add Component Lab entry and clarify portal status                        | Low  |
| `packages/portal/src/app/globals.css`                                                             | Responsive preview frame and state controls                              | Low  |
| `tests/browser/portal.spec.js`                                                                    | Exercise overflow, search, collapse and axe against actual toolbar       | Low  |
| `docs/portal/development-studio.md`, `docs/engineering/implementation-status.md`, `MANIFEST.json` | Record M8 scope/evidence and update portal spec digest                   | Low  |
| `docs/audits/2026-10-17T01-28-31Z-m8-real-toolbar-component-lab.md`                               | This evidence audit                                                      | Low  |

## 3. Verification evidence

Use `PASS`, `FAIL`, `NOT RUN` (with reason), or `N/A`. Record exact commands and results.

| Check                        | Result          | Exact command / method                                                                                              | Evidence / notes                                                                                                                                   |
| ---------------------------- | --------------- | ------------------------------------------------------------------------------------------------------------------- | -------------------------------------------------------------------------------------------------------------------------------------------------- |
| Formatting / lint            | PASS            | `$env:TOOLS_MOORKYZ_COM_FIXTURE_PORT='48731'; $env:TOOLS_MOORKYZ_COM_PORTAL_PORT='48732'; pnpm check`                   | ESLint and workspace format checks passed.                                                                                                         |
| Typecheck                    | PASS            | Same `pnpm check` invocation                                                                                        | Portal, SDK, React/vanilla consumers and package typechecks passed.                                                                            |
| Unit / integration tests     | PASS            | Same `pnpm check` invocation                                                                                        | 127 Node tests passed; 0 failed.                                                                                                                   |
| Browser / accessibility      | PASS            | Same `pnpm check` invocation; targeted: `pnpm exec playwright test tests/browser/portal.spec.js -g "component lab"` | 28 Chromium tests passed; actual preview state test and axe checks passed.                                                                         |
| Build / consumer fixture     | PASS            | Same `pnpm check` invocation                                                                                        | Portal Next.js production build passed and mounts the real workspace package.                                                                      |
| Security / dependency review | PASS WITH RISKS | Source/test review                                                                                                  | No dependencies changed, no network adapter, and no privileged commands are simulated. Preview actions are returned only to a local host callback. |
| Performance (if relevant)    | N/A             | Not measured                                                                                                        | One controller per mounted preview; the adapter disposes it on unmount.                                                                            |
| `git diff --check`           | PASS            | `git diff --check`                                                                                                  | No whitespace errors.                                                                                                                              |
| Manifest integrity           | PASS            | PowerShell SHA-256 comparison over `MANIFEST.json`                                                                  | All 16 listed document hashes matched.                                                                                                             |

## 4. Risk and quality review

- Public contract, feature-default, and compatibility impact: Portal-only examples consume existing toolbar configuration; no SDK contract or feature default changes.
- Security, privacy, permissions, and secrets: All preview actions remain host callbacks; lab scenarios grant no permissions, call no remote service, and store no state.
- Accessibility, keyboard, and reduced motion: Scenario buttons expose pressed state; toolbar controls retain native keyboard behavior; axe passes with search panel open and after restore.
- Disabled-feature work/resource/network behavior: Only the selected toolbar instance is mounted; its search feature is off except in the explicit Search scenario. No polling or external requests.
- Error handling, cleanup, and recovery: React adapter reports mount errors; scenario updates dispose/recreate the preview; collapse/restore use the controller's explicit methods.
- Dependency, performance, and regression exposure: No dependency or core-package changes. Narrow mode exercises actual overflow behavior in the browser.

## 5. Findings and residual risks

| ID  | Severity | Finding and evidence                                                                                | Mitigation / owner                                                         | Status |
| --- | -------- | --------------------------------------------------------------------------------------------------- | -------------------------------------------------------------------------- | ------ |
| R1  | LOW      | This is a small Component Lab slice; only default, narrow, search, and collapse states are covered. | Extend with task/notification/failure/viewport scenarios in later M8 work. | Open   |

- Known gaps / checks not run: Broader Scenario Lab replay, fault/time/permission simulation, manual assistive-technology testing, and additional component states remain.
- Required approvals: None; existing SDK behavior is shown through a local preview. No external integration or release action was performed.
- Rollback or recovery: Revert the focused Component Lab commit; the portal landing content and SDK remain functional.
- Release readiness: `NOT READY`

## 6. Completion record

- Audit created before the task commit and included in that commit, or reason not committed: Created before staging; will be included in the focused local commit.
- Final commit hash: `PENDING`
- External actions: `NOT AUTHORIZED` unless separately approved.

**Limitations:** An agent self-audit is not an independent security review, accessibility certification, or release approval.
