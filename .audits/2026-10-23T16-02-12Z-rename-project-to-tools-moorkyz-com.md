# Development Audit: Rename project to tools.moorkyz.com

> Agent self-audit of the local project identity rename and its verification.

| Field | Value |
|---|---|
| Audit ID | `2026-10-23T16-02-12Z-rename-project-to-tools-moorkyz-com` |
| Created (UTC) | `2026-10-23T16:02:12.554Z` |
| Task / issue | Rename project to tools.moorkyz.com |
| Auditor | Copilot in VS Code; self-audit |
| Branch / baseline commit | `master` / `a84eb08` |
| Final commit | Report in task completion; do not edit this audit after committing |
| Scope reviewed | Repository branding, packages, CLI/state, environment variables, CI/deployment references, assets, historical docs and rename regressions |
| Outcome | `PASS WITH RISKS`: required project gates pass; optional native VideoCrunch browser suite fails as recorded below |

## 1. Request and outcome

- Requested behavior: fully rename the project and every project reference to
  `tools.moorkyz.com`; retain feature-specific terminology only for the
  standalone Toolbar Builder.
- User clarification: complete local identifier/filename/environment rename;
  repository links target `Moorky/tools.moorkyz.com`, but do not change GitHub
  or the Git remote. Preserve author/third-party attribution and the separate
  moorkyz.com footer credit.
- Implemented behavior: renamed the workspace and portal package, visible brand,
  canonical/social metadata, manifest, local logo path, launchers/configuration,
  ignored runtime directory, theme key, service flags, test fixtures, Docker/CI
  identifiers and documentation, including historical audits. Retired SDK
  package/path project-name segments are normalized to `sdk`.
- Acceptance evidence: the identity regression scans tracked and unignored text
  and filenames, including historical records, with zero retired identifiers.
  Browser assertions verify the rendered brand, GitHub target, canonical/social
  URLs, image, manifest, per-tool title and persisted renamed theme preference.
- Existing development supervisor PID 32456 was verified running and stopped
  with the user's explicit permission before builds and state renaming. Its
  existing log was preserved in the renamed ignored runtime directory. The
  original portal remains stopped; verification servers were test-owned.
- Intentional exclusions: Git history, actual GitHub repository/remote and
  checkout directory, DNS, Coolify, deployment, publishing, attribution, dependency
  versions, ignored dependency/build/test caches and existing runtime log contents.
  Generated source downloads were rebuilt from their existing allowlists.
- Relevant authority: [root contract](../../AGENTS.md),
  [current decisions](../decisions/decision-register.md),
  [testing](../engineering/testing.md), and the user's scope clarifications.

## 2. Change inventory

| Path | Change | Risk |
|---|---|---|
| Root manifests, launchers/config, ignore/attribute rules | Consistent project/package/command/runtime identities | Medium: command and environment names intentionally change |
| Portal metadata, header, manifest, mark, theme and tool setup/gates | Consistent visible identity, URLs and opt-in flag consumers | Medium: existing theme preference is not migrated |
| CI and deployment guide | New image/concurrency names, target domain/DNS label and repository links | Medium: external migration remains manual |
| Root/project instructions, guides and historical audits | Normalize project identifiers while preserving feature and legal attribution | Low: original historical evidence remains in Git |
| Identity/CLI/builder/proxy/browser tests and Playwright configs | Enforce zero retired project identifiers; verify renamed enabled/disabled behavior | Low |
| This audit | Evidence, residual failures and external-action boundaries | Low |

## 3. Verification evidence

| Check | Result | Exact command / method | Evidence / notes |
|---|---|---|---|
| Complete configured gate | PASS | `$env:CI='1'; $env:TOOLS_MOORKYZ_COM_PORTAL_PORT='48793'; pnpm check` | Final run passes lint, formatting, production build, 47 Node tests, 21 Chromium/Firefox/WebKit tests and typecheck |
| Formatting / lint | PASS | `pnpm lint`; `pnpm format:check` via `pnpm check` | Configured repository checks pass |
| Typecheck | PASS | `pnpm typecheck` directly and via `pnpm check` | Renamed portal filter selects the package; strict TypeScript passes |
| Identity and CLI | PASS | `node --test .\tests\tooling\project-identity.test.js .\tests\tooling\tools.moorkyz.com-cli.test.cjs` | 9 tests; zero retired identifiers, feature-name exceptions, package/filter/launcher/state checks and exact config errors |
| CLI syntax / launcher | PASS | `node --check .\tools.moorkyz.com.cjs`; `.\tools.moorkyz.com.cmd doctor`; `.\tools.moorkyz.com.cmd status` | Node syntax passes; config present; runtime directory renamed; original portal stopped |
| Browser / accessibility | PASS | `pnpm test:browser` via final `pnpm check` | 21 tests; identity, disabled-feature zero-network, downloads, mobile fit at 320/390/1440px, keyboard and automated axe in both themes |
| Enabled Directory Cleaner browser integration | PASS | `$env:CI='1'; $env:TOOLS_MOORKYZ_COM_PORTAL_PORT='48793'; pnpm test:directory-cleaner:browser` | 6 tests across three engines; isolated fixtures, move/archive bytes and stale-preview denial |
| Optional enabled VideoCrunch browser integration | FAIL | `$env:CI='1'; $env:TOOLS_MOORKYZ_COM_PORTAL_PORT='48793'; pnpm test:videocrunch:browser` | All 6 cases fail awaiting enabled iframe submit button; no export is started. Details in finding R1 |
| VideoCrunch native configuration diagnostic | PASS | Inline Node isolated temporary `createApplication` fixture, loopback ephemeral port, GET `/api/config`, then `app.close()` and fixture cleanup | HTTP 200; PowerShell 5.1, yt-dlp 2026.10.21 and FFmpeg 9.0.2 available; `storageError: null` |
| Build / generated source | PASS | `pnpm build` via final `pnpm check`; `pnpm check:source` | Production standalone/assets build; 202 repository paths checked by bounded source scanner before this audit |
| Dependency advisory check | PASS | `pnpm audit --audit-level=moderate` | No known vulnerabilities reported; dependency versions/lockfile unchanged |
| Performance | N/A | No new execution work or dependencies | Existing output-size and disabled-feature tests pass |
| Worktree / staged whitespace | PASS | `git diff --check`; `git diff --cached --check` | No whitespace errors |

During test development, the initial full gate and a focused identity-browser
rerun failed on test assumptions about trailing slashes: Next.js normalizes
root canonical/Open Graph URLs without a trailing slash. Expectations were
corrected; all three browsers pass in the final gate. One filename selector
using Windows backslashes matched no Playwright tests; selecting `tools.spec.js`
resolved discovery. Identity-detector false positives involving escaped dots
and the word "Builder" were fixed and covered with explicit positive/negative
cases. The source scan initially encountered renamed-but-not-yet-staged deleted
paths; staging the exact task-owned renames resolved that index mismatch.

## 4. Risk and quality review

- Public contract/compatibility: intentional complete local command/config,
  package filter, environment and preference-key rename, as explicitly approved.
  No compatibility aliases retaining retired identifiers are introduced.
- Security/privacy: no new connection, permission, dependency, account or
  telemetry. Local gateways retain loopback/origin checks, fixed ports,
  allowlists, bounded bodies and explicit failures.
- Accessibility/keyboard/reduced motion: brand remains a named native link;
  existing keyboard, axe, mobile/theme and reduced-motion tests pass.
- Disabled behavior: both service connections remain off unless their new
  flags are explicitly set; tests verify no disabled service requests.
- Error/recovery: renamed config errors identify the actual filename; existing
  CLI locking/process verification behavior is unchanged. Existing logs were
  preserved, not deleted or rewritten.
- Dependency/regression: no versions or mandatory dependencies change; renamed
  package filters resolve in build/typecheck. Original image bytes and legal
  attribution are preserved. Generated downloads contain the updated guides.

## 5. Findings and residual risks

| ID | Severity | Finding and evidence | Mitigation / owner | Status |
|---|---|---|---|---|
| R1 | Medium | Optional VideoCrunch browser suite: 6/6 cases fail because the iframe submit button remains disabled; native configuration succeeds | Follow-up investigation required. Client/service/workspace/proxy middleware implementation is unchanged; gateway edits only rename environment keys. Do not claim a verified integrated native export | Open; outside the required `pnpm check` gate |
| R2 | Medium | Repository links/domain target the new identity, while GitHub, checkout/remote, DNS and Coolify were deliberately not changed | Maintainer must perform/authorize external migration before links/hosting can be assumed live; see deployment guide | Deferred by user choice |
| R3 | Low | Existing browser theme preference is not migrated to the renamed key | Documented intentional reset to default light theme; renamed persistence is browser-tested | Accepted |
| R4 | Low | Historical audit references/commands are identity-normalized, not rerun historical experiments | Decision register documents normalization; original records remain in Git history | Documented |

- Known gaps / checks not run: independent security/accessibility review,
  manual assistive technology, Docker/container execution, production HTTP,
  native PowerShell/media verification commands and clean-machine testing:
  `NOT RUN`, not required for this local identity-only change. Optional browser
  exports remain unverified due to R1.
- Required approvals: external repository/remote/folder, DNS/hosting changes and
  any deployment/publication. None performed.
- Rollback/recovery: revert the focused local commit; coordinate any running
  supervisors before changing command/state identities back. Preserve logs.
- Release readiness: `NOT APPLICABLE`; local rename only, no deployment approval.

## 6. Completion record

- Audit created before the task commit and included in that commit.
- Final commit hash: `PENDING`
- External actions: `NOT AUTHORIZED` unless separately approved.

**Limitations:** An agent self-audit is not an independent security review, accessibility certification, or release approval.
