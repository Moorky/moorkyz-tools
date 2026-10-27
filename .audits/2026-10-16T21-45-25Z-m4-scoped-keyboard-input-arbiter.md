# Development Audit: M4 scoped keyboard input arbiter

> Fill in with observed evidence. Never mark an unexecuted check as PASS.

| Field | Value |
|---|---|
| Audit ID | `2026-10-16T21-45-25Z-m4-scoped-keyboard-input-arbiter` |
| Created (UTC) | `2026-10-16T21:45:25.322Z` |
| Task / issue | M4 scoped keyboard input arbiter |
| Auditor | Copilot coding agent; self-audit |
| Branch / baseline commit | `master` / `26a3f4c` |
| Final commit | Report in task completion; do not edit this audit after committing |
| Scope reviewed | Opt-in scoped keyboard shortcut arbitration, host authorization, reserved-key behavior, conflict resolution, and lifecycle cleanup |
| Outcome | `PASS WITH RISKS` |

## 1. Request and outcome

- Requested behavior: continue M4 input arbitration without taking global shortcut authority away from the host.
- Implemented behavior: added `createShortcutManager(target, policy)` with explicit host authorization, active-scope matching, scoped-over-global resolution, priority handling, ambiguity diagnostics, reserved browser/OS shortcut rejection, editable-field and pre-prevented-event guards, and listener cleanup after unregister/disposal.
- Acceptance criteria and evidence: tests prove no ambiguous registration wins, unauthorized/revoked shortcuts do not prevent browser handling, Ctrl/Meta browser shortcuts are rejected, editable fields are untouched, handler errors reach the host, and keyboard listener behavior is active only while registered. Browser test dispatches a shortcut inside the live toolbar scope and verifies cleanup.
- Intentional exclusions: no default/global listener, no auto-registration, no pointer/touch or advanced gesture handling, and no OS/browser integration beyond the reserved-key denylist.
- Relevant decisions/contracts: Q13, Q18, Q20, Q28, Q71–Q72, Q80; `docs/design/interaction-and-visual-system.md`; `docs/architecture/security-and-trust.md`; `docs/contracts/sdk-and-schema.md`.

## 2. Change inventory

| Path | Change | Risk |
|---|---|---|
| `packages/sdk/src/input.ts`, `src/index.ts` | Added and exported the opt-in shortcut manager | Medium |
| `packages/sdk/test/input.test.js` | Added authorization, scope, conflict, reserved-key, editable-target and cleanup tests | Medium |
| `tests/browser/fixtures/fixture.js`, `server.mjs`, `tests/browser/input.spec.js` | Exercised the live keyboard path in Chromium and served the new package subpath | Low |
| `docs/contracts/sdk-and-schema.md`, `docs/engineering/implementation-status.md` | Documented the draft M4 input behavior and remaining gaps | Low |
| `docs/audits/2026-10-16T21-45-25Z-m4-scoped-keyboard-input-arbiter.md` | Recorded this M4 input slice | Low |

## 3. Verification evidence

Use `PASS`, `FAIL`, `NOT RUN` (with reason), or `N/A`. Record exact commands and results.

| Check | Result | Exact command / method | Evidence / notes |
|---|---|---|---|
| Formatting / lint | PASS | `pnpm check` | ESLint and Prettier passed. |
| Typecheck | PASS | `pnpm check` | Package, vanilla/React consumers, and portal typechecks passed. |
| Unit / integration tests | PASS | `pnpm check` | 51 Node tests passed, 0 failed. |
| Browser / accessibility | PASS WITH RISKS | `pnpm check` | 11 Chromium tests passed, including the new scoped shortcut scenario; axe and existing layout/CSP tests remained green. Manual assistive-technology testing remains. |
| Build / consumer fixture | PASS | `pnpm check` | SDK and portal builds plus both consumer fixtures passed. |
| Security / dependency review | PASS WITH RISKS | Code inspection and `pnpm check` | No external calls or dependencies added. Shortcut registrations are host-authorized and denied/revoked events are not intercepted. The reserved-key list remains a finite best-effort list, not proof of all OS/browser combinations. |
| Performance (if relevant) | N/A | N/A | No measurement budget; manager installs one root keydown listener only while registrations exist. |
| `git diff --check` | PASS | `git diff --check` | No whitespace errors in task changes. |

## 4. Risk and quality review

- Public contract, feature-default, and compatibility impact: new draft API names only; no feature defaults changed and no shortcut is registered by default.
- Security, privacy, permissions, and secrets: host callback must authorize registration and dispatch; reserved combinations, editable elements and previously handled events are left untouched. No shortcut contents are recorded or transmitted.
- Accessibility, keyboard, and reduced motion: every registered shortcut is host-defined; toolbar buttons remain keyboard operable. The manager never installs a page-global listener unless the host explicitly supplies that root.
- Disabled-feature work/resource/network behavior: no listener exists before the first registration or after last unregister/disposal; no network, timer or storage resources are acquired.
- Error handling, cleanup, and recovery: authorization failures return registration errors; runtime conflicts are surfaced as diagnostics; thrown/rejected handlers reach the host error callback.
- Dependency, performance, and regression exposure: no dependencies added; one listener per manager while active.

## 5. Findings and residual risks

| ID | Severity | Finding and evidence | Mitigation / owner | Status |
|---|---|---|---|---|
| M4-INPUT-1 | Medium | Shortcut API names and reserved-key coverage remain draft; key behavior varies across operating systems and browser layouts. | Keep host authorization mandatory; complete the supported browser/device and AT matrix in M10. | Open |
| M4-INPUT-2 | Low | Priority is local to the supplied active scope; a host must resolve its broader context/surface precedence. | Host provides `getActiveScope` and owns global registration policy. | Open |

- Known gaps / checks not run: touch/pen gesture arbitration, manual screen-reader testing, OS-specific shortcut matrix, and performance budgets remain.
- Required approvals: API names remain draft; no release or external integration authorization granted.
- Rollback or recovery: revert this input-arbiter commit; no persisted data or external registration created.
- Release readiness: `NOT READY`

## 6. Completion record

- Audit created before the task commit and included in that commit.
- Final commit hash: `PENDING`
- External actions: `NOT AUTHORIZED` unless separately approved.

**Limitations:** An agent self-audit is not an independent security review, accessibility certification, or release approval.
