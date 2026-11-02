# Development Audit: Fix Toolbar deployment gate

| Field                    | Value                                                                                                          |
| ------------------------ | -------------------------------------------------------------------------------------------------------------- |
| Audit ID                 | `2026-10-17T15-38-51Z-fix-tools-deployment`                                                                  |
| Created (UTC)            | 2026-10-17 15:38 UTC                                                                                           |
| Task / issue             | Fix the CI failures blocking Toolbar deployment                                                                |
| Auditor                  | Codex self-audit; GitHub Actions logs and local source review                                                  |
| Branch / baseline commit | `master` / `15043ff6365a000752e0c527f6076e2171276668`                                                          |
| Final commit             | Report in task completion                                                                                      |
| Scope reviewed           | WebKit theme contrast, responsive portal header, keyboard access to toolbar action scrolling, CI release gates |
| Outcome                  | `PASS WITH RISKS`                                                                                              |

## 1. Request and outcome

- Requested behavior: Repair the Toolbar deployment pipeline.
- Implemented behavior: Address the WebKit accessibility and responsive failures that stop `verify`, and make the overflowing action strip keyboard-focusable with a visible focus indicator.
- Acceptance criteria and evidence: GitHub Actions run [#13](https://github.com/Moorky/moorkyz-tools/actions/runs/39043787864) at commit `15043ff` failed in `pnpm check`; 249 browser cases passed and six WebKit cases failed. The `container` and `Deploy to Coolify` jobs were skipped. The fixes target the reported color contrast, page overflow, and `scrollable-region-focusable` failures. A CI-equivalent local `pnpm check` passed all 255 browser cases across Chromium, Firefox, and WebKit; the focused WebKit keyboard regression also passed.
- Intentional exclusions: No push, production deployment, package publication, or infrastructure change.
- Relevant decisions/contracts: WCAG 2.2 AA and keyboard reachability remain mandatory. No public API or dependency changed.

## 2. Change inventory

| Path                                  | Change                                                                                         | Risk   |
| ------------------------------------- | ---------------------------------------------------------------------------------------------- | ------ |
| `packages/portal/src/app/globals.css` | Apply the dark foreground token directly to the body theme scope for WebKit inheritance        | Low    |
| `packages/portal/src/app/website.css` | Constrain and wrap the brand and navigation row on narrow screens                              | Low    |
| `packages/sdk/src/toolbar.ts`     | Label the action strip as a group and make it keyboard-focusable only while it overflows       | Medium |
| `packages/sdk/src/toolbar.css`    | Show keyboard focus on the scrollable action strip, including forced-colors mode               | Low    |
| `tests/browser/toolbar.spec.js`       | Assert that an overflowing action group receives focus and leaves the tab order after widening | Low    |
| This audit                            | Record CI evidence, changes, and remaining gates                                               | Low    |

## 3. Verification evidence

| Check                            | Result  | Exact command / method                                                                                                                                                                                                                                    | Evidence / notes                                                                                                                                                                                                                                                     |
| -------------------------------- | ------- | --------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- | -------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| Formatting / lint                | PASS    | `pnpm lint`; `pnpm format:check`; `pnpm exec prettier --check docs/audits/2026-10-17T15-38-51Z-fix-tools-deployment.md`                                                                                                                                 | ESLint and Prettier passed, including the audit file.                                                                                                                                                                                                                |
| Typecheck                        | PASS    | `$env:CI='1'; $env:TOOLS_MOORKYZ_COM_FIXTURE_PORT='47973'; $env:TOOLS_MOORKYZ_COM_PORTAL_PORT='47974'; pnpm check`                                                                                                                                            | SDK, portal, and vanilla, React, Vue, and Angular consumer TypeScript checks passed.                                                                                                                                                                             |
| Unit / integration tests         | PASS    | `$env:CI='1'; $env:TOOLS_MOORKYZ_COM_FIXTURE_PORT='47973'; $env:TOOLS_MOORKYZ_COM_PORTAL_PORT='47974'; pnpm check`; `pnpm test:coverage`                                                                                                                      | 203 tests passed. Coverage gate passed: 94.79% core branch coverage and 86.40% emitted-library overall branch coverage.                                                                                                                                              |
| Browser / accessibility          | PASS    | CI-configured `pnpm check`; `$env:CI='1'; $env:TOOLS_MOORKYZ_COM_FIXTURE_PORT='47973'; $env:TOOLS_MOORKYZ_COM_PORTAL_PORT='47974'; pnpm exec playwright test --project=webkit tests/browser/toolbar.spec.js --grep 'keeps the dock within a narrow viewport'` | CI-configured run passed all 255 browser cases across three engines. The focused WebKit keyboard regression passed after its addition. An unthrottled local run at 8 workers had two timeout failures from resource saturation; the CI configuration uses 3 workers. |
| Build / consumer fixtures        | PASS    | `$env:CI='1'; $env:TOOLS_MOORKYZ_COM_FIXTURE_PORT='47973'; $env:TOOLS_MOORKYZ_COM_PORTAL_PORT='47974'; pnpm check`                                                                                                                                            | Production build and consumer fixtures passed.                                                                                                                                                                                                                       |
| Container build / smoke          | NOT RUN | `docker version --format '{{.Server.Version}}'`                                                                                                                                                                                                           | Local Docker Engine pipe is unavailable, so the production image and container HTTP smoke request could not run locally.                                                                                                                                             |
| Security / dependency review     | PASS    | `pnpm check:source`; `pnpm audit --audit-level=low`                                                                                                                                                                                                       | Source scan checked 256 paths; dependency audit reported no known vulnerabilities. This is not a complete security audit.                                                                                                                                            |
| Performance / bundle measurement | PASS    | `pnpm measure:bundles`                                                                                                                                                                                                                                    | All measurements completed; the repository script reports a baseline and does not enforce budgets.                                                                                                                                                                   |
| License inventory                | PASS    | `pnpm licenses list --json`                                                                                                                                                                                                                               | Command exited 0 and emitted the dependency license inventory.                                                                                                                                                                                                       |
| `git diff --check`               | PASS    | `git diff --check`                                                                                                                                                                                                                                        | No whitespace errors.                                                                                                                                                                                                                                                |

## 4. Risk and quality review

- Public contract, feature-default, and compatibility impact: No API or feature-default changes. The internal action container gains an accessible group name and enters the tab order only when its content overflows.
- Security, privacy, permissions, and secrets: No changes.
- Accessibility, keyboard, and reduced motion: Scrollable toolbar actions can receive keyboard focus and show a focus ring; the browser regression covers both overflow and non-overflow states.
- Disabled-feature work/resource/network behavior: No changes.
- Error handling, cleanup, and recovery: No new resource or listener lifecycle.
- Dependency, performance, and regression exposure: No dependency additions. Bundle measurements completed without new budgets.

## 5. Findings and residual risks

| ID   | Severity     | Finding and evidence                                                                                                                  | Mitigation / owner                                                                | Status                                      |
| ---- | ------------ | ------------------------------------------------------------------------------------------------------------------------------------- | --------------------------------------------------------------------------------- | ------------------------------------------- |
| TD-1 | Release gate | Run #13 failed `verify`. The equivalent CI check now passes locally at three workers, but GitHub Actions has not run for this change. | Confirm the new remote CI run after an authorized push.                           | Mitigated locally; remote verification open |
| TD-2 | Container    | Local Docker Engine is unavailable, so the production image build and smoke test remain unverified locally.                           | Run the workflow's container job in GitHub Actions.                               | Open                                        |
| TD-3 | Deployment   | No new commit was pushed; production state was not verified or changed.                                                               | Push and deployment require explicit authorization under repository instructions. | Open                                        |

- Known gaps / checks not run: Local Docker image build/container smoke and GitHub Actions verification for the new commit. No live HTTP check was possible without the image or a remote deployment.
- Required approvals: Explicit authorization is required before pushing or triggering production deployment.
- Rollback or recovery: Revert the focused changes in this task if the remote checks identify a regression.
- Release readiness: `NOT READY` until the remote verify and container jobs pass.

## 6. Completion record

- Audit created before the task commit and included in that commit.
- Final commit hash: Report in task completion.
- External actions: No push or deployment performed.

**Limitations:** This is a self-audit, not an independent security review, accessibility certification, or production release approval.
