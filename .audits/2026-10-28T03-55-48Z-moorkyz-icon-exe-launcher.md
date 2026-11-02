# Development Audit: Unified startup and icon-bearing EXE

| Field | Value |
|---|---|
| Audit ID | `2026-10-28T03-55-48Z-moorkyz-icon-exe-launcher` |
| Created (UTC) | `2026-10-28T03:55:48Z`; completed after the follow-up startup request |
| Task / issue | Create EXE deployer with icon; unify RUN/moorkyz/EXE startup, bounded repairs and task-style error reports |
| Auditor | Copilot agent self-audit |
| Branch / baseline commit | `master` / `a59794e`; extensive pre-existing staged/unstaged workspace migration |
| Final commit | Not created: feature changes overlap pre-existing staged additions/renames; see completion record |
| Scope reviewed | New EXE source/builder, shared startup, launch wiring, package-manager configuration, related documentation and regression tests |
| Outcome | `PASS WITH RISKS`: focused functionality and structure checks pass; source scanner remains blocked by a pre-existing deleted/moved audit path |

## 1. Request and outcome

- Initial request: "create deployer for exe with icon". Clarification was
  attempted; the owner was unavailable. The reversible initial assumption was a
  local unsigned launcher with the existing icon, not an installer/publication.
- Follow-up: RUN, moorkyz and EXE must start the same numbered list, checking
  files/structure, installs and tech stack, installing missing dependencies,
  retrying once, then writing task-like errors with suggested fixes and repeated
  history entries after later failed pairs.
- Implemented: all three default entrypoints use one Node startup module:
  required files plus repository syntax/structure; manifest-derived tech stack;
  dependency/lockfile checks and frozen-lockfile repair. Two attempts total per
  stage. Successful preflight shows one action list, with no automatic selection.
- Error tasks: local JSONL history per startup stage, both attempt results,
  timestamp, status, stack when readable, suggested fix, verification command
  and previous-entry ID. Subsequent failed pairs append; successful retries append
  resolution. Task listing/retry commands are exposed. No recursive report loop.
- The EXE uses the console subsystem so the actual checklist/menu remains visible
  and interactive. It embeds the existing icon and preserves the shared exit code.
- Existing explicit CLI commands retain their behavior. Optional tools/services
  remain independently enabled. Machine-wide prerequisites are not installed.
- Stack remains Node >=22.18.0, pnpm 11.8.0, Next.js 16.4.1, React/React DOM
  ^19.3.1 and TypeScript ^6.0.3. No dependency versions or lockfile were changed.
- Contracts: root AGENTS, current product decisions, engineering workflow,
  local GUI and repository structure guides. No unattended cleanup was added.

## 2. Change inventory

| Path | Task-owned change | Risk |
|---|---|---|
| `src/.moorkyz/app/MoorkyzLauncher.cs` | Icon-bearing console launcher, checkout/runtime checks and shared startup dispatch | Low; explicit launch only |
| `src/scripts/deployment/build-moorkyz-exe.ps1` | Local C# compilation, icon/output options, SHA-256, no overwrite and temporary cleanup | Low; no installer/publication |
| `src/.moorkyz/scripts/helpers/startup.cjs` | Ordered preflight, dependency probing/repair, bounded retry, task histories and action list | Medium; explicitly authorized project-local dependency installation |
| `src/.moorkyz/scripts/moorkyz.cjs` | Default/explicit launch, tasks commands and root resolution | Low; existing explicit commands preserved |
| `RUN.cmd` | Thin shared CLI delegate with error exit preservation | Low |
| `src/package.json` | Add dependency-probe script | Low; no dependency changes |
| `src/pnpm-workspace.yaml` | Explicitly retain non-global virtual store | Low; matches existing installed state |
| `src/tests/tooling/moorkyz-exe.test.cjs` | Real Windows compilation, icon, native errors and common startup checks | Low; isolated build/runtime fixtures |
| `src/tests/tooling/moorkyz-startup.test.cjs` | Retry/history/safety fixtures and actual common-menu integration | Low; no live service actions |
| `src/tests/tooling/repository-structure.test.cjs` | Permit user-facing RUN.cmd | Low |
| `README.md`, `src/.moorkyz/app/README.md`, `src/.moorkyz/scripts/README.md`, `src/scripts/deployment/README.md`, `src/docs/engineering/repository-structure.md` | Explain common flow, stack, artifact, boundaries and commands | Low |
| `.audits/README.md`, `src/docs/README.md`, CLI README | Fix stale migration links that blocked the new preflight | Low; no historical audit evidence changed |
| This audit | Verification evidence and limitations | Documentation only |

Generated ignored artifact: `src/.moorkyz/app/dist/Moorkyz.exe`.
Final observed SHA-256:
`C8AB3E759ED9D9FA52E05964741A0EEFC86FCAC0913FC64D0D2A20B9F3D3DDF0`.
No executable binary is staged or committed.

## 3. Verification evidence

| Check | Result | Exact command / method | Evidence / notes |
|---|---|---|---|
| Focused plus CLI compatibility suite | PASS | `node --test --test-concurrency=1 src\tests\tooling\moorkyz-startup.test.cjs src\tests\tooling\moorkyz-exe.test.cjs src\tests\tooling\moorkyz-gui.test.cjs src\tests\tooling\launcher-startup.test.cjs src\tests\tooling\tools.moorkyz.com-cli.test.cjs` | 31 passed, 0 failed, 0 skipped |
| Final strengthened default-menu tests | PASS | `node --test --test-concurrency=1 src\tests\tooling\moorkyz-startup.test.cjs src\tests\tooling\moorkyz-exe.test.cjs src\tests\tooling\moorkyz-gui.test.cjs` | 14 passed, 0 failed, 0 skipped; additionally runs real EXE default list and compares default RUN/moorkyz menu text |
| Repository structure/syntax | PASS | `pnpm --dir src check:repository` | 280 JavaScript syntax checks, 14/14 structural tests |
| Installed dependencies | PASS | `pnpm --dir src run startup:dependencies` | Existing declared workspace packages installed; no repair required |
| Final native artifact | PASS | `powershell.exe -NoProfile -File .\src\scripts\deployment\build-moorkyz-exe.ps1` then `& .\src\.moorkyz\app\dist\Moorkyz.exe --check` | Real C# build with warnings as errors; all three checklist stages pass |
| Native artifact safety | PASS | EXE test above | PE console subsystem, exact embedded icon-image bytes, overwrite refusal, broken/missing icon, wrong extension, root discovery, old/missing/malformed Node, drive-relative PATH rejection, exit-code preservation |
| Bounded recovery/history | PASS | Startup test above | At most two install attempts; second-attempt recovery; no install in check mode or on unrelated errors; second failed pair appends linked entry; later success records resolution |
| Task storage/privacy | PASS (self-test only) | Startup fixtures and direct review | Junction destination rejected; common credentials/URLs redacted; outside fixture unchanged; missing source not fabricated |
| Lint | PASS | `node .\src\node_modules\eslint\bin\eslint.js --config .\src\eslint.config.mjs --max-warnings 0 src\.moorkyz\scripts\helpers\startup.cjs src\.moorkyz\scripts\moorkyz.cjs src\tests\tooling\moorkyz-startup.test.cjs src\tests\tooling\moorkyz-exe.test.cjs src\tests\tooling\repository-structure.test.cjs` | Correct repository-root context, zero warnings/errors |
| Formatting | PASS | `pnpm --dir src exec prettier --check package.json pnpm-workspace.yaml .moorkyz\scripts\helpers\startup.cjs tests\tooling\moorkyz-startup.test.cjs tests\tooling\moorkyz-exe.test.cjs .moorkyz\app\README.md` | All selected implementation files match project style |
| Bounded source gate | FAIL (pre-existing migration blocker) | `pnpm --dir src check:source` | Scanner attempts to read tracked-but-deleted `audits/2026-10-23-repository-operations.md`; that evidence is in the pending .audits migration. Scanner not weakened or changed |
| Typecheck | N/A | No TypeScript changed | Native C# compiler check passed |
| Full portal build/browser suite | NOT RUN | Launcher/tooling scope; no web UI or TS changes | Existing native WPF fixture passed; no independent accessibility certification |
| Dependencies/security review | PASS (self-review only) | Direct inspection and fixtures | No new package, dependency upgrade, credential, policy bypass, hosted connection or native prerequisite installation |
| Whitespace | PASS | `git diff --check` on task paths; `git diff --no-index --check` against an empty temporary file for task-owned new files | No task whitespace errors |

Earlier checks exposed:

- Unavailable `Get-FileHash`: replaced with .NET SHA-256 before output publication.
- Stale CLI/audit documentation links: repaired because they blocked shared
  startup's structure stage. The final structure check passes.
- pnpm verification differed for implicit global-virtual-store defaults after
  manifest edits. Explicit `enableGlobalVirtualStore: false` preserves the
  already-installed workspace setting and project-local package layout.
- A parallel native GUI hover-timing fixture failed once under load; sequential
  focused/native runs pass. Test commands above intentionally serialize files.
- The EXE test's initial runtime failure stub returned an error during version
  probing instead of dispatch. The fixture was corrected and exit-code forwarding
  is verified. Full structure scanning required a longer integration-test bound
  than the original 15-second EXE smoke check.

## 4. Risk and quality review

- Defaults/compatibility: bare startup now performs the user-requested checks and
  can install missing workspace packages. Explicit existing CLI commands preserve
  their prior no-implicit-install behavior; compatibility tests verify this.
- Authorization: local package restoration is limited to declared dependencies,
  the exact pnpm pin and frozen lockfile. Existing pnpm lifecycle/build policies
  remain active. No mandatory dependency, native compiler, runtime, browser,
  FFmpeg, per-tool runtime, profile or machine prerequisite is auto-installed.
- Safety: neither source validation nor task retry starts optional tool services.
  The action list requires selection. Headless sessions select nothing.
- Reporting: task files are local ignored data; bounded diagnostic excerpts and
  common credential fields are redacted. Other paths/metadata can remain private.
  Storage uses the existing scoped-path helper to reject symlinks/junctions.
- Failure handling: a stage is tried twice, then fails with a report/fix. Task
  retries preserve history. Report write failure stays explicit and does not
  recursively generate new reports. Frozen installs are allowed to finish under
  pnpm's own policy rather than killing a shim and leaving an installer running.
- Bootstrap boundary: Node and readable bootstrap code must exist before the
  Node-based task reporter can run; native/CLI bootstrap errors cannot auto-install
  Node or manufacture a report through unavailable code.
- EXE builder: unique temporary directory, cleanup in finally, hash before
  publication and non-overwriting File.Move. No signing, installer, elevation,
  remote deployment or publication.
- Accessibility/performance: existing GUI keyboard/reduced-motion behavior is
  unchanged. Complete syntax/structure scanning adds startup latency; native
  fixtures observe roughly 15 seconds per complete preflight on this machine.

## 5. Findings and residual risks

| ID | Severity | Finding and evidence | Mitigation / owner | Status |
|---|---|---|---|---|
| START-1 | Low | EXE is unsigned and depends on checkout/Node | Documented, explicit errors; signing/distribution remains an owner decision | Accepted local-use limitation |
| START-2 | Low | Machine-wide prerequisites cannot be auto-installed safely here | Report suggested explicit setup; keep independent tools opt-in | Intentional boundary |
| START-3 | Low | Source scanner cannot read a pre-existing moved/deleted audit path | Preserve migration and history; do not silently skip scanner inputs | Pre-existing blocker |
| START-4 | Low | Task changes overlap staged CLI relocation and untracked moved docs | Leave index untouched rather than commit unrelated work or an incomplete feature | Commit blocked |
| START-5 | Low | Parallel native GUI timing test is load-sensitive | Final focused checks serialize test files; no claim that full parallel suite passed | Known test limitation |

- No full portal build, browser suite, clean-machine installer or public release
  was performed. No installer was implemented.
- `--check` does not install dependencies or show an action prompt, but can append
  a failed-check task entry. It does not certify all native toolchains or WPF policy.
- Build and integration checks use local fixtures; dependency-repair failure
  branches use injected commands, not deliberate disruption of real packages.
- Rollback: revert task-owned edits while preserving earlier migration changes;
  remove only this task's source/test/guide and named ignored build output.
  No personal files or existing runtime data were cleaned or migrated.
- Release readiness: `READY WITH CONDITIONS` for local launch use; not a signed,
  standalone bundled desktop release or proof that the full repository gate passes.

## 6. Completion record

- Audit prepared from the current template before completion.
- No local commit created. The initial index already contains 26 migration
  entries, including the startup core's destination as a staged addition/rename.
  Committing its full current file would absorb prior user changes; committing
  only the new files would leave a nonworking feature against HEAD. The feature
  and audit are saved in the worktree, and the original index is preserved.
- Final commit hash: `NOT CREATED - pre-existing staged migration overlap`.
- External actions: `NOT AUTHORIZED`; no push, signing, publication or deployment.

**Limitations:** An agent self-audit is not an independent security review,
accessibility certification, or release approval.
