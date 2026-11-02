# Development Audit: Fix Toolbar layout paint CI follow-up

| Field                    | Value                                                                                          |
| ------------------------ | ---------------------------------------------------------------------------------------------- |
| Audit ID                 | `2026-10-17T16-49-13Z-fix-toolbar-layout-paint-ci`                                             |
| Created (UTC)            | 2026-10-17 16:49 UTC                                                                           |
| Task / issue             | Correct the remaining WebKit viewport overflow failures in the toolbar deployment gate         |
| Auditor                  | Agent self-audit; GitHub Actions run logs and local source review                              |
| Branch / baseline commit | `master` / `3f4f6402a25a417c10f9632707a990331cc1ce9b`                                          |
| Final commit             | Report in task completion                                                                      |
| Scope reviewed           | Viewport and large-text layout timing, narrow responsive header checks, remote deployment gate |
| Outcome                  | `PASS WITH RISKS`                                                                              |

## 1. Request and outcome

- Requested behavior: Repair the toolbar deployment pipeline and complete its gated deployment workflow.
- Implemented behavior: Wait for two animation frames after viewport and root font-size changes before measuring page overflow; remove the ineffective root overflow clip.
- Acceptance criteria and evidence: GitHub Actions run [#15](https://github.com/Moorky/moorkyz-tools/actions/runs/39049916934) failed three WebKit browser assertions after the prior follow-up; 252 browser cases passed and container/deploy were skipped. After this correction, local `pnpm check` passed 203 unit tests, all 255 browser cases across Chromium, Firefox and WebKit, production build, formatting, lint and type checks. The next remote run must still pass before deployment.
- Intentional exclusions: No public API, package dependency, feature default, persistence namespace or infrastructure setting changed.
- Relevant decisions/contracts: WCAG 2.2 AA and keyboard reachability remain required. Deployment stays gated on successful verification and container smoke checks.

## 2. Change inventory

| Path                                  | Change                                                                                            | Risk |
| ------------------------------------- | ------------------------------------------------------------------------------------------------- | ---- |
| `packages/portal/src/app/globals.css` | Remove the root overflow clip that did not resolve the reported `scrollWidth` measurements        | Low  |
| `tests/browser/qa-matrix.spec.js`     | Wait for viewport and large-text layout paint before measuring overflow; retain settled axe scans | Low  |
| `tests/browser/website.spec.js`       | Wait for viewport layout paint before checking responsive header overflow                         | Low  |
| This audit                            | Record the second remote failure, local verification and remaining release gates                  | Low  |

## 3. Verification evidence

| Check                        | Result  | Exact command / method                                                                                                               | Evidence / notes                                                                                                                      |
| ---------------------------- | ------- | ------------------------------------------------------------------------------------------------------------------------------------ | ------------------------------------------------------------------------------------------------------------------------------------- |
| Formatting / lint            | PASS    | `$env:CI='1'; $env:TOOLS_MOORKYZ_COM_FIXTURE_PORT='47973'; $env:TOOLS_MOORKYZ_COM_PORTAL_PORT='47974'; pnpm check`                       | ESLint and Prettier passed for the repository.                                                                                        |
| Typecheck                    | PASS    | `$env:CI='1'; $env:TOOLS_MOORKYZ_COM_FIXTURE_PORT='47973'; $env:TOOLS_MOORKYZ_COM_PORTAL_PORT='47974'; pnpm check`                       | SDK, portal and Vanilla, React, Vue and Angular consumer type checks passed.                                                      |
| Unit / integration tests     | PASS    | `$env:CI='1'; $env:TOOLS_MOORKYZ_COM_FIXTURE_PORT='47973'; $env:TOOLS_MOORKYZ_COM_PORTAL_PORT='47974'; pnpm check`; `pnpm test:coverage` | 203 unit tests passed; the coverage gate passed with 94.79% core branch and 86.40% emitted-library overall branch coverage.           |
| Browser / accessibility      | PASS    | `$env:CI='1'; $env:TOOLS_MOORKYZ_COM_FIXTURE_PORT='47973'; $env:TOOLS_MOORKYZ_COM_PORTAL_PORT='47974'; pnpm check`                       | All 255 cases passed across Chromium, Firefox and WebKit, including responsive, large-text, theme and axe checks.                     |
| Build / consumer fixture     | PASS    | `$env:CI='1'; $env:TOOLS_MOORKYZ_COM_FIXTURE_PORT='47973'; $env:TOOLS_MOORKYZ_COM_PORTAL_PORT='47974'; pnpm check`                       | Production portal build and framework consumer type checks passed.                                                                    |
| Security / dependency review | PASS    | `pnpm check:source`; `pnpm audit --audit-level=low`                                                                                  | Source scan checked 257 paths; dependency audit reported no known vulnerabilities. The bounded scan is not a complete security audit. |
| Performance (if relevant)    | N/A     | No toolbar library or runtime JavaScript changes                                                                                     | Only portal CSS and browser-test sequencing changed.                                                                                  |
| License inventory            | PASS    | `pnpm licenses list --json`                                                                                                          | Exited 0 and emitted the dependency license inventory.                                                                                |
| `git diff --check`           | PASS    | `git diff --check`                                                                                                                   | No whitespace errors.                                                                                                                 |
| Container build / smoke      | NOT RUN | `docker version --format '{{.Server.Version}}'`                                                                                      | Local Docker Desktop Linux engine pipe is unavailable. Remote run #15 skipped container and deploy after `verify` failed.             |
| Remote verify / deployment   | NOT RUN | GitHub Actions run following the authorized correction push                                                                          | A new remote run remains required; run #15 failed before the container gate.                                                          |

## 4. Risk and quality review

- Public contract, feature-default, and compatibility impact: No API, configuration or feature-default changes.
- Security, privacy, permissions, and secrets: No changes.
- Accessibility, keyboard, and reduced motion: No interaction behavior changed. The cross-browser viewport, large-text, keyboard and axe checks passed locally after layout paint was awaited.
- Disabled-feature work/resource/network behavior: No changes.
- Error handling, cleanup, and recovery: No new listeners or asynchronous work; the test helper waits for two browser animation frames.
- Dependency, performance, and regression exposure: No dependency or library runtime changes. The previous root clipping rule was removed because it did not fix the CI measurement and could hide visible overflow.

## 5. Findings and residual risks

| ID   | Severity     | Finding and evidence                                                                                  | Mitigation / owner                                                                               | Status                  |
| ---- | ------------ | ----------------------------------------------------------------------------------------------------- | ------------------------------------------------------------------------------------------------ | ----------------------- |
| TD-6 | Release gate | Run #15 failed three WebKit overflow checks that read layout immediately after viewport/font changes. | Re-run the full remote verification, container smoke and deploy gates after the correction push. | Open pending remote run |
| TD-7 | Container    | Local Docker Engine is unavailable; run #15 skipped its container image and HTTP smoke steps.         | Use the GitHub workflow's container build and HTTP smoke check.                                  | Open pending remote run |

- Known gaps / checks not run: The correction has not run through GitHub Actions; no production HTTP check was performed locally.
- Required approvals: The user authorized pushing the deployment fix to `master`; the workflow deploys only after verification and container checks pass.
- Rollback or recovery: Revert the focused follow-up commit if remote checks reveal a regression.
- Release readiness: `NOT READY` until remote verification, container smoke and deployment complete successfully.

## 6. Completion record

- Audit created before the task commit and included in that commit.
- Final commit hash: Report in task completion.
- External actions: Follow-up push authorized in this task; production deployment remains automated and gated by CI.

**Limitations:** This is a self-audit, not an independent security review, accessibility certification, or production release approval.
