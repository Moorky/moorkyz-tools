# Development Audit: M6 host-coordinated lifecycle adapter

| Field | Value |
|---|---|
| Audit ID | `2026-10-16T22-58-55Z-m6-host-coordinated-lifecycle-adapter` |
| Created (UTC) | `2026-10-16T22:58:55.226Z` |
| Task / issue | M6 host-coordinated lifecycle adapter |
| Auditor | Self-audit by Copilot SDK agent; no independent reviewer |
| Branch / baseline commit | `master` / `8a7670f` |
| Final commit | Report in task completion; do not edit this audit after commit |
| Scope reviewed | Explicit document visibility/BFCache lifecycle coordinator and cleanup |
| Outcome | `PASS WITH RISKS` |

## 1. Request and outcome

- Requested behavior: Continue M6 resilience work in line with host-coordinated visibility and BFCache decisions.
- Implemented behavior: Added an explicitly created lifecycle coordinator that observes `visibilitychange`, `pagehide`, and `pageshow`, reports active/suspended transitions once, exposes state/reconciliation, and removes listeners on disposal. It does not install automatically from `createToolbar()`.
- Acceptance criteria and evidence: `pnpm check` passes 100 Node tests and 16 Chromium tests. Focused lifecycle tests cover repeated event idempotence, hidden-state reconciliation across pageshow, and listener removal after disposal.
- Intentional exclusions: No automatic lifecycle policy for toolbar features, host command cancellation, background task suspension, timers, visibility polling, or global singleton.
- Relevant decisions/contracts: Q18, Q28, Q31, Q75, Q77; lifecycle draft in `docs/contracts/sdk-and-schema.md`; lifecycle resource policy in `docs/architecture/features-and-resources.md`.

## 2. Change inventory

| Path | Change | Risk |
|---|---|---|
| `MANIFEST.json` | Refreshed the tracked SDK contract digest | Low |
| `docs/audits/2026-10-16T22-58-55Z-m6-host-coordinated-lifecycle-adapter.md` | Added this evidence-backed audit | Low |
| `docs/contracts/sdk-and-schema.md` | Documented lifecycle states, events, and host responsibilities | Low |
| `docs/engineering/implementation-status.md` | Recorded lifecycle adapter progress and remaining integration | Low |
| `packages/sdk/README.md` | Added explicit lifecycle coordinator usage guidance | Low |
| `packages/sdk/src/index.ts` | Exported the draft lifecycle API | Low |
| `packages/sdk/src/lifecycle.ts` | Added per-instance visibility/BFCache transitions and disposal | Medium |
| `packages/sdk/test/lifecycle.test.js` | Added state-transition and cleanup tests | Low |
| `tests/browser/server.mjs` | Allowed local serving of the lifecycle module for package-root imports | Low |

## 3. Verification evidence

| Check | Result | Exact command / method | Evidence / notes |
|---|---|---|---|
| Formatting / lint | PASS | `pnpm check` | ESLint and Prettier checks passed |
| Typecheck | PASS | `pnpm check` | SDK, portal, vanilla, and React fixtures passed |
| Unit / integration tests | PASS | `pnpm check` | 100 Node tests passed, 0 failed |
| Browser / accessibility | PASS | `pnpm check` | 16 Chromium tests passed; lifecycle coordinator adds no UI |
| Build / consumer fixture | PASS | `pnpm check` | SDK and portal builds and consumer fixture typechecks passed |
| Frozen install / package contents | PASS | `pnpm install --frozen-lockfile`; `pnpm --filter @tools.moorkyz.com/sdk pack --dry-run` | No dependencies changed; lifecycle module is packaged |
| Specification hashes | PASS | PowerShell SHA-256 verification of all `MANIFEST.json` entries | 16 of 16 hashes matched |
| Security / dependency review | NOT RUN | No dependency manifest or lockfile changed | Manual event/resource review only; no independent audit claimed |
| Performance | N/A | No benchmark run | No polling or timers; only three owned event listeners per explicit instance |
| `git diff --check` | PASS | `git diff --check` | No whitespace errors; Git emits line-ending normalization warnings only |

## 4. Risk and quality review

- Public contract, feature-default, and compatibility impact: Additive draft utility only; no defaults or existing controller behavior changed. The coordinator activates only when explicitly constructed.
- Security, privacy, permissions, and secrets: No user data is read or persisted. The helper observes only document visibility and window page lifecycle events; callbacks and host policy remain caller-owned.
- Accessibility, keyboard, and reduced motion: No UI or motion surface was added; no user-facing accessibility behavior changed.
- Disabled-feature work/resource/network behavior: There is no automatic construction. Once explicitly constructed, it installs one document and up to two window listeners; disposal removes them. No timers, network, polling, page scanning, or observers are used.
- Error handling, cleanup, and recovery: State changes precede callback invocation, so callback exceptions propagate and state remains observable; handlers should be idempotent. Listener cleanup is idempotent. Initial hidden state is exposed without emitting a callback.
- Dependency, performance, and regression exposure: No dependencies changed. This helper is statically included in the root package; bundle/lazy-subpath budgets remain a later gate.

## 5. Findings and residual risks

| ID | Severity | Finding and evidence | Mitigation / owner | Status |
|---|---|---|---|---|
| R1 | Low | Lifecycle callbacks that throw propagate through browser event dispatch and are not retried automatically | Host handlers should be idempotent, bounded, and report their own failures | Open |
| R2 | Low | The coordinator signals the host but is not automatically wired to suspend toolbar commands or other optional modules | Integrate feature-specific suspension in later M6 slices | Open |
| R3 | Low | No independent security review or dependency advisory scan was performed | Retain as M10 release gates | Open |

- Known gaps / checks not run: Direct integration with toolbar task/search/AI operations; broader BFCache/browser matrix; manual AT review; independent security review; dependency advisory scan.
- Required approvals: None for this reversible additive utility; no security policy or feature default changed.
- Rollback or recovery: Revert the focused lifecycle commit; callers can invoke `dispose()` to remove owned listeners.
- Release readiness: `NOT READY` — M6 and M7–M11 remain incomplete.

## 6. Completion record

- Audit created before the task commit and included in that commit.
- Final commit hash: `PENDING`
- External actions: `NOT AUTHORIZED`.

**Limitations:** This is a self-audit, not an independent security review, accessibility certification, or release approval.
