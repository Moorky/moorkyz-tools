# Development Audit: M6 host-authorized AI adapter

| Field | Value |
|---|---|
| Audit ID | `2026-10-16T22-42-24Z-m6-host-authorized-ai-adapter` |
| Created (UTC) | `2026-10-16T22:42:24.614Z` |
| Task / issue | M6 host-authorized AI adapter |
| Auditor | Self-audit by Copilot SDK agent; no independent reviewer |
| Branch / baseline commit | `master` / `197b171` |
| Final commit | Report in task completion; do not edit this audit after commit |
| Scope reviewed | Headless host-provided AI boundary, opt-in policy, cancellation, and bounded plain-text results |
| Outcome | `PASS WITH RISKS` |

## 1. Request and outcome

- Requested behavior: Continue M6 optional capabilities while preserving the approved AI-off default, host authority, and no-default-network policy.
- Implemented behavior: Added `createHostAuthorizedAIAdapter()` with required provider, feature-gate callback, per-request authorization callback, and caller-supplied `AbortSignal`. It sends only an explicit prompt to the host provider and accepts a bounded non-empty text response.
- Acceptance criteria and evidence: `pnpm check` passes 86 Node tests and 15 Chromium tests. Seven focused AI tests cover enabled/authorized provider invocation, disabled and denied paths, invalid prompt, cancellation, response type/size, exact limits, and provider/policy failures.
- Intentional exclusions: No built-in model/service, endpoint, search-context transfer, UI, provider selection, retry, cache, prompt history, usage logging, or telemetry.
- Relevant decisions/contracts: Q20, Q25, Q28, Q53, Q60, Q64, Q69, Q77; M6 host-authorized AI draft in `docs/contracts/sdk-and-schema.md`; host trust and no-network rules in `docs/architecture/security-and-trust.md`.

## 2. Change inventory

| Path | Change | Risk |
|---|---|---|
| `MANIFEST.json` | Refreshed the tracked SDK contract digest | Low |
| `docs/audits/2026-10-16T22-42-24Z-m6-host-authorized-ai-adapter.md` | Added this evidence-backed audit | Low |
| `docs/contracts/sdk-and-schema.md` | Documented the AI gate, authorization, bounds, and exclusions | Low |
| `docs/engineering/implementation-status.md` | Recorded current M6 progress and remaining features | Low |
| `packages/sdk/README.md` | Added explicit host-provider integration guidance | Low |
| `packages/sdk/src/ai.ts` | Added provider boundary, validation, cancellation, and structured diagnostics | Medium |
| `packages/sdk/src/index.ts` | Exported the draft adapter API | Low |
| `packages/sdk/test/ai.test.js` | Added seven focused provider/policy tests | Low |
| `tests/browser/server.mjs` | Allowed serving the AI module for package-root browser imports | Low |

## 3. Verification evidence

| Check | Result | Exact command / method | Evidence / notes |
|---|---|---|---|
| Formatting / lint | PASS | `pnpm check` | ESLint and Prettier checks passed |
| Typecheck | PASS | `pnpm check` | SDK, portal, vanilla, and React fixtures passed |
| Unit / integration tests | PASS | `pnpm check` | 86 Node tests passed, 0 failed |
| Browser / accessibility | PASS | `pnpm check` | 15 Chromium tests passed; AI adapter is headless and introduces no UI |
| Build / consumer fixture | PASS | `pnpm check` | SDK and portal builds and consumer fixture typechecks passed |
| Frozen install / package contents | PASS | `pnpm install --frozen-lockfile`; `pnpm --filter @tools.moorkyz.com/sdk pack --dry-run` | No dependency changes; package includes `ai.js` and declarations |
| Specification hashes | PASS | PowerShell SHA-256 verification of all `MANIFEST.json` entries | 16 of 16 hashes matched |
| Security / dependency review | NOT RUN | No dependency manifest or lockfile changed | Manual policy/data-boundary review only; no independent audit claimed |
| Performance | N/A | No benchmark run | No timers, caches, retries, or automatic provider calls |
| `git diff --check` | PASS | `git diff --check` | No whitespace errors; Git emits line-ending normalization warnings only |

## 4. Risk and quality review

- Public contract, feature-default, and compatibility impact: Added a draft root export; no feature default changed. `features.ai` remains false. Callers must connect the required gate to current host feature policy.
- Security, privacy, permissions, and secrets: Provider and authorization are host-owned. Disabled or unauthorized requests do not invoke the provider. Prompts and responses are not stored or logged by the adapter. Output is returned as a string and is not interpreted/rendered as HTML.
- Accessibility, keyboard, and reduced motion: No UI or interaction surface was added; host UI remains responsible for accessible text presentation.
- Disabled-feature work/resource/network behavior: On explicit completion calls, the feature gate runs before authorization/provider callbacks; disabled requests return without provider execution. Creating the adapter installs no listeners/timers and performs no network work.
- Error handling, cleanup, and recovery: Invalid input/output, policy failures, provider failures, disabled state, denial, and cancellation return structured outcomes. Providers must cooperatively observe the caller signal; the adapter owns no provider resources.
- Dependency, performance, and regression exposure: No dependencies changed. Prompt and result sizes are bounded. The AI module is part of the root package, so bundle-cost optimization remains for M7/M10.

## 5. Findings and residual risks

| ID | Severity | Finding and evidence | Mitigation / owner | Status |
|---|---|---|---|---|
| R1 | Low | Host providers may transmit explicit prompts to external systems or return unsafe text for later rendering | Host owns data policy, provider selection, and text-only rendering; keep this adapter UI-free | Open |
| R2 | Low | Providers that ignore `AbortSignal` may continue side effects after cancellation | Require cooperative host providers; consider provider-specific timeouts in a later policy contract | Open |
| R3 | Low | No independent security review or dependency advisory scan was performed | Retain as M10 release gates | Open |

- Known gaps / checks not run: AI UI, user-context selection/data-redaction tooling, real provider matrix, manual accessibility tests for future UI, independent security review, dependency advisory scan.
- Required approvals: None for this reversible draft adapter. Any provider with cost, external data handling, or credentials remains under explicit host policy; publishing, deployment, and external integration need separate authorization.
- Rollback or recovery: Revert the focused M6 AI commit. The adapter stores no prompts or responses.
- Release readiness: `NOT READY` — M6 remains incomplete and M7–M11 evidence is pending.

## 6. Completion record

- Audit created before the task commit and included in that commit.
- Final commit hash: `PENDING`
- External actions: `NOT AUTHORIZED`.

**Limitations:** This is a self-audit, not an independent security review, accessibility certification, or release approval.
