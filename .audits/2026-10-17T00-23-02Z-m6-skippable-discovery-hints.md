# Development Audit: M6 skippable discovery hints

| Field | Value |
|---|---|
| Audit ID | `2026-10-17T00-23-02Z-m6-skippable-discovery-hints` |
| Created (UTC) | `2026-10-17T00:23:02.488Z` |
| Task / issue | M6 skippable discovery hints |
| Auditor | Self-audit by Copilot SDK agent; no independent reviewer |
| Branch / baseline commit | `master` / `27a623e` |
| Final commit | Report in task completion; do not edit this audit after commit |
| Scope reviewed | Optional host-fed, user-opened discovery hints |
| Outcome | `PASS WITH RISKS` |

## 1. Request and outcome

- Requested behavior: Continue M6 with progressive discovery that is skippable, fully disableable, and does not infer or track user behavior.
- Implemented behavior: Added bounded host-provided text hints behind `features.discovery` (still false by default). The Tips panel is user-opened only, never auto-opens, never restores a collapsed dock, and records no completion/usage state.
- Acceptance criteria and evidence: `pnpm check` passes 120 Node tests and 22 Chromium tests. Tests cover bounded/plain-data hint validation, accessor and duplicate rejection, disabled no-inspection/no-DOM behavior, no automatic opening/expansion, safe text rendering, keyboard activation, runtime disable, and axe.
- Intentional exclusions: No tutorial tracking, progress/completion state, hints generated from usage, persistence, notifications, external links, or actions.
- Relevant decisions/contracts: Q07, Q18, Q23, Q26, Q28, Q53, Q64; M6 discovery draft in `docs/contracts/sdk-and-schema.md`; no-learning policy in `docs/architecture/features-and-resources.md`.

## 2. Change inventory

| Path | Change | Risk |
|---|---|---|
| `MANIFEST.json` | Refreshed the tracked SDK contract digest | Low |
| `docs/audits/2026-10-17T00-23-02Z-m6-skippable-discovery-hints.md` | Added this evidence-backed audit | Low |
| `docs/contracts/sdk-and-schema.md` | Documented bounded host hints and no-auto-open/no-learning behavior | Low |
| `docs/engineering/implementation-status.md` | Recorded M6 discovery progress and remaining work | Low |
| `packages/sdk/README.md` | Added host snapshot usage and opt-in behavior | Low |
| `packages/sdk/src/discovery.ts` | Added bounded plain-data hint validation | Medium |
| `packages/sdk/src/index.ts` | Exported the draft hint validator and types | Low |
| `packages/sdk/src/toolbar.css` | Added themed Tips panel styles | Low |
| `packages/sdk/src/toolbar.ts` | Added gated Tips button/panel and clear-on-disable/dispose behavior | Medium |
| `packages/sdk/test/discovery.test.js` | Covered bounds, duplicates, accessors, and immutable snapshots | Low |
| `packages/sdk/test/toolbar.test.js` | Covered manual opening, sticky collapse, safe rendering, and disabled no-work | Low |
| `tests/browser/fixtures/fixture.js` | Added a discovery-panel test fixture | Low |
| `tests/browser/fixtures/index.html` | Added a discovery target | Low |
| `tests/browser/server.mjs` | Allowed local serving of the discovery module | Low |
| `tests/browser/toolbar.spec.js` | Added keyboard, non-intrusion, safe-rendering, and axe checks | Low |

An unrelated `packages/portal/public/site.webmanifest` change was excluded and left untouched.

## 3. Verification evidence

| Check | Result | Exact command / method | Evidence / notes |
|---|---|---|---|
| Formatting / lint | PASS | `pnpm check` | ESLint and Prettier checks passed |
| Typecheck | PASS | `pnpm check` | SDK, portal, vanilla, and React fixtures passed |
| Unit / integration tests | PASS | `pnpm check` | 120 Node tests passed, 0 failed |
| Browser / accessibility | PASS | `pnpm check` | 22 Chromium tests passed; axe passed with Tips panel open |
| Build / consumer fixture | PASS | `pnpm check` | SDK and portal builds and consumer fixture typechecks passed |
| Frozen install / package contents | PASS | `pnpm install --frozen-lockfile`; `pnpm --filter @tools.moorkyz.com/sdk pack --dry-run` | No dependencies changed; discovery module is included |
| Specification hashes | PASS | PowerShell SHA-256 verification of all `MANIFEST.json` entries | 16 of 16 hashes matched |
| Security / dependency review | NOT RUN | No dependency manifest or lockfile changed | Manual text/data-boundary review only; no independent audit claimed |
| Performance | N/A | No benchmark run | At most 10 hints; no timers, polling, or background computation |
| `git diff --check` | PASS | `git diff --check` | No whitespace errors; line-ending warnings only |

## 4. Risk and quality review

- Public contract, feature-default, and compatibility impact: Additive draft API; `features.discovery` remains false. No stable API contract is claimed.
- Security, privacy, permissions, and secrets: Hints contain only bounded IDs/title/message; unknown keys/accessors rejected; all text is inserted as text nodes. No user behavior, completion status, or hint selection is recorded or persisted.
- Accessibility, keyboard, and reduced motion: Tips button is keyboard operable; panel is a labeled region with a heading. Axe passed. No animation or unsolicited announcement is added. Manual AT validation remains outstanding.
- Disabled-feature work/resource/network behavior: When disabled, `updateDiscoveryHints()` returns before inspecting input, no Tips button/panel is created, and no hint state is retained. Disabling clears data.
- Error handling, cleanup, and recovery: Invalid snapshots return structured diagnostics and preserve previous state. Dispose clears in-memory hints and removes panel DOM.
- Dependency, performance, and regression exposure: No dependencies changed; list is bounded at 10, root export is static, and bundle-size gates remain later work.

## 5. Findings and residual risks

| ID | Severity | Finding and evidence | Mitigation / owner | Status |
|---|---|---|---|---|
| R1 | Low | Discovery is a host-fed hint panel, not a full guided-learning flow | Keep learning paths skippable and entirely explicit; implement broader studio content later | Open |
| R2 | Low | No manual screen-reader/device testing or independent security review was performed | Complete M10 accessibility/security gates | Open |

- Known gaps / checks not run: Tutorial sequencing, host contextual hint policy, manual AT/device matrix, independent security review, dependency advisory scan.
- Required approvals: None for this reversible implementation; no defaults changed.
- Rollback or recovery: Revert the focused discovery slice; disabling the feature clears hints.
- Release readiness: `NOT READY` — M6 and M7–M11 remain incomplete.

## 6. Completion record

- Audit created before the task commit and included in that commit.
- Final commit hash: `PENDING`
- External actions: `NOT AUTHORIZED`.

**Limitations:** This is a self-audit, not an independent security review, accessibility certification, or release approval.
