# Development Audit: Coolify push deployment

| Field                    | Value                                                                                                   |
| ------------------------ | ------------------------------------------------------------------------------------------------------- |
| Audit ID                 | `2026-10-17T01-01-48Z-coolify-push-deploy`                                                              |
| Created (UTC)            | 2026-10-17 01:01 UTC                                                                                    |
| Task / issue             | Prepare `tools.moorkyz.com` for validated push-to-deploy on Coolify                                   |
| Auditor                  | Codex self-audit; local PowerShell, pnpm, Next.js, and Playwright                                       |
| Branch / baseline commit | `master` / `760bffe51183b444a721b7e866607489999435c6`                                                   |
| Final commit             | `PENDING`                                                                                               |
| Scope reviewed           | Next.js standalone build, production container, CI image smoke check, Coolify trigger and setup runbook |
| Outcome                  | `PASS WITH RISKS`                                                                                       |

## 1. Request and outcome

- Requested behavior: Prepare the tools.moorkyz.com repository and server workflow so a valid push deploys to `tools.moorkyz.com`.
- Implemented behavior: The portal builds into a monorepo-aware standalone Next.js output, is packaged in a non-root Node 22 container on port 3000, and receives a CI container build/smoke check. Successful current-head `master` pushes invoke Coolify's authenticated deploy webhook only after repository checks and the image smoke check pass; stale workflow runs are skipped.
- Acceptance criteria and evidence: `pnpm check` passed (lint, formatting, production build, 86 Node tests, 15 Playwright tests, and workspace typechecks). The standalone server returned HTTP 200 in a local smoke request. Formatting and `git diff --check` passed.
- Intentional exclusions: The deployment setup task does not publish the npm package or provision/modify a Coolify application. The same merge also includes a separate M8 Theme Studio preview, covered by its own audit. At audit creation time, no remote push had occurred.
- Relevant decisions/contracts: Root `AGENTS.md` requires standalone deployment evidence and explicit deployment authorization. The user explicitly requested push-triggered deployment setup in this task.

## 2. Change inventory

| Path                                                                                                               | Change                                                                                               | Risk   |
| ------------------------------------------------------------------------------------------------------------------ | ---------------------------------------------------------------------------------------------------- | ------ |
| `Dockerfile`, `.dockerignore`                                                                                      | Multi-stage standalone production image, port 3000, non-root runtime, HTTP health check              | Medium |
| `packages/portal/next.config.ts`, `packages/portal/package.json`, `packages/portal/scripts/prepare-standalone.mjs` | Monorepo tracing and copy static/public into standalone output                                       | Medium |
| `.github/workflows/ci.yml`                                                                                         | Build and smoke-test image; invoke Coolify after successful `master` checks using repository secrets | Medium |
| `playwright.config.mjs`, `tests/browser/portal.spec.js`                                                            | Test the standalone production server and allow a separate portal test port                          | Low    |
| `.prettierrc.json`, `package.json`                                                                                 | Preserve platform line endings so formatting checks work on Windows and Linux                        | Low    |
| `README.md`, `docs/deployment/coolify.md`                                                                          | Link and document the Coolify application, DNS, ports, and required secrets                          | Low    |
| `docs/audits/2026-10-17T01-01-48Z-coolify-push-deploy.md`                                                          | Record this task's evidence and remaining infrastructure work                                        | Low    |

## 3. Verification evidence

| Check                        | Result          | Exact command / method                                     | Evidence / notes                                                                                                                                                          |
| ---------------------------- | --------------- | ---------------------------------------------------------- | ------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| Formatting / lint            | PASS            | `$env:CI='true'; $env:PORTAL_TEST_PORT='4175'; pnpm check` | ESLint and `prettier --check` passed. Explicit Prettier checks for the README, Coolify guide, and workflow passed.                                                        |
| Typecheck                    | PASS            | `pnpm check`; also `pnpm typecheck`                        | SDK, consumer fixtures, and portal TypeScript checks passed.                                                                                                          |
| Unit / integration tests     | PASS            | `pnpm check`                                               | 86 Node tests passed.                                                                                                                                                     |
| Browser / accessibility      | PASS            | `pnpm check`                                               | 15 Playwright tests passed, including portal CSP and axe checks. The configurable port avoided reusing an unrelated Moorkyz AI server on port 4174.                       |
| Build / consumer fixture     | PASS WITH RISKS | `pnpm check`; standalone server smoke request              | Next production build passed; standalone server returned HTTP 200. A Docker image build could not run locally because Docker Desktop's Linux engine pipe was unavailable. |
| Security / dependency review | PASS            | Review of Dockerfile, workflow, and secrets handling       | No credentials or new dependencies added; Coolify values are referenced only as GitHub Actions secrets.                                                                   |
| Performance (if relevant)    | N/A             | N/A                                                        | No performance behavior changed.                                                                                                                                          |
| `git diff --check`           | PASS            | `git diff --check`                                         | No whitespace errors.                                                                                                                                                     |

## 4. Risk and quality review

- Public contract, feature-default, and compatibility impact: None.
- Security, privacy, permissions, and secrets: The runtime uses the non-root `node` user. Deployment credentials must be stored as `COOLIFY_WEBHOOK` and `COOLIFY_TOKEN` GitHub repository secrets and are never embedded in the image.
- Accessibility, keyboard, and reduced motion: No UI behavior changed; existing browser accessibility checks passed.
- Disabled-feature work/resource/network behavior: No change.
- Error handling, cleanup, and recovery: CI smoke test removes the test container even on failure. Coolify rollback is documented.
- Dependency, performance, and regression exposure: No dependency additions. Docker image size and deployment behavior remain unverified until a Docker-enabled runner and Coolify app execute the workflow.

## 5. Findings and residual risks

| ID   | Severity | Finding and evidence                                                                                                              | Mitigation / owner                                                                    | Status |
| ---- | -------- | --------------------------------------------------------------------------------------------------------------------------------- | ------------------------------------------------------------------------------------- | ------ |
| CD-1 | Medium   | Coolify app, deploy webhook, token, and GitHub secrets are not configured; no management connection is available in this session. | Create the app and add the two secrets using `docs/deployment/coolify.md`.            | Open   |
| CD-2 | Medium   | Local Docker image build could not run because `dockerDesktopLinuxEngine` was unavailable.                                        | The new GitHub CI `container` job builds and smoke-tests the image before deployment. | Open   |

- Known gaps / checks not run: No actual image build, Coolify deployment, TLS provisioning, or production HTTP verification. The currently configured public host previously returned 503/404 before the Coolify app existed.
- Required approvals: The user explicitly authorized this push, including the possibility that the configured CI workflow may deploy to Coolify. Actual deployment remains conditional on CI, a configured Coolify application, and repository secrets.
- Rollback or recovery: Revert the task commit to remove this setup; after provisioning, use Coolify deployment history to roll back a failed app release.
- Release readiness: `READY WITH CONDITIONS`

## 6. Completion record

- Audit created before the task commit and included in that commit.
- Final commit hash: `PENDING`
- External actions: User-authorized push is part of task completion. No manual Coolify provisioning or secret handling is performed by this agent; workflow status must be checked after push.

**Limitations:** An agent self-audit is not an independent security review, accessibility certification, or release approval.
