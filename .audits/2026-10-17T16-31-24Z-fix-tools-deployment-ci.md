# Development Audit: Fix Toolbar deployment CI follow-up

| Field                    | Value                                                                                                                        |
| ------------------------ | ---------------------------------------------------------------------------------------------------------------------------- |
| Audit ID                 | `2026-10-17T16-31-24Z-fix-tools-deployment-ci`                                                                             |
| Created (UTC)            | 2026-10-17 16:31 UTC                                                                                                         |
| Task / issue             | Fix the Toolbar deployment gate after remote CI exposed remaining browser failures                                           |
| Auditor                  | Agent self-audit; GitHub Actions run logs and local source review                                                            |
| Branch / baseline commit | `master` / `53e801e780468894b01b0c1ba1fe36e5f597b475`                                                                        |
| Final commit             | Report in task completion                                                                                                    |
| Scope reviewed           | Theme synchronization across viewport changes, narrow page overflow, preview toolbar theme assertion, remote deployment gate |
| Outcome                  | `PASS WITH RISKS`                                                                                                            |

## 1. Request and outcome

- Requested behavior: Repair the toolbar deployment pipeline and complete the fix through its deployment workflow.
- Implemented behavior: Synchronize the active theme on `html` and `body`, wait for WebKit's viewport paint before accessibility scans, clip page-level horizontal overflow, and assert the preview toolbar's color scheme through its mounted DOM node.
- Acceptance criteria and evidence: GitHub Actions run [#14](https://github.com/Moorky/moorkyz-tools/actions/runs/39048000815) failed `verify` at commit `53e801e`; 250 browser cases passed and five failed, so container and deploy were skipped. After the follow-up changes, the local CI-configured `pnpm check` passed all 203 unit tests, all 255 browser tests across Chromium, Firefox and WebKit, production build, formatting, lint and type checks. The new remote run is still required to complete the deployment gate.
- Intentional exclusions: No public API, package dependency, feature default, persistence namespace or infrastructure setting changed.
- Relevant decisions/contracts: WCAG 2.2 AA and keyboard reachability remain required. The root `AGENTS.md` authorizes reversible implementation and test changes, while deployment remains gated by successful CI.

## 2. Change inventory

| Path                                              | Change                                                                                     | Risk |
| ------------------------------------------------- | ------------------------------------------------------------------------------------------ | ---- |
| `packages/portal/src/app/globals.css`             | Apply dark foreground directly in the body theme scope and clip horizontal page overflow   | Low  |
| `packages/portal/src/components/portal-shell.tsx` | Keep body and document theme attributes synchronized on initialization and toggle          | Low  |
| `tests/browser/qa-matrix.spec.js`                 | Wait two animation frames after viewport changes before theme and contrast scans           | Low  |
| `tests/browser/website.spec.js`                   | Check the mounted preview toolbar's data attribute even when the dock is temporarily inert | Low  |
| This audit                                        | Record remote failure evidence, remediation and remaining deployment gates                 | Low  |

## 3. Verification evidence

| Check                        | Result  | Exact command / method                                                                                                               | Evidence / notes                                                                                                                             |
| ---------------------------- | ------- | ------------------------------------------------------------------------------------------------------------------------------------ | -------------------------------------------------------------------------------------------------------------------------------------------- |
| Formatting / lint            | PASS    | `$env:CI='1'; $env:TOOLS_MOORKYZ_COM_FIXTURE_PORT='47973'; $env:TOOLS_MOORKYZ_COM_PORTAL_PORT='47974'; pnpm check`                       | ESLint and Prettier passed for the changed source and tests.                                                                                 |
| Typecheck                    | PASS    | `$env:CI='1'; $env:TOOLS_MOORKYZ_COM_FIXTURE_PORT='47973'; $env:TOOLS_MOORKYZ_COM_PORTAL_PORT='47974'; pnpm check`                       | SDK, portal, Vanilla, React, Vue and Angular consumer type checks passed.                                                                |
| Unit / integration tests     | PASS    | `$env:CI='1'; $env:TOOLS_MOORKYZ_COM_FIXTURE_PORT='47973'; $env:TOOLS_MOORKYZ_COM_PORTAL_PORT='47974'; pnpm check`; `pnpm test:coverage` | `pnpm check`: 203 tests passed. Coverage gate: 94.79% core branch coverage and 86.40% emitted-library overall branch coverage.               |
| Browser / accessibility      | PASS    | `$env:CI='1'; $env:TOOLS_MOORKYZ_COM_FIXTURE_PORT='47973'; $env:TOOLS_MOORKYZ_COM_PORTAL_PORT='47974'; pnpm check`                       | Full suite passed all 255 cases across Chromium, Firefox and WebKit. WebKit theme, responsive, large-text and accessibility checks passed.   |
| Build / consumer fixture     | PASS    | `$env:CI='1'; $env:TOOLS_MOORKYZ_COM_FIXTURE_PORT='47973'; $env:TOOLS_MOORKYZ_COM_PORTAL_PORT='47974'; pnpm check`                       | Production portal build and framework consumer type checks passed.                                                                           |
| Security / dependency review | PASS    | `pnpm check:source`; `pnpm audit --audit-level=low`                                                                                  | Source scan checked 256 paths; dependency audit reported no known vulnerabilities. The bounded source scan is not a complete security audit. |
| Performance (if relevant)    | PASS    | `pnpm measure:bundles`                                                                                                               | All baseline measurements completed; repository script does not enforce budgets.                                                             |
| License inventory            | PASS    | `pnpm licenses list --json`                                                                                                          | Exited 0 and emitted the dependency license inventory.                                                                                       |
| `git diff --check`           | PASS    | `git diff --check`                                                                                                                   | No whitespace errors.                                                                                                                        |
| Container build / smoke      | NOT RUN | `docker version --format '{{.Server.Version}}'`                                                                                      | Local Docker Desktop Linux engine pipe is unavailable; the remote workflow must run its container image build and HTTP smoke check.          |
| Remote verify / deployment   | NOT RUN | GitHub Actions run following the authorized follow-up push                                                                           | Run #14 failed before container/deploy. The follow-up remote run is pending.                                                                 |

## 4. Risk and quality review

- Public contract, feature-default, and compatibility impact: No API, configuration or feature-default changes.
- Security, privacy, permissions, and secrets: No changes.
- Accessibility, keyboard, and reduced motion: The theme remains synchronized for the page and mounted toolbar; tests wait for settled viewport styles before axe scans. Toolbar keyboard overflow coverage from the preceding commit remains in place.
- Disabled-feature work/resource/network behavior: No changes.
- Error handling, cleanup, and recovery: No new listeners or asynchronous work. The theme attributes are assigned from the existing theme state.
- Dependency, performance, and regression exposure: No dependency changes. Page-level horizontal overflow is clipped; existing intentionally scrollable panels keep their own scroll behavior.

## 5. Findings and residual risks

| ID   | Severity     | Finding and evidence                                                                            | Mitigation / owner                                                                                     | Status                  |
| ---- | ------------ | ----------------------------------------------------------------------------------------------- | ------------------------------------------------------------------------------------------------------ | ----------------------- |
| TD-4 | Release gate | Run #14 failed five browser cases; the new changes pass the complete local CI-equivalent suite. | Push the follow-up under the existing authorization and monitor its verify, container and deploy jobs. | Open pending remote run |
| TD-5 | Container    | Local Docker Engine is unavailable, so no local image build or smoke request was run.           | Use the GitHub workflow's container build and HTTP smoke check.                                        | Open pending remote run |

- Known gaps / checks not run: The follow-up has not yet run through GitHub Actions; no production HTTP check was performed locally.
- Required approvals: The user authorized pushing the deployment fix to `master`; the workflow performs deployment only after its verification and container gates pass.
- Rollback or recovery: Revert the focused follow-up commit if remote checks reveal a regression.
- Release readiness: `NOT READY` until remote verify, container smoke and deployment complete successfully.

## 6. Completion record

- Audit created before the task commit and included in that commit.
- Final commit hash: Report in task completion.
- External actions: Follow-up push authorized in this task; production deployment remains automated and gated by CI.

**Limitations:** This is a self-audit, not an independent security review, accessibility certification, or production release approval.
