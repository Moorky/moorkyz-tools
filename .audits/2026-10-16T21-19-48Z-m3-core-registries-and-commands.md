# Development Audit: M3 core registries and commands

> Fill in with observed evidence. Never mark an unexecuted check as PASS.

| Field | Value |
|---|---|
| Audit ID | `2026-10-16T21-19-48Z-m3-core-registries-and-commands` |
| Created (UTC) | `2026-10-16T21:19:48.378Z` |
| Task / issue | M3 core registries and commands |
| Auditor | Copilot coding agent; self-audit |
| Branch / baseline commit | `master` / `8f3c451` |
| Final commit | Report in task completion; do not edit this audit after committing |
| Scope reviewed | M3 capability registry, context resolver, command executor, surface stack, typed consumer fixture, and contract documentation |
| Outcome | `PASS WITH RISKS` |

## 1. Request and outcome

- Requested behavior: continue the complete project in milestone order with host-authorized capability/context/command/surface primitives.
- Implemented behavior: added atomic namespaced capability transactions with host-gated cross-owner overrides; hierarchical versioned context scopes with cycle and unsafe-record checks; typed command handles with five concurrency modes, required runtime input validators, permission checks/revocation, cancellation and timeout; and a bounded nested surface stack with dismissal rules and focus-return targets.
- Acceptance criteria and evidence: 23 focused M3 unit cases are included in the 44-test Node suite; nine Chromium/axe checks remain green; package, portal, and vanilla/React consumer typechecks pass. Permissioned commands fail closed without host authorization, retries are absent, and no M3 dependency was added.
- Intentional exclusions: dedicated router adapters, DOM focus restoration, extension adapters, stable API approval, optional feature implementations, and M4+ work.
- Relevant decisions/contracts: Q20, Q31–Q35, Q57–Q64, Q77–Q80; `docs/contracts/sdk-and-schema.md`; `docs/architecture/product-and-system.md`; `docs/architecture/security-and-trust.md`.

## 2. Change inventory

| Path | Change | Risk |
|---|---|---|
| `packages/sdk/src/capabilities.ts` | Added transactional, owner-scoped capability registry | Medium |
| `packages/sdk/src/context.ts` | Added hierarchical scoped context resolution and revision tracking | Medium |
| `packages/sdk/src/commands.ts` | Added typed host-authorized command handles, validation, concurrency, timeout, cancellation and revocation | High |
| `packages/sdk/src/surfaces.ts` | Added a bounded nested surface stack with dismissal policy and focus-return targets | Medium |
| `packages/sdk/src/index.ts` | Exported M3 draft services from the package root | Medium |
| `packages/sdk/test/capabilities.test.js`, `context.test.js`, `commands.test.js`, `surfaces.test.js` | Added transaction, security, race/policy, hierarchy, and dismissal tests | Medium |
| `examples/vanilla-consumer/src/index.ts` | Added a compile-time example of M3 APIs | Low |
| `docs/contracts/sdk-and-schema.md`, `docs/engineering/implementation-status.md`, `MANIFEST.json` | Documented draft semantics, remaining M3 work, and refreshed the tracked contract hash | Low |
| `docs/audits/2026-10-16T21-19-48Z-m3-core-registries-and-commands.md` | Recorded this M3 validation slice | Low |

## 3. Verification evidence

Use `PASS`, `FAIL`, `NOT RUN` (with reason), or `N/A`. Record exact commands and results.

| Check | Result | Exact command / method | Evidence / notes |
|---|---|---|---|
| Formatting / lint | PASS | `pnpm check` | ESLint and scoped Prettier checks passed. |
| Typecheck | PASS | `pnpm check` | Package, portal, vanilla fixture (including M3 types), and React fixture compiled. |
| Unit / integration tests | PASS | `pnpm check` | 44 Node tests passed, 0 failed. |
| Browser / accessibility | PASS | `pnpm check` | 9 Chromium/axe checks passed; M3 services are non-visual and have focused Node tests. |
| Build / consumer fixture | PASS | `pnpm check` | SDK package and Next.js portal production builds passed; consumer fixtures typechecked. |
| Security / dependency review | PASS WITH RISKS | `pnpm check`; `pnpm install --frozen-lockfile` | No new dependencies, network activity, or permissions added. Permission checks are host-provided and fail closed; adversarial input and revocation cases are covered. This is not a formal security review. |
| Performance (if relevant) | N/A | N/A | No runtime budgets approved; command timers are created only for commands with a timeout and active execution. |
| `git diff --check` | PASS | `git diff --check` | No whitespace errors in task changes. |

## 4. Risk and quality review

- Public contract, feature-default, and compatibility impact: introduces draft M3 exports without changing feature defaults; names and semantics remain subject to contract approval.
- Security, privacy, permissions, and secrets: cross-owner capability replacement requires the host authorization callback; permissioned commands are denied without a host policy and reauthorized for queued execution. No credentials, telemetry, or remote operations introduced.
- Accessibility, keyboard, and reduced motion: surface stack enforces top-only closure and returns the focus restoration target; actual DOM focus behavior remains future renderer work.
- Disabled-feature work/resource/network behavior: M3 services acquire no work until registered/invoked; timers are allocated only for active commands with explicit timeouts.
- Error handling, cleanup, and recovery: transactions abort atomically; command failures, cancellation, timeout and denial return structured errors; executor/handle disposal aborts active work.
- Dependency, performance, and regression exposure: no added dependencies; cooperative cancellation requires host handlers to honor `AbortSignal`.

## 5. Findings and residual risks

| ID | Severity | Finding and evidence | Mitigation / owner | Status |
|---|---|---|---|---|
| M3-R1 | Medium | New M3 names and semantics remain unapproved public contract drafts. | Maintain draft status; approve before stabilization. | Open |
| M3-R2 | Medium | Context snapshots freeze their outer value; nested host-owned objects are shared references. | Hosts should treat nested context values as immutable; consider a stronger data contract before release. | Open |
| M3-R3 | Medium | A handler that ignores `AbortSignal` can continue its own side effects after the executor returns a cancellation/timeout result. | Document cooperative cancellation, test host adapters, and never use unsafe automatic retries. | Open |
| M3-R4 | Low | Dedicated navigation adapters and DOM-managed focus restoration are not implemented. | Complete remaining M3/M7 integration work. | Open |

- Known gaps / checks not run: no standalone security audit, coverage-threshold run, performance baseline, framework-router integration, or manual assistive-technology test. Surface focus target is returned but not focused by the core.
- Required approvals: public API stabilization and any release operations remain outside this task's authority.
- Rollback or recovery: revert the M3 slice to the M2 commit; no persisted product state or migrations.
- Release readiness: `NOT READY`

## 6. Completion record

- Audit created before the task commit and included in that commit.
- Final commit hash: `PENDING`
- External actions: `NOT AUTHORIZED` unless separately approved.

**Limitations:** An agent self-audit is not an independent security review, accessibility certification, or release approval.
