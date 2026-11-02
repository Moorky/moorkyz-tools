# Development Audit: M7 optional React adapter subpath

> Fill in with observed evidence. Never mark an unexecuted check as PASS.

| Field                    | Value                                                                                                   |
| ------------------------ | ------------------------------------------------------------------------------------------------------- |
| Audit ID                 | `2026-10-17T00-53-52Z-m7-optional-react-adapter-subpath`                                                |
| Created (UTC)            | `2026-10-17T00:53:52.423Z`                                                                              |
| Task / issue             | M7 optional React adapter subpath                                                                       |
| Auditor                  | Copilot self-audit; direct source, tests, package diff, and output inspection                           |
| Branch / baseline commit | `master` / `7c31ead`                                                                                    |
| Final commit             | Report in task completion; do not edit this audit after committing                                      |
| Scope reviewed           | Optional React package subpath, effect lifecycle, React consumer fixture, dependency/lock changes, docs |
| Outcome                  | `PASS WITH RISKS`                                                                                       |

## 1. Request and outcome

- Requested behavior: Begin M7 adapter work while keeping the core framework-neutral and avoiding a mandatory React dependency.
- Implemented behavior: Added `@tools.moorkyz.com/sdk/react` with `ToolbarMount`, React-effect-owned mount/disposal, optional controller notifications, an optional React peer, and a consumer fixture using the adapter.
- Acceptance criteria and evidence: Root export remains separate; server rendering does not access browser globals; Strict Mode mount/cleanup, host dispatch, and controller notification pass in ReactDOM/Happy DOM tests. Full `pnpm check` passes (126 Node, 24 Chromium).
- Intentional exclusions: Vue/Angular adapters, trusted extension SDK, experimental namespace, and full browser/framework/bundle compatibility matrix remain.
- Relevant decisions/contracts: Q55 and `docs/decisions/decision-register.md`; draft adapter contract in `docs/contracts/sdk-and-schema.md`; framework-neutral core invariant in `docs/architecture/features-and-resources.md`.

## 2. Change inventory

| Path                                                                    | Change                                                                               | Risk                                |
| ----------------------------------------------------------------------- | ------------------------------------------------------------------------------------ | ----------------------------------- |
| `packages/sdk/src/react.ts`                                         | Client-only React mount adapter with cleanup and host controller callback            | Medium: draft framework API         |
| `packages/sdk/package.json`, `pnpm-lock.yaml`                       | Add `./react` export, optional React peer, and local adapter build/test dependencies | Low: no new core runtime dependency |
| `packages/sdk/test/react-adapter.test.js`                           | SSR, Strict Mode, dispatch, controller notification, and disposal tests              | Low                                 |
| `examples/react-consumer/src/index.tsx`                                 | Exercise the adapter using memoized configuration and host objects                   | Low                                 |
| `packages/sdk/README.md`, `docs/contracts/sdk-and-schema.md`        | Document optional subpath and identity/remount behavior                              | Low                                 |
| `docs/engineering/implementation-status.md`, `MANIFEST.json`            | Record M7 progress and refresh contract hash                                         | Low                                 |
| `docs/audits/2026-10-17T00-53-52Z-m7-optional-react-adapter-subpath.md` | This evidence audit                                                                  | Low                                 |

## 3. Verification evidence

Use `PASS`, `FAIL`, `NOT RUN` (with reason), or `N/A`. Record exact commands and results.

| Check                        | Result          | Exact command / method                                                                            | Evidence / notes                                                                                                                                                                                                                                                                     |
| ---------------------------- | --------------- | ------------------------------------------------------------------------------------------------- | ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------ |
| Formatting / lint            | PASS            | `$env:TOOLS_MOORKYZ_COM_FIXTURE_PORT='48731'; $env:TOOLS_MOORKYZ_COM_PORTAL_PORT='48732'; pnpm check` | Lint and repository format checks passed.                                                                                                                                                                                                                                            |
| Typecheck                    | PASS            | Same `pnpm check` invocation                                                                      | SDK, vanilla, React, and portal typechecks passed.                                                                                                                                                                                                                               |
| Unit / integration tests     | PASS            | Same `pnpm check` invocation                                                                      | 126 Node tests passed; includes 2 adapter SSR/Strict Mode lifecycle tests.                                                                                                                                                                                                           |
| Browser / accessibility      | PASS WITH RISKS | Same `pnpm check` invocation                                                                      | 24 Chromium tests passed, including existing axe checks. Adapter lifecycle itself is exercised in ReactDOM/Happy DOM, not a dedicated Playwright React mount.                                                                                                                        |
| Build / consumer fixture     | PASS            | Same `pnpm check` invocation; `pnpm --filter @tools.moorkyz.com/sdk pack --dry-run`                     | SDK build, React consumer typecheck, and package dry-run passed; dry-run includes `dist/react.js` and `dist/react.d.ts`.                                                                                                                                                         |
| Security / dependency review | PASS WITH RISKS | `pnpm install --frozen-lockfile`; package/lock and local license metadata inspection              | Frozen lockfile and supply-chain policy passed. React is an optional peer; React and ReactDOM are development dependencies only. React, ReactDOM, and `@types/react` metadata report MIT. No network, credential, or permission path added; no separate vulnerability audit was run. |
| Performance (if relevant)    | N/A             | Not measured                                                                                      | Adapter allocates one controller in an effect and disposes it during cleanup; no background work when unmounted.                                                                                                                                                                     |
| `git diff --check`           | PASS            | `git diff --check`                                                                                | No whitespace errors.                                                                                                                                                                                                                                                                |
| Manifest integrity           | PASS            | PowerShell SHA-256 comparison over `MANIFEST.json`                                                | All 16 listed document hashes matched.                                                                                                                                                                                                                                               |

## 4. Risk and quality review

- Public contract, feature-default, and compatibility impact: Additive draft subpath only; core entry remains unchanged and no feature defaults changed. Adapter recreates the controller when config, host, or callback identity changes; documented for callers.
- Security, privacy, permissions, and secrets: Adapter delegates all host actions and permissions to the existing controller/host. It adds no network access, persistence, telemetry, or secrets.
- Accessibility, keyboard, and reduced motion: No adapter-specific controls; the existing accessible toolbar is mounted unchanged. Existing Chromium axe checks passed.
- Disabled-feature work/resource/network behavior: React effect runs only when the component is mounted; unmount disposes the controller. Root package import does not import the React subpath.
- Error handling, cleanup, and recovery: Mount failure throws with the structured toolbar error as cause; controller is disposed if mounting or `onController` notification fails. Cleanup disposes before notifying `onController(null)`, including React Strict Mode effect replay.
- Dependency, performance, and regression exposure: Adds optional React peer metadata plus dev-only React/types/ReactDOM entries. Frozen install and package dry-run passed; main entry stays React-free.

## 5. Findings and residual risks

| ID  | Severity | Finding and evidence                                                                              | Mitigation / owner                                       | Status                  |
| --- | -------- | ------------------------------------------------------------------------------------------------- | -------------------------------------------------------- | ----------------------- |
| R1  | LOW      | Config/host/callback reference changes intentionally unmount and recreate the toolbar controller. | Consumers should memoize stable values; documented.      | Accepted draft behavior |
| R2  | LOW      | React adapter has not yet been exercised in the full browser/framework/device matrix.             | Continue M7 consumer compatibility work; owner: project. | Open                    |

- Known gaps / checks not run: M7 remains in progress. No Vue/Angular adapter, extension fault-containment suite, dedicated Playwright React mount, bundle isolation measurement, or manual assistive-technology test has been completed.
- Required approvals: None for this optional additive adapter; no mandatory React dependency was added. No publishing, deployment, push, or external integration was performed.
- Rollback or recovery: Revert the focused M7 adapter commit; the framework-neutral root remains available.
- Release readiness: `NOT READY`

## 6. Completion record

- Audit created before the task commit and included in that commit, or reason not committed: Created before staging; will be included in the focused local commit.
- Final commit hash: `PENDING`
- External actions: `NOT AUTHORIZED` unless separately approved.

**Limitations:** An agent self-audit is not an independent security review, accessibility certification, or release approval.
