# Development Audit: M4 adaptive composition and placement

> Fill in with observed evidence. Never mark an unexecuted check as PASS.

| Field | Value |
|---|---|
| Audit ID | `2026-10-16T21-32-21Z-m4-adaptive-composition-and-placement` |
| Created (UTC) | `2026-10-16T21:32:21.315Z` |
| Task / issue | M4 adaptive composition and placement |
| Auditor | Copilot coding agent; self-audit |
| Branch / baseline commit | `master` / `9c2132c` |
| Final commit | Report in task completion; do not edit this audit after committing |
| Scope reviewed | M4 placement schema, renderer host positioning, overflow composition/controls, resize lifecycle, reduced motion, and browser fixtures |
| Outcome | `PASS WITH RISKS` |

## 1. Request and outcome

- Requested behavior: implement approved adaptive placement and responsive composition while keeping viewport placement as the default and respecting reduced motion.
- Implemented behavior: added draft viewport/container/fullscreen placement values with a fixed bottom anchor; container/fullscreen modes position inside the host-selected mount target. Narrow action rows retain native scrolling and expose direction-aware scroll buttons/fades only when overflow exists. ResizeObserver is preferred with a cleaned resize fallback, and scroll/resize listeners are removed on rerender/disposal.
- Acceptance criteria and evidence: 320px viewport tests verify scrollable actions and edge buttons; a11y scan passes with overflow controls; browser tests verify fixed viewport default, container/fullscreen coordinate placement, and safe-area local CSS. Reduced-motion test covers static transitions and scrolling.
- Intentional exclusions: global shortcut/input arbitration, advanced gesture recognition, spring retargeting, real Fullscreen API lifecycle, RTL/zoom/device matrix, spatial coordination, and numeric performance budgets.
- Relevant decisions/contracts: Q10, Q12–Q13, Q17–Q18, Q28, Q69–Q75, Q79–Q80; `docs/design/interaction-and-visual-system.md`; `docs/contracts/sdk-and-schema.md`.

## 2. Change inventory

| Path | Change | Risk |
|---|---|---|
| `packages/sdk/src/configuration.ts` | Added draft viewport/container/fullscreen placement schema and validation | Medium |
| `packages/sdk/package.json` | Updated the draft package description | Low |
| `packages/sdk/src/toolbar.ts` | Applied placement mode; added conditional scroll controls, edge tracking, resize observation/fallback, and cleanup | Medium |
| `packages/sdk/src/toolbar.css` | Added container/fullscreen positioning, overflow fades, scroll-button styling, and reduced-motion handling | Medium |
| `packages/sdk/README.md` | Documented placement target ownership and overflow behavior | Low |
| `packages/sdk/test/configuration.test.js`, `toolbar.test.js` | Added placement defaults, invalid settings, and runtime update checks | Low |
| `tests/browser/fixtures/`, `tests/browser/server.mjs`, `tests/browser/toolbar.spec.js` | Added local positioned-container fixture and Chromium placement/overflow/axe tests | Low |
| `packages/sdk/README.md`, `docs/contracts/sdk-and-schema.md`, `docs/engineering/implementation-status.md`, `MANIFEST.json` | Documented draft placement contract and refreshed tracked contract hash | Low |
| `docs/audits/2026-10-16T21-32-21Z-m4-adaptive-composition-and-placement.md` | Recorded this M4 slice and residual gates | Low |

## 3. Verification evidence

Use `PASS`, `FAIL`, `NOT RUN` (with reason), or `N/A`. Record exact commands and results.

| Check | Result | Exact command / method | Evidence / notes |
|---|---|---|---|
| Formatting / lint | PASS | `pnpm check` | Scoped ESLint and Prettier passed. |
| Typecheck | PASS | `pnpm check` | Package, vanilla/React consumers, and portal typechecks passed. |
| Unit / integration tests | PASS | `pnpm check` | 45 Node tests passed, 0 failed. |
| Browser / accessibility | PASS WITH RISKS | `pnpm check` | 10 Chromium tests passed; axe scans include narrow overflow controls. Real RTL, zoom, and assistive technology matrix remains. |
| Build / consumer fixture | PASS | `pnpm check` | SDK package and Next.js portal production builds passed. |
| Security / dependency review | PASS | `pnpm check`; no dependency manifests changed | No new network, permission, or data-handling paths were introduced. CSP and no-external-request browser tests remained green. |
| Performance (if relevant) | NOT RUN | N/A | No approved budgets/baselines; defer profiling to M10. |
| `git diff --check` | PASS | `git diff --check` | No whitespace errors in task changes. |

## 4. Risk and quality review

- Public contract, feature-default, and compatibility impact: adds proposed `placement.mode` names to draft schema `1.0`; viewport/bottom remains the approved default and spatial coordination remains off.
- Security, privacy, permissions, and secrets: no arbitrary URL, network, permission, or host-style mutation added. Host selects the positioning container and remains responsible for its layout/fullscreen state.
- Accessibility, keyboard, and reduced motion: scroll controls have labels, follow the toolbar roving focus model, and only appear while overflow exists. Axe checks pass. Reduced-motion users get non-smooth scroll and no nonessential transition.
- Disabled-feature work/resource/network behavior: resize observer/listener is active only for a mounted toolbar and is disconnected on re-render/disposal; no optional feature work or external request added.
- Error handling, cleanup, and recovery: updates remain validated and atomic; mode updates rerender in place; cleanup disconnects observers and fallback listeners.
- Dependency, performance, and regression exposure: no package dependencies or runtime baseline changes; performance has no measured budget.

## 5. Findings and residual risks

| ID | Severity | Finding and evidence | Mitigation / owner | Status |
|---|---|---|---|---|
| M4-R1 | Medium | Container/fullscreen modes rely on the host supplying a positioned mount target; real Fullscreen API transitions were not exercised. | Keep the requirement explicit; add native fullscreen lifecycle tests before claiming full support. | Open |
| M4-R2 | Medium | Input arbitration, advanced gestures, velocity-aware motion and broad RTL/zoom/device scenarios remain unimplemented. | Continue remaining M4 work and M10 compatibility validation. | Open |
| M4-R3 | Low | `ResizeObserver` browser/device behavior and measured idle overhead are not benchmarked. | Profile and validate the fallback on the supported browser matrix in M10. | Open |

- Known gaps / checks not run: no actual fullscreen enter/exit exercise, shortcut arbitration, gesture tests, broad RTL/zoom/device matrix, manual screen-reader pass, or performance baseline.
- Required approvals: placement names remain draft; no feature defaults changed; release actions remain unauthorized.
- Rollback or recovery: revert the M4 slice to the M3 commit; placement defaults remain viewport/bottom and no persisted state is involved.
- Release readiness: `NOT READY`

## 6. Completion record

- Audit created before the task commit and included in that commit.
- Final commit hash: `PENDING`
- External actions: `NOT AUTHORIZED` unless separately approved.

**Limitations:** An agent self-audit is not an independent security review, accessibility certification, or release approval.
