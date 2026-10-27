# Development Audit: M2 dock and portal foundation

> Fill in with observed evidence. Never mark an unexecuted check as PASS.

| Field | Value |
|---|---|
| Audit ID | `2026-10-16T20-48-01Z-m2-dock-and-portal-foundation` |
| Created (UTC) | `2026-10-16T20:48:01.049Z` |
| Task / issue | M2 dock and portal foundation |
| Auditor | Copilot coding agent; self-audit |
| Branch / baseline commit | `master` / `cde4bd6` |
| Final commit | Report in task completion; do not edit this audit after committing |
| Scope reviewed | M2 controller/DOM dock, package assets, browser accessibility/CSP checks, consumer fixtures, and the user-approved Next.js portal workspace integration |
| Outcome | `PASS WITH RISKS` |

## 1. Request and outcome

- Requested behavior: implement the approved project in milestone order, including the actual reusable dock and an integrated portal foundation, without publishing or deploying.
- Implemented behavior: added the SSR-safe toolbar controller, Shadow/Light renderer, same-package CSS/license assets, host-only actions, manual collapse, roving keyboard controls, atomic patch updates, and disposal. Integrated the existing private Next.js portal as a real consumer with context action updates, a local theme preference, a nonce-based CSP Proxy, and local social-card metadata.
- Acceptance criteria and evidence: `pnpm check` passed with 21 Node tests and 9 Playwright tests, including Chromium axe scans in both portal themes, real portal action updates, CSP/no-external-request assertions, reduced motion, narrow viewport overflow, and disposal. 50 repeated mount/dispose cycles leave no attached DOM. Frozen install and package dry-run passed.
- Intentional exclusions: no stable public API approval, manual screen-reader/device certification, framework adapters, complete Development Studio, generator, optional feature modules, release, or external integration.
- Relevant decisions/contracts: Q01–Q18, Q20, Q28–Q35, Q37–Q54, Q55–Q64, Q79–Q80; `docs/contracts/sdk-and-schema.md`; `docs/design/interaction-and-visual-system.md`; `docs/architecture/security-and-trust.md`.

## 2. Change inventory

| Path | Change | Risk |
|---|---|---|
| `packages/sdk/src/toolbar.ts`, `src/index.ts`, `src/toolbar.css` | Added the framework-neutral controller, native accessible controls, keyboard/lifecycle behavior, and responsive dock styling | Medium |
| `packages/sdk/scripts/`, `package.json`, `tsconfig.build.json`, `README.md` | Added clean asset build, bundled local CSS/Apache license, package usage guide, and pack boundary | Medium |
| `packages/sdk/test/toolbar.test.js` | Added DOM lifecycle, failure, update, keyboard, and cleanup tests | Low |
| `packages/portal/` | Preserved/integrated the user-approved Next.js consumer, real library mount/context updates, local theme, nonce CSP Proxy, and documentation | Medium |
| `packages/portal/public/`, `src/app/layout.tsx` | Added a local social-card asset and metadata; no remote image dependency | Low |
| `tests/browser/`, `playwright.config.mjs` | Added Chromium CSP, responsive, motion, axe, portal, and consumer package browser tests | Low |
| Root manifests/config, `.github/workflows/ci.yml`, `.gitignore` | Added browser checks and portal build/typecheck to CI; ignored generated output | Medium |
| `examples/react-consumer/` | Updated the fixture to exercise controller mount/dispose lifecycle | Low |
| `README.md`, `MANIFEST.json`, contract/engineering/portal docs, `implementation-status.md` | Documented the live foundation, actual commands, and remaining gates | Low |
| `docs/audits/2026-10-16T20-48-01Z-m2-dock-and-portal-foundation.md` | Recorded this M2/portal integration audit | Low |

## 3. Verification evidence

Use `PASS`, `FAIL`, `NOT RUN` (with reason), or `N/A`. Record exact commands and results.

| Check | Result | Exact command / method | Evidence / notes |
|---|---|---|---|
| Formatting / lint | PASS | `pnpm check` | ESLint and scoped Prettier checks passed. |
| Typecheck | PASS | `pnpm check` | SDK, vanilla/React fixtures, and portal TypeScript checks passed. |
| Unit / integration tests | PASS | `pnpm check` | 21 Node tests passed, 0 failed. |
| Browser / accessibility | PASS WITH RISKS | `pnpm check` | 9 Chromium tests passed; axe had zero violations for the toolbar and portal light/dark themes; no external requests in tested routes; manual assistive-technology checks remain. |
| Build / consumer fixture | PASS | `pnpm check`; `pnpm pack --filter @tools.moorkyz.com/sdk --dry-run` | Next.js production build, package build, and both consumer fixtures passed. Dry-run includes JS/types, local CSS, Apache license, package README; no CLI or React runtime bundled in `@tools.moorkyz.com/sdk`. |
| Security / dependency review | PASS WITH RISKS | Browser CSP tests; `pnpm licenses list`; `pnpm install --frozen-lockfile` | Portal production CSP uses nonce + strict-dynamic, no `unsafe-inline`; dock CSS is same-origin; tested pages make no external requests. License inventory includes MPL-2.0 test tooling and Next's optional Sharp binary (metadata reports Apache-2.0 AND LGPL-3.0-or-later); review necessity/notice before RC. No secrets or telemetry introduced. |
| Performance (if relevant) | NOT RUN | N/A | No approved numerical budgets/baselines; defer profiling to M10. |
| `git diff --check` | PASS | `git diff --check` | No whitespace errors in task changes. |

## 4. Risk and quality review

- Public contract, feature-default, and compatibility impact: adds candidate `createToolbar`/controller contracts and partial configuration updates; remains explicitly draft. Approved feature defaults are unchanged; no optional module activation is implemented by this slice.
- Security, privacy, permissions, and secrets: action execution only calls the host dispatcher; failure details are not rendered, only a generic alert. Portal CSP is nonce-based. Configuration and labels render as text. No telemetry, credentials, or remote service calls.
- Accessibility, keyboard, and reduced motion: semantic toolbar, labels, roving focus, arrow/Home/End navigation, visible focus, explicit collapse, media-based reduced motion, and axe coverage implemented. Manual screen-reader/device validation remains.
- Disabled-feature work/resource/network behavior: no optional feature module runs or creates resources; CSS is same-package and loads only on mount; browser tests observe no third-party requests.
- Error handling, cleanup, and recovery: mount/update return structured errors; invalid updates are atomic; host action errors trigger a generic alert and optional host callback; disposal removes the UI and listeners, including stale-node dispatch tests.
- Dependency, performance, and regression exposure: production library has no runtime dependencies. Portal uses Next/React; Playwright/axe/happy-dom are development-only. Optional Sharp is not directly used by portal components but is present transitively; no performance budgets were available.

## 5. Findings and residual risks

| ID | Severity | Finding and evidence | Mitigation / owner | Status |
|---|---|---|---|---|
| M2-R1 | Medium | Public controller/config names remain unapproved draft contracts. | Keep pre-release status; seek contract approval before stabilization. | Open |
| M2-R2 | Medium | Manual screen-reader and broader browser/device smoke remain incomplete. | Complete WCAG/device validation at M10 before any release claim. | Open |
| M8-R1 | Medium | Portal is a landing foundation, not the full Development Studio. | Continue roadmap M8 work after dependencies/adapters are ready. | Open |
| M10-R1 | Medium | License inventory includes MPL test tools and optional Sharp with LGPL metadata. | Confirm notices/necessity and package policy in hardening before RC; do not publish now. | Open |
| M8-R2 | Low | The imported social-card asset's original provenance was not independently verified. | Confirm ownership/license before external deployment. | Open |

- Known gaps / checks not run: formal security audit, dependency vulnerability scan, perf baselines, full consumer matrix, framework adapters, Studio/generator, manual screen-reader tests, and full M3–M11 acceptance gates remain.
- Required approvals: public API names remain drafts; deployment, publishing, signing, spending, and external integration remain unauthorized.
- Rollback or recovery: revert the M2 integration commit to return to its `dc4717c` parent state; preserve the separately committed portal public-folder and maintainer CLI configuration. No user data or migrations.
- Release readiness: `NOT READY`

## 6. Completion record

- Audit created before the task commit and included in that commit.
- Final commit hash: `PENDING`
- External actions: `NOT AUTHORIZED` unless separately approved.

**Limitations:** An agent self-audit is not an independent security review, accessibility certification, or release approval.
