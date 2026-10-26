# Development Audit: Refactor repository hierarchy using moorkyz-template

> Scoped implementation self-audit. Observed browser failures in concurrent,
> unrelated product work remain explicitly unresolved.

| Field | Value |
|---|---|
| Audit ID | `2026-10-26T17-33-06Z-refactor-repository-hierarchy-using-moorkyz-template` |
| Created (UTC) | `2026-10-26T17:33:06.573Z` |
| Task / issue | Refactor repository hierarchy using moorkyz-template |
| Auditor | AI assistant using Copilot SDK in VS Code; self-audit |
| Branch / baseline commit | `master`; initial `75ebf05`, intervening unrelated startup-policy commit `e255cf5b6a1321767fb28256a51add5389e198ef` |
| Final commit | Report in task completion; do not edit this audit after committing |
| Scope reviewed | Shared CLI/storage extraction, launcher compatibility, template-style hierarchy, read-only maintenance, quality gates, deployment-script relocation, documentation and audit workflow |
| Outcome | `PASS WITH RISKS`: scoped implementation checks pass; complete browser gate is not green |

## 1. Request and outcome

- Requested behavior: refactor the disorganized repository using the hierarchy,
  structure and useful systems in `Moorky/moorkyz-template`.
- Implemented behavior: a version-referenced shared-tooling catalog under
  `.template/`, one CLI implementation behind both existing root launcher
  identities, helpers below their owning system, product-owned deployment
  automation below `scripts/deployment/`, canonical new audits in `.audits/`
  from `docs/AUDIT_TEMPLATE.md`, and read-only maintenance/structural checks.
  The legacy `READM.html` links to the maintained offline guide instead of
  duplicating it.
- Acceptance criteria and evidence:
  - Both launchers retain their configuration, state namespace, default portal,
    preferred ports, HTTP readiness, restart/production/status/log/stop behavior.
  - The generic launcher keeps managed canonical logs and existing retention.
    The repository-specific launcher keeps append-only legacy logs, creates no
    canonical storage folders on startup and does not enable automatic retention.
    Fixture assertions verify old log bytes and unrelated log files survive.
  - The extracted storage helper is byte-identical to the baseline Git blob.
    Deployment code/tests and the audit template are 100% renames.
  - Maintenance checks parse JavaScript without executing checked source,
    reject escaped/linked paths, report syntax failures and provide true no-op
    previews. Tests cover source non-execution, error paths, namespace wiring,
    canonical/legacy audit selection, historical-byte preservation and junction
    rejection.
  - Existing storage/deployment suites now run through `pnpm test:unit`, and
    repository/source checks are included in `pnpm check`.
- Intentional exclusions: no application-workspace rename, tool feature changes,
  new dependencies, upstream template overwrite, stack presets, user-setting
  changes, runtime data migration/cleanup, rewritten historical audits, Git
  history changes, publishing or deployment. Preserve unrelated edits and new
  files, including Media Recover, catalog cards and branding work.
- Relevant decisions/contracts: [root contract](../AGENTS.md),
  [product decisions](../docs/decisions/decision-register.md),
  [structure/adaptation guide](../docs/engineering/repository-structure.md),
  and upstream template revision
  `1c413a3a0e7ea24c39fc995e1a947541a4efac01`. Upstream retention and port-conflict
  semantics were deliberately not imported over this repository's behavior.

## 2. Change inventory

| Path | Change | Risk |
|---|---|---|
| `.template/cli/`, root CLI/storage compatibility files | Extract one implementation and preserve namespace-specific logging | Lifecycle and recovery regressions; covered by synthetic fixture suites |
| `.template/maintenance/`, `tests/tooling/repository-structure.test.cjs` | Read-only syntax checkups and 12 hierarchy/safety tests | Explicit nonzero failures, no checked-source execution or network use |
| `scripts/deployment/`, `.github/workflows/ci.yml` | Move existing deployment code/tests unchanged; update CI paths | No deployment executed; container/live gates not run locally |
| `package.json`, launcher/identity tests | Add repository/unit gates, include shared tooling in lint/format, isolate pnpm fixture environment | Existing dependencies/pins/lockfile unchanged |
| `.audits/`, `docs/AUDIT_TEMPLATE.md`, audit skill/workflow | One current destination for new evidence; historical reports stay in place | Legacy template/output fallback remains supported |
| Root guides, `.template/README.md`, `scripts/README.md`, engineering docs | Ownership map, compatibility/adoption decisions and maintained links | Documentation-link regressions included in structural tests |

## 3. Verification evidence

Commands ran in the shared Windows worktree. Product files continued changing
in other sessions during verification; results describe the observed executions,
not a certification of subsequent concurrent work.

| Check | Result | Exact command / method | Evidence / notes |
|---|---|---|---|
| Baseline tooling | PASS | `node --test tests\tooling\tools.moorkyz.com-cli.test.cjs tests\tooling\moorkyz-runtime.node.cjs scripts\deploy-coolify.test.cjs` | 36 tests before implementation |
| Repository maintenance | PASS | `pnpm check:repository` | 48 JavaScript files parsed initially, 49 on the final run after another concurrent browser file appeared; 12 structural/safety tests passed |
| Formatting / lint | PASS | `pnpm lint`; `pnpm format:check` | Both passed after final CLI compatibility edits |
| Typecheck | PASS | `pnpm typecheck` | Portal `tsc --noEmit` passed |
| Production build / integration | PASS | `pnpm test` | Next.js 16.4.1 production build and 102 tests passed; final changes only adjusted CLI compatibility and were retested below |
| Final unit / integration suite | PASS | `pnpm test:unit` | 102 passed, 0 failed after append-only legacy log preservation and pnpm fixture isolation |
| Initial full pipeline | FAIL, corrected scoped issues | `pnpm check` | Initial run exposed three fixture failures caused by inherited `pnpm_config_verify_deps_before_run=false`; fixtures now remove that lifecycle-only override. Another run met an unrelated browser test while it was unformatted; subsequent format check passed. No quality gate was bypassed. |
| Full browser / accessibility | FAIL | `$env:TOOLS_MOORKYZ_COM_PORTAL_PORT = '55808'; pnpm test:browser` | 39 tests across Chromium/Firefox/WebKit: 30 passed, 9 failed. Eight failures in pre-existing untracked Media Recover tests; one WebKit Directory Cleaner select contrast failure in the concurrently modified portal. Product code was not changed by this task. |
| CI-environment browser attempt | FAIL, environment mismatch | `$env:CI = '1'; $env:TOOLS_MOORKYZ_COM_PORTAL_PORT = '64645'; pnpm test:browser` | pnpm explicitly rejected a CI-dependent `enableGlobalVirtualStore` setting change. Retried without altering CI or reinstalling; the actual browser suite above executed. |
| No-op maintenance | PASS | `node .template\maintenance\run-checkups.cjs --dry-run` | Explicit `PREVIEW ONLY`, no discovery/check execution or writes |
| Legacy diagnostics | PASS | `node tools.moorkyz.com.cjs doctor` | Correct config, runtime namespace, audit template and append-only logging policy |
| Existing portal preservation | PASS | `node moorkyz.cjs status`; `Invoke-WebRequest -Uri 'http://127.0.0.1:4320' -TimeoutSec 30` | Original user supervisor PID 2940 remained running and returned HTTP 200; it was not stopped or restarted |
| Storage-byte preservation | PASS | `git diff --exit-code HEAD:moorkyz-storage.cjs :.template/cli/helpers/storage.cjs` | Empty diff, exit 0 |
| Bounded source review | PASS | `pnpm check:source` | 258 paths scanned for configured credential/unsafe-rendering patterns; not a complete security review |
| Whitespace | PASS | `git diff --check`; `git diff --cached --check` | No whitespace errors |
| Dependency audit | NOT RUN | `pnpm audit` | No dependency/version/lockfile changes; no new dependency assurance claimed |
| Native service browser/media gates | NOT RUN | `pnpm test:directory-cleaner:browser`; `pnpm test:videocrunch`; `pnpm test:videocrunch:browser` | No native tool or gateway behavior changed; no prerequisites installed |
| Container / deployment | NOT RUN | Docker build/smoke and live Coolify deployment | No local container or external production action authorized/performed |
| Performance | N/A | No product performance contract changed | Checkups are bounded local subprocesses, not background services |

## 4. Risk and quality review

- Public contract, feature-default, and compatibility impact: product tool routes,
  service opt-ins, workspace/package names and runtime pins remain unchanged.
  Both old launcher names work. The repository-specific alias uses the shared
  command helpers but preserves its old log/automatic-maintenance behavior.
  Maintainer deployment-script paths and new audit destinations are documented.
  Copying only the root CLI shims is no longer a portable CLI distribution;
  the guide now requires including `.template/cli/`.
- Security, privacy, permissions, and secrets: no network integration, permission
  change, secret, new package or runtime migration introduced. Checkups reuse
  existing scoped-path validation and never execute the sources being checked.
  Audit generation rejects junctions and uses exclusive output creation.
- Accessibility, keyboard, and reduced motion: no portal UI code changed.
  Offline HTML remains static and JavaScript-free. Browser results include
  unresolved current-worktree accessibility failures; no certification claimed.
- Disabled-feature work/resource/network behavior: local services stay opt-in,
  loopback-only and independent. Neither new maintenance nor opening docs
  installs packages, connects upstream, modifies settings or cleans runtime data.
- Error handling, cleanup, and recovery: malformed syntax, invalid arguments,
  failed subprocesses and unsafe paths produce explicit failures. Fixture hooks
  stop their own services and remove their temporary directories. The existing
  user service stays running; ignored Playwright results remain available.
- Dependency, performance, and regression exposure: existing package versions,
  lockfile and pnpm verification policy unchanged. Fixture environment cleanup
  prevents the pnpm lifecycle runner's recursion override from masking real
  standalone dependency verification; it does not change production policy.

## 5. Findings and residual risks

| ID | Severity | Finding and evidence | Mitigation / owner | Status |
|---|---|---|---|---|
| F1 | Medium | Media Recover browser tests expect a Tool/source section missing from the production build; 6 section/source failures across browsers plus 2 accessibility failures | Existing independent Media Recover integration owner; unrelated source/test edits preserved | OPEN, outside this refactor |
| F2 | Medium | WebKit Directory Cleaner action-select contrast measured 1.08:1, expected 4.5:1 | Existing portal accessibility owner; no portal CSS/control code changed here | OPEN, outside this refactor |
| F3 | Low | Concurrent edits/new browser tests appeared during validation; full-worktree green status cannot be claimed | Keep commits scoped; rerun the full gate after those changes stabilize | OPEN |
| F4 | Low | Portable CLI copies now need `.template/cli/`, not only root files | Updated copying/setup guides and root compatibility shims | DOCUMENTED |

- Known gaps / checks not run: see full browser failures and unexecuted native,
  container, dependency-audit and deployment checks above. No independent
  security/accessibility review or clean-machine verification was performed.
- Required approvals: none for this reversible local organization change.
  Runtime cleanup, native setup, user settings, publishing, pushing and deployment
  remain separately authorized actions.
- Rollback or recovery: revert this focused local commit in a separately
  authorized task; historical audits/runtime data need no restoration because
  they were not moved, cleaned or rewritten. Do not reset unrelated work.
- Release readiness: `NOT APPLICABLE` to this repository refactor; the current
  full worktree is not certified release-ready while browser failures remain.

## 6. Completion record

- Audit generated with `node moorkyz.cjs audit "Refactor repository hierarchy using moorkyz-template"`, filled before the task commit and included with task-owned changes.
- Final commit hash: reported in task completion; not embedded after committing.
- Unrelated work: original Media Recover edits plus concurrent catalog/branding
  changes and other audit files remain excluded. Only the hierarchy section
  was staged from the shared, dirty root README.
- External actions: `NOT AUTHORIZED` unless separately approved.

**Limitations:** An agent self-audit is not an independent security review, accessibility certification, or release approval.
