# Development Audit: M7 Next.js React adapter integration

> Fill in with observed evidence. Never mark an unexecuted check as PASS.

| Field                    | Value                                                                          |
| ------------------------ | ------------------------------------------------------------------------------ |
| Audit ID                 | `2026-10-17T00-59-26Z-m7-next-js-react-adapter-integration`                    |
| Created (UTC)            | `2026-10-17T00:59:26.781Z`                                                     |
| Task / issue             | M7 Next.js React adapter integration                                           |
| Auditor                  | Copilot self-audit; direct source, tests, build, and browser output inspection |
| Branch / baseline commit | `master` / `351b2f3`                                                           |
| Final commit             | Report in task completion; do not edit this audit after committing             |
| Scope reviewed           | React adapter mount-error reporting and Next.js portal consumption             |
| Outcome                  | `PASS WITH RISKS`                                                              |

## 1. Request and outcome

- Requested behavior: Continue M7 by validating the optional React adapter in a real Next.js consumer and surfacing failed mounts without leaking resources.
- Implemented behavior: Added structured `onMountError` handling to `ToolbarMount` and refactored the portal navigation to consume the React subpath while preserving host preferences, actions, preset updates, and error messaging.
- Acceptance criteria and evidence: Full `pnpm check` passed (127 Node tests, 24 Chromium tests); portal production build and browser preference/navigation/accessibility tests passed. Adapter tests verify mount-error cleanup.
- Intentional exclusions: No new framework dependencies, feature-default changes, extension execution, or release actions.
- Relevant decisions/contracts: Q55; framework-neutral core and optional adapters in `docs/decisions/decision-register.md`, `docs/architecture/features-and-resources.md`, and draft `docs/contracts/sdk-and-schema.md`.

## 2. Change inventory

| Path                                                                       | Change                                                                       | Risk                              |
| -------------------------------------------------------------------------- | ---------------------------------------------------------------------------- | --------------------------------- |
| `packages/sdk/src/react.ts`                                            | Structured mount-error callback after controller disposal                    | Low                               |
| `packages/sdk/test/react-adapter.test.js`                              | Verify mount failure is reported and no toolbar host remains                 | Low                               |
| `packages/portal/src/components/portal-shell.tsx`                          | Use the optional React adapter in the Next.js portal; preserve host behavior | Medium: real consumer integration |
| `packages/sdk/README.md`, `docs/contracts/sdk-and-schema.md`           | Document error behavior and Next.js consumer validation                      | Low                               |
| `docs/engineering/implementation-status.md`, `MANIFEST.json`               | Update M7 evidence and contract digest                                       | Low                               |
| `docs/audits/2026-10-17T00-59-26Z-m7-next-js-react-adapter-integration.md` | This evidence audit                                                          | Low                               |

## 3. Verification evidence

Use `PASS`, `FAIL`, `NOT RUN` (with reason), or `N/A`. Record exact commands and results.

| Check                        | Result          | Exact command / method                                                                            | Evidence / notes                                                                                                                                                                     |
| ---------------------------- | --------------- | ------------------------------------------------------------------------------------------------- | ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------ |
| Formatting / lint            | PASS            | `$env:TOOLS_MOORKYZ_COM_FIXTURE_PORT='48731'; $env:TOOLS_MOORKYZ_COM_PORTAL_PORT='48732'; pnpm check` | ESLint and repository Prettier checks passed.                                                                                                                                        |
| Typecheck                    | PASS            | Same `pnpm check` invocation                                                                      | SDK, vanilla, React consumer, and portal typechecks passed.                                                                                                                      |
| Unit / integration tests     | PASS            | Same `pnpm check` invocation                                                                      | 127 Node tests passed; includes SSR, Strict Mode, cleanup, dispatch, and structured mount-error coverage.                                                                            |
| Browser / accessibility      | PASS            | Same `pnpm check` invocation                                                                      | 24 Chromium tests passed, including Next.js portal interactions, preference persistence, and axe checks.                                                                             |
| Build / consumer fixture     | PASS            | Same `pnpm check` invocation; `pnpm --filter @tools.moorkyz.com/sdk pack --dry-run`                     | Next.js production build and React consumer typecheck passed; package output includes `dist/react.js` and `dist/react.d.ts`.                                                         |
| Security / dependency review | PASS WITH RISKS | `pnpm install --frozen-lockfile`; source/diff review                                              | Frozen install passed. No dependency manifest changed in this slice. Adapter delegates authorization and action handling to the host; no network, persistence, or permission change. |
| Performance (if relevant)    | N/A             | Not measured                                                                                      | No additional persistent listener/resource; controller remains effect-scoped.                                                                                                        |
| `git diff --check`           | PASS            | `git diff --check`                                                                                | No whitespace errors.                                                                                                                                                                |
| Manifest integrity           | PASS            | PowerShell SHA-256 comparison over `MANIFEST.json`                                                | All 16 listed document hashes matched.                                                                                                                                               |

## 4. Risk and quality review

- Public contract, feature-default, and compatibility impact: Additive draft React adapter behavior only; no defaults or authorization contracts changed.
- Security, privacy, permissions, and secrets: Portal passes its existing host callbacks and namespaced local preference adapter; the React wrapper creates no authority or network path.
- Accessibility, keyboard, and reduced motion: The same accessible core toolbar is mounted; portal axe/browser checks passed.
- Disabled-feature work/resource/network behavior: React effect owns mount/disposal; error path disposes immediately; unmounted component has no controller.
- Error handling, cleanup, and recovery: `onMountError` receives structured errors after cleanup; without it, errors throw with the toolbar error as cause. Portal renders a visible `role=alert` message.
- Dependency, performance, and regression exposure: No dependency or root-export changes; Next.js build exercises the optional client subpath.

## 5. Findings and residual risks

| ID  | Severity | Finding and evidence                                                                                              | Mitigation / owner                                               | Status                  |
| --- | -------- | ----------------------------------------------------------------------------------------------------------------- | ---------------------------------------------------------------- | ----------------------- |
| R1  | LOW      | The portal adapter use depends on memoized configuration/host identities; reference changes deliberately remount. | Portal memoizes values; contract documents this behavior.        | Accepted draft behavior |
| R2  | LOW      | React adapter compatibility outside the tested React 19/Next.js environment remains unverified.                   | Continue M7 framework/version matrix validation; owner: project. | Open                    |

- Known gaps / checks not run: M7 remains in progress. Vue/Angular adapters, extension SDK/fault containment, bundle isolation measurement, and manual assistive-technology testing remain.
- Required approvals: None for this additive adapter integration. No publishing, deployment, push, or external integration was performed.
- Rollback or recovery: Revert the focused portal/adapter integration commit; the core controller API remains independently usable.
- Release readiness: `NOT READY`

## 6. Completion record

- Audit created before the task commit and included in that commit, or reason not committed: Created before staging; will be included in the focused local commit.
- Final commit hash: `PENDING`
- External actions: `NOT AUTHORIZED` unless separately approved.

**Limitations:** An agent self-audit is not an independent security review, accessibility certification, or release approval.
