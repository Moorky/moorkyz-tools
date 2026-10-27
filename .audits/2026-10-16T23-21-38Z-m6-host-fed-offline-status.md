# Development Audit: M6 host-fed offline status

| Field | Value |
|---|---|
| Audit ID | `2026-10-16T23-21-38Z-m6-host-fed-offline-status` |
| Created (UTC) | `2026-10-16T23:21:38.378Z` |
| Task / issue | M6 host-fed offline status |
| Auditor | Self-audit by Copilot SDK agent; no independent reviewer |
| Branch / baseline commit | `master` / `8ccdd0e` |
| Final commit | Report in task completion; do not edit this audit after commit |
| Scope reviewed | Optional host-fed connectivity status display only; no offline queue |
| Outcome | `PASS WITH RISKS` |

## 1. Request and outcome

- Requested behavior: Continue the non-blocked part of M6 offline integration while respecting open validation V09, which blocks an offline queue until idempotency, authorization, and retention policy are settled.
- Implemented behavior: Added `updateConnectivityStatus()` behind the existing `features.offline` flag (still false by default). When enabled, a host-supplied `unknown`/`online`/`offline` status is shown as a static label. No browser connectivity API is read.
- Acceptance criteria and evidence: `pnpm check` passes 109 Node tests and 17 Chromium tests. Tests cover disabled no-work, host-provided state, no automatic collapse restore, runtime disable cleanup, visible status, no external requests, and axe.
- Intentional exclusions: No offline queue, command retry, service worker, cache, connectivity listener, polling, storage, or automatic replay.
- Relevant decisions/contracts: Q20, Q28, Q53, Q64, Q76; open validation V09; offline status draft in `docs/contracts/sdk-and-schema.md`; offline integration constraints in `docs/architecture/features-and-resources.md`.

## 2. Change inventory

| Path | Change | Risk |
|---|---|---|
| `MANIFEST.json` | Refreshed the tracked SDK contract digest | Low |
| `docs/audits/2026-10-16T23-21-38Z-m6-host-fed-offline-status.md` | Added this evidence-backed audit | Low |
| `docs/contracts/sdk-and-schema.md` | Documented host-fed status and queue exclusion | Low |
| `docs/engineering/implementation-status.md` | Recorded M6 offline status progress and remaining work | Low |
| `packages/sdk/README.md` | Added host status usage and no-polling/no-queue description | Low |
| `packages/sdk/src/index.ts` | Exported connectivity status names and type | Low |
| `packages/sdk/src/offline.ts` | Added immutable connectivity status catalog and type guard | Low |
| `packages/sdk/src/toolbar.ts` | Added feature-gated status API and static status label | Medium |
| `packages/sdk/src/toolbar.css` | Added compact theme-aware status styling | Low |
| `packages/sdk/test/toolbar.test.js` | Covered disabled handling, status changes, and sticky collapse | Low |
| `tests/browser/fixtures/fixture.js` | Added an offline-status toolbar fixture | Low |
| `tests/browser/fixtures/index.html` | Added a dedicated offline-status mount target | Low |
| `tests/browser/server.mjs` | Allowed local serving of the offline module | Low |
| `tests/browser/toolbar.spec.js` | Added browser/axe and no-external-request checks | Low |

The worktree also contained an unrelated modification to `packages/portal/public/site.webmanifest`; it was excluded from staging and left untouched.

## 3. Verification evidence

| Check | Result | Exact command / method | Evidence / notes |
|---|---|---|---|
| Formatting / lint | PASS | `pnpm check` | ESLint and Prettier checks passed |
| Typecheck | PASS | `pnpm check` | SDK, portal, vanilla, and React fixtures passed |
| Unit / integration tests | PASS | `pnpm check` | 109 Node tests passed, 0 failed |
| Browser / accessibility | PASS | `pnpm check` | 17 Chromium tests passed; axe passed with status displayed |
| Build / consumer fixture | PASS | `pnpm check` | SDK and portal builds and consumer fixture typechecks passed |
| Frozen install / package contents | PASS | `pnpm install --frozen-lockfile`; `pnpm --filter @tools.moorkyz.com/sdk pack --dry-run` | No dependencies changed; offline status module is packaged |
| Specification hashes | PASS | PowerShell SHA-256 verification of all `MANIFEST.json` entries | 16 of 16 hashes matched |
| Security / dependency review | NOT RUN | No dependency manifest or lockfile changed | Manual no-network/data review only; no independent audit claimed |
| Performance | N/A | No benchmark run | Host supplies values; no listeners, polling, or background work |
| `git diff --check` | PASS | `git diff --check` | No whitespace errors; Git emits line-ending normalization warnings only |

## 4. Risk and quality review

- Public contract, feature-default, and compatibility impact: Added a draft connectivity type and controller method only. `offline` remains false by default; queue behavior remains unimplemented.
- Security, privacy, permissions, and secrets: Status accepts only three literal values and contains no user data. The toolbar does not query `navigator.onLine`, store status, or contact a service.
- Accessibility, keyboard, and reduced motion: The status is plain text with an accessible label and no control/focus behavior. It is not a live region, avoiding unsolicited announcements.
- Disabled-feature work/resource/network behavior: When disabled, `updateConnectivityStatus()` returns before validating its argument and no status DOM is created. Disabling clears state. No network requests, listeners, observers, timers, queue, or storage are present.
- Error handling, cleanup, and recovery: Unknown values return a structured `INVALID_CONNECTIVITY_STATUS`; disabling the feature resets to unknown and removes the label. Collapse state is unchanged by status updates.
- Dependency, performance, and regression exposure: No dependencies changed. The status is one small static element; feature UI is absent by default.

## 5. Findings and residual risks

| ID | Severity | Finding and evidence | Mitigation / owner | Status |
|---|---|---|---|---|
| R1 | Low | Offline status remains a host hint and may be stale if the host does not update it | Host owns freshness and connectivity detection | Open |
| R2 | Medium | Offline queues and retries remain blocked by V09 until host idempotency/auth/storage-retention policy is defined | Do not add queue/replay behavior before that validation is resolved | Open |
| R3 | Low | A separate unrelated `site.webmanifest` modification was present and intentionally excluded | Preserve user change; no action taken | Open |

- Known gaps / checks not run: Offline queue, manual AT validation, device/RTL/zoom matrix, independent security review, dependency advisory scan.
- Required approvals: None for this reversible status-only API. Persistent data, retries, or external network handling require explicit policy and authorization.
- Rollback or recovery: Revert the focused offline-status commit. No connectivity state is persisted.
- Release readiness: `NOT READY` — V09 and M6/M7–M11 gates remain open.

## 6. Completion record

- Audit created before the task commit and included in that commit.
- Final commit hash: `PENDING`
- External actions: `NOT AUTHORIZED`.

**Limitations:** This is a self-audit, not an independent security review, accessibility certification, or release approval.
