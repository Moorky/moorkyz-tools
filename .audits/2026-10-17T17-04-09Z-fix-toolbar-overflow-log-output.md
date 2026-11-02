# Development Audit: Fix Toolbar overflow log output

| Field                    | Value                                                                           |
| ------------------------ | ------------------------------------------------------------------------------- |
| Audit ID                 | `2026-10-17T17-04-09Z-fix-toolbar-overflow-log-output`                          |
| Created (UTC)            | 2026-10-17 17:04 UTC                                                            |
| Task / issue             | Make Linux WebKit overflow diagnostics visible in the toolbar deployment logs   |
| Auditor                  | Agent self-audit; GitHub Actions run logs and local source review               |
| Branch / baseline commit | `master` / `12e9939c5e45cf018c5ab3794c42ec62b1ecd35f`                           |
| Final commit             | Report in task completion                                                       |
| Scope reviewed           | Playwright matcher output for failure-only responsive page-overflow diagnostics |
| Outcome                  | `PASS WITH RISKS`                                                               |

## 1. Request and outcome

- Requested behavior: Repair the toolbar deployment pipeline and complete its gated deployment workflow.
- Implemented behavior: Return the full width and overflowing-element diagnostic object as the actual failed `expect.poll` value so Playwright includes it in the CI error output.
- Acceptance criteria and evidence: Run [#17](https://github.com/Moorky/moorkyz-tools/actions/runs/39051691626) reproduced the same three Linux WebKit overflow failures. The first diagnostic attempt returned the details, but `toMatchObject` only printed the mismatched `fits` field. The matcher now returns the full diagnostic object on failure. Local lint, formatting and all 14 focused WebKit browser cases pass. The layout cause remains unknown until the next remote run.
- Intentional exclusions: No product behavior, public API, package dependency, feature default, persistence namespace or infrastructure setting changed.
- Relevant decisions/contracts: WCAG 2.2 AA and keyboard reachability remain required. Deployment stays gated on successful verification and container smoke checks.

## 2. Change inventory

| Path                              | Change                                                                     | Risk |
| --------------------------------- | -------------------------------------------------------------------------- | ---- |
| `tests/browser/qa-matrix.spec.js` | Return the complete page-overflow state when its assertion fails           | Low  |
| `tests/browser/website.spec.js`   | Return the complete page-overflow state for responsive header failures     | Low  |
| This audit                        | Record why the previous diagnostic attempt did not expose its measurements | Low  |

## 3. Verification evidence

| Check                        | Result  | Exact command / method                                                                                                                                                                                          | Evidence / notes                                                                                                   |
| ---------------------------- | ------- | --------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- | ------------------------------------------------------------------------------------------------------------------ |
| Formatting / lint            | PASS    | `pnpm lint`; `pnpm exec prettier --check tests/browser/qa-matrix.spec.js tests/browser/website.spec.js`                                                                                                         | ESLint and Prettier passed.                                                                                        |
| Typecheck                    | N/A     | No TypeScript or production source changes                                                                                                                                                                      | The parent commit's full `pnpm check` passed type checks.                                                          |
| Unit / integration tests     | N/A     | No library or unit-test changes                                                                                                                                                                                 | The parent commit's full `pnpm check` passed 203 unit tests.                                                       |
| Browser / accessibility      | PASS    | `$env:TOOLS_MOORKYZ_COM_FIXTURE_PORT='47973'; $env:TOOLS_MOORKYZ_COM_PORTAL_PORT='47974'; pnpm exec playwright test tests/browser/qa-matrix.spec.js tests/browser/website.spec.js --project=webkit --reporter=line` | All 14 targeted local WebKit cases passed.                                                                         |
| Build / consumer fixture     | N/A     | No production source or build configuration changes                                                                                                                                                             | The parent commit's full `pnpm check` passed the production build and consumer type checks.                        |
| Security / dependency review | N/A     | No dependency, permission, secret-handling or production-source changes                                                                                                                                         | The parent commit's source scan and dependency audit passed.                                                       |
| Performance (if relevant)    | N/A     | Test failure diagnostics only                                                                                                                                                                                   | No runtime code changed.                                                                                           |
| License inventory            | N/A     | No dependency changes                                                                                                                                                                                           | The parent commit's license inventory exited 0.                                                                    |
| `git diff --check`           | PASS    | `git diff --check`                                                                                                                                                                                              | No whitespace errors.                                                                                              |
| Container build / smoke      | NOT RUN | `docker version --format '{{.Server.Version}}'`                                                                                                                                                                 | Local Docker Desktop Linux engine pipe is unavailable. Run #17 skipped container and deploy after `verify` failed. |
| Remote verify / deployment   | NOT RUN | GitHub Actions run following this diagnostics correction                                                                                                                                                        | A new remote run must show the failing DOM geometry before the source fix can be selected.                         |

## 4. Risk and quality review

- Public contract, feature-default, and compatibility impact: No API, configuration or feature-default changes.
- Security, privacy, permissions, and secrets: No changes. Failure-only diagnostic values include a short text excerpt and geometry from static test pages.
- Accessibility, keyboard, and reduced motion: No behavior changed; the targeted local WebKit suite covers responsive, large-text, keyboard and axe checks.
- Disabled-feature work/resource/network behavior: No changes.
- Error handling, cleanup, and recovery: No new listeners or asynchronous work; the overflow scan runs only after a failed measurement.
- Dependency, performance, and regression exposure: No runtime code or dependency changes. Extra DOM inspection occurs only for failed browser assertions.

## 5. Findings and residual risks

| ID    | Severity     | Finding and evidence                                                                                            | Mitigation / owner                                                                        | Status                  |
| ----- | ------------ | --------------------------------------------------------------------------------------------------------------- | ----------------------------------------------------------------------------------------- | ----------------------- |
| TD-10 | Release gate | Run #17 repeated three Linux WebKit overflow failures; Playwright suppressed extra fields with `toMatchObject`. | Use the full diagnostic value from the next remote run to make a targeted layout fix.     | Open pending remote run |
| TD-11 | Container    | Local Docker Engine is unavailable; run #17 skipped its image build and HTTP smoke steps.                       | Use the GitHub workflow's container build and HTTP smoke check after verification passes. | Open pending remote run |

- Known gaps / checks not run: The source layout cause and remote verification are still pending.
- Required approvals: The user authorized pushing the deployment fix to `master`; deployment remains automated and gated.
- Rollback or recovery: Revert this focused diagnostic-output commit if it introduces test noise; keep the remote failure evidence in the task notes.
- Release readiness: `NOT READY` until remote verification, container smoke and deployment complete successfully.

## 6. Completion record

- Audit created before the task commit and included in that commit.
- Final commit hash: Report in task completion.
- External actions: Follow-up push authorized in this task; production deployment remains automated and gated by CI.

**Limitations:** This is a self-audit, not an independent security review, accessibility certification, or production release approval.
