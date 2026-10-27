# Development Audit: M5 themes customization and preferences

| Field | Value |
|---|---|
| Audit ID | `2026-10-16T22-15-22Z-m5-themes-customization-and-preferences` |
| Created (UTC) | `2026-10-16T22:15:22.508Z` |
| Task / issue | M5 themes customization and preferences |
| Auditor | Self-audit by Copilot SDK agent; no independent reviewer |
| Branch / baseline commit | `master` / `bbab506` |
| Final commit | Report in task completion; this audit is not to be edited after commit |
| Scope reviewed | M5 theme tokens, customization controls, controller integration, and local preferences |
| Outcome | `PASS WITH RISKS` |

## 1. Request and outcome

- Requested behavior: Continue implementing the project, finish the current M5 themes/customization/persistence slice, and commit the current changes locally.
- Implemented behavior: Added six built-in themes and contrast validation; an accessible edit mode for cycling theme/color scheme and reordering actions; a host settings API; namespaced, schema-versioned preferences with v0 migration, explicit storage adapters, memory fallback, and surfaced diagnostics. Manual collapse/restore persists when enabled. The user-facing contract remains a draft.
- Acceptance criteria and evidence: `pnpm check` passed 67 Node tests, 13 Playwright tests, all axe checks, lint, format, builds, and typechecks. Preference tests cover storage round trips, migration, invalid/corrupt records, failures, deferred browser-storage access, controller reload, collapse persistence, disabled customization with zero store I/O, and edit-mode action suppression.
- Intentional exclusions: No custom-theme file import/export, full Theme Design Studio, cross-project sync, remote storage, telemetry, publishing, deployment, or integration into another Moorkyz application.
- Relevant decisions/contracts: Q07, Q08, Q13, Q15, Q22, Q28, Q30, Q53, Q63, Q64; draft M5 contract in `docs/contracts/sdk-and-schema.md`; roadmap M5 in `docs/engineering/roadmap.md`.

## 2. Change inventory

| Path | Change | Risk |
|---|---|---|
| `MANIFEST.json` | Refreshed the tracked SDK contract digest; all 16 listed hashes were verified | Low |
| `docs/contracts/sdk-and-schema.md` | Recorded M5 draft API, persistence and limitations | Low |
| `docs/engineering/implementation-status.md` | Recorded M5 evidence and remaining later-milestone work | Low |
| `docs/audits/2026-10-16T22-15-22Z-m5-themes-customization-and-preferences.md` | Added this evidence-backed audit | Low |
| `packages/sdk/README.md` | Documented explicit host storage setup and customization behavior | Low |
| `packages/sdk/src/configuration.ts` | Added validated theme and color-scheme configuration | Medium |
| `packages/sdk/src/index.ts` | Exported theme and preference APIs | Low |
| `packages/sdk/src/preferences.ts` | Added namespaced v1 preference store, v0 migration, browser adapter, validation and diagnostics | Medium |
| `packages/sdk/src/themes.ts` | Added built-in token catalog, contrast calculation and token validation | Low |
| `packages/sdk/src/toolbar.css` | Added palettes, forced-colors styling and edit controls | Medium |
| `packages/sdk/src/toolbar.ts` | Integrated preference load/save, customization UI, action reordering, disabled-path behavior and error reporting | Medium |
| `packages/sdk/test/configuration.test.js` | Covered theme configuration validation | Low |
| `packages/sdk/test/preferences.test.js` | Covered persistence, migration, error and deferred-storage paths | Low |
| `packages/sdk/test/themes.test.js` | Covered built-in tokens and contrast validation | Low |
| `packages/sdk/test/toolbar.test.js` | Covered controller persistence, edit mode and disabled customization | Low |
| `packages/portal/src/components/portal-shell.tsx` | Wired the portal dock to explicit local preference storage and local diagnostics | Low |
| `tests/browser/server.mjs` | Allowed local loading of new preference/theme package modules | Low |
| `tests/browser/portal.spec.js` | Verified portal toolbar theme preferences survive reload | Low |
| `tests/browser/toolbar.spec.js` | Added all-theme axe checks, edit-mode keyboard/reorder and reliable CSS-load waits | Low |

## 3. Verification evidence

| Check | Result | Exact command / method | Evidence / notes |
|---|---|---|---|
| Formatting / lint | PASS | `pnpm check` | ESLint and Prettier check both passed |
| Typecheck | PASS | `pnpm check` | SDK, vanilla fixture, React fixture, and portal typechecks passed |
| Unit / integration tests | PASS | `pnpm check` | 67 Node tests passed, 0 failed |
| Browser / accessibility | PASS | `pnpm check` | 14 Chromium tests passed; axe reported no violations for built-in themes, customization mode, Light DOM, and portal; the portal preference integration survives reload |
| Build / consumer fixture | PASS | `pnpm check` | SDK and portal production builds and both consumer fixture typechecks passed |
| Frozen install / package contents | PASS | `pnpm install --frozen-lockfile`; `pnpm --filter @tools.moorkyz.com/sdk pack --dry-run` | Lockfile install up to date; package contents include preference/theme modules and local CSS |
| Security / dependency review | NOT RUN | Not applicable to a dependency-change gate; no dependency manifests or lockfile changed | No formal dependency advisory scan or independent security review is claimed; these remain M10 gates |
| Performance | N/A | No benchmark command run | This slice adds no background work; edit controls render only when activated |
| Specification hashes | PASS | PowerShell SHA-256 verification of all `MANIFEST.json` entries | 16 of 16 hashes matched |
| `git diff --check` | PASS | `git diff --check` | No whitespace errors; Git emitted only line-ending normalization warnings |

## 4. Risk and quality review

- Public contract, feature-default, and compatibility impact: Additive draft config fields and exports only; no approved stable contract or feature default was changed. Theme and preference shapes are documented as unapproved drafts.
- Security, privacy, permissions, and secrets: Preference data is schema-validated, namespaced, and limited to theme/scheme/collapse/action identifiers. Storage is host-provided; corrupt data is not overwritten automatically. No URLs, callbacks, credentials, remote service, or telemetry are introduced.
- Accessibility, keyboard, and reduced motion: Theme and edit-mode axe checks pass. Reorder controls are labeled buttons usable by keyboard; action dispatch buttons are disabled during edit mode. Existing reduced-motion and forced-colors behavior remains covered by the suite. Manual assistive-technology certification was not performed.
- Disabled-feature work/resource/network behavior: With `customize: false`, controller tests confirm zero preference-store reads/writes, no customization control, and a structured API rejection. No timers or network work were added.
- Error handling, cleanup, and recovery: Storage errors retain active values in memory and appear via structured diagnostics, host callback, and an accessible alert. Corrupt bytes are retained as evidence. Existing controller disposal removes owned DOM/listeners/observers.
- Dependency, performance, and regression exposure: No dependencies changed. Browser checks caught and fixed an accessible-name issue; full-suite browser testing also exposed a stylesheet-load race in the placement test, fixed by waiting for computed CSS.

## 5. Findings and residual risks

| ID | Severity | Finding and evidence | Mitigation / owner | Status |
|---|---|---|---|---|
| R1 | Low | Built-in CSS palette values duplicate token values in TypeScript; custom token validation does not install arbitrary themes into the controller | Keep the built-in theme catalog and CSS synchronized; consolidate/generated CSS and portable custom themes with Theme Studio work | Open |
| R2 | Low | No manual screen-reader/real-device validation or independent security review was performed | Complete the M10 accessibility, device, security, and supply-chain evidence gates before any release request | Open |

- Known gaps / checks not run: Full Theme Design Studio and portable theme format; device/zoom/RTL matrix; manual AT validation; dependency advisory scan.
- Required approvals: None for this reversible private implementation. Public contract approval, publication, deployment, signing, and external integration remain separate gates.
- Rollback or recovery: Revert the focused M5 commit; preference schema is namespaced and versioned. Existing corrupt storage is retained rather than destructively cleared.
- Release readiness: `NOT READY` — M6–M11 and the M10 hardening evidence remain incomplete.

## 6. Completion record

- Audit created before the task commit and included in that commit.
- Final commit hash: `PENDING`
- External actions: `NOT AUTHORIZED`.

**Limitations:** This is a self-audit, not an independent security review, accessibility certification, or release approval.
