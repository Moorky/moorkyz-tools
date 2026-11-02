# Development Audit: M6 controller gesture intent integration

| Field | Value |
|---|---|
| Audit ID | `2026-10-17T00-15-22Z-m6-controller-gesture-intent-integration` |
| Created (UTC) | `2026-10-17T00:15:22.072Z` |
| Task / issue | M6 controller gesture intent integration |
| Auditor | Self-audit by Copilot SDK agent; no independent reviewer |
| Branch / baseline commit | `master` / `27a623e` |
| Final commit | Report in task completion; do not edit this audit after commit |
| Scope reviewed | Controller wiring for the opt-in pointer gesture manager |
| Outcome | `PASS WITH RISKS` |

## 1. Request and outcome

- Requested behavior: Finish the M6 integration of the already-tested touch/pen gesture intent manager with the built-in toolbar lifecycle.
- Implemented behavior: When `features.advancedGestures` is explicitly true, the controller requires a host `onGesture` callback and creates an element-scoped gesture manager. Turning the flag off or disposing the controller removes listeners and clears active pointer state. The host decides what a gesture means.
- Acceptance criteria and evidence: `pnpm check` passes 115 Node tests and 21 Chromium tests. Tests cover missing-handler rejection, controller flag enable/disable, host intent delivery, prior edge/vertical/editable/mouse/cancel filtering, no-default-prevention, and cleanup.
- Intentional exclusions: No fixed gesture-to-command mapping, momentum, `touch-action` changes, global listeners, or persistent gesture preferences.
- Relevant decisions/contracts: Q13, Q18, Q20, Q28, Q53, Q64, Q71, Q72; M6 gesture draft in `docs/contracts/sdk-and-schema.md`.

## 2. Change inventory

| Path | Change | Risk |
|---|---|---|
| `MANIFEST.json` | Refreshed the tracked SDK contract digest | Low |
| `docs/audits/2026-10-17T00-15-22Z-m6-controller-gesture-intent-integration.md` | Added this evidence-backed audit | Low |
| `docs/contracts/sdk-and-schema.md` | Clarified controller-owned listener lifecycle and host callback requirements | Low |
| `docs/engineering/implementation-status.md` | Recorded M6 gesture integration progress and remaining profile work | Low |
| `packages/sdk/README.md` | Documented built-in controller integration and accessible alternatives | Low |
| `packages/sdk/src/toolbar.ts` | Required host policy and synchronized the gesture manager with config/mount/dispose | Medium |
| `packages/sdk/test/toolbar.test.js` | Added missing-handler and feature-toggle coverage | Low |
| `tests/browser/fixtures/fixture.js` | Added a controller-backed gesture fixture | Low |
| `tests/browser/toolbar.spec.js` | Added integration coverage for host callback delivery and runtime disable | Low |

The worktree contained an unrelated `packages/portal/public/site.webmanifest` modification; it was not staged or changed.

## 3. Verification evidence

| Check | Result | Exact command / method | Evidence / notes |
|---|---|---|---|
| Formatting / lint | PASS | `pnpm check` | ESLint and Prettier checks passed |
| Typecheck | PASS | `pnpm check` | SDK, portal, vanilla, and React fixtures passed |
| Unit / integration tests | PASS | `pnpm check` | 115 Node tests passed, 0 failed |
| Browser / accessibility | PASS | `pnpm check` | 21 Chromium tests passed; pointer intent integration covered |
| Build / consumer fixture | PASS | `pnpm check` | SDK and portal builds and consumer fixture typechecks passed |
| Frozen install / package contents | PASS | `pnpm install --frozen-lockfile`; `pnpm --filter @tools.moorkyz.com/sdk pack --dry-run` | No dependencies changed |
| Specification hashes | PASS | PowerShell SHA-256 verification of all `MANIFEST.json` entries | 16 of 16 hashes matched |
| Security / dependency review | NOT RUN | No dependency manifest or lockfile changed | Manual host-authorization and listener review only |
| Performance | N/A | No benchmark run | Listener set is limited to the toolbar element and exists only while enabled |
| `git diff --check` | PASS | `git diff --check` | No whitespace errors; line-ending warnings only |

## 4. Risk and quality review

- Public contract, feature-default, and compatibility impact: Additive draft host callback; `advancedGestures` remains false. Enabling the flag without a handler fails explicitly.
- Security, privacy, permissions, and secrets: Gesture intent carries only direction, pointer type, and distance; host callback retains command authority. No event object, coordinates, or history is passed.
- Accessibility, keyboard, and reduced motion: The manager is not a UI; hosts must retain button/keyboard alternatives. Existing collapse/restore buttons remain the accessible path. No animation or motion is added.
- Disabled-feature work/resource/network behavior: No gesture listeners are installed unless enabled. Runtime disable/dispose removes the scoped listeners and clears the active pointer. No network, timer, polling, or DOM scan occurs.
- Error handling, cleanup, and recovery: Missing host handler is a structured update/mount failure. Callback exceptions propagate through event dispatch. Cleanup is idempotent.
- Dependency, performance, and regression exposure: No dependencies changed. Gesture thresholds remain conservative heuristics; broader device coverage remains open.

## 5. Findings and residual risks

| ID | Severity | Finding and evidence | Mitigation / owner | Status |
|---|---|---|---|---|
| R1 | Low | The host chooses gesture meaning; no universal gesture profile is supplied | Define supported profiles only after real-device and accessibility validation | Open |
| R2 | Low | No independent security review or dependency advisory scan was performed | Retain as M10 release gates | Open |

- Known gaps / checks not run: Real-device/RTL/zoom/touch matrix, manual assistive-technology validation, higher-level gesture profiles, independent security review, dependency advisory scan.
- Required approvals: None; no default or authorization policy changed.
- Rollback or recovery: Revert the focused controller integration commit; disable the feature to remove listeners at runtime.
- Release readiness: `NOT READY` — M6 and M7–M11 remain incomplete.

## 6. Completion record

- Audit created before the task commit and included in that commit.
- Final commit hash: `PENDING`
- External actions: `NOT AUTHORIZED`.

**Limitations:** This is a self-audit, not an independent security review, accessibility certification, or release approval.
