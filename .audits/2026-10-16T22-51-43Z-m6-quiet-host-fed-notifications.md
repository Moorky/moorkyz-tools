# Development Audit: M6 quiet host-fed notifications

| Field | Value |
|---|---|
| Audit ID | `2026-10-16T22-51-43Z-m6-quiet-host-fed-notifications` |
| Created (UTC) | `2026-10-16T22:51:43.812Z` |
| Task / issue | M6 quiet host-fed notifications |
| Auditor | Self-audit by Copilot SDK agent; no independent reviewer |
| Branch / baseline commit | `master` / `e1e64b3` |
| Final commit | Report in task completion; do not edit this audit after commit |
| Scope reviewed | Optional host-fed notification snapshot validation and user-opened presentation |
| Outcome | `PASS WITH RISKS` |

## 1. Request and outcome

- Requested behavior: Continue the approved M0–M11 implementation with non-intrusive optional notifications, preserving host authority and the disabled-feature no-work invariant.
- Implemented behavior: Added bounded notification snapshots behind `features.notifications` (still false by default). The toolbar displays a count and opens a panel only on user activation. Notifications do not create toasts, auto-open, modify unread state, expand a collapsed toolbar, or persist.
- Acceptance criteria and evidence: `pnpm check` passes 91 Node tests and 16 Chromium tests. Tests cover snapshot validation, disabled no-inspection/no-DOM behavior, clear-on-disable/re-enable, safe text rendering, preserved collapse state, manual keyboard activation, and axe.
- Intentional exclusions: No notification delivery service, polling, push subscription, auto-dismiss, read-state mutation, persistence, notification actions, or user-context inference.
- Relevant decisions/contracts: Q18, Q20, Q26, Q28, Q53, Q64, Q75; M6 quiet notification presentation in `docs/contracts/sdk-and-schema.md`; feature-resource invariants in `docs/architecture/features-and-resources.md`.

## 2. Change inventory

| Path | Change | Risk |
|---|---|---|
| `MANIFEST.json` | Refreshed the tracked SDK contract digest | Low |
| `docs/audits/2026-10-16T22-51-43Z-m6-quiet-host-fed-notifications.md` | Added this evidence-backed audit | Low |
| `docs/contracts/sdk-and-schema.md` | Documented quiet notification behavior, fields, and limits | Low |
| `docs/engineering/implementation-status.md` | Recorded M6 progress and remaining work | Low |
| `packages/sdk/README.md` | Added host-snapshot integration guidance | Low |
| `packages/sdk/src/index.ts` | Exported notification types and validator | Low |
| `packages/sdk/src/notifications.ts` | Added bounded data-only validation and diagnostics | Medium |
| `packages/sdk/src/toolbar.css` | Added responsive, themed, forced-colors panel styles | Medium |
| `packages/sdk/src/toolbar.ts` | Added feature-gated count/panel, manual activation, and clear-on-disable/dispose behavior | Medium |
| `packages/sdk/test/notifications.test.js` | Covered validation, bounds, accessors, and immutable snapshots | Low |
| `packages/sdk/test/toolbar.test.js` | Covered manual presentation, safe text, feature toggle, and disabled no-work path | Low |
| `tests/browser/fixtures/fixture.js` | Added a dedicated notification-toolbar fixture | Low |
| `tests/browser/fixtures/index.html` | Added a notification mount target | Low |
| `tests/browser/server.mjs` | Allowed local serving of the notification module | Low |
| `tests/browser/toolbar.spec.js` | Added keyboard, non-intrusive behavior, safe-rendering, and axe coverage | Low |

## 3. Verification evidence

| Check | Result | Exact command / method | Evidence / notes |
|---|---|---|---|
| Formatting / lint | PASS | `pnpm check` | ESLint and Prettier checks passed |
| Typecheck | PASS | `pnpm check` | SDK, portal, vanilla, and React fixtures passed |
| Unit / integration tests | PASS | `pnpm check` | 91 Node tests passed, 0 failed |
| Browser / accessibility | PASS | `pnpm check` | 16 Chromium tests passed; axe passed with notification panel open |
| Build / consumer fixture | PASS | `pnpm check` | SDK and portal builds and consumer fixture typechecks passed |
| Frozen install / package contents | PASS | `pnpm install --frozen-lockfile`; `pnpm --filter @tools.moorkyz.com/sdk pack --dry-run` | No dependencies added; notification module is included in the package |
| Specification hashes | PASS | PowerShell SHA-256 verification of all `MANIFEST.json` entries | 16 of 16 hashes matched |
| Security / dependency review | NOT RUN | No dependency manifest or lockfile changed | Manual data-boundary review only; no independent audit claimed |
| Performance | N/A | No benchmark run | Snapshot capped at 50; no polling/listeners/timers/network work added |
| `git diff --check` | PASS | `git diff --check` | No whitespace errors; Git emits line-ending normalization warnings only |

## 4. Risk and quality review

- Public contract, feature-default, and compatibility impact: Added draft `updateNotifications()` API and diagnostic codes; no default changed. `notifications` remains false. The public SDK contract remains unapproved.
- Security, privacy, permissions, and secrets: Notification records contain only bounded IDs, text, severity, and host-owned unread state. Accessors/unknown keys/duplicate IDs are rejected; text is rendered as text nodes. Data is not stored or sent over the network.
- Accessibility, keyboard, and reduced motion: The panel is a labeled region with a heading; its native Notifications button is keyboard-activatable. Updates do not use a live region or toast while closed. Axe passed with the panel open. Manual assistive-technology validation remains outstanding.
- Disabled-feature work/resource/network behavior: When disabled, update returns before reading the snapshot; no notification feature DOM is created and no state is retained. Disabling clears entries and panel state. No notification-specific listeners, observers, requests, or timers were added.
- Error handling, cleanup, and recovery: Invalid snapshots return structured diagnostics and leave existing data unchanged. Disabling and disposal clear retained notification data. Host owns freshness, unread/read state, and any external delivery mechanisms.
- Dependency, performance, and regression exposure: No dependencies changed. The notification module is statically included from the root barrel; bundle optimization remains a later M7/M10 gate.

## 5. Findings and residual risks

| ID | Severity | Finding and evidence | Mitigation / owner | Status |
|---|---|---|---|---|
| R1 | Low | There is no read/dismiss control or delivery integration; state must be supplied by the host | Host remains authoritative; add interaction only with an approved behavior contract | Open |
| R2 | Low | No manual screen-reader/device validation or independent security review was performed | Complete M10 accessibility, device, security, and supply-chain gates | Open |

- Known gaps / checks not run: Delivery integration, read/dismiss interaction, notification actions, manual AT/device review, independent security review, dependency advisory scan.
- Required approvals: None for this reversible implementation; feature default and data/permission policy are unchanged.
- Rollback or recovery: Revert the focused M6 notification commit; no notifications are persisted.
- Release readiness: `NOT READY` — M6 and M7–M11 remain incomplete.

## 6. Completion record

- Audit created before the task commit and included in that commit.
- Final commit hash: `PENDING`
- External actions: `NOT AUTHORIZED`.

**Limitations:** This is a self-audit, not an independent security review, accessibility certification, or release approval.
