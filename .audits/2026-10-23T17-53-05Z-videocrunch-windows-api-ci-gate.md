# Development Audit: VideoCrunch Windows API CI gate

| Field | Value |
|---|---|
| Audit ID | `2026-10-23T17-53-05Z-videocrunch-windows-api-ci-gate` |
| Created (UTC) | 2026-10-23T17:53:05Z |
| Task | Repair the platform mismatch blocking the renamed Tools deployment |
| Auditor | Codex web-projects, with scoped read-only peer review (self-audit) |
| Baseline | Remote `master`, `59df7bf0135766b997c3a6f5e4276ccf9bcd5a18` |
| Final commit | Reported at completion; this audit is not edited after committing |
| Outcome | PASS locally; hosted CI/container/deployment must still complete |

## Request and diagnosis

CI run `39995883879`, job `120567294534`, passed browser installation but
failed six of its 47 Node tests. The first failure was an export request
returning HTTP 400 rather than 202 at
`tools/videocrunch/test/server.test.js:76`. Its fixture uses `os.tmpdir()` for
the output directory, which is a POSIX path on the Ubuntu runner. VideoCrunch's
documented Windows path validator correctly rejected it; later export tests
then had no accepted job or media response. The service is Windows-first and
its nested contract explicitly requires absolute Windows filesystem paths and
Windows PowerShell 5.1 compatibility. Changing the production validator to
accept POSIX paths would not repair the incorrect test platform.

The earlier canceled run `39995110609` had a separate Ubuntu package-download
delay. It is not the application failure repaired here. No runner/mirror change
or browser assertion change is included.

## Change inventory

| Path | Change | Risk |
|---|---|---|
| `package.json` | Keep portable root tests separate; expose `test:videocrunch:api` for the existing Windows suite | Same assertions remain mandatory in aggregate CI |
| `.github/workflows/ci.yml` | Run that unchanged suite in `videocrunch-windows` using `.node-version`; require it before container and deployment | A failed Windows job blocks production |
| `docs/engineering/testing.md`, `docs/deployment/coolify.md` | Explain the platform split and required checks | No runtime behavior change |
| This audit | Record the actual failure, checks and residual release limits | Self-audit only |

The Windows job needs only Node's built-in modules: exporters and prerequisite
checks are already injected by the existing suite. It does not install or run
media tools, Pester, native installers or production credentials. The current
application, validator, all 13 server tests, runtime/shared configuration and
unification files are unchanged. No skip or continue-on-error condition was
added. The former 47-test aggregate is now 34 portable tests plus the same
13 Windows API tests. Existing helper, browser, accessibility, source,
dependency and container gates remain.

## Verification evidence

| Check | Result | Exact command / observed evidence |
|---|---|---|
| Windows API suite | PASS | `pnpm test:videocrunch:api`; all 13 existing tests passed on local Windows, Node v22.18.0 |
| Portable tests and production build | PASS | `pnpm test`; Next.js standalone production build and all 34 portable tests passed |
| Lint | PASS | `pnpm lint`; all existing configured scopes passed |
| Formatting | PASS | `pnpm format:check`; all matched files passed |
| Workflow syntax/format | PASS | `pnpm exec prettier --check .github/workflows/ci.yml` |
| Bounded source scan | PASS | `pnpm check:source`; 238 paths passed before adding this audit; selected patterns are not a complete security audit |
| Product/server test preservation | PASS | `git diff --exit-code HEAD -- tools/videocrunch/server.js tools/videocrunch/test/server.test.js`; no changes |
| Whitespace | PASS | `git diff --check` |
| Hosted Windows/Linux/browser/container/deployment | NOT RUN locally | Follow the pushed CI run; local Windows success does not certify the remote release |
| Real-media/Pester/native installer checks | NOT RUN | No native application, PowerShell, media or installer source changed; these are separate optional gates |

## Risk, recovery and completion

Production still requires the current `master` push, explicit enable variable,
successful verification jobs and container smoke test, a stale-head check,
Coolify source-SHA confirmation and public HTTPS health. This change adds a
required Windows result rather than relaxing validation. The provider token
update/revocation was reported complete by the parent task; this subtask did not
inspect or store credentials.

Hosted package mirrors and runner availability can still delay CI. The existing
configured-branch deployment race remains documented in the deployment runbook.
Rollback is a focused source revert; no package was published and no history
was rewritten. The user authorized this deployment-blocking CI repair and a
non-force push after confirming the remote head has not advanced. The exact
commit and run are reported after pushing.
