# Development Audit: WebKit root overflow diagnostics

| Field                    | Value                                                                    |
| ------------------------ | ------------------------------------------------------------------------ |
| Audit ID                 | `2026-10-17T17-35-00Z-webkit-root-overflow-diagnostics`                  |
| Created (UTC)            | 2026-10-17 17:35 UTC                                                     |
| Task / issue             | Identify the Linux WebKit elements expanding the portal document width   |
| Auditor                  | Agent self-audit; GitHub Actions run logs and browser test source review |
| Branch / baseline commit | `master` / `f0df5eb91c0d95517e5999b77c2cd9157f2bf9b5`                    |
| Final commit             | Report in task completion; do not edit this audit after committing       |
| Scope reviewed           | Failure-only responsive overflow diagnostics in two portal browser specs |
| Outcome                  | `PASS WITH RISKS`                                                        |

## 1. Request and outcome

- Requested behavior: Repair the toolbar deployment pipeline and complete its gated deployment workflow.
- Implemented behavior: Include direct body child widths, toolbar host/control sizes, and ancestor overflow containers in the failed WebKit assertion output.
- Acceptance criteria and evidence: Run [#19](https://github.com/Moorky/moorkyz-tools/actions/runs/39053849471) repeated the same three Linux WebKit document-width failures after the mobile control sizing change. Its current diagnostic list is dominated by descendants inside intentional horizontal scroll areas and does not show which element expands the document. The assertion now reports uncontained candidates and their containing geometry for the next remote run.
- Intentional exclusions: No product behavior, public API, dependency or deployment configuration changed.
- Relevant decisions/contracts: Narrow action rows retain native horizontal scrolling; responsive page content must remain within the viewport.

## 2. Change inventory

| Path                              | Change                                                                          | Risk |
| --------------------------------- | ------------------------------------------------------------------------------- | ---- |
| `tests/browser/qa-matrix.spec.js` | Report uncontained page overflow, body children and toolbar geometry on failure | Low  |
| `tests/browser/website.spec.js`   | Report the same geometry for responsive header failures                         | Low  |
| This audit                        | Record why run #19's previous top-right element list was insufficient           | Low  |

## 3. Verification evidence

| Check                        | Result | Exact command / method                                                                                                                                                                                          | Evidence / notes                                                                                                                                                 |
| ---------------------------- | ------ | --------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- | ---------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| Formatting / lint            | PASS   | `pnpm exec prettier --write tests/browser/qa-matrix.spec.js tests/browser/website.spec.js docs/audits/2026-10-17T17-35-00Z-webkit-root-overflow-diagnostics.md`; `pnpm lint`                                    | Prettier and ESLint passed.                                                                                                                                      |
| Typecheck                    | PASS   | `$env:CI='1'; $env:TOOLS_MOORKYZ_COM_FIXTURE_PORT='47973'; $env:TOOLS_MOORKYZ_COM_PORTAL_PORT='47974'; pnpm check` on `f0df5eb`                                                                                     | Full check passed on the immediately preceding commit; this change is browser-test diagnostics only.                                                             |
| Unit / integration tests     | PASS   | Same `pnpm check` command on `f0df5eb`                                                                                                                                                                          | 203 Node tests passed on the immediately preceding commit.                                                                                                       |
| Browser / accessibility      | PASS   | `$env:TOOLS_MOORKYZ_COM_FIXTURE_PORT='47973'; $env:TOOLS_MOORKYZ_COM_PORTAL_PORT='47974'; pnpm exec playwright test tests/browser/qa-matrix.spec.js tests/browser/website.spec.js --project=webkit --reporter=line` | All 14 focused local WebKit tests passed. Run #19 on `f0df5eb` had 252 passed and the same 3 Linux responsive failures; this commit changes only failure output. |
| Build / consumer fixture     | PASS   | Same `pnpm check` command on `f0df5eb`                                                                                                                                                                          | Production build and consumer type checks passed.                                                                                                                |
| Security / dependency review | N/A    | No dependency or security-boundary changes                                                                                                                                                                      | No dependency changed.                                                                                                                                           |
| Performance (if relevant)    | N/A    | Failure-only test diagnostics                                                                                                                                                                                   | No runtime work added.                                                                                                                                           |
| `git diff --check`           | PASS   | `git diff --check`                                                                                                                                                                                              | No whitespace errors.                                                                                                                                            |

## 4. Risk and quality review

- Public contract, feature-default, and compatibility impact: No product or public API changes.
- Security, privacy, permissions, and secrets: Diagnostics inspect only geometry, classes and short text excerpts from the local fixture pages when an assertion fails.
- Accessibility, keyboard, and reduced motion: No behavior changes.
- Disabled-feature work/resource/network behavior: No changes.
- Error handling, cleanup, and recovery: The extra geometry scan runs only after a failed page-width measurement.
- Dependency, performance, and regression exposure: Failure-only browser test output is more detailed; runtime performance is unchanged.

## 5. Findings and residual risks

| ID    | Severity     | Finding and evidence                                                                                                                   | Mitigation / owner                                                                              | Status                  |
| ----- | ------------ | -------------------------------------------------------------------------------------------------------------------------------------- | ----------------------------------------------------------------------------------------------- | ----------------------- |
| TD-14 | Release gate | Run #19 still fails three Linux WebKit checks with document widths 386px at a 320px viewport and 425px at a 390px large-text viewport. | Use uncontained-element and toolbar geometry from the next run to select a specific layout fix. | Open pending remote run |
| TD-11 | Container    | The container and deployment jobs remain skipped after verification failure.                                                           | Complete the Linux verification fix and use the workflow's container build and smoke check.     | Open pending remote run |

- Known gaps / checks not run: The actual Linux overflow source and deployment remain unresolved.
- Required approvals: The user authorized pushing the deployment fix to `master`; deployment remains automated and gated by CI.
- Rollback or recovery: Revert this test-only diagnostic change if it makes failure output unmanageably large; use the run's geometry to adjust the report.
- Release readiness: `NOT READY` until Linux verification, container smoke and deployment complete successfully.

## 6. Completion record

- Audit created before the task commit and included in that commit.
- Final commit hash: Report in task completion.
- External actions: Follow-up push authorized in this task; production deployment remains automated and gated by CI.

**Limitations:** This is a self-audit, not an independent security review, accessibility certification, or production release approval.
