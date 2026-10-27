# Development Audit: Fix Studio import field overflow

| Field                    | Value                                                                 |
| ------------------------ | --------------------------------------------------------------------- |
| Audit ID                 | `2026-10-17T17-59-42Z-fix-studio-import-overflow`                     |
| Created (UTC)            | 2026-10-17 17:59 UTC                                                  |
| Task / issue             | Fix Linux WebKit mobile overflow blocking toolbar deployment          |
| Auditor                  | Agent self-audit; local checks and GitHub Actions run #21 diagnostics |
| Branch / baseline commit | `master` / `846e769ee58637129f0a794a73b3527920904702`                 |
| Final commit             | Report in task completion; do not edit this audit after committing    |
| Scope reviewed           | Studio versioned configuration file input responsive sizing           |
| Outcome                  | `PASS WITH RISKS`                                                     |

## 1. Request and outcome

- Requested behavior: Fix the deployment-blocking toolbar issue and let the authorized `master` push trigger the CI-gated Coolify deployment.
- Implemented behavior: Constrain the Studio file input to its containing card and add a 320px browser regression assertion for the input bounds.
- Acceptance criteria and evidence: The targeted local WebKit Studio responsive test passed at 320px; the complete local `pnpm check` passed (203 Node tests, 255 browser tests across Chromium, Firefox and WebKit, type checks and build). Remote Linux WebKit and deployment remain pending.
- Intentional exclusions: No package/API changes, dependency changes, or external repository changes.
- Relevant decisions/contracts: Preserve responsive page fit and the labeled, native file import control.

## 2. Change inventory

| Path                                  | Change                                                                      | Risk |
| ------------------------------------- | --------------------------------------------------------------------------- | ---- |
| `packages/portal/src/app/globals.css` | Give the Studio file input a bounded, shrinkable full-container inline size | Low  |
| `tests/browser/qa-matrix.spec.js`     | Assert the Studio import input stays within the 320px viewport              | Low  |
| This audit                            | Record root cause, implementation and verification                          | Low  |

## 3. Verification evidence

| Check                        | Result | Exact command / method                                                                                                                                                                                                                                       | Evidence / notes                                                                           |
| ---------------------------- | ------ | ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------ | ------------------------------------------------------------------------------------------ |
| Formatting / lint            | PASS   | `pnpm check`                                                                                                                                                                                                                                                 | ESLint and Prettier checks passed.                                                         |
| Typecheck                    | PASS   | `$env:CI='1'; $env:TOOLS_MOORKYZ_COM_FIXTURE_PORT='47973'; $env:TOOLS_MOORKYZ_COM_PORTAL_PORT='47974'; pnpm check`                                                                                                                                               | Workspace, consumer and portal type checks passed.                                         |
| Unit / integration tests     | PASS   | Same `pnpm check` command                                                                                                                                                                                                                                    | 203 Node tests passed.                                                                     |
| Browser / accessibility      | PASS   | `$env:TOOLS_MOORKYZ_COM_FIXTURE_PORT='47973'; $env:TOOLS_MOORKYZ_COM_PORTAL_PORT='47974'; pnpm exec playwright test tests/browser/qa-matrix.spec.js --project=webkit --grep 'responsive, theme and keyboard audit of /studio' --reporter=line`; and `pnpm check` | Targeted Studio test passed; 255 browser tests passed across Chromium, Firefox and WebKit. |
| Build / consumer fixture     | PASS   | `pnpm check`                                                                                                                                                                                                                                                 | Portal production build and all consumer type checks passed.                               |
| Security / dependency review | N/A    | No dependency, permission, or security-boundary changes                                                                                                                                                                                                      | No dependency changed.                                                                     |
| Performance (if relevant)    | N/A    | CSS sizing constraint and failure regression assertion                                                                                                                                                                                                       | No runtime JavaScript or new work added.                                                   |
| `git diff --check`           | PASS   | `git diff --check` after all task files were present                                                                                                                                                                                                         | No whitespace errors.                                                                      |

## 4. Risk and quality review

- Public contract, feature-default, and compatibility impact: No public API or behavior contract change; native file selection remains intact.
- Security, privacy, permissions, and secrets: No change.
- Accessibility, keyboard, and reduced motion: The existing visible label remains associated with the native file input; no interaction behavior changed.
- Disabled-feature work/resource/network behavior: No change.
- Error handling, cleanup, and recovery: No new resources or state.
- Dependency, performance, and regression exposure: CSS-only runtime change; viewport regression assertion covers the previously failing boundary.

## 5. Findings and residual risks

| ID    | Severity   | Finding and evidence                                                                                                                                                          | Mitigation / owner                                                                  | Status                                     |
| ----- | ---------- | ----------------------------------------------------------------------------------------------------------------------------------------------------------------------------- | ----------------------------------------------------------------------------------- | ------------------------------------------ |
| TD-15 | Deployment | Workflow run [#21](https://github.com/Moorky/moorkyz-tools/actions/runs/39055230239) measured `#workbench-import` at 347px wide and extending to x=386 on a 320px viewport. | Bound the control to the workbench and assert its viewport bounds in browser tests. | Fixed locally; remote verification pending |
| TD-11 | Deployment | Container and deployment jobs were skipped while Linux verification failed in run #21.                                                                                        | Push the fix and follow the new CI run through container smoke and Coolify.         | Open pending remote run                    |

- Known gaps / checks not run: GitHub Actions Linux WebKit, source/coverage/bundle/dependency gates, container smoke, and Coolify deployment have not yet run on this change.
- Required approvals: User authorized the fix push and its CI-gated deployment in this task; no separate release action is planned outside that workflow.
- Rollback or recovery: Revert the focused stylesheet/test commit if the remote run reveals a new regression.
- Release readiness: `NOT READY` until the authorized remote workflow completes deployment.

## 6. Completion record

- Audit created before the task commit and included in that commit.
- Final commit hash: Report in task completion.
- External actions: Push and CI-triggered deployment authorized by the user in this task.

**Limitations:** This is a self-audit, not an independent security review, accessibility certification, or production release approval.
