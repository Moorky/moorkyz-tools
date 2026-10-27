# Development Audit: Fix Moorkyz launcher and card identifiers

> Fill in with observed evidence. Never mark an unexecuted check as PASS.

| Field | Value |
|---|---|
| Audit ID | `2026-10-26T16-53-43Z-fix-moorkyz-launcher-and-card-identifiers` |
| Created (UTC) | `2026-10-26T16:53:43.036Z` |
| Task / issue | Fix Moorkyz launcher and card identifiers |
| Auditor | Copilot agent self-audit using Node.js, pnpm and Playwright |
| Branch / baseline commit | `master` / `e50a0e7c4bdf0c253865db43e9d13dbe5b6e0c96` |
| Final commit | Report in task completion; do not edit this audit after committing |
| Scope reviewed | Both local launchers, loopback portal scripts, displayed Moorkyz names and per-card identifiers |
| Outcome | `PASS WITH RISKS` (unrelated WebKit Directory Cleaner contrast failure noted below) |

## 1. Request and outcome

- Requested behavior: Fix `moorkyz start` and its related script surfaces; correct the display prefix to Moorkyz and show each tool's small gray DM Mono slug on its card.
- Implemented behavior: Both launchers wait for an HTTP 2xx/3xx response plus a verified supervisor heartbeat, report startup diagnostics and stop their own unhealthy instance on timeout. The repository-specific launcher now supports validated startup timeouts and production NODE_ENV. Portal dev/start scripts bind to 127.0.0.1. pnpm's dependency verification reports an error instead of automatically reinstalling packages.
- Names are Moorkyz Toolbar Builder, Moorkyz Video Crunch and Moorkyz Directory Cleaner. Each card and detail heading shows its stable registry ID: `moorkyz-tools-toolbar-builder`, `moorkyz-tools-video-crunch` or `moorkyz-tools-directory-cleaner`, in 0.65rem DM Mono and neutral gray adapted to light/dark themes. Removed the incorrect header placeholder.
- Acceptance criteria and evidence: Both real launchers started the actual portal successfully in both dev and production modes; HTTP 200 pages contained the corrected name/ID and listeners were loopback-only. All four instances were stopped afterward. Card identity/style tests passed in Chromium, Firefox and WebKit, including dark-theme homepage axe checks.
- Intentional exclusions: No unrelated server was killed, dependencies were not installed, internal API routes/tokens and command filenames were not renamed, historical audits were not rewritten. Preserved the pre-existing uncommitted `packages/portal/next-env.d.ts` development-type references and excluded that file from the task commit.
- Relevant decisions/contracts: Root AGENTS.md, current decision register, local-only service contract and existing portal/CLI documentation.

## 2. Change inventory

| Path | Change | Risk |
|---|---|---|
| `.npmrc` | Fail on stale pnpm installation instead of running an implicit install. | Low |
| `moorkyz.cjs`, `tools.moorkyz.com.cjs`, `tools.moorkyz.com.config.json` | HTTP readiness, startup failure diagnostics/cleanup, configurable timeout and production environment consistency. | Medium: lifecycle behavior |
| `packages/portal/package.json` | Bind dev and production portal scripts to loopback. | Low |
| `packages/portal/src/data/tools.ts`, `src/app/page.tsx`, `src/app/tools/[slug]/page.tsx`, `src/app/tools.css` | Correct names and visible registry IDs, with neutral gray DM Mono styling. | Low |
| `packages/portal/src/app/globals.css`, `src/components/site-header.tsx` | Remove the misplaced header identifier and unused style. | Low |
| Portal local-tool components/proxy and standalone cleaner/video tool labels | Correct Moorky to Moorkyz without changing operations or routes. | Low |
| `tests/tooling/launcher-startup.test.cjs`, `tools.moorkyz.com-cli.test.cjs` | Isolated delayed/transient/unhealthy server and lifecycle/production/timeout regression tests for both launchers. | Low |
| Existing browser/identity/proxy tests | Correct name expectations and assert visible card IDs, typography, gray colors and detail routes. | Low |
| Root README, CLI docs, portal docs and current standalone tool guides | Document startup behavior and corrected display names/IDs. | Low |

## 3. Verification evidence

Use `PASS`, `FAIL`, `NOT RUN` (with reason), or `N/A`. Record exact commands and results.

| Check | Result | Exact command / method | Evidence / notes |
|---|---|---|---|
| Formatting / lint | `PASS` | `pnpm lint`; `pnpm format:check` | Both repository checks passed on final code. |
| CLI syntax | `PASS` | `node --check moorkyz.cjs`; `node --check tools.moorkyz.com.cjs` | Both parse successfully. |
| Typecheck | `PASS` | `pnpm typecheck` | Strict portal TypeScript check passed. |
| Unit / integration tests | `PASS` | `node --test tests\tooling\*.test.js tests\tooling\*.test.cjs tools\videocrunch\test\server.test.js` | 54/54 tests passed, including both launchers' readiness/lifecycle tests. |
| Storage regression tests | `PASS` | `node --test tests\tooling\moorkyz-runtime.node.cjs tests\tooling\local-tool-proxy.test.js tests\tooling\project-identity.test.js` | 28/28 tests passed. |
| Chromium portal browser tests | `PASS` | `pnpm exec playwright test tools.spec.js compact-tools.spec.js --project=chromium` | 8/8 tests passed on fresh production output, including cards, details, mobile/themes and axe coverage. |
| All-browser card ID checks | `PASS` | `pnpm exec playwright test tools.spec.js --grep 'every card displays'` | 3/3 passed after allowing normal browser fractional-font rounding in the assertion; typography, visibility, neutral gray, detail IDs and dark homepage axe passed. |
| Broader browser matrix | `FAIL` | `pnpm exec playwright test tools.spec.js compact-tools.spec.js` | Initial 22/24 passed. Firefox computed 10.4063px rather than 10.4px; test tolerance corrected and verified across all browsers. WebKit reported Directory Cleaner label contrast in unchanged form styling; retained as a residual issue, not hidden or fixed outside scope. |
| Directory Cleaner browser integration | `PASS` | `$env:TOOLS_MOORKYZ_COM_PORTAL_PORT = '4175'; pnpm exec playwright test --config=playwright.directory-cleaner.config.mjs --project=chromium` | 2/2 passed using temporary file fixtures, including stale-preview rejection. |
| Video Crunch browser integration | `PASS` | `$env:TOOLS_MOORKYZ_COM_PORTAL_PORT = '4176'; pnpm exec playwright test --config=playwright.videocrunch.config.mjs --project=chromium` | 2/2 passed with already-installed native prerequisites and isolated media fixtures. |
| Video Crunch standalone verification | `PASS` | `powershell.exe -NoProfile -ExecutionPolicy Bypass -File tools\videocrunch\tools\Verify.ps1` | Syntax, API tests and Windows PowerShell regression verification completed successfully. |
| Video Crunch standalone audit | `PASS` (bounded) | `powershell.exe -NoProfile -ExecutionPolicy Bypass -File tools\videocrunch\tools\Audit.ps1` | Existing npm advisory audit found 0 vulnerabilities; credential-pattern scan passed for 48 files. Not an independent security review. |
| Build / source bundles | `PASS` | `pnpm build`; `pnpm check:source` | Production build and allowlisted bundles succeeded; final source scan checked 242 paths. |
| Real launcher smoke tests | `PASS` | `.\moorkyz.cmd start`; `.\moorkyz.cmd start --prod`; `.\tools.moorkyz.com.cmd start`; `.\tools.moorkyz.com.cmd start --prod`; each followed by `status` and `stop` | For each run, read its tracked URL, verified `Invoke-WebRequest` returned HTTP 200 with corrected name/ID, and verified `Get-NetTCPConnection -State Listen -LocalPort <tracked-port>` had no wildcard listener. Both launchers ended STOPPED. |
| Security / dependency review | `PASS` (bounded self-review) | Diff inspection and `pnpm check:source` | No dependencies, external integrations or data-handling changes; startup HTTP probes use configured local URLs and do not follow redirects. No independent security certification. |
| Performance (if relevant) | `N/A` | No benchmark required | Startup readiness probes end on success/failure; no ongoing polling or connections were added to the portal. |
| `git diff --check` | `PASS` | `git diff --check` | No whitespace errors. |

## 4. Risk and quality review

- Public contract, feature-default, and compatibility impact: Startup now requires successful HTTP readiness rather than a transient listener/process. Non-ready apps return a failure and diagnostics. Portal services bind only to loopback. Display names and IDs are corrected without changing slugs/API routes.
- Security, privacy, permissions, and secrets: No dependencies or permission changes. pnpm cannot silently reinstall stale dependencies from script startup. No unrelated process termination or global profile modifications.
- Accessibility, keyboard, and reduced motion: Text identifiers remain readable in the DOM, use neutral gray with theme-specific colors and wrap on small screens. Card navigation semantics and motion preferences remain intact. The new card test passes dark homepage axe checks in all three browsers.
- Disabled-feature work/resource/network behavior: Native tool gateways remain independently opt-in; dormant-path browser coverage passed. Launcher health probes exist only during explicit startup.
- Error handling, cleanup, and recovery: Isolated tests verify delayed readiness, early process failure, unhealthy timeout cleanup, restart, production environment, status/logs and stop. No test-managed services were left running.
- Dependency, performance, and regression exposure: No install or lockfile changes. Existing pre-run dependency checks now fail explicitly and point to an intentional install. No background connections were added.

## 5. Findings and residual risks

| ID | Severity | Finding and evidence | Mitigation / owner | Status |
|---|---|---|---|---|
| R1 | Medium | Broader WebKit axe check reports light text on white for Directory Cleaner rule labels in dark theme (contrast 1.08). The affected form styles are unchanged by this task. | Separate accessibility investigation; no out-of-scope style change or test suppression. | Open, outside task scope |

- Known gaps / checks not run: The complete broader browser matrix is not green because of R1. Initial browser checks against an older standalone output failed on stale Moorky names; rebuilt output passed Chromium and all-browser card identity checks. An early real-start test assumed the previous manual dev instance was still active; it had exited, so startup correctly succeeded. Final real smoke tests verified the actual intended dev/production behavior.
- Required approvals: No new dependencies, publishing or external actions requested/performed.
- Rollback or recovery: Revert the focused task commit. Runtime ports/logs remain local and ignored; no user file processing was performed outside temporary test fixtures.
- Release readiness: `READY WITH CONDITIONS` (R1 is not certified resolved)

## 6. Completion record

- Audit created before the task commit and included in that commit, or reason not committed: Created and filled before staging the task-owned paths.
- Final commit hash: `PENDING`
- External actions: `NOT AUTHORIZED` unless separately approved.

**Limitations:** An agent self-audit is not an independent security review, accessibility certification, or release approval.
