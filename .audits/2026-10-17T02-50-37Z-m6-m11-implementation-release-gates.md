# Development Audit: M6–M11 implementation and release gates

| Field | Value |
|---|---|
| Audit ID | `2026-10-17T02-50-37Z-m6-m11-implementation-release-gates` |
| Created (UTC) | `2026-10-17T02:50:37Z` |
| Task / issue | Implement the remaining M6–M11 work without publishing or deployment |
| Auditor | Copilot SDK agent self-audit; bounded implementation agents for compatibility and generator/CLI, not independent review |
| Branch / baseline commit | `master`, `79a47b3` |
| Final commit | Reported in task completion; no post-commit audit rewrite |
| Scope reviewed | Optional resilience, extensions/adapters, Studio, generator/CLI, automated hardening and release preparation |
| Outcome | `PASS WITH RISKS` for the implemented local slice; `BLOCKED / NOT AUTHORIZED` for full RC/release acceptance |

## 1. Request and outcome

The user requested M6 through M11. Existing implementation and all relevant
decisions/contracts were inspected first; the initial worktree was clean and
the baseline `pnpm check` passed 127 Node and 30 Chromium tests.

Delivered:

- M6: explicitly enabled in-memory recovery with current host authorization,
  safe/idempotent admission, bounded attempts, cancellation and breaker;
  separately imported redacted local Inspector; authorized gesture profiles;
  mounted visibility/BFCache coordination, retaining host authority and sticky
  collapse.
- M7: optional Vue/Angular adapters and actual lifecycle/SSR fixtures; trusted
  scoped extension registration/dispatch, revocation, cleanup, fault containment
  and explicit experimental namespace. React/Next.js remain optional consumers.
- M8: shared-schema JSON/visual workbench, confirmed versioned configuration
  import/export, searchable preview-version reference, custom nonce-scoped theme
  authoring/import/export and contrast validation, scenario import/replay,
  static trusted Extension Studio, explicit Inspector and shared starter gallery.
- M9: one browser-safe generator and Node-only consumer CLI; five presets,
  runnable Vanilla projects, manifest integrity, safe creation/isolated-child
  addition, validation/doctor and preview-first non-destructive upgrades.
- M10: three-browser automation, coverage gates, source/credential-pattern scan,
  dependency/license inventory, browser/CLI isolation and measured baselines.
- M11: explicit NOT READY authorization dossier and exclusive local
  package/source/checksum evidence preparation. No release authority inferred.

Relevant decisions: Q18–Q20, Q23–Q29, Q34–Q35, Q38, Q43–Q53, Q60,
Q63–Q80. Feature defaults, mandatory accessibility/permissions, local-only data
handling and the single-package architecture are unchanged. All new API names
remain drafts; final stable contract approval is still open.

Intentional exclusions and genuine blockers are explicit in
[release-readiness.md](../engineering/release-readiness.md): persisted queues,
untrusted portal code execution, non-Vanilla generation, unapproved numerical
budgets, whole-workspace coverage claims, manual assistive-technology/device
certification and release execution.

## 2. Change inventory

| Path / area | Change | Risk |
|---|---|---|
| SDK recovery, Inspector, gesture-profile subpaths | Off-by-default explicit optional capabilities | Medium: host idempotency/consent remains essential |
| SDK extensions/experimental/Vue/Angular subpaths | Scoped trust/lifecycle boundaries and optional peers | Medium: trusted JS is not sandboxed |
| SDK generator and `cli/` | Shared schema, versioned files, strict filesystem safety and reviewable upgrade plans | Medium: trusted nonconcurrent destination and backups remain required |
| SDK configuration/theme/controller | Symbol rejection, portable theme validation, host nonce styles, mounted lifecycle, bounded composition/focus | Medium: public drafts, CSP and native browser differences |
| Portal components/styles | Real-package tools, no arbitrary code, local-only exports, hydration safety, native control contrast | Medium: manual usability/AT remains outstanding |
| Root scripts/Playwright/CI/Docker manifest copies | Coverage/source/bundle gates, three engines, JSON results, all fixture manifests in build context | Low–medium: remote Linux/container gates not locally executed |
| Documentation/specification manifest | Current dossier, API/CLI contracts, package docs and accurate limitations; 16 original hashes verified | Low |

Concurrent unrelated changes appeared during the task in the maintainer
launchers/configuration (`tools.moorkyz.com`, `tools.moorkyz.com.cjs`, `tools.moorkyz.com.cmd`,
`tools.moorkyz.com.config.json`). They were inspected only to establish exclusion, never
reverted, edited or included in this task's staging. No unrelated user data is
part of the task commit or package evidence.

## 3. Verification evidence

| Check | Result | Exact command / method | Observed evidence |
|---|---|---|---|
| Initial baseline | PASS | `pnpm check` | 127 Node / 30 Chromium tests; baseline build/typechecks |
| Full integrated pipeline | PASS | `pnpm check` | Lint/format, production build, 190 Node tests, 120 browser tests and all library/consumer/portal typechecks; final additional package-surface test checked separately |
| Final Node regressions | PASS | `node --test` | **191/191**, zero failed/cancelled/skipped |
| Final coverage | PASS, scoped | `node scripts/verify-coverage.mjs` | **187/187** library tests in each run; core **94.79% branches** >=90; emitted library **88.64% lines / 86.03% branches / 95.21% functions**, each >=85 |
| Final browser matrix | PASS | `pnpm test:browser` | **120/120**, 40 per engine; JSON report has 0 unexpected, skipped or flaky cases |
| Initial hydration accessibility repeat | PASS | `pnpm exec playwright test -g 'portal landing page has no automated' --repeat-each=3` | 9/9 across all three engines, no axe violations |
| Final lint/format | PASS | `pnpm lint`; `pnpm format:check` | Includes new CLI, scripts and all consumer fixtures |
| Build / consumers | PASS | `pnpm build`; `pnpm typecheck` through the integrated pipeline | Next.js production build; Vanilla/React/Vue/Angular fixtures and portal compile |
| Frozen restore | PASS | `pnpm install --frozen-lockfile --ignore-scripts` | Seven workspaces, lockfile current, no lifecycle scripts run |
| Dependency advisories | PASS at execution | `pnpm audit --json` | Zero reported advisories, 220 resolved dependencies; no bypass |
| License inventory | PASS inventory, release review OPEN | `pnpm licenses list --json` | New Vue/Angular/esbuild MIT; RxJS Apache-2.0. Existing native Sharp Apache/LGPL, MPL and CC-BY distribution obligations still require exact artifact/image review |
| Bounded local source scan | PASS, limited | `node scripts/verify-source.mjs` | No selected credential-pattern or library eval/HTML-assignment findings; not comprehensive secret scanning or independent security review |
| Package boundaries | PASS | `node --test packages\sdk\test\package-surface.test.js` | Types/ESM/assets/license/bin present; optional peers; AST-based actual imports reject Node entry leakage, not harmless generated source strings |
| Package dry run | PASS | `pnpm --filter @tools.moorkyz.com/sdk exec npm pack --dry-run --json` | 0.1.0 package, 78,520 packed bytes / 392,229 unpacked bytes at inspection; CLI, README and `dist/LICENSE` present |
| Bundle isolation / baseline | PASS baseline, budget OPEN | `node scripts/measure-bundles.mjs` | Browser builds have no contributing framework/portal/CLI imports; validator 2,560 B gzip, dock 17,955 B, search 1,634 B, AI 798 B, Inspector 385 B, generator 6,130 B |
| Mounted timing baseline | PASS observation, budget OPEN | 100-cycle Playwright mount/update/dispose fixture | Chromium median/p95 0.6/1.2 ms; Firefox 2/4 ms; WebKit 2/4 ms, shared-machine observations only |
| Restrictive policy | PASS | Existing strict CSP plus `/trusted-types` fixture, portal custom-token workflow | No eval/unsafe HTML sinks or policy weakening; Firefox's browser-owned favicon denial is explicitly classified, not confused with script/style failures |
| Specification integrity | PASS | SHA-256 comparison of all `MANIFEST.json` entries using Node crypto | All **16** original specification hashes match after related documentation updates |
| Whitespace | PASS | `git diff --check` | No whitespace errors; expected Windows line-ending warnings are not test failures |
| Docker engine / image | NOT RUN / BLOCKED | `docker version --format '{{.Server.Version}}'` | Docker Desktop Linux engine pipe unavailable. Did not start/alter the shared host's engine or claim container smoke success |
| Manual AT/device/a11y certification | NOT RUN | Requires actual screen-reader/touch/device sessions | Automated axe and viewport cases are not WCAG certification |
| Publication/deployment/signing/spend | N/A; NOT AUTHORIZED | No command executed | No push or external-product integration |

Exact local browser versions: Chromium 156.0.8078.4, Firefox 157.0, WebKit 27.2.
Runtime/toolchain: Node 22.18.0, pnpm 11.8.0, TypeScript 6.0.3. Framework fixtures:
React 19.3.1, Next.js 16.4.0, Vue 3.5.44, Angular 20.3.34, RxJS 7.8.2.

Verification artifacts remain local: the session evidence directory contains
Node/coverage logs and package/bundle/license inventories; the ignored browser
JSON report retains per-engine results and base64 performance attachments.
The immutable tarball/checksum metadata is prepared after the task commit so
its source revision can be recorded without a circular commit hash.

## 4. Findings fixed during validation

- Expanded tests exposed unsupported symbol keys being silently ignored by
  configuration validation. They now fail plain-data validation.
- Narrow Firefox layout had a zero-width action viewport when auxiliary controls
  were pinned. The shared natural scroller now retains at least a 44px action
  viewport in the 320px fixture. Scroll operations target only the owned row.
  Existing collapsed notification/discovery availability is preserved; host
  actions remain hidden. The structural unit assertion was updated to verify
  actual hidden host actions rather than the former container location.
- WebKit exposed pre-hydration input loss, native secondary-button contrast and
  submit-button focus differences. Interactive Studio boundaries start
  disabled/inert until hydrated; button backgrounds are explicit; AI submit
  retains an owned keyboard target. No assertions or a11y rules were disabled.
- A portal-wide theme override incorrectly overrode preview token styles; it is
  now scoped only to the actual site-navigation mount.
- New import controls initially made an existing scenario label ambiguous; their
  accessible names are distinct.
- Early concurrent verification collided with the package build's intentional
  dist cleanup. Those runs were not accepted as evidence; final tests ran only
  after build completion with no concurrent dist mutation.
- The editor's unit-test tool did not discover Node tests; the repository's
  existing Node runner was used instead. Initial test/layout failures were fixed
  and rerun; they are not reported as successful first attempts.

## 5. Risk and quality review

- **Permissions/security:** deny-by-default/current authorization is retained;
  no extension, queue or gesture mapping grants host authority. Trusted extensions
  are not untrusted-code isolation. Unsafe operations are never automatically
  retried. Persisted queues remain unsupported.
- **Data/privacy:** no new remote service, telemetry, prompt/result persistence
  or behavioral tracking. The portal's Next.js runner explicitly disables its
  development/build telemetry. Local exports contain validated configuration,
  generated source or theme/scenario data, not host callbacks or credentials.
- **Accessibility:** native keyboard controls, visible focus, contrast validation,
  both DOM modes and reduced motion are exercised. Mandatory manual acceptance
  remains open; no conformance claim is made.
- **Disabled work/cleanup:** optional helpers are inert by default; disabling
  clears owned state/UI, aborts recovery and releases resources. Mounted lifecycle
  listeners and nonce styles dispose with the controller. No polling was added.
- **Dependency/bundle exposure:** framework peers are optional; new packages are
  development/fixture dependencies. CLI and generator imports are separate.
  Runtime false flags do not magically exclude all implemented dock renderer
  paths; measured bytes are documented honestly.
- **Generation/upgrade:** exclusive writes, whole-plan collision preflight,
  traversal/symlink/hard-link checks, integrity, backups and rollback are tested.
  Concurrent hostile filesystem mutation and untrusted local Node execution are
  outside the stated trusted-directory contract.
- **Release metadata:** preparation requires committed clean package inputs and
  explicitly records unrelated dirty worktree status. This does not authorize
  release and is not a signature or whole-workspace clean-state assertion.

## 6. Residual risks, approvals and completion

| ID | Severity | Remaining finding | Handling / owner | Status |
|---|---|---|---|---|
| RC01 | Release blocker | Stable contracts and exact platform/coverage scope not approved | User contract/matrix review; V02/V03/V11 | OPEN |
| RC02 | Release blocker | Manual screen-reader, touch/device/zoom/fullscreen evidence incomplete | Human accessibility/device acceptance; V06/V15 | OPEN |
| RC03 | Release blocker | Numerical performance budgets not approved | Review reproducible baseline before setting failing budgets; V04 | OPEN |
| RC04 | Release blocker | Exact distribution notices and independent release security review pending | Release owner/legal/security review; V14 | OPEN |
| RC05 | Release blocker | Local container engine unavailable | Authorized environment must run configured Docker image and HTTP smoke gate | OPEN |
| RC06 | Authorization hold | Costs, infrastructure/publishing/signing ownership and exact rollback target unapproved | Separately scoped written authorization; V10 | OPEN |
| S01 | Scope limit | No persisted offline queue, arbitrary portal JS runner, share links or non-Vanilla generator | Explicit unsupported scope, safe static/local alternatives; V09/V13 | DOCUMENTED |

Release readiness is **NOT READY**. Full M10/M11 exits are not marked complete.
The validated implementation is suitable for a focused **local development
commit**, not a release-candidate certification.

Rollback: revert the focused task commit in a new commit; preserve unrelated
work. Use generator previews/backups for generated projects and retain prior
accepted package/image artifacts. No destructive reset or history rewrite.

- Audit created before and included in the task's scoped commit.
- Final commit hash: `PENDING` (reported in completion, never falsely claimed).
- External actions: **NOT AUTHORIZED / NOT PERFORMED**.

**Limitations:** This is an agent self-audit, not an independent security review,
accessibility certification, legal clearance or release approval.
