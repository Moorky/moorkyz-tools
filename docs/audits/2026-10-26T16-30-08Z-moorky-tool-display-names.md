# Development Audit: Moorky tool display names

> Fill in with observed evidence. Never mark an unexecuted check as PASS.

| Field | Value |
|---|---|
| Audit ID | `2026-10-26T16-30-08Z-moorky-tool-display-names` |
| Created (UTC) | `2026-10-26T16:30:08.928Z` |
| Task / issue | Moorky tool display names |
| Auditor | Agent / person and relevant tool (self-audit unless stated otherwise) |
| Branch / baseline commit | `master` / `d82fc5359cb5018130b082574f5b17c060755473` |
| Final commit | Report in task completion; do not edit this audit after committing |
| Scope reviewed | Tool display names across portal, embedded Video Crunch workspace, local-service messages, current documentation and tests |
| Outcome | `PASS` |

## 1. Request and outcome

- Requested behavior: Rename displayed tools to the `Moorky <Project Name>` pattern with normal word spacing and add a small gray DM Mono `moorkyz-tools-name` identifier.
- Implemented behavior: Catalog titles and detail metadata now use `Moorky Toolbar Builder`, `Moorky Video Crunch`, and `Moorky Directory Cleaner`. The Video Crunch embedded workspace and service messages, Directory Cleaner messages, and current tool documentation use the same names. The site header has a subdued 0.6rem muted-gray DM Mono identifier; it is decorative and hidden from assistive technology.
- Acceptance criteria and evidence: Focused Chromium portal tests passed (7/7); isolated Video Crunch local-workspace tests passed (2/2); isolated Directory Cleaner service tests passed (2/2); production portal build and TypeScript checks passed.
- Intentional exclusions: Internal slugs, API routes/tokens, environment variables, source filenames, executable commands and historical audit evidence remain unchanged to preserve technical contracts and provenance.
- Relevant decisions/contracts: Root `AGENTS.md`, `docs/decisions/decision-register.md`, and `docs/portal/modular-tools.md`. Portal navigation and tool independence remain unchanged.

## 2. Change inventory

| Path | Change | Risk |
|---|---|---|
| `packages/portal/src/data/tools.ts` | Updated three registry titles; cards and detail metadata use the new titles. | Low |
| `packages/portal/src/components/site-header.tsx`, `packages/portal/src/app/globals.css` | Added the small subdued project identifier in the header. | Low |
| `packages/portal/src/components/local-tool-workspace.tsx`, `packages/portal/src/components/directory-cleaner.tsx`, `packages/portal/src/components/directory-cleaner-model.ts`, `packages/portal/src/server/local-tool-proxy.ts` | Updated user-facing local workspace, error and service labels only. | Low |
| `tools/videocrunch/public/index.html`, `tools/videocrunch/server.js`, `tools/videocrunch/README.md`, `tools/videocrunch/AGENTS.md`, `tools/videocrunch/SECURITY.md`, `tools/videocrunch/docs/DEPENDENCIES.md` | Updated the standalone workspace brand, startup label and current docs. Internal command/API identifiers are untouched. | Low |
| `tools/directory-cleaner/server.mjs`, `tools/directory-cleaner/clean.mjs`, `tools/directory-cleaner/README.md`, `tools/toolbar-builder/README.md` | Updated service/help labels and current guide headings. | Low |
| `packages/portal/README.md`, `docs/portal/modular-tools.md` | Updated current portal documentation. | Low |
| `tests/browser/compact-tools.spec.js`, `tests/browser/directory-cleaner-service.spec.js`, `tests/browser/local-service.spec.js`, `tests/browser/tools.spec.js`, `tests/tooling/local-tool-proxy.test.js`, `tests/tooling/project-identity.test.js` | Updated expected titles/messages and assert the identifier's text/font. | Low |

## 3. Verification evidence

Use `PASS`, `FAIL`, `NOT RUN` (with reason), or `N/A`. Record exact commands and results.

| Check | Result | Exact command / method | Evidence / notes |
|---|---|---|---|
| Formatting / lint | `PASS` | `.\node_modules\.bin\prettier.cmd --check package.json pnpm-workspace.yaml tsconfig.base.json .prettierrc.json eslint.config.mjs playwright*.mjs tools\*.files.json tools\directory-cleaner tools\toolbar-builder packages\portal tests\tooling tests\browser scripts`; `.\node_modules\.bin\eslint.cmd packages\portal\src packages\portal\scripts tools\directory-cleaner\*.mjs tools\toolbar-builder\*.mjs scripts tests\tooling\*.test.js tests\browser playwright*.mjs` | All checked files formatted; repository lint completed without findings. |
| Typecheck | `PASS` | `Push-Location packages\portal; & ..\..\node_modules\.bin\tsc.cmd --noEmit; Pop-Location` | TypeScript completed without errors; production build also ran its TypeScript phase. |
| Unit / integration tests | `PASS` | `node --test tests\tooling\*.test.js tests\tooling\*.test.cjs tools\videocrunch\test\server.test.js` | Node test suite exited successfully. Naming/local gateway focused tests also passed (4/4). |
| Browser / accessibility | `PASS` | `.\node_modules\.bin\playwright.cmd test tools.spec.js compact-tools.spec.js --project=chromium`; `.\node_modules\.bin\playwright.cmd test --config=playwright.directory-cleaner.config.mjs --project=chromium`; `.\node_modules\.bin\playwright.cmd test --config=playwright.videocrunch.config.mjs --project=chromium` | 7 focused portal tests, 2 real Directory Cleaner fixture tests and 2 Video Crunch local-workspace tests passed. Catalog/detail browser test includes accessibility and mobile/theme checks. |
| Build / consumer fixture | `PASS` | `Push-Location packages\portal; node ..\..\scripts\prepare-tool-sources.mjs; node scripts\run-next.mjs build; node scripts\prepare-standalone.mjs; Pop-Location`; `node scripts\verify-source.mjs` | Production build and standalone preparation succeeded; explicit source check passed (239 repository paths scanned). |
| Security / dependency review | `N/A` | Self-review of diff and existing `node scripts\verify-source.mjs` output | Display labels/styles only; no dependencies, permissions, data handling or network behavior added. The bounded source scan is not a complete security audit. |
| Performance (if relevant) | `N/A` | Not applicable | No runtime work, polling, assets or dependencies added. |
| `git diff --check` | `PASS` | `git diff --check` | No whitespace errors. |

## 4. Risk and quality review

- Public contract, feature-default, and compatibility impact: Display titles and page metadata changed as requested. Routes, slugs, service flags, API paths, commands and defaults remain stable.
- Security, privacy, permissions, and secrets: No changes to trust boundaries, data handling, permissions, secrets or service availability. Local services remain opt-in and loopback-only.
- Accessibility, keyboard, and reduced motion: Technical identifier is `aria-hidden`; browser checks including the existing axe/mobile/theme coverage passed. No controls or motion behavior changed.
- Disabled-feature work/resource/network behavior: Disabled local-service path remains dormant; browser coverage passed without service requests.
- Error handling, cleanup, and recovery: Only display strings changed; isolated service integration tests passed, including stale-preview rejection and recoverable file handling.
- Dependency, performance, and regression exposure: No dependency changes. Name changes are covered by updated portal and local-service browser tests.

## 5. Findings and residual risks

| ID | Severity | Finding and evidence | Mitigation / owner | Status |
|---|---|---|---|---|
| None | N/A | No task-related issues found in the reviewed diff and checks. | N/A | Closed |

- Known gaps / checks not run: No full multi-browser matrix was run; Chromium portal and both isolated local-service suites passed. An ad-hoc lint invocation that included the legacy CommonJS `tools/videocrunch/server.js` reported its existing `require()` rule violations; that file is excluded from the configured repository lint command, which passed. The Video Crunch fixture used the already-installed FFmpeg executable; no installation was performed.
- Required approvals: None for this reversible display-only change.
- Rollback or recovery: Revert the focused task commit; no persistent user data or configuration changes.
- Release readiness: `READY`

## 6. Completion record

- Audit created before the task commit and included in that commit, or reason not committed: Created before commit; will be staged with task-owned changes.
- Final commit hash: `PENDING`
- External actions: `NOT AUTHORIZED` unless separately approved.

**Limitations:** An agent self-audit is not an independent security review, accessibility certification, or release approval.
