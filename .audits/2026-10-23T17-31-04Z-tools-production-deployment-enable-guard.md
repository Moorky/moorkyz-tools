# Development Audit: Tools production deployment enable guard

| Field | Value |
|---|---|
| Audit ID | `2026-10-23T17-31-04Z-tools-production-deployment-enable-guard` |
| Created (UTC) | 2026-10-23T17:31:04Z |
| Task | Finish the Tools rename deployment while preserving concurrent project work |
| Auditor | Codex web-projects (self-audit) |
| Baseline | Remote `master`, `2011239d63fd636bc6aba35f84aed25e48be32e2` |
| Final commit | Reported in task completion; this audit is not edited after committing |
| Scope | Existing deployment gate and operational documentation only |
| Outcome | PASS WITH RISKS: focused source checks pass; live CI/deployment remains unverified |

## 1. Request and outcome

The user withdrew wider project-file unification and retained the Tools rename
and deployment task. A pre-push fetch found that another chat had advanced the
remote branch, including the current Tools portal, complete identity cleanup,
project tooling and verified Coolify deployment helper. The earlier isolated
change based on a previous product revision was preserved separately and was
not committed, pushed or merged into that work.

This change starts from the new remote head and retains its application,
unification, source files, dependencies and CI checks. It adds the missing
`COOLIFY_DEPLOY_ENABLED=true` production-job condition. The runbook explains
read/deploy token permissions, the enable variable, source verification and the
configured-branch race. No application, tool-runtime or public contract changes
are included. No manifest checksum file exists at this baseline.

## 2. Change inventory

| Path | Change | Risk |
|---|---|---|
| `.github/workflows/ci.yml` | Require the explicit enable variable for the production job | Missing/false variable skips deployment; verify/container gates remain intact |
| `docs/deployment/coolify.md` | Correct polling token permissions and document enable/source/health behavior | Operational guidance; no credential values included |
| This audit | Record scope, checks and remaining live verification limits | Self-audit only |

## 3. Verification evidence

| Check | Result | Exact command / observation |
|---|---|---|
| Frozen install | PASS | `pnpm install --frozen-lockfile`; pnpm 11.8.0, two workspaces, 126 cached packages, lockfile unchanged |
| Deployment/identity/existing CLI regressions | PASS | `node --test scripts/deploy-coolify.test.cjs tests/tooling/project-identity.test.js tests/tooling/tools.moorkyz.com-cli.test.cjs`; all 14 passed |
| Lint | PASS | `pnpm lint`; current portal/tool/script scopes passed |
| Formatting | PASS | `pnpm format:check`; all matched files passed |
| Bounded source scan | PASS | `pnpm check:source`; 237 repository paths passed selected patterns, not a complete security audit |
| Whitespace | PASS | `git diff --check` |
| Build/typecheck/browser/container | NOT RUN locally | This change affects workflow conditions and documentation only; unchanged full gates remain mandatory in pushed CI |
| Live deployment | NOT VERIFIED | Await the pushed CI run and corrected token installation; a queued request is insufficient |

## 4. Risk and quality review

The unchanged production job requires successful `verify` and `container` jobs,
a `push` event on `master`, and now explicit enablement. Pull requests and manual
verification runs cannot deploy. Jobs serialize; a read-only GitHub API check
skips stale source commits before the credential-bearing deployment step.
Checkout credentials are not persisted. The unchanged helper polls the returned
deployment UUID, requires Coolify's completed commit to match the tested full
SHA, then probes public HTTPS without forwarding its API token. Failed/unknown
states, mismatches and readiness failures do not report success.

The helper queues the configured branch rather than pinning an arbitrary SHA.
A newer push between the stale-head check and Coolify's fetch can run before
the post-completion mismatch is detected. Inspect the deployment and keep a
known healthy image available for recovery. Do not infer that a failed source
verification left the prior image active.

The parent reported a user-approved read/deploy token replacement, with GitHub
secret installation awaiting fresh account authentication. An old deploy-only
token cannot poll and must fail verification. This local task does not inspect
or store token values. Availability checks do not certify the complete product.

## 5. Recovery and completion

Rollback is a source revert of these three files or disabling the enable
variable; that does not undo an already queued production release. Use Coolify
history to recover the previous healthy image when needed. The user authorized
the focused commit and a non-force push only after confirming the current
remote base. Other chats' work and the original checkout remain untouched.
The exact commit and CI run are reported after pushing. Live release readiness
remains conditional on the retained CI/container/deployment gates succeeding.

Primary references reviewed: [GitHub job conditions and dependencies](https://docs.github.com/en/actions/reference/workflows-and-actions/workflow-syntax#jobsjob_idneeds),
[Coolify deployment request](https://coolify.io/docs/api/endpoints/deployments/deploy-by-tag-or-uuid),
and [deployment status/source commit](https://coolify.io/docs/api/endpoints/deployments/get-deployment-by-uuid).
