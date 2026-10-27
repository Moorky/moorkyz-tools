# Development Audit: M6 mounted opt-in spatial placement

> Fill in with observed evidence. Never mark an unexecuted check as PASS.

| Field                    | Value                                                                                                |
| ------------------------ | ---------------------------------------------------------------------------------------------------- |
| Audit ID                 | `2026-10-17T00-43-18Z-m6-mounted-opt-in-spatial-placement`                                           |
| Created (UTC)            | `2026-10-17T00:43:18.079Z`                                                                           |
| Task / issue             | M6 mounted opt-in spatial placement                                                                  |
| Auditor                  | Copilot self-audit; direct source, test, and diff inspection                                         |
| Branch / baseline commit | `master` / `8a3dcc4`                                                                                 |
| Final commit             | Report in task completion; do not edit this audit after committing                                   |
| Scope reviewed           | Mounted spatial integration, host contract/docs, unit/browser tests, browser test-port configuration |
| Outcome                  | `PASS WITH RISKS`                                                                                    |

## 1. Request and outcome

- Requested behavior: Continue M6 implementation, integrating opt-in spatial coordination with the mounted toolbar while preserving default-off and host-authority rules.
- Implemented behavior: Added atomic `updateSpatialZones()`, host-supplied viewport/safe-area data, six CSS anchor placements, spatial error callbacks/warnings, conflict state without forced movement, and resize-listener cleanup on empty zones, feature disable, and disposal.
- Acceptance criteria and evidence: Disabled mode returns before inspecting zone input; host zones are bounded/validated; both viewport and container placements are browser-tested; no-safe placement preserves the current anchor; full suite passes (124 Node, 24 Chromium).
- Intentional exclusions: No page scan, host padding mutation, automatic collapse/restore, offline queue/retry, or feature-default changes. Host owns coordinate-frame freshness and safe-area correspondence.
- Relevant decisions/contracts: `docs/architecture/features-and-resources.md`; `docs/contracts/sdk-and-schema.md` (draft API); `docs/engineering/open-validations.md` V09 remains a blocker for offline queueing.

## 2. Change inventory

| Path                                                                                | Change                                                                                        | Risk                                             |
| ----------------------------------------------------------------------------------- | --------------------------------------------------------------------------------------------- | ------------------------------------------------ |
| `packages/sdk/src/toolbar.ts`                                                   | Host viewport contract, atomic zone updates, placement resolution, feature gating and cleanup | Medium: spatial behavior is a draft optional API |
| `packages/sdk/src/spatial.ts`                                                   | Spatial errors can carry host viewport callback causes                                        | Low                                              |
| `packages/sdk/src/toolbar.css`                                                  | Top/start/end anchor styles in Shadow and Light DOM                                           | Low                                              |
| `packages/sdk/test/toolbar.test.js`                                             | Disabled-input, viewport requirement, collision, no-safe, disable, and disposal tests         | Low                                              |
| `tests/browser/toolbar.spec.js`                                                     | Viewport and container anchor behavior, resize cleanup, axe check                             | Low                                              |
| `tests/browser/fixtures/fixture.js`, `tests/browser/fixtures/index.html`            | Spatial browser fixture                                                                       | Low                                              |
| `playwright.config.mjs`, `tests/browser/server.mjs`, `tests/browser/portal.spec.js` | Environment-configurable fixture and portal ports to avoid unrelated local server collisions  | Low                                              |
| `packages/sdk/README.md`, `docs/contracts/sdk-and-schema.md`                    | Document controller contract, coordinate frames, safe areas, and host ownership               | Low                                              |
| `docs/engineering/implementation-status.md`, `MANIFEST.json`                        | Record M6 evidence and refresh the edited contract hash                                       | Low                                              |
| `docs/audits/2026-10-17T00-43-18Z-m6-mounted-opt-in-spatial-placement.md`           | This evidence audit                                                                           | Low                                              |

## 3. Verification evidence

Use `PASS`, `FAIL`, `NOT RUN` (with reason), or `N/A`. Record exact commands and results.

| Check                        | Result          | Exact command / method                                                                                                   | Evidence / notes                                                                                                                                                      |
| ---------------------------- | --------------- | ------------------------------------------------------------------------------------------------------------------------ | --------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| Formatting / lint            | PASS            | `$env:TOOLS_MOORKYZ_COM_FIXTURE_PORT='48731'; $env:TOOLS_MOORKYZ_COM_PORTAL_PORT='48732'; pnpm check`                        | `pnpm lint` and `pnpm format:check` passed.                                                                                                                           |
| Typecheck                    | PASS            | Same `pnpm check` invocation                                                                                             | SDK, vanilla, React, and portal typechecks passed.                                                                                                                |
| Unit / integration tests     | PASS            | Same `pnpm check` invocation                                                                                             | 124 Node tests passed; 0 failed.                                                                                                                                      |
| Browser / accessibility      | PASS            | Same `pnpm check` invocation; targeted: `pnpm exec playwright test tests/browser/toolbar.spec.js -g "spatial placement"` | Full browser suite: 24 passed. Targeted viewport/container spatial tests: 2 passed; axe assertions passed.                                                            |
| Build / consumer fixture     | PASS            | Same `pnpm check` invocation; `pnpm --filter @tools.moorkyz.com/sdk pack --dry-run`                                            | Workspace build and consumer typechecks passed; dry-run listed expected `dist`, stylesheet, README, and package metadata.                                             |
| Security / dependency review | PASS WITH RISKS | `pnpm install --frozen-lockfile`; source/diff inspection                                                                 | Frozen install passed; no dependency was added. No dedicated dependency vulnerability audit was run. Spatial inputs are validated; no network or new permission path. |
| Performance (if relevant)    | N/A             | Not measured                                                                                                             | No new polling/timer; resize listener is conditional on enabled spatial coordination and non-empty zones.                                                             |
| `git diff --check`           | PASS            | `git diff --check`                                                                                                       | No whitespace errors.                                                                                                                                                 |
| Manifest integrity           | PASS            | PowerShell SHA-256 comparison over `MANIFEST.json`                                                                       | All 16 listed document hashes matched.                                                                                                                                |

## 4. Risk and quality review

- Public contract, feature-default, and compatibility impact: Draft additive host/controller API (`getSpatialViewport`, `onSpatialError`, `updateSpatialZones`, `spatialWarning`); `spatialCoordination` remains false by default. No stable-contract or default change.
- Security, privacy, permissions, and secrets: Accepts only explicit host zone data and a host viewport callback; validates bounded plain data through the spatial coordinator and rejects sparse/accessor/custom array entries. No DOM scanning, network, credentials, telemetry, or host authority expansion.
- Accessibility, keyboard, and reduced motion: Adds no interactive UI; CSS placement does not affect keyboard behavior. Full browser/axe suite passed.
- Disabled-feature work/resource/network behavior: Disabled updates reject before input inspection; no host viewport callback or spatial resize listener is used. Tests cover no callback while disabled and cleanup after disable/dispose.
- Error handling, cleanup, and recovery: Invalid zones return structured diagnostics; missing viewport is explicit; provider failure and no safe anchor are reported to `onSpatialError`; failed placement preserves the current anchor. Listeners and spatial state clear on disable/dispose.
- Dependency, performance, and regression exposure: No dependency changes. Adds one conditional window resize listener only while active zones exist; package dry-run and full check passed.

## 5. Findings and residual risks

| ID  | Severity | Finding and evidence                                                                                                                                                      | Mitigation / owner                                                          | Status           |
| --- | -------- | ------------------------------------------------------------------------------------------------------------------------------------------------------------------------- | --------------------------------------------------------------------------- | ---------------- |
| R1  | LOW      | Safe placement depends on host zones, viewport dimensions, and safe-area values using the same coordinate frame; stale/inconsistent data can produce an incorrect anchor. | Host implementation; contract docs require matching frames and safe insets. | Open, host-owned |
| R2  | LOW      | Container geometry can change independently of the browser window resize; hosts must refresh supplied zone/frame data after relevant layout changes.                      | Host implementation; call `updateSpatialZones()` with fresh zones.          | Open, host-owned |

- Known gaps / checks not run: M6 remains in progress. Offline queue remains blocked by V09; Diagnostics Inspector, gesture action profiles, and broader lifecycle integration remain. No manual assistive-technology or device-matrix certification was performed. The default browser test ports collided with an unrelated Vite service; configurable test ports enabled the full pass without stopping that process.
- Required approvals: None for this private, reversible, default-off draft integration. No publishing, deployment, push, or external integration was performed.
- Rollback or recovery: Revert the focused spatial-integration commit; the feature remains off by default and host data is not persisted.
- Release readiness: `NOT READY`

## 6. Completion record

- Audit created before the task commit and included in that commit, or reason not committed: Created before staging; will be included in the focused local commit.
- Final commit hash: `PENDING`
- External actions: `NOT AUTHORIZED` unless separately approved.

**Limitations:** An agent self-audit is not an independent security review, accessibility certification, or release approval.
