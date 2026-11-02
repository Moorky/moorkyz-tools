# Development Audit: M6 opt-in pointer gesture manager

| Field | Value |
|---|---|
| Audit ID | `2026-10-16T23-27-23Z-m6-opt-in-pointer-gesture-manager` |
| Created (UTC) | `2026-10-16T23:27:23.201Z` |
| Task / issue | M6 opt-in pointer gesture manager |
| Auditor | Self-audit by Copilot SDK agent; no independent reviewer |
| Branch / baseline commit | `master` / `2189f45` |
| Final commit | Report in task completion; do not edit this audit after commit |
| Scope reviewed | Explicitly enabled, element-scoped touch/pen swipe intent recognition |
| Outcome | `PASS WITH RISKS` |

## 1. Request and outcome

- Requested behavior: Continue M6 advanced-gesture work while preserving native interaction defaults, edge gestures, keyboard alternatives, and the disabled-feature no-work requirement.
- Implemented behavior: Added `createGestureManager()` that attaches four pointer listeners only when explicitly enabled. It recognizes bounded horizontal touch/pen intent and ignores mouse, edge, vertical, editable/interactive, canceled, and already-prevented sequences. It never prevents browser defaults or performs commands.
- Acceptance criteria and evidence: `pnpm check` passes 110 Node tests and 18 Chromium tests. Tests cover disabled listener installation, explicit enable/disable cleanup, right/left intent, mouse/edge/vertical/editable/cancel filters, and proof that dispatch events remain unprevented.
- Intentional exclusions: No automatic toolbar action mapping, momentum, drag/reorder, global handlers, `touch-action` changes, velocity tracking, pointer capture, or gesture UI.
- Relevant decisions/contracts: Q13, Q18, Q28, Q53, Q64, Q71, Q72; M6 gesture draft in `docs/contracts/sdk-and-schema.md`; native-first interaction guidance in `docs/design/interaction-and-visual-system.md`.

## 2. Change inventory

| Path | Change | Risk |
|---|---|---|
| `MANIFEST.json` | Refreshed the tracked SDK contract digest | Low |
| `docs/audits/2026-10-16T23-27-23Z-m6-opt-in-pointer-gesture-manager.md` | Added this evidence-backed audit | Low |
| `docs/contracts/sdk-and-schema.md` | Documented opt-in behavior, thresholds, exclusions, and host responsibility | Low |
| `docs/engineering/implementation-status.md` | Recorded M6 gesture progress and remaining validation | Low |
| `packages/sdk/README.md` | Added opt-in host integration guidance | Low |
| `packages/sdk/src/gestures.ts` | Added instance-scoped pointer intent manager and cleanup | Medium |
| `packages/sdk/src/index.ts` | Exported the gesture manager draft API | Low |
| `packages/sdk/test/gestures.test.js` | Proved disabled mode allocates no listeners and cleanup is exact | Low |
| `tests/browser/fixtures/fixture.js` | Added an explicit gesture test mount | Low |
| `tests/browser/fixtures/index.html` | Added a gesture target | Low |
| `tests/browser/server.mjs` | Allowed local serving of the gesture module | Low |
| `tests/browser/toolbar.spec.js` | Added browser-level gesture/edge/native behavior checks | Low |

The worktree also contained an unrelated modification to `packages/portal/public/site.webmanifest`; it was excluded from staging and left untouched.

## 3. Verification evidence

| Check | Result | Exact command / method | Evidence / notes |
|---|---|---|---|
| Formatting / lint | PASS | `pnpm check` | ESLint and Prettier checks passed |
| Typecheck | PASS | `pnpm check` | SDK, portal, vanilla, and React fixtures passed |
| Unit / integration tests | PASS | `pnpm check` | 110 Node tests passed, 0 failed |
| Browser / accessibility | PASS | `pnpm check` | 18 Chromium tests passed; swipe intent behavior verified in Chromium |
| Build / consumer fixture | PASS | `pnpm check` | SDK and portal builds and consumer fixture typechecks passed |
| Frozen install / package contents | PASS | `pnpm install --frozen-lockfile`; `pnpm --filter @tools.moorkyz.com/sdk pack --dry-run` | No dependencies changed; gesture module is included |
| Specification hashes | PASS | PowerShell SHA-256 verification of all `MANIFEST.json` entries | 16 of 16 hashes matched |
| Security / dependency review | NOT RUN | No dependency manifest or lockfile changed | Manual event-scope review only; no independent audit claimed |
| Performance | N/A | No benchmark run | At most four listeners per explicitly enabled instance; no timers/RAF |
| `git diff --check` | PASS | `git diff --check` | No whitespace errors; Git emits line-ending normalization warnings only |

## 4. Risk and quality review

- Public contract, feature-default, and compatibility impact: Additive draft utility; no configuration default changed. `advancedGestures` remains false. Callers must explicitly pass the host-controlled enabled value.
- Security, privacy, permissions, and secrets: The manager retains only one in-progress pointer tuple and emits only direction/type/distance. It does not retain event objects or history. Callback authority remains host-owned.
- Accessibility, keyboard, and reduced motion: No UI was added; hosts must provide a keyboard/button alternative for any action mapped to a swipe. No movement or animation is performed by the manager.
- Disabled-feature work/resource/network behavior: Disabled creation attaches no pointer listener. Disabling and disposing remove all listeners and clear active pointer state. No network, timers, polling, or DOM scanning occurs.
- Error handling, cleanup, and recovery: Callback exceptions propagate through the pointer event dispatch; no action is silently swallowed. Listener cleanup is idempotent.
- Dependency, performance, and regression exposure: No dependencies changed. Thresholds are fixed conservative heuristics; broad real-device, touch cancellation, and edge-browser testing remains outstanding.

## 5. Findings and residual risks

| ID | Severity | Finding and evidence | Mitigation / owner | Status |
|---|---|---|---|---|
| R1 | Low | Gesture thresholds and device behavior have only focused Chromium coverage | Expand device/RTL/zoom/touch matrix and manual tests before stable contract approval | Open |
| R2 | Low | Gesture callbacks may throw from an event handler | Host callbacks should be bounded and handle their own failures | Open |
| R3 | Low | An unrelated `site.webmanifest` modification was present and intentionally excluded | Preserve user change; no action taken | Open |

- Known gaps / checks not run: Real-device/browser edge matrix, manual assistive-technology validation, integration with toolbar actions, independent security review, dependency advisory scan.
- Required approvals: None for the reversible opt-in draft; no feature default or authorization rule changed.
- Rollback or recovery: Revert the focused gesture commit; call `dispose()` to remove listeners.
- Release readiness: `NOT READY` — M6 and M7–M11 remain incomplete.

## 6. Completion record

- Audit created before the task commit and included in that commit.
- Final commit hash: `PENDING`
- External actions: `NOT AUTHORIZED`.

**Limitations:** This is a self-audit, not an independent security review, accessibility certification, or release approval.
