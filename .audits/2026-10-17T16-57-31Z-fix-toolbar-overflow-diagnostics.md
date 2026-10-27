# Development Audit: Fix Toolbar overflow diagnostics

| Field                    | Value                                                                                     |
| ------------------------ | ----------------------------------------------------------------------------------------- |
| Audit ID                 | `2026-10-17T16-57-31Z-fix-toolbar-overflow-diagnostics`                                   |
| Created (UTC)            | 2026-10-17 16:57 UTC                                                                      |
| Task / issue             | Identify the remaining Linux WebKit page-overflow failures in the toolbar deployment gate |
| Auditor                  | Agent self-audit; GitHub Actions run logs and local source review                         |
| Branch / baseline commit | `master` / `d4a378003570ec1a6684569c3af93f052fb542fd`                                     |
| Final commit             | Report in task completion                                                                 |
| Scope reviewed           | Failure diagnostics for responsive and large-text page-overflow assertions                |
| Outcome                  | `PASS WITH RISKS`                                                                         |

## 1. Request and outcome

- Requested behavior: Repair the toolbar deployment pipeline and complete its gated deployment workflow.
- Implemented behavior: When a page-overflow assertion fails, report the viewport/document/body widths, root overflow styles and the ten rightmost overflowing DOM elements to the browser test log.
- Acceptance criteria and evidence: GitHub Actions run [#16](https://github.com/Moorky/moorkyz-tools/actions/runs/39051160845) reproduced the same three Linux WebKit overflow failures as run #15 after paint waits were added. The local focused WebKit suite passed all 14 cases, so Linux-only layout evidence is needed to select the correct source fix. This diagnostic commit is complete; the deployment gate is not.
- Intentional exclusions: No product behavior, public API, package dependency, feature default, persistence namespace or infrastructure setting changed.
- Relevant decisions/contracts: WCAG 2.2 AA and keyboard reachability remain required. Deployment stays gated on successful verification and container smoke checks.

## 2. Change inventory

| Path                              | Change                                                                                       | Risk |
| --------------------------------- | -------------------------------------------------------------------------------------------- | ---- |
| `tests/browser/qa-matrix.spec.js` | Include failure-only layout metrics and overflowing element summaries in overflow assertions | Low  |
| `tests/browser/website.spec.js`   | Include the same diagnostics in responsive header overflow assertions                        | Low  |
| This audit                        | Record the repeated remote failure and diagnostic step                                       | Low  |

## 3. Verification evidence

| Check                        | Result  | Exact command / method                                                                                                                                                                                          | Evidence / notes                                                                                                   |
| ---------------------------- | ------- | --------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- | ------------------------------------------------------------------------------------------------------------------ |
| Formatting / lint            | PASS    | `pnpm lint`; `pnpm format:check`                                                                                                                                                                                | ESLint and Prettier passed for the repository.                                                                     |
| Typecheck                    | N/A     | No TypeScript or production source changes                                                                                                                                                                      | The parent commit's full `pnpm check` passed type checks; this commit only changes JavaScript test assertions.     |
| Unit / integration tests     | N/A     | No library or unit-test changes                                                                                                                                                                                 | The parent commit's full `pnpm check` passed 203 unit tests; this change only adds failure diagnostics.            |
| Browser / accessibility      | PASS    | `$env:TOOLS_MOORKYZ_COM_FIXTURE_PORT='47973'; $env:TOOLS_MOORKYZ_COM_PORTAL_PORT='47974'; pnpm exec playwright test tests/browser/qa-matrix.spec.js tests/browser/website.spec.js --project=webkit --reporter=line` | All 14 targeted local WebKit cases passed. Run #16 still failed the three Linux WebKit overflow cases.             |
| Build / consumer fixture     | N/A     | No production source or build configuration changes                                                                                                                                                             | The parent commit's full `pnpm check` passed the production build and consumer type checks.                        |
| Security / dependency review | N/A     | No dependency, permission, secret-handling or production-source changes                                                                                                                                         | The parent commit's source scan and dependency audit passed.                                                       |
| Performance (if relevant)    | N/A     | Test failure diagnostics only                                                                                                                                                                                   | No runtime code changed.                                                                                           |
| License inventory            | N/A     | No dependency changes                                                                                                                                                                                           | The parent commit's license inventory exited 0.                                                                    |
| `git diff --check`           | PASS    | `git diff --check`                                                                                                                                                                                              | No whitespace errors.                                                                                              |
| Container build / smoke      | NOT RUN | `docker version --format '{{.Server.Version}}'`                                                                                                                                                                 | Local Docker Desktop Linux engine pipe is unavailable. Run #16 skipped container and deploy after `verify` failed. |
| Remote verify / deployment   | NOT RUN | GitHub Actions run following the diagnostic push                                                                                                                                                                | The next remote run must expose the failing DOM geometry before the source correction can be chosen.               |

## 4. Risk and quality review

- Public contract, feature-default, and compatibility impact: No API, configuration or feature-default changes.
- Security, privacy, permissions, and secrets: No changes. Diagnostics run only when a test detects overflow on the static portal page and report a short DOM text excerpt with element geometry.
- Accessibility, keyboard, and reduced motion: No behavior changed; the targeted local WebKit tests include the responsive, large-text, keyboard and axe checks.
- Disabled-feature work/resource/network behavior: No changes.
- Error handling, cleanup, and recovery: No new listeners or asynchronous work. The diagnostic query runs only on overflow and returns at most ten elements.
- Dependency, performance, and regression exposure: No runtime code or dependencies changed; the additional page scan runs only for a failing assertion.

## 5. Findings and residual risks

| ID   | Severity     | Finding and evidence                                                                                          | Mitigation / owner                                                                                   | Status                  |
| ---- | ------------ | ------------------------------------------------------------------------------------------------------------- | ---------------------------------------------------------------------------------------------------- | ----------------------- |
| TD-8 | Release gate | Runs #15 and #16 fail the same three Linux WebKit page-overflow assertions; local WebKit passes those checks. | Use the failure-only geometry details from the next remote run to make a measured layout correction. | Open pending remote run |
| TD-9 | Container    | Local Docker Engine is unavailable; run #16 skipped its image build and HTTP smoke steps.                     | Use the GitHub workflow's container build and HTTP smoke check after verification passes.            | Open pending remote run |

- Known gaps / checks not run: No full local rerun was needed for this test-only diagnostic change; the full local check passed on parent commit `d4a3780`. The next remote verify run is pending.
- Required approvals: The user authorized pushing the deployment fix to `master`; deployment remains automated and gated.
- Rollback or recovery: Revert this focused diagnostic commit if it introduces CI noise; retain the failing geometry in the task notes.
- Release readiness: `NOT READY` until remote verification, container smoke and deployment complete successfully.

## 6. Completion record

- Audit created before the task commit and included in that commit.
- Final commit hash: Report in task completion.
- External actions: Follow-up push authorized in this task; production deployment remains automated and gated by CI.

**Limitations:** This is a self-audit, not an independent security review, accessibility certification, or production release approval.
