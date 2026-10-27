# Development Audit: Minimal root and src developer workspace

> Fill in with observed evidence. Never mark an unexecuted check as PASS.

| Field | Value |
|---|---|
| Audit ID | `2026-10-27T04-16-26Z-minimal-root-and-src-developer-workspace` |
| Created (UTC) | `2026-10-27T04:16:26.857Z` |
| Task / issue | Minimal root and src developer workspace |
| Auditor | Copilot assistant self-audit; local Windows/Node, native fixtures, Playwright and local Docker |
| Branch / baseline commit | `master` / `54ad783` |
| Final commit | Report in task completion; do not edit this audit after committing |
| Scope reviewed | Minimal-root migration, internal workspace/config/scripts/tests/packages, user launchers, CI/container paths, docs/source bundles and preserved runtime data |
| Outcome | `PASS WITH RISKS`: migration checks pass; confirmed pre-existing branding and intermittent WebKit contrast failures remain documented |

## 1. Request and outcome

- Requested behavior: Inspect the sibling template structure, keep close to no root files, and place most non-user implementation/developer material under `src/`.
- Implemented behavior: Tracked root files reduced from 36 to 15 (10 non-dot files plus five required discovery/ignore/runtime-pin files). Moved workspace manifest/unchanged lockfile, pnpm/formatter/linter/TypeScript/browser config, portal packages, product scripts, repository/browser tests and shared CLI/checkups under `src`. Launcher configs, optional installers and internal delegates are under `src/.moorkyz`. GitHub-recognized contribution/security guides moved to `.github`; the old READM typo entry is retained under `docs/legacy`.
- Acceptance criteria and evidence: A root-file allowlist regression enforces the small root and absence of a root package manifest. Both Windows launchers resolve the new portal location and original 4320/3001 preferences. Existing installed `moorkyz` profiles retain their root Node entrypoint. CLI tests cover new source-folder configs and explicit legacy-root config precedence. CI, Docker, standalone copies, generated bundles, test fixtures and active documentation reference the new paths.
- Public setup/copy commands in portal metadata, the contributor page, local-service connection-error guidance and independent tool integration readmes were also rewired to `pnpm --dir src`; a registry regression enforces this surface.
- Safety/continuity: The originally running verified CLI-managed portal was stopped for migration, restored on its original `http://127.0.0.1:4320` URL, and checked for HTTP 200 after the final production rebuild. No other service was intentionally stopped. Logs/state/preferences, canonical independent tools, history and existing audits were not moved or deleted.
- Intentional exclusions: No UI redesign, language/feature change, template overwrite/adoption, source-repository split, dependency upgrade, historical evidence rewrite, archive pruning, user data cleanup, profile installation or remote action. Required root Git/container/agent discovery files and thin user launchers stay compatible.
- Relevant decisions/contracts: Root AGENTS and current decisions, sibling template inspection (read-only), owner clarification that internal material belongs under `src`, CLI safety/process ownership and tool source-provenance rules. The scope form was unavailable; the reversible full-workspace option was used while retaining runtime data and compatibility entrypoints.

## 2. Change inventory

| Path | Change | Risk |
|---|---|---|
| `src/{package.json,pnpm-lock.yaml,pnpm-workspace.yaml,tsconfig.base.json,eslint.config.mjs,.prettier*,playwright*.mjs}` | Relocate the pinned workspace/build/test configuration; scripts retain repository-root CWD where needed | Medium; package/developer command location changes |
| `packages/portal` -> `src/packages/portal` | Move source/assets intact; update trace root and standalone asset/server paths | Medium; build/deployment layout |
| `.template` -> `src/.template`, CLI config/installers/delegates -> `src/.moorkyz` | Centralize internals, keep working user wrappers and root profile compatibility | Medium; source resolution and config discovery |
| `scripts` -> `src/scripts`, `tests` -> `src/tests` | Update tool indexing/download output, imports, fixtures, scanners and runner paths | Medium; cross-surface references |
| Root wrappers, Dockerfile/ignore, GitHub workflow/dependency config | Rewire to `src` without changing ports, hosting context, permissions or dependency pins | Medium; CI/runtime consumers |
| Root/current docs, `src/README.md`, contribution/security guides, legacy HTML | Document the minimal root, new commands and compatibility boundaries | Low |
| Tool integration guidance and Media Recover/Visory provenance | Update moved root automation commands and explicitly regenerate hashes/bundles | Low; canonical tool paths remain unchanged |
| Regression tests and this audit | Root allowlist, actual formatter exclusions, source-config precedence and evidence | Low |

## 3. Verification evidence

Use `PASS`, `FAIL`, `NOT RUN` (with reason), or `N/A`. Record exact commands and results.

| Check | Result | Exact command / method | Evidence / notes |
|---|---|---|---|
| Formatting / lint | `PASS` | `pnpm --dir src lint`; `pnpm --dir src format:check` | Final configured checks exit 0. Added a real Prettier API regression proving generated `.next`, source assets, dependency/lock/test output are ignored while maintained source/delegates are not. |
| Typecheck | `PASS` | `pnpm --dir src typecheck` | Generated index check and strict portal TypeScript pass. |
| Unit / integration tests | `PASS` | `pnpm --dir src test:unit` | Final 139/139, no failures or skips. Includes Windows GUI compile/launch/close, storage, lifecycle/readiness/ownership, source bytes/provenance, public setup commands, fixtures and deployment mocks. |
| Browser / accessibility | `PASS` (selected scope) | `pnpm --dir src test:browser --grep-invert "crossed-tools branding stays large"` | 114/114 passed across Chromium/Firefox/WebKit. Nine pre-existing logo-size cases intentionally excluded; see finding R1. This is not independent accessibility certification. |
| Final source-download browser recheck | `PASS` | `pnpm --dir src test:browser --grep "desktop source.*readable"` | Final 6/6 passed after the last documentation/provenance regeneration. |
| Final public-command views | `PASS WITH BASELINE FAILURE` | `pnpm --dir src test:browser --grep "tool catalog and detail pages remain accessible|/contributing has no|desktop source.*readable"` | 11/12 passed; all contributor/source-download cases passed. One WebKit contrast failure in the unchanged form was isolated and reproduced on the original commit (R5), rather than silently retried away or fixed as unrelated styling. |
| WebKit baseline classification | `CONFIRMED PRE-EXISTING FAIL` | Current: `pnpm --dir src test:browser --project webkit --grep "tool catalog and detail pages remain accessible" --repeat-each 3`; baseline: `git archive HEAD` into an isolated fixture, `pnpm install --offline --frozen-lockfile`, `pnpm build`, then `PORTAL_TEST_PORT=4189 pnpm exec playwright test tools.spec.js --project webkit --grep "tool catalog and detail pages remain accessible" --repeat-each 3` | Current 2/3 passed; unchanged `54ad783` baseline 1/3 passed. Both report the same 1.08 contrast for native form controls. Form CSS/component are unchanged after EOL normalization. Baseline fixture/ZIP removed after retaining its evidence log; no baseline Git root or user data created. |
| Opt-in local-service browser config | `PASS` | `pnpm --dir src test:directory-cleaner:browser` | 6/6 in three engines, including stale-preview rejection and recoverable fixture bytes. Helpers were runner-owned and stopped on completion. |
| Production build | `PASS` | `pnpm --dir src build` | Final Next.js 16.4.1 optimized build/type generation and standalone copies succeed. Managed dev portal still returns HTTP 200 afterward. |
| Container build / smoke | `PASS` | `docker build --tag moorkyz-tools:root-cleanup-check .`; `docker run --detach --name <unique-task-name> --publish 127.0.0.1::3000 moorkyz-tools:root-cleanup-check` | Confirmed local `npipe` daemon before sending context. Final container root HTTP 200 and served Visory provenance matched canonical files exactly. Specific task container and temporary image tag removed afterward; no push/prune. |
| Repository / source checks | `PASS` | `pnpm --dir src check:repository`; `pnpm --dir src check:source` | Final 64 syntax checks and 14/14 structure tests; bounded scanner checked 456 paths and current portal/cleaner sink prefixes. Not a complete security audit. |
| Dependency restoration / pin preservation | `PASS` | `pnpm --dir src install --frozen-lockfile`; compare `git show HEAD:pnpm-lock.yaml` bytes with `src/pnpm-lock.yaml` | All 126 packages reused, none downloaded in the local restore. Lockfile is byte-identical; no new dependencies or version changes. |
| Source/catalog regeneration | `PASS` | `node tools\visory-media-downloader\src\scripts\development\readme.mjs`; `node src\scripts\sync-visory-source.mjs`; `node src\scripts\sync-mediarecover-source.mjs`; `node src\scripts\prepare-tool-sources.mjs` | Existing generators refreshed only reviewed source/doc outputs. Full source-byte tests and index checks pass. |
| User entrypoints / service continuity | `PASS` | `.\moorkyz.cmd list`; `.\tools.moorkyz.com.cmd list`; `node .\moorkyz.cjs start`; `node .\moorkyz.cjs status`; HTTP GET `http://127.0.0.1:4320/` | Both Windows wrappers work; restored original managed namespace/mode/port is running, with a fresh verified supervisor and HTTP 200. |
| Performance | `N/A` | No benchmark | No product algorithm change; generated folders are explicitly excluded from formatting and container context where applicable. |
| `git diff --check` | `PASS` | `git diff --check` | No whitespace errors; Git prints expected checkout-normalization notices. |
| Unfiltered aggregate gate | `NOT RUN` to completion / known baseline `FAIL` | Initial `pnpm --dir src test:browser` exposed the existing 64px expectation vs unchanged 40px implementation and was interrupted during migration fixes | Do not claim `pnpm --dir src check` is fully green. All task-caused path, byte and formatter findings were corrected and the affected suites rerun. |

## 4. Risk and quality review

- Public contract, feature-default, and compatibility impact: Developer package commands now use `pnpm --dir src ...`. User-facing shell/CMD entrypoints and the main root Node compatibility file stay available. Explicit legacy root configs retain precedence; this repository's normal configs live under `src/.moorkyz/config`. Tool roots, commands, ports and runtime state namespaces remain unchanged.
- Security, privacy, permissions, and secrets: No new dependency, permissions, account/telemetry, listener, profile/registry change or hosted processing. Docker was a local build/smoke only and excluded runtime/recovery/test data. The bounded source scanner uses the new source prefix. No credentials were used for deployment or transmitted.
- Accessibility, keyboard, and reduced motion: No redesign or interaction change; only necessary setup/copy/error instructions changed. Broad browser/axe tests and native GUI/keyboard/motion fixtures pass in their recorded scope, except the confirmed pre-existing intermittent WebKit finding R5. Branding source/global CSS are byte-identical to baseline; form styles/components are unchanged after EOL normalization. Do not infer independent certification.
- Disabled-feature work/resource/network behavior: Existing local services remain opt-in. The only restored long-lived process is the previously running managed portal. Test helpers and the task container/tag were cleaned up.
- Error handling, cleanup, and recovery: Old generated dependencies/build caches were preserved under `archive/workspace-cleanup-20261027T043830351/` with `receipt.json`. A Windows junction-aware Move-Item interruption split the old dependency cache; the remaining directory was preserved by atomic rename and fresh active dependencies restored from the unchanged lock. No personal files or logs were used for fixtures. Eight task-created duplicate generated files were deleted only after verifying identical correct canonical copies, then their now-empty scratch directories were removed.
- Dependency, performance, and regression exposure: No dependency upgrade. Root-file and formatter-source/generated-boundary regressions protect the new organization. CI still builds from the root Docker context; only internal workspace/COPY/server locations changed.

## 5. Findings and residual risks

| ID | Severity | Finding and evidence | Mitigation / owner | Status |
|---|---|---|---|---|
| R1 | Low / pre-existing | Branding tests require 64px, while the existing header/CSS use 40px. Nine matching cases remain outside the selected pass. Baseline header and global CSS were compared byte-for-byte; branding test text is unchanged after line-ending normalization. | Owner should reconcile the prior compact-branding decision and stale expectations separately. No unrelated UI change here. | Residual |
| R2 | Medium / task-caused | Initial moves left fixture/source-reader paths and one duplicated `src` output prefix stale; stale public ZIP/JSON bytes were caught by exact comparisons. | Corrected every consumer and canonical output location; final 139 unit cases and source-download browsers pass. | Fixed |
| R3 | Medium / task-caused | Relocating the formatter ignore file initially exposed generated build output to formatting and hid maintained launcher source. | Explicit recursive generated-output patterns, source visibility regression, final fresh build and checks. | Fixed |
| R4 | Low | Preserved generated caches occupy local recovery space and may need re-resolution if rolled back because pnpm links are location-specific. | Receipt records both cache portions; prefer a frozen install in the restored layout rather than blindly executing preserved caches. Owner may review recovery data later. | Expected |
| R5 | Medium / pre-existing | Intermittent WebKit native form-control contrast failure (light text against a white control surface) reproduced on the original `54ad783` snapshot, with 2/3 baseline runs failing and 1/3 current repeat runs failing. | Address separately with the existing form/theme/accessibility work; no cleanup-caused CSS change or hidden test suppression. | Residual, baseline confirmed |

- Known gaps / checks not run: The unfiltered full gate retains R1 and can encounter R5; no production deployment, remote CI execution or independent review was performed. No real Visory account, personal media or phone was used. Historical audit records and the pre-existing untracked Visory audit/tools LICENSE are preserved and excluded from this commit.
- Required approvals: No push, merge, publishing, deployment, signing, profile changes, prerequisite installation or archive pruning authorized/performed.
- Rollback or recovery: Revert this focused commit and restore dependencies with that layout's frozen install. Existing logs/state/preferences and all source/audit history remain in place. The ignored recovery receipt records preserved generated directories; do not move caches over later edits or running processes.
- Release readiness: `READY WITH CONDITIONS` for the scoped local cleanup; resolve the pre-existing branding mismatch and intermittent WebKit form contrast before claiming the entire unfiltered CI gate is green.

## 6. Completion record

- Audit created before the task commit and included in that commit, or reason not committed: Generated with the canonical CLI/template and filled before staging only task-owned changes.
- Final commit hash: `PENDING`
- External actions: `NOT AUTHORIZED` unless separately approved.

**Limitations:** An agent self-audit is not an independent security review, accessibility certification, or release approval.
