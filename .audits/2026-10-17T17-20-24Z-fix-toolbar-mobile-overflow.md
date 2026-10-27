# Development Audit: Fix Toolbar mobile overflow

| Field                    | Value                                                                     |
| ------------------------ | ------------------------------------------------------------------------- |
| Audit ID                 | `2026-10-17T17-20-24Z-fix-toolbar-mobile-overflow`                        |
| Created (UTC)            | 2026-10-17 17:20 UTC                                                      |
| Task / issue             | Fix the Linux WebKit responsive overflow gate blocking toolbar deployment |
| Auditor                  | Agent self-audit; GitHub Actions logs and local WebKit geometry review    |
| Branch / baseline commit | `master` / `7a8c888dd867d596a1959f4b80037925a672e165`                     |
| Final commit             | Report in task completion; do not edit this audit after committing        |
| Scope reviewed           | Small viewport toolbar control sizing and large-text reflow               |
| Outcome                  | `PASS WITH RISKS`                                                         |

## 1. Request and outcome

- Requested behavior: Repair the toolbar deployment pipeline and complete its gated deployment workflow.
- Implemented behavior: Keep mobile scroll and collapse controls within the available toolbar width; use compact visible symbols for collapse/restore while retaining descriptive accessible names.
- Acceptance criteria and evidence: Run [#18](https://github.com/Moorky/moorkyz-tools/actions/runs/39052209415) showed the collapse control extending beyond the viewport under Linux WebKit and 200% text. A local WebKit geometry probe with the proposed mobile rules kept the navigation dock within 320px and 390px viewports; the 200% text probe at 390px had no document overflow. CI verification is pending for the source change.
- Intentional exclusions: No public API, feature default, persistence namespace, dependency or deployment configuration changed.
- Relevant decisions/contracts: Narrow action rows use native horizontal scrolling and accessible controls; keyboard access and large-text reflow remain required.

## 2. Change inventory

| Path                               | Change                                                                                                                        | Risk |
| ---------------------------------- | ----------------------------------------------------------------------------------------------------------------------------- | ---- |
| `packages/sdk/src/toolbar.css` | Keep narrow-screen scroll/collapse controls at 44px and show an accessible-name-backed plus/minus symbol for collapse/restore | Low  |
| `tests/browser/qa-matrix.spec.js`  | Check collapse control bounds, target size, accessible name and mobile symbol in responsive and large-text audits             | Low  |
| This audit                         | Record Linux CI evidence and the proposed responsive fix                                                                      | Low  |

## 3. Verification evidence

| Check                        | Result | Exact command / method                                                                                                                        | Evidence / notes                                                                                                                                                                                |
| ---------------------------- | ------ | --------------------------------------------------------------------------------------------------------------------------------------------- | ----------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| Formatting / lint            | PASS   | `$env:CI='1'; $env:TOOLS_MOORKYZ_COM_FIXTURE_PORT='47973'; $env:TOOLS_MOORKYZ_COM_PORTAL_PORT='47974'; pnpm check`                                | The full repository gate passed lint and Prettier.                                                                                                                                              |
| Typecheck                    | PASS   | Same `pnpm check` command                                                                                                                     | Toolbar, consumer fixtures, and portal type checks passed.                                                                                                                                      |
| Unit / integration tests     | PASS   | Same `pnpm check` command                                                                                                                     | All 203 Node tests passed.                                                                                                                                                                      |
| Browser / accessibility      | PASS   | `pnpm exec playwright test tests/browser/qa-matrix.spec.js tests/browser/website.spec.js --project=webkit --reporter=line`; full `pnpm check` | Focused WebKit passed all 14 tests; full Chromium, Firefox, and Windows WebKit matrix passed all 255 browser tests, including the mobile symbol and 200% text checks. Linux CI remains pending. |
| Build / consumer fixture     | PASS   | Same `pnpm check` command                                                                                                                     | Production portal build and consumer type checks passed after the source change.                                                                                                                |
| Security / dependency review | N/A    | No dependency or security-boundary changes                                                                                                    | No dependency changed; GitHub CI retains dependency and source scans.                                                                                                                           |
| Performance (if relevant)    | N/A    | CSS-only responsive control sizing                                                                                                            | No runtime work added.                                                                                                                                                                          |
| `git diff --check`           | PASS   | `git diff --check`                                                                                                                            | No whitespace errors.                                                                                                                                                                           |

## 4. Risk and quality review

- Public contract, feature-default, and compatibility impact: No API or configuration changes. Narrow screens switch the visible collapse label to a symbol; `aria-label` continues to identify the action.
- Security, privacy, permissions, and secrets: No changes.
- Accessibility, keyboard, and reduced motion: Controls retain 44px minimum targets and accessible names; action scrolling and existing keyboard behavior are unchanged.
- Disabled-feature work/resource/network behavior: No changes.
- Error handling, cleanup, and recovery: No new work or resources.
- Dependency, performance, and regression exposure: CSS-only; the responsive browser audit checks geometry and the symbol.

## 5. Findings and residual risks

| ID    | Severity     | Finding and evidence                                                                                                                                   | Mitigation / owner                                                       | Status                          |
| ----- | ------------ | ------------------------------------------------------------------------------------------------------------------------------------------------------ | ------------------------------------------------------------------------ | ------------------------------- |
| TD-12 | Release gate | Linux WebKit run #18 found the toolbar controls extending beyond the small viewport; large text also enlarged fixed controls enough to force overflow. | Keep mobile utility controls compact and validate on Linux CI.           | Fix locally; pending remote run |
| TD-11 | Container    | Local Docker Engine is unavailable; prior runs skipped the production image and deployment after verification failed.                                  | Use the GitHub container build and smoke check once verification passes. | Open pending remote run         |

- Known gaps / checks not run: Local verification passed on Windows; Linux CI, container smoke and production deployment remain pending.
- Required approvals: The user authorized pushing the deployment fix to `master`; deployment remains automated and gated.
- Rollback or recovery: Revert the focused responsive CSS/test commit if it regresses toolbar layout; keep the CI evidence in the task audit.
- Release readiness: `NOT READY` until remote verification, container smoke and deployment complete successfully.

## 6. Completion record

- Audit created before the task commit and included in that commit.
- Final commit hash: Report in task completion.
- External actions: Follow-up push authorized in this task; production deployment remains automated and gated by CI.

**Limitations:** This is a self-audit, not an independent security review, accessibility certification, or production release approval.
