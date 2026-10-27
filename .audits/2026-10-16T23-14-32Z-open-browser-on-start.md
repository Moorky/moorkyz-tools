# Development Audit: Open the app on start and restart

| Field | Value |
|---|---|
| Audit ID | `2026-10-16T23-14-32Z-open-browser-on-start` |
| Created (UTC) | `2026-10-16T23:14:32Z` |
| Task / issue | Open the configured app URL after `tools.moorkyz.com start` or `tools.moorkyz.com restart` |
| Auditor | Self-audit by Copilot SDK agent; no independent reviewer |
| Branch / baseline commit | `master` / `e1e64b3` |
| Final commit | Included in the local merge commit; hash reported in task completion |
| Scope reviewed | `tools.moorkyz.com.cjs`, `docs/engineering/tools.moorkyz.com-cli.md` |
| Outcome | PASS WITH RISKS |

## 1. Request and outcome

- Requested behavior: open the selected local app automatically on `start` and `restart`.
- Implemented behavior: use the OS default browser launcher after the managed process is confirmed, including when already running; `start all` opens each URL.
- Acceptance evidence: `node --check tools.moorkyz.com.cjs`, `node --test tests/tooling/tools.moorkyz.com-cli.test.cjs` (4/4), `pnpm check` (112 Node tests and 19 Chromium tests), and `git diff --check` passed.
- Intentional exclusions: CI and non-interactive invocations do not launch a browser. No consumer CLI or application code changed.
- Relevant decisions/contracts: this is maintainer CLI behavior only; no public package API changed.

## 2. Change inventory

| Path | Change | Risk |
|---|---|---|
| `tools.moorkyz.com.cjs` | Add OS browser launching and update help | low |
| `docs/engineering/tools.moorkyz.com-cli.md` | Document the behavior | low |

## 3. Verification evidence

| Check | Result | Exact command / method | Evidence / notes |
|---|---|---|---|
| Formatting / lint | PASS | `pnpm check` | ESLint and Prettier checks passed |
| Typecheck | N/A | - | Plain JavaScript helper |
| Unit / integration tests | PASS | `node --test tests/tooling/tools.moorkyz.com-cli.test.cjs`; `pnpm check` | 4 focused CLI tests and 112 workspace Node tests passed |
| Browser / accessibility | N/A | No UI change | Browser opening is an external OS action; no real browser was launched in this validation |
| Build / consumer fixture | PASS | `pnpm check` | Workspace package and consumer fixture checks passed |
| Security / dependency review | NOT RUN | No dependency changed | Manual diff review only; no independent security or advisory audit claimed |
| Performance (if relevant) | N/A | - | One detached OS opener per selected project |
| `git diff --check` | PASS | `git diff --check` | No whitespace errors; line-ending normalization warnings only |
| JavaScript syntax | PASS | `node --check tools.moorkyz.com.cjs` | Exited with code 0 |

## 4. Risk and quality review

- Public contract, feature-default, and compatibility impact: no package contract or defaults changed.
- Security, privacy, permissions, and secrets: existing validated URL passed as an argument; no shell execution, secrets, or data movement added.
- Accessibility, keyboard, and reduced motion: N/A; no UI changed.
- Disabled-feature work/resource/network behavior: N/A; no toolbar feature flags changed.
- Error handling, cleanup, and recovery: opener launch failures are reported; the CLI service result is preserved. The opener is detached and not tracked as a managed service.
- Dependency, performance, and regression exposure: no dependencies; the current heartbeat confirms process supervision, not HTTP readiness.

## 5. Findings and residual risks

| ID | Severity | Finding and evidence | Mitigation / owner | Status |
|---|---|---|---|---|
| AUD-001 | Low | A slow app may not be accepting HTTP when the browser opens because startup confirmation is based on the existing heartbeat. | Project owner can reload if observed; consider readiness polling if needed. | Open |

- Known gaps / checks not run: The real OS browser launch path was not exercised because the session is non-interactive; it is intentionally skipped without a TTY.
- Required approvals: none for this local CLI change.
- Rollback or recovery: revert the CLI helper and documentation edits.
- Release readiness: CI validation is pending on the requested push.

## 6. Completion record

- This audit and implementation are included in the local merge commit; no push was performed.
- Final commit hash: Report in task completion.
- External actions: `NOT AUTHORIZED`.

**Limitations:** This self-audit is not an independent security review, accessibility certification, or release approval.
