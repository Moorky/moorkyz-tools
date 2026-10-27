# Development Audit: Merge upstream and preserve current tools portal

> Scoped merge self-audit; normal push is authorized, not a deployment certification.

| Field | Value |
|---|---|
| Audit ID | `2026-10-23T17-24-25Z-merge-upstream-and-preserve-current-tools-portal` |
| Created (UTC) | `2026-10-23T17:24:25.136Z` |
| Task / issue | Merge upstream and preserve current tools portal |
| Auditor | Copilot in VS Code; self-audit |
| Branch / baseline commit | `master` / `26a07fb`; incoming `origin/master` / `55fde74` |
| Final commit | Report in task completion; do not edit this audit after committing |
| Scope reviewed | Upstream divergence, conflict resolutions, current catalog/theme/footer, imported historical audits and verification |
| Outcome | `PASS WITH RISKS`: local merge gates pass; remote CI/deployment and Evidence destination remain separately verified operations |

## 1. Request and outcome

- Request: commit outstanding edits, pull/merge and normally push both selected
  repositories; no history rewrite or force-push.
- This repository's outstanding runtime-guide correction was first committed
  as `26a07fb`. `git fetch --prune origin` retrieved the tools origin, then
  `git merge --no-ff --no-commit origin/master` exposed the expected divergence.
- Resolutions preserve the current independent tools catalog and approved SDK
  removal. Deleted legacy packages, Studio styles and old test suites stay
  deleted; current package identity, Next.js 16.4.1 and lockfile are preserved.
- The incoming Evidence footer link is adapted to the current footer without
  restoring legacy navigation. The exact mark/credit block remains intact.
  The link is static navigation only, with no automatic Evidence request.
- Incoming dark-theme body synchronization/styles are retained. Retired
  Studio-only rules and obsolete SDK fixes are not reintroduced.
- Ten incoming historical audits are preserved, with project identity/path
  references normalized under the existing identity decision. Original incoming
  records remain accessible through their original Git commits.
- Exclusions: no evidence processing, service enablement, dependency versions,
  infrastructure configuration, force-push or claim of successful live deployment.
- Authority: root AGENTS.md, current product decisions, approved SDK retirement
  and the user's explicit normal merge/push authorization.

## 2. Change inventory

| Path | Change | Risk |
|---|---|---|
| Portal README/package/globals/footer and legacy modify/delete conflicts | Keep current architecture; retain applicable upstream navigation/theme changes | Medium |
| `portal-shell.tsx`, `globals.css` | Synchronize body theme attributes and dark text styling | Low |
| `site-footer.tsx`, browser identity test | Preserve Evidence link intent and assert destination/new-tab protection | Low |
| Incoming historical audits | Preserve records and normalize retired project references | Low |
| This audit | Record merge choices and observed verification | Low |

## 3. Verification evidence

Use `PASS`, `FAIL`, `NOT RUN` (with reason), or `N/A`. Record exact commands and results.

| Check | Result | Exact command / method | Evidence / notes |
|---|---|---|---|
| Complete configured gate | PASS | `$env:CI='1'; $env:TOOLS_MOORKYZ_COM_PORTAL_PORT='48793'; pnpm check` | Lint, format, build, 47 Node tests, 21 three-engine browser tests and typecheck |
| Formatting / lint | PASS | `pnpm lint`; `pnpm format:check` via `pnpm check` | Existing configured checks |
| Typecheck | PASS | `pnpm typecheck` via `pnpm check` | Current portal package filter and strict types pass |
| Shared CLI/deployment tests | PASS | `node --test scripts\deploy-coolify.test.cjs tests\tooling\moorkyz-runtime.node.cjs` | 23/23 tests; synthetic fixtures/mocked deployment, no live release |
| Browser / accessibility | PASS | `pnpm test:browser` via `pnpm check` | 21/21; Evidence footer assertion, mobile fit, keyboard, themes and automated axe |
| Build / consumer fixture | PASS | `pnpm build` via `pnpm check` | Current standalone portal builds; no retired SDK consumer is restored |
| Source scan | PASS | `pnpm check:source` | 236 paths at the pre-audit run; bounded credential/sink scan, not complete security certification |
| Dependency advisories | PASS | `pnpm audit --audit-level=low` | No known vulnerabilities found |
| Identity preservation | PASS | `node --test .\tests\tooling\project-identity.test.js` and full gate | No retired project identifiers in current repository text/paths |
| Conflict inspection | PASS | `git ls-files -u` | No unresolved entries after explicit resolutions |
| Performance | N/A | No new execution service or dependency | Static link and existing theme synchronization |
| Whitespace | PASS | `git diff --check`; staged whitespace gate before commit | No whitespace errors |

## 4. Risk and quality review

- Public/default behavior: current product wins over stale upstream architecture;
  one incoming external navigation link retained; no local-service flag changes.
- Security/privacy: no evidence requests/uploads, hosted workers, credentials or
  new dependencies. Existing service origin/loopback checks remain intact.
- Accessibility/keyboard/reduced motion: native named footer anchor with
  existing 44px text-link style; current browser/axe/mobile/theme checks pass.
- Disabled resources/network: the incoming Evidence link does not connect
  until followed. Existing disabled-service zero-network tests pass.
- Failure/recovery: normal two-parent merge preserves both histories; revert a
  future merge deliberately if rollback is required, not a history rewrite.
- Dependency/regression: existing approved patched Next.js and lockfile remain;
  old SDK fixes survive in Git history, not as reintroduced active files.

## 5. Findings and residual risks

| ID | Severity | Finding and evidence | Mitigation / owner | Status |
|---|---|---|---|---|
| M1 | Medium | Tools push triggers the repository's configured CI and possible protected production job | User explicitly authorized normal push; do not claim CI/container/deploy success without observing it | Remote follow-up |
| M2 | Low | Evidence destination availability/authentication is separate from this static link | Preserve exact upstream target; no TLS or production verification claimed | UNVERIFIED |
| M3 | Medium | The other selected repository's configured local-drive origin is unreachable | No replacement URL supplied; keep it unchanged, commit locally and report its pull/push blocker | Blocked outside this merge |

- NOT RUN: local Docker build, live deployment, private destination verification,
  independent security/accessibility certification and optional native media
  browser exports. Existing optional VideoCrunch export failure remains outside
  this merge's configured gate and is not represented as passing.
- Normal push is explicitly authorized. No force-push or private-data release.
- Rollback: an explicitly reviewed merge revert; preserve both parents/history.
- Release readiness: `READY WITH CONDITIONS` for Git synchronization, not a
  production deployment claim.

## 6. Completion record

- Audit created before and included in the merge commit.
- Final commit hash: `PENDING`
- External actions: tools fetch completed; normal merge/push explicitly authorized.

**Limitations:** An agent self-audit is not an independent security review, accessibility certification, or release approval.
