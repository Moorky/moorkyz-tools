# Development Audit: M6 opt-in spatial coordination calculator

| Field | Value |
|---|---|
| Audit ID | `2026-10-16T23-03-50Z-m6-opt-in-spatial-coordination-calculator` |
| Created (UTC) | `2026-10-16T23:03:50.284Z` |
| Task / issue | M6 opt-in spatial coordination calculator |
| Auditor | Self-audit by Copilot SDK agent; no independent reviewer |
| Branch / baseline commit | `master` / `f38e43f` |
| Final commit | Report in task completion; do not edit this audit after commit |
| Scope reviewed | Disabled-by-default host exclusion-zone geometry coordinator |
| Outcome | `PASS WITH RISKS` |

## 1. Request and outcome

- Requested behavior: Continue the approved M6 optional spatial-coordination work without page scanning or host-layout mutation.
- Implemented behavior: Added a pure coordinator that is disabled by default, accepts bounded host-supplied exclusion rectangles, and deterministically selects a safe candidate anchor/rectangle using host-supplied viewport, safe-area, and dock dimensions. No DOM or CSS is inspected or modified.
- Acceptance criteria and evidence: `pnpm check` passes 107 Node tests and 16 Chromium tests. Seven focused spatial tests cover disabled no-inspection, safe-area geometry, preferred-anchor stability, collision avoidance, no-safe-placement diagnostics, invalid/proxy data, bounded zone count, and clearing zones when disabled.
- Intentional exclusions: No mounted-toolbar integration, automatic host offsets, DOM scanning, observers, CSS injection, placement persistence, automatic collapse, or background work.
- Relevant decisions/contracts: Q17, Q18, Q28, Q53, Q64, Q69, Q74, Q75, Q77; M6 spatial contract in `docs/contracts/sdk-and-schema.md`; spatial invariants in `docs/architecture/features-and-resources.md`.

## 2. Change inventory

| Path | Change | Risk |
|---|---|---|
| `MANIFEST.json` | Refreshed the tracked SDK contract digest | Low |
| `docs/audits/2026-10-16T23-03-50Z-m6-opt-in-spatial-coordination-calculator.md` | Added this evidence-backed audit | Low |
| `docs/contracts/sdk-and-schema.md` | Documented anchors, bounds, disabled behavior, and host responsibilities | Low |
| `docs/engineering/implementation-status.md` | Recorded M6 spatial calculator progress and remaining integration | Low |
| `packages/sdk/README.md` | Added opt-in host-zone usage guidance | Low |
| `packages/sdk/src/index.ts` | Exported the spatial coordinator draft API | Low |
| `packages/sdk/src/spatial.ts` | Added validated zone registry and deterministic safe-anchor calculation | Medium |
| `packages/sdk/test/spatial.test.js` | Covered enable/disable, geometry, conflicts, and bounds | Low |
| `tests/browser/server.mjs` | Allowed local serving of the spatial module for package-root imports | Low |

The worktree also contained untracked portal public icons/web manifest outside this task. They were excluded from staging and left untouched.

## 3. Verification evidence

| Check | Result | Exact command / method | Evidence / notes |
|---|---|---|---|
| Formatting / lint | PASS | `pnpm check` | ESLint and Prettier checks passed |
| Typecheck | PASS | `pnpm check` | SDK, portal, vanilla, and React fixtures passed |
| Unit / integration tests | PASS | `pnpm check` | 107 Node tests passed, 0 failed |
| Browser / accessibility | PASS | `pnpm check` | 16 Chromium tests passed; spatial helper is headless and adds no UI |
| Build / consumer fixture | PASS | `pnpm check` | SDK and portal builds and consumer fixture typechecks passed |
| Frozen install / package contents | PASS | `pnpm install --frozen-lockfile`; `pnpm --filter @tools.moorkyz.com/sdk pack --dry-run` | No dependencies changed; spatial module is packaged |
| Specification hashes | PASS | PowerShell SHA-256 verification of all `MANIFEST.json` entries | 16 of 16 hashes matched |
| Security / dependency review | NOT RUN | No dependency manifest or lockfile changed | Manual geometry/data review only; no independent audit claimed |
| Performance | N/A | No benchmark run | At most 64 zones; no observers, polling, or background work |
| `git diff --check` | PASS | `git diff --check` | No whitespace errors; Git emits line-ending normalization warnings only |

## 4. Risk and quality review

- Public contract, feature-default, and compatibility impact: Additive draft utility; `spatialCoordination` remains false by default. It is not wired into the controller's mounted geometry.
- Security, privacy, permissions, and secrets: Only host-supplied geometry is accepted; unknown keys/accessors and out-of-range values are rejected. No page DOM, user content, or sensitive host state is read.
- Accessibility, keyboard, and reduced motion: No UI or motion behavior was added. The host remains responsible for accessible placement and focus behavior when applying a returned rectangle.
- Disabled-feature work/resource/network behavior: Disabled by default; register/remove/resolve return before inspecting inputs when off. Disabling clears zones. No event listeners, timers, observers, fetches, or layout mutations are installed.
- Error handling, cleanup, and recovery: Invalid geometry and no-safe-placement conditions return structured errors. The calculator does not move or hide the dock; the host can reject results or clear zones.
- Dependency, performance, and regression exposure: No dependencies changed. Candidate selection is deterministic and bounded; root package static export may affect bundle size until M7/M10 gates.

## 5. Findings and residual risks

| ID | Severity | Finding and evidence | Mitigation / owner | Status |
|---|---|---|---|---|
| R1 | Low | The utility is not integrated into mounted toolbar positioning; the host must apply returned geometry | Complete a CSP-safe renderer/host placement integration and feedback-loop tests in a later slice | Open |
| R2 | Low | Host zone coordinates can be stale or in a different coordinate space | Host owns coordinate conversion and freshness; include these rules in its adapter contract | Open |
| R3 | Low | No independent security review or dependency advisory scan was performed | Retain as M10 release gates | Open |

- Known gaps / checks not run: Mounted controller integration, real container/fullscreen coordinate matrix, RTL/device/zoom tests, manual accessibility review, security/dependency audit.
- Required approvals: None for this reversible calculator; spatial coordination remains explicitly opt-in.
- Rollback or recovery: Revert the focused M6 spatial commit; call `setEnabled(false)` to clear all registered zones.
- Release readiness: `NOT READY` — M6 and M7–M11 remain incomplete.

## 6. Completion record

- Audit created before the task commit and included in that commit.
- Final commit hash: `PENDING`
- External actions: `NOT AUTHORIZED`.

**Limitations:** This is a self-audit, not an independent security review, accessibility certification, or release approval.
