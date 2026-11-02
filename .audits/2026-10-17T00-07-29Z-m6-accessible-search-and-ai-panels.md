# Development Audit: M6 accessible search and AI panels

| Field | Value |
|---|---|
| Audit ID | `2026-10-17T00-07-29Z-m6-accessible-search-and-ai-panels` |
| Created (UTC) | `2026-10-17T00:07:29.655Z` |
| Task / issue | M6 accessible search and AI panels |
| Auditor | Self-audit by Copilot SDK agent; no independent reviewer |
| Branch / baseline commit | `master` / `7fbee8b` |
| Final commit | Report in task completion; do not edit this audit after commit |
| Scope reviewed | Toolbar search panel integration and opt-in AI prompt/response surface |
| Outcome | `PASS WITH RISKS` |

## 1. Request and outcome

- Requested behavior: Continue M6 after completing the merge, adding user-facing search and AI surfaces that preserve host authority and optional-feature boundaries.
- Implemented behavior: Added a user-opened Search panel (local action matching plus optional host federator) and an AI panel shown only when `features.ai` is explicitly enabled. Search provider results activate only through host callbacks. AI calls occur only after explicit prompt submission; prompt and response clear on close, disable, or dispose.
- Acceptance criteria and evidence: `pnpm check` passes 114 Node tests and 20 Chromium tests. Tests cover local/provider result selection, keyboard submit/escape, safe text rendering, AI no-call-before-submit, abort-on-close/disable, and axe for both panels.
- Intentional exclusions: No AI default provider, network endpoint, prompt context injection, persistence, query history, search ranking/fuzzy matching, provider-progress UI, or automatic context inference.
- Relevant decisions/contracts: Q05, Q07, Q20, Q25, Q28, Q31, Q53, Q57, Q60, Q64, Q69; current M6 search/AI drafts in `docs/contracts/sdk-and-schema.md`.

## 2. Change inventory

| Path | Change | Risk |
|---|---|---|
| `MANIFEST.json` | Refreshed the tracked SDK contract digest | Low |
| `docs/audits/2026-10-17T00-07-29Z-m6-accessible-search-and-ai-panels.md` | Added this evidence-backed audit | Low |
| `docs/contracts/sdk-and-schema.md` | Updated the search contract with the integrated panel and cleanup behavior | Low |
| `docs/engineering/implementation-status.md` | Updated M6 progress and test evidence | Low |
| `packages/sdk/README.md` | Documented integrated panel behavior | Low |
| `packages/sdk/src/toolbar.css` | Added responsive, themed, keyboard-focusable search and AI panel styles | Medium |
| `packages/sdk/src/toolbar.ts` | Added local search, optional federation, AI prompt flow, host callbacks, cancellation, panel mutual exclusion, and cleanup | Medium |
| `packages/sdk/test/toolbar.test.js` | Covered local/provider result selection, cancellation, explicit AI submission, safe output, and abort-on-close | Low |
| `tests/browser/toolbar.spec.js` | Updated the Light DOM action locator and added keyboard/axe search and AI coverage | Low |
| `tests/browser/fixtures/fixture.js` | Added a host-provided AI fixture | Low |
| `tests/browser/fixtures/index.html` | Added an AI test mount target | Low |

An unrelated staged CLI/browser-startup merge and a modified portal `site.webmanifest` were preserved separately; they are not part of this audit's scope.

## 3. Verification evidence

| Check | Result | Exact command / method | Evidence / notes |
|---|---|---|---|
| Formatting / lint | PASS | `pnpm check` | ESLint and Prettier checks passed |
| Typecheck | PASS | `pnpm check` | SDK, portal, vanilla, and React fixtures passed |
| Unit / integration tests | PASS | `pnpm check` | 114 Node tests passed, 0 failed |
| Browser / accessibility | PASS | `pnpm check` | 20 Chromium tests passed; axe passed with Search and AI panels open |
| Build / consumer fixture | PASS | `pnpm check` | SDK and portal builds and consumer fixture typechecks passed |
| Frozen install / package contents | PASS | `pnpm install --frozen-lockfile`; `pnpm --filter @tools.moorkyz.com/sdk pack --dry-run` | No dependency changes; package contains search/AI panel code |
| Specification hashes | PASS | PowerShell SHA-256 verification of all `MANIFEST.json` entries | 16 of 16 hashes matched |
| Security / dependency review | NOT RUN | No dependency changes | Manual result-safety and host-authority review only; no independent audit claimed |
| Performance | N/A | No benchmark run | Search runs on explicit submit; results capped at 50; prompts/results are not persisted |
| `git diff --check` | PASS | `git diff --check` | No whitespace errors; line-ending normalization warnings only |

## 4. Risk and quality review

- Public contract, feature-default, and compatibility impact: Search was already enabled by the approved config default; AI remains false by default. API names remain draft.
- Security, privacy, permissions, and secrets: Search results are rendered as text and contain no URLs; local actions dispatch only through `dispatchAction`, provider result IDs through `onSearchResult`. AI sends only the explicit prompt to the host adapter; output is rendered as text. No prompts, queries, or results are persisted.
- Accessibility, keyboard, and reduced motion: Search/AI panels are labeled regions with labeled controls, native keyboard submission, Escape close, polite status messaging, visible focus, and axe coverage. Manual assistive-technology/device certification remains outstanding.
- Disabled-feature work/resource/network behavior: Panels are absent when their flags are off. Search is called only after submit; disabling/closing aborts in-flight work and clears state. AI calls only after explicit submit and host authorization.
- Error handling, cleanup, and recovery: Stale search results are rejected by request version; provider abort signals are sent; close/disable/dispose clears prompts/results. Provider errors are reduced to safe status text and optionally reported to host callbacks.
- Dependency, performance, and regression exposure: No dependencies changed. UI rendering is bounded; root package imports remain statically bundled, so bundle-size optimization remains a later gate.

## 5. Findings and residual risks

| ID | Severity | Finding and evidence | Mitigation / owner | Status |
|---|---|---|---|---|
| R1 | Low | No manual screen-reader or real-device search/AI validation was performed | Complete M10 accessibility/device validation before stable contract approval | Open |
| R2 | Low | Provider result activation and sensitive prompt policy remain host-owned | Host must authorize result mapping and provider data handling | Open |
| R3 | Low | No independent security review or dependency advisory scan was performed | Keep as M10 release gates | Open |

- Known gaps / checks not run: AI context/prompt policy tooling, ranking/progress UI, manual AT/device matrix, independent security review, dependency advisory scan.
- Required approvals: None for this reversible draft UI. AI provider/cost/data handling remains host-authorized.
- Rollback or recovery: Revert the focused search/AI UI changes; no prompt/query state is persisted.
- Release readiness: `NOT READY` — M6 and M7–M11 remain incomplete.

## 6. Completion record

- Audit created before the task commit and included in that commit.
- Final commit hash: `PENDING`
- External actions: `NOT AUTHORIZED`.

**Limitations:** This is a self-audit, not an independent security review, accessibility certification, or release approval.
