# Development Audit: Fix pnpm 11 noninteractive portal dependency verification

> Scoped agent self-audit with observed verification evidence.

| Field | Value |
|---|---|
| Audit ID | `2026-10-26T17-12-48Z-fix-pnpm-11-noninteractive-portal-dependency-verification` |
| Created (UTC) | `2026-10-26T17:12:48.323Z` |
| Task / issue | Fix pnpm 11 noninteractive portal dependency verification |
| Auditor | Copilot SDK assistant; self-audit using Node.js, pnpm and Git |
| Branch / baseline commit | `master` / `75ebf05add9823cda4f0508a5ed5ecc6ee15937b` |
| Final commit | Report in task completion; do not edit this audit after committing |
| Scope reviewed | pnpm workspace policy, both launcher regression fixtures and directly related CLI documentation |
| Outcome | `PASS WITH RISKS`: all task checks pass; the pre-existing portal became unavailable during verification |

## 1. Request and outcome

- Requested behavior: diagnose and fix the reported background portal startup
  failure, where pnpm automatically installs dependencies and aborts module removal
  because no interactive terminal is available.
- Implemented behavior: move `verifyDepsBeforeRun: error` into
  `pnpm-workspace.yaml`, the pnpm 11 configuration surface. Remove the ineffective
  one-line `.npmrc`. Root and nested portal commands now reject stale dependencies
  explicitly rather than initiating installation.
- Acceptance criteria and evidence: effective configuration reads `error` in both
  directories. Both launchers pass healthy startup/restart/production/readiness
  tests and reject stale dependencies during startup and foreground script runs.
  Rejected operations leave the manifest, lockfile, existing-installation marker
  and workspace-state bytes unchanged, with no new dependency installed.
- Intentional exclusions: no launcher process-control refactor, dependency
  upgrades, shared-workspace installation, log rewriting, running-service restart
  or unrelated portal/Media Recover changes.
- Relevant decisions/contracts: root `AGENTS.md`, current product decisions,
  engineering workflow/testing/security guidance and both existing CLI guides.
  Preserve explicit installation and do not bypass dependency checks.

## 2. Change inventory

| Path | Change | Risk |
|---|---|---|
| `.npmrc` | Remove ignored pnpm setting; no credentials were present | Low |
| `pnpm-workspace.yaml` | Apply the existing error-only verification policy in pnpm 11's supported location | Low |
| `tests/tooling/launcher-startup.test.cjs` | Check real configuration, prepare dependency-free fixtures offline and test stale-dependency rejection in both launchers | Low |
| `docs/moorkyz-cli.md` | Explain explicit installation and pnpm 11 configuration | Low |
| `docs/engineering/tools.moorkyz.com-cli.md` | Explain the root cause and avoid unattended module-purge workarounds | Low |
| This audit | Record observed evidence and limitations | Low |

## 3. Verification evidence

| Check | Result | Exact command / method | Evidence / notes |
|---|---|---|---|
| Root-cause evidence | PASS | `pnpm config get verify-deps-before-run`; `pnpm --dir packages\portal config get verify-deps-before-run`; `pnpm config get verifyDepsBeforeRun`; `pnpm --dir packages\portal config get verifyDepsBeforeRun` before editing | All returned `undefined` despite the old `.npmrc`. Installed pnpm 11.8.0 source confirms the default `install` policy and workspace YAML configuration. |
| Corrected configuration | PASS | `pnpm config get verifyDepsBeforeRun`; `pnpm --dir packages\portal config get verifyDepsBeforeRun` after editing | Both returned `error`; the same assertion is retained in the regression suite. |
| Formatting | PASS | `pnpm exec prettier --write pnpm-workspace.yaml tests\tooling\launcher-startup.test.cjs`; `pnpm exec prettier --check pnpm-workspace.yaml tests\tooling\launcher-startup.test.cjs` | Final check reports all matched files use Prettier style. |
| Lint | PASS | `pnpm exec eslint tests\tooling\launcher-startup.test.cjs` | Exit 0. |
| Typecheck | PASS | `pnpm typecheck` | Root command and filtered portal `tsc --noEmit` exit 0 under the corrected policy. |
| Initial test attempt | FAIL, corrected | `node --test tests\tooling\launcher-startup.test.cjs tests\tooling\tools.moorkyz.com-cli.test.cjs` | 14 passed, 2 failed because dependency-free fixtures do not create `.modules.yaml`. Replaced that assumption with an explicit installation marker and retained real workspace-state byte checks. |
| Focused stale-dependency tests | PASS | `node --test --test-name-pattern "stale dependencies" tests\tooling\launcher-startup.test.cjs` | 2 passed after the fixture correction. |
| Final unit / integration tests | PASS | `node --test tests\tooling\launcher-startup.test.cjs tests\tooling\tools.moorkyz.com-cli.test.cjs` | 16 passed, 0 failed, skipped or cancelled. The final stale dependency is a missing local file reference, not a registry package. |
| Fixture preparation | PASS | `pnpm install --offline --frozen-lockfile --ignore-scripts` in each isolated temporary fixture | Empty reviewed fixture lockfile; no downloads, lifecycle scripts, shared dependency changes or native prerequisites. Test cleanup stops fixture-owned processes and removes only its temporary directory. |
| Initial existing portal check | PASS | `node .\moorkyz.cjs status`; `Invoke-WebRequest -Uri http://127.0.0.1:4320 -UseBasicParsing -TimeoutSec 20` | Initially RUNNING at PID 14184, HTTP 200. No root start/stop command was executed by this task. |
| Later existing portal check | FAIL, environment limitation | Same status and HTTP commands | Later STOPPED / connection refused. Read-only log and listener inspection did not establish the cause; no unrelated process was stopped or restarted. |
| Browser / accessibility | N/A | No browser test run | No UI, accessibility, keyboard or motion behavior changed. |
| Build / complete repository suites | NOT RUN | `pnpm build`, `pnpm test`, `pnpm check`, `pnpm test:browser`, `pnpm check:source` | No application-source changes; focused launcher tests cover the change. Avoid build/dev artifact contention and unrelated ongoing work. |
| Security / dependency review | PASS, scoped self-review | Reviewed the complete task diff and installed pnpm configuration/error paths | No dependency additions/upgrades, permission changes, secrets, hosted processing, telemetry or verification bypasses. No external advisory audit was run because package dependencies are unchanged. |
| Performance | N/A | No benchmark run | One configuration value; no runtime algorithm change. |
| Whitespace | PASS | `git --no-pager diff --check -- .npmrc pnpm-workspace.yaml tests\tooling\launcher-startup.test.cjs docs\moorkyz-cli.md docs\engineering\tools.moorkyz.com-cli.md` | Exit 0; Git reports only existing platform line-ending conversion warnings. |

## 4. Risk and quality review

- Public contract, feature-default, and compatibility impact: enforce the already
  documented installed-dependencies-only policy. pnpm 11.8.0 remains pinned; no
  generic launcher behavior is changed for other package managers.
- Security, privacy, permissions, and secrets: do not set `CI=true` or disable
  purge confirmation for user startup. Test-only CI settings are confined to
  temporary fixtures; setup is offline and ignores lifecycle scripts.
- Accessibility, keyboard, and reduced motion: unchanged.
- Disabled-feature work/resource/network behavior: no new services, connections,
  background activity or optional-feature work. Missing fixture dependencies use
  local paths so regression failures cannot trigger registry downloads.
- Error handling, cleanup, and recovery: preserve pnpm's explicit
  `ERR_PNPM_VERIFY_DEPS_BEFORE_RUN`; stale startup reports failure and STOPPED
  rather than false readiness. Existing transient/timeout cleanup tests pass.
- Dependency, performance, and regression exposure: no dependency or lockfile
  changes; fixtures now prepare installed state deliberately instead of allowing
  pnpm's default auto-install to make startup tests pass accidentally.

## 5. Findings and residual risks

| ID | Severity | Finding and evidence | Mitigation / owner | Status |
|---|---|---|---|---|
| F1 | Medium | The one-line `.npmrc` was ignored by pnpm 11, leaving automatic installation enabled | Move to workspace YAML and test effective root/nested values | Fixed |
| F2 | Low | Old dependency-free launcher fixtures silently relied on pnpm auto-install and never exercised stale dependencies | Offline explicit fixture preparation and byte-preservation assertions | Fixed |
| R1 | Informational | Existing portal became unavailable while unrelated worktree changes continued | Leave shared runtime ownership intact; maintainer can start it deliberately | Observed, cause unverified |

- Known gaps / checks not run: no full build/browser/advisory suite, independent
  security review or live shared-portal restart. Initial unrelated tracked and
  untracked changes, plus later unrelated changes, are excluded from the commit.
- Required approvals: none for this reversible correction; external actions and
  new dependencies remain unauthorized.
- Rollback or recovery: revert this focused commit if necessary. The old behavior
  would restore the automatic-install hazard; prefer retaining the error policy
  and synchronizing dependencies explicitly with `pnpm install --frozen-lockfile`.
  Existing log files are retained rather than cleared to hide historical failures.
- Release readiness: `NOT APPLICABLE`; local development-tooling repair only.

## 6. Completion record

- Audit created before the task commit and included with only task-owned paths.
- Final commit hash: reported in task completion, not backfilled after committing.
- External actions: `NOT AUTHORIZED` unless separately approved.

**Limitations:** An agent self-audit is not an independent security review, accessibility certification, or release approval.
