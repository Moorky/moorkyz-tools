# Development Audit: Refine homepage and tool versions

| Field | Value |
|---|---|
| Audit ID | `2026-10-26T18-33-06Z-refine-homepage-and-tool-versions` |
| Created (UTC) | 2026-10-26T18:33:06Z |
| Task / issue | Compact homepage, essential-tools copy, editorial scores and uniform detail versions |
| Auditor | Copilot agent self-audit using local build, Node tests, Playwright and axe |
| Branch / baseline commit | `master` / `4480bfa`, with pre-existing uncommitted catalog changes |
| Final commit | Not created: task edits depend on and overlap pre-existing uncommitted work |
| Scope reviewed | Homepage, shared header/styles, tool display metadata, focused browser tests and portal guide |
| Outcome | `PASS WITH RISKS`: requested behavior verified; broader WebKit accessibility check fails outside changed styling |

## 1. Request and outcome

- Removed the homepage's "Explore the tools" link and WORKBENCH eyebrow label.
  Retained the plain domain identifier below the introduction. Stable per-tool
  identifiers and routes remain unchanged.
- Reduced the header mark from 64 px to 40 px and the wordmark to 1 rem.
  "Tools" uses the existing theme-aware purple accent. Reduced hero title,
  introduction, section headings, card titles and vertical spacing.
- Introduction: "A library of tools I never planned to build - until they became
  essential." The rendered text uses an em dash. Catalog heading: "Small tools.
  Real lifesavers."
- Set editorial scores: Media Recover 9/10, Directory Cleaner 7/10, Video Crunch
  8/10, Toolbar Builder 6/10.
- Follow-up: every tool detail page now displays exactly `1.0.0`; catalog
  badges derive `v1.0.0` from the same registry instead of a literal.
  Unpublished status, desktop limitations and source package versions are unchanged.
- Relevant authority: [root contract](../AGENTS.md),
  [current decisions](../docs/decisions/decision-register.md) and
  [portal guide](../docs/portal/modular-tools.md).
- Excluded: deployment, source applications, new dependencies, opt-in service
  behavior, and the pre-existing card implementation/tests/audit.

## 2. Change inventory

| Path | Task change | Risk |
|---|---|---|
| `packages/portal/src/app/page.tsx` | New copy, lower domain identifier, removed hero link, registry-driven version badge | Low; overlaps initial card edits |
| `packages/portal/src/app/tools.css` | More compact homepage typography and spacing | Low; overlaps initial card styling |
| `packages/portal/src/app/globals.css` | Compact header mark/wordmark and theme accent | Low; shared across portal routes |
| `packages/portal/src/components/site-header.tsx` | Matching image dimensions and accent span | Low; accessible brand name retained |
| `packages/portal/src/data/tools.ts` | Exact scores and plain display versions | Low; scores rely on initial uncommitted registry fields |
| `tests/browser/homepage-refinements.spec.js` | Exact copy, placement, sizing, themes, scores, IDs, versions and release-status coverage | Test only |
| `docs/portal/modular-tools.md` | Document compact branding, copy, scores and uniform display versions | Documentation only |

Starting changes were recorded before implementation. Original versions of the
four overlapping files were retained in the session workspace for task-only
comparison. The existing catalog tests and previous audit were not edited or staged.
The build regenerated `packages/portal/next-env.d.ts` to the tracked production
type-reference form; its initial generated development-reference diff disappeared.

## 3. Verification evidence

| Check | Result | Exact command / method | Evidence / notes |
|---|---|---|---|
| Production build | PASS | `pnpm build` | Final build compiled and completed TypeScript, page generation and standalone preparation |
| Strict types | PASS | `pnpm typecheck` | `tsc --noEmit` exited 0 after final source changes |
| Scoped formatting | PASS | `pnpm exec prettier --check packages\portal\src\components\site-header.tsx packages\portal\src\app\globals.css packages\portal\src\app\page.tsx packages\portal\src\app\tools.css packages\portal\src\data\tools.ts tests\browser\homepage-refinements.spec.js` | All matched files passed; an initial new-test formatting failure was corrected with the existing formatter |
| Scoped lint | PASS | `pnpm exec eslint packages\portal\src\components\site-header.tsx packages\portal\src\app\page.tsx packages\portal\src\data\tools.ts tests\browser\homepage-refinements.spec.js` | Exit 0 |
| Catalog/source unit tests | PASS | `node --test tests\tooling\tools-catalog.test.js` | 6/6, including registry integrity and exact source ZIP bytes |
| Requested browser behavior | PASS | `$env:CI='1'; $env:TOOLS_MOORKYZ_COM_PORTAL_PORT='4196'; node node_modules\@playwright\test\cli.js test 'homepage-refinements\.spec\.js$'` | 18/18 in Chromium, Firefox and WebKit; exact scores and all four detail versions verified |
| Responsive and homepage accessibility | PASS | Same focused browser command | Light/dark, 320/390/1440 px; title 32 px on narrow widths and 64 px desktop, mark 40 px, identifier after lede, no horizontal overflow, keyboard skip activation and zero homepage axe violations |
| Existing portal/workspace regressions | FAIL | `$env:CI='1'; $env:TOOLS_MOORKYZ_COM_PORTAL_PORT='4196'; node node_modules\@playwright\test\cli.js test '(?:^|[\\/])tools\.spec\.js$' 'compact-tools\.spec\.js$'` | 23 passed, 1 failed: WebKit axe reports Directory Cleaner form-label color contrast while switching themes; unchanged form/theme code was not modified |
| Visual inspection | PASS | Temporary standalone preview on `127.0.0.1:4197`, integrated browser screenshot and DOM measurements | HTTP 200; compact mobile layout inspected; desktop title 64 px, logo 40 px and no horizontal overflow measured |
| Diff whitespace | PASS | `git --no-pager diff --check` | Exit 0; only Git line-ending normalization warnings |
| Task-only diff review | PASS | `git --no-pager diff --no-index -- <saved-initial-file> <current-file>` for each overlapping file; regular Git diff for header/styles | Only requested copy, layout, scores, version and documentation changes relative to initial work |
| Dependency/security scope | N/A | Inspected task-only diff | No dependencies, permissions, services, filesystem processing, telemetry or external integrations changed; not an independent security assessment |
| Full repository gate | NOT RUN | `pnpm check` | Scoped checks used; unrelated known broader WebKit failure is explicitly reported |

Initial test-harness failures were resolved rather than weakening thresholds:
viewport dimensions update before computed `vw` styles settle, so tests now
wait for the expected computed font size. Keyboard skip-link activation is
tested with explicit focus to avoid platform-specific WebKit tabbing preferences.
The package-manager browser invocation under `CI=1` failed before tests with
`ERR_PNPM_VERIFY_DEPS_BEFORE_RUN` because the global virtual-store setting
changed. The installed Playwright CLI was used directly, with no installation
or configuration changes. An initial Windows path selector matched no tests;
basename regular-expression selectors resolved that invocation issue.

## 4. Risk and quality review

- Public contract/defaults: display-only changes; existing URLs, stable IDs,
  source notices and explicit unpublished status are preserved.
- Security/privacy/permissions: no account, upload, storage, telemetry, external
  connection, CSP or service-enable changes.
- Accessibility: semantic links/headings remain; header image is decorative;
  accessible brand name remains "Moorkyz Tools." Accent contrast and homepage axe
  checks passed in both themes. Reduced motion is enabled during layout tests.
- Disabled services: existing compact-workspace tests passed, including zero
  native requests with the service off.
- Failure handling: source-bundle loading, visible errors and recovery paths were
  not changed; existing source-error/copy-failure browser coverage passed.
- Cleanup/performance: no new runtime listeners, downloads, connections or timers.
  Temporary preview is stopped after inspection. No native tools or shared
  prerequisites were installed.

## 5. Findings and residual risks

| ID | Severity | Finding and evidence | Mitigation / owner | Status |
|---|---|---|---|---|
| R1 | Medium | Existing WebKit multi-route axe test reports transient/mismatched form-label foreground/background colors on Directory Cleaner during theme changes. Both broad runs failed in this unchanged surface; a clean-baseline reproduction was not performed. | Leave unchanged in this display/copy task; maintainer may investigate separately | Open, outside task scope |
| R2 | Workflow | Pre-existing edits add `catalogDescription` and `usefulness`, card markup, tests and documentation. These fields are absent from `HEAD`; the requested score changes cannot form a coherent implementation commit without incorporating those initial edits. | Preserve index/worktree rather than silently commit another task's work; obtain explicit permission or commit the earlier work first | Local implementation commit blocked |

- Known gaps: full repository/native suites and production deployment were not run.
  Source application package versions were intentionally not changed.
- Required approvals: none for this local display update. Including initial
  uncommitted card work in a combined commit needs user approval.
- Rollback: reverse only this task's edits using the recorded task-only diff and
  initial session snapshots; do not revert whole overlapping files or initial work.
- Release readiness: `NOT APPLICABLE`; no deployment or release was requested.

## 6. Completion record

- Audit created after focused checks. Code, tests and audit are persistent locally.
- Final commit hash: `NOT CREATED` because task-only staging would either omit the
  score implementation or incorporate pre-existing user edits. The Git index was
  left untouched; no history was rewritten.
- External actions: `NOT AUTHORIZED`; no push, publication, merge or deployment.

**Limitations:** An agent self-audit is not an independent security review,
accessibility certification, or release approval.
