# Development Audit: M6 opt-in local diagnostics recorder

| Field | Value |
|---|---|
| Audit ID | `2026-10-16T22-55-23Z-m6-opt-in-local-diagnostics-recorder` |
| Created (UTC) | `2026-10-16T22:55:23.656Z` |
| Task / issue | M6 opt-in local diagnostics recorder |
| Auditor | Self-audit by Copilot SDK agent; no independent reviewer |
| Branch / baseline commit | `master` / `b7aceee` |
| Final commit | Report in task completion; do not edit this audit after commit |
| Scope reviewed | Disabled-by-default, redacted, bounded in-memory diagnostics buffer |
| Outcome | `PASS WITH RISKS` |

## 1. Request and outcome

- Requested behavior: Continue M6 optional capabilities with developer-owned local diagnostics, respecting the approved disabled-by-default and no-telemetry policy.
- Implemented behavior: Added a headless recorder that starts disabled, requires a host redaction function, accepts only narrow diagnostic code/severity/message fields, bounds the in-memory ring buffer, and clears data when disabled or disposed.
- Acceptance criteria and evidence: `pnpm check` passes 97 Node tests and 16 Chromium tests. Six focused tests cover disabled no-inspection, explicit redaction, capacity/order, invalid/accessor data, redactor failures, and clear-on-disable/dispose.
- Intentional exclusions: No diagnostics UI/Inspector, export, timestamps, persistence, remote reporting, telemetry, automatic capture, or cause/detail collection.
- Relevant decisions/contracts: Q18, Q28, Q29, Q52, Q53, Q64, Q69; M6 diagnostics draft in `docs/contracts/sdk-and-schema.md`; no-telemetry and disabled-feature invariants in `docs/architecture/features-and-resources.md`.

## 2. Change inventory

| Path | Change | Risk |
|---|---|---|
| `MANIFEST.json` | Refreshed the tracked SDK contract digest | Low |
| `docs/audits/2026-10-16T22-55-23Z-m6-opt-in-local-diagnostics-recorder.md` | Added this evidence-backed audit | Low |
| `docs/contracts/sdk-and-schema.md` | Documented local-only operation, redaction, limits, and host responsibilities | Low |
| `docs/engineering/implementation-status.md` | Recorded M6 progress and remaining work | Low |
| `packages/sdk/README.md` | Added host-controlled enablement and redactor usage | Low |
| `packages/sdk/src/diagnostics.ts` | Added the disabled recorder, input validation, redaction, bounded storage, and cleanup | Medium |
| `packages/sdk/src/index.ts` | Exported the draft recorder API | Low |
| `packages/sdk/test/diagnostics.test.js` | Covered disabled, redaction, capacity, validation, and cleanup behavior | Low |
| `tests/browser/server.mjs` | Allowed local serving of the diagnostics module for package-root imports | Low |

## 3. Verification evidence

| Check | Result | Exact command / method | Evidence / notes |
|---|---|---|---|
| Formatting / lint | PASS | `pnpm check` | ESLint and Prettier checks passed |
| Typecheck | PASS | `pnpm check` | SDK, portal, vanilla, and React fixtures passed |
| Unit / integration tests | PASS | `pnpm check` | 97 Node tests passed, 0 failed |
| Browser / accessibility | PASS | `pnpm check` | 16 Chromium tests passed; diagnostics recorder has no UI |
| Build / consumer fixture | PASS | `pnpm check` | SDK and portal builds and consumer fixture typechecks passed |
| Frozen install / package contents | PASS | `pnpm install --frozen-lockfile`; `pnpm --filter @tools.moorkyz.com/sdk pack --dry-run` | No dependencies changed; diagnostics module is packaged |
| Specification hashes | PASS | PowerShell SHA-256 verification of all `MANIFEST.json` entries | 16 of 16 hashes matched |
| Security / dependency review | NOT RUN | No dependency manifest or lockfile changed | Manual data-boundary review only; no independent audit claimed |
| Performance | N/A | No benchmark run | No listeners/timers/network; capacity is capped at 100 |
| `git diff --check` | PASS | `git diff --check` | No whitespace errors; Git emits line-ending normalization warnings only |

## 4. Risk and quality review

- Public contract, feature-default, and compatibility impact: Added draft recorder API only; no config default changed. `diagnostics` remains false. Host must explicitly synchronize recorder enablement to current configuration.
- Security, privacy, permissions, and secrets: Redaction is mandatory; input fields are restricted, causes and arbitrary details are omitted, and data stays in a bounded in-memory buffer. Redaction quality remains a host responsibility.
- Accessibility, keyboard, and reduced motion: No UI surface was added; future Inspector presentation remains responsible for accessibility.
- Disabled-feature work/resource/network behavior: Recorder starts disabled. `record()` rejects before inspecting the input; no record is retained. `setEnabled(false)` and `dispose()` clear the buffer. No listeners, timers, observers, network, or persistence are created.
- Error handling, cleanup, and recovery: Malformed input and redaction failures return structured errors and are not stored. The host can clear, disable, or dispose the recorder.
- Dependency, performance, and regression exposure: No dependencies changed. Capacity is at most 100 entries, stored messages at most 500 characters, and there is no persistent or remote sink. Module is statically exported from the root package; bundle optimization is a later gate.

## 5. Findings and residual risks

| ID | Severity | Finding and evidence | Mitigation / owner | Status |
|---|---|---|---|---|
| R1 | Low | A host redactor may fail to remove sensitive text despite satisfying the interface | Host owns redaction policy and should test it with realistic sensitive fixtures | Open |
| R2 | Low | No built-in dev-only diagnostics panel or environment enforcement is included | Host must expose snapshots only in its intended developer experience; implement Inspector in a later portal milestone | Open |
| R3 | Low | No independent security review or dependency advisory scan was performed | Keep these as M10 release gates | Open |

- Known gaps / checks not run: Built-in Inspector UI, manual accessibility review of future UI, security review, dependency advisory scan, bundle-size baseline.
- Required approvals: None for this reversible local-only API. Any future persistence, telemetry, or remote reporting requires separate approval.
- Rollback or recovery: Revert the focused M6 diagnostics commit; call `dispose()` or `setEnabled(false)` to clear active in-memory records.
- Release readiness: `NOT READY` — M6 and M7–M11 remain incomplete.

## 6. Completion record

- Audit created before the task commit and included in that commit.
- Final commit hash: `PENDING`
- External actions: `NOT AUTHORIZED`.

**Limitations:** This is a self-audit, not an independent security review, accessibility certification, or release approval.
