# Development Audit: Serialize WebKit overflow diagnostics

| Field                    | Value                                                                          |
| ------------------------ | ------------------------------------------------------------------------------ |
| Audit ID                 | `2026-10-17T17-45-09Z-serialize-webkit-overflow-diagnostics`                   |
| Created (UTC)            | 2026-10-17 17:45 UTC                                                           |
| Task / issue             | Preserve Linux WebKit overflow geometry in GitHub Actions logs                 |
| Auditor                  | Agent self-audit; GitHub Actions run #20 output and browser test source review |
| Branch / baseline commit | `master` / `b941ad6a1069f726c1ad6ac2cbac4477bd95f133`                          |
| Final commit             | Report in task completion; do not edit this audit after committing             |
| Scope reviewed           | Failure-only overflow state formatting in two portal browser specs             |
| Outcome                  | `PASS WITH RISKS`                                                              |

## 1. Request and outcome

- Requested behavior: Repair the toolbar deployment pipeline and complete its gated deployment workflow.
- Implemented behavior: Return the detailed overflow state as a JSON string when responsive page-width assertions fail, so Playwright does not replace nested geometry with `[Object]` placeholders.
- Acceptance criteria and evidence: Run [#20](https://github.com/Moorky/moorkyz-tools/actions/runs/39054613435) still reproduced the same three Linux WebKit failures. The uncontained-element report was present, but Playwright's matcher output collapsed its nested arrays. The callback now serializes the complete state for the next CI run.
- Intentional exclusions: No product behavior, public API, dependency or deployment configuration changed.
- Relevant decisions/contracts: Keep page content within the viewport while native toolbar action scrolling remains operable.

## 2. Change inventory

| Path                              | Change                                                                    | Risk |
| --------------------------------- | ------------------------------------------------------------------------- | ---- |
| `tests/browser/qa-matrix.spec.js` | Serialize diagnostic state on responsive and large-text overflow failures | Low  |
| `tests/browser/website.spec.js`   | Serialize diagnostic state on header overflow failures                    | Low  |
| This audit                        | Record why nested measurements were not readable in run #20               | Low  |

## 3. Verification evidence

| Check                        | Result | Exact command / method                                                                                                                                                                                          | Evidence / notes                                                                                      |
| ---------------------------- | ------ | --------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- | ----------------------------------------------------------------------------------------------------- |
| Formatting / lint            | PASS   | `pnpm exec prettier --write tests/browser/qa-matrix.spec.js tests/browser/website.spec.js docs/audits/2026-10-17T17-45-09Z-serialize-webkit-overflow-diagnostics.md`; `pnpm lint`                               | Prettier and ESLint passed.                                                                           |
| Typecheck                    | PASS   | `$env:CI='1'; $env:TOOLS_MOORKYZ_COM_FIXTURE_PORT='47973'; $env:TOOLS_MOORKYZ_COM_PORTAL_PORT='47974'; pnpm check` on `f0df5eb`                                                                                     | Full check passed on the immediately preceding product change; current edit affects only test output. |
| Unit / integration tests     | PASS   | Same `pnpm check` command on `f0df5eb`                                                                                                                                                                          | All 203 Node tests passed on the immediately preceding product change.                                |
| Browser / accessibility      | PASS   | `$env:TOOLS_MOORKYZ_COM_FIXTURE_PORT='47973'; $env:TOOLS_MOORKYZ_COM_PORTAL_PORT='47974'; pnpm exec playwright test tests/browser/qa-matrix.spec.js tests/browser/website.spec.js --project=webkit --reporter=line` | All 14 focused WebKit tests passed; serialization only runs on failure.                               |
| Build / consumer fixture     | PASS   | Same `pnpm check` command on `f0df5eb`                                                                                                                                                                          | Production build and consumer type checks passed.                                                     |
| Security / dependency review | N/A    | No dependency or security-boundary changes                                                                                                                                                                      | No dependency changed.                                                                                |
| Performance (if relevant)    | N/A    | Failure-only test diagnostics                                                                                                                                                                                   | No runtime work added.                                                                                |
| `git diff --check`           | PASS   | `git diff --check`                                                                                                                                                                                              | No whitespace errors.                                                                                 |

## 4. Risk and quality review

- Public contract, feature-default, and compatibility impact: No product or public API changes.
- Security, privacy, permissions, and secrets: The failure-only report contains geometry, CSS classes and short text excerpts from static portal pages.
- Accessibility, keyboard, and reduced motion: No behavior changes.
- Disabled-feature work/resource/network behavior: No changes.
- Error handling, cleanup, and recovery: No new resources; report generation runs only after a failed assertion.
- Dependency, performance, and regression exposure: Diagnostic strings are emitted only on failure.

## 5. Findings and residual risks

| ID    | Severity     | Finding and evidence                                                                                                      | Mitigation / owner                                                                   | Status                  |
| ----- | ------------ | ------------------------------------------------------------------------------------------------------------------------- | ------------------------------------------------------------------------------------ | ----------------------- |
| TD-14 | Release gate | Run #20 failed the same three Linux WebKit document-width checks; nested diagnostics were shortened in Playwright output. | Emit the geometry as a string and use the next run to choose a specific layout fix.  | Open pending remote run |
| TD-11 | Container    | Container and deployment jobs remain skipped while verification fails.                                                    | Complete Linux verification and pass the workflow's container build and smoke check. | Open pending remote run |

- Known gaps / checks not run: Actual Linux overflow cause and deployment remain unresolved.
- Required approvals: The user authorized pushing the deployment fix to `master`; deployment remains automated and gated by CI.
- Rollback or recovery: Revert this test-only output change if the serialized failure report is too large; preserve the next run's measured cause.
- Release readiness: `NOT READY` until Linux verification, container smoke and deployment complete successfully.

## 6. Completion record

- Audit created before the task commit and included in that commit.
- Final commit hash: Report in task completion.
- External actions: Follow-up push authorized in this task; production deployment remains automated and gated by CI.

**Limitations:** This is a self-audit, not an independent security review, accessibility certification, or production release approval.
