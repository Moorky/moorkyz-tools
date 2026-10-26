# Development Audit: Self-contained drop-in tool projects

> Local observed evidence. Agent self-audit, not independent certification.

| Field | Value |
|---|---|
| Audit ID | `2026-10-26T21-39-18Z-self-contained-drop-in-tool-projects` |
| Created (UTC) | `2026-10-26T21:39:18.730Z` |
| Task / issue | Keep tool projects inside a fitting project folder and automate integration/indexing when dragged in |
| Auditor | AI assistant using Copilot SDK in VS Code; self-audit |
| Branch / baseline commit | `master`, `ec313cd`; pending catalog/hero/header/version/monogram edits preserved |
| Final commit | Report in task completion; do not edit this audit after committing |
| Scope reviewed | Tool discovery/metadata, in-folder allowlists, generated index/bundles, source provenance, portal consumers, docs, regression fixtures and isolated drop-in browser acceptance |
| Outcome | `PASS WITH RISKS` - integration verified; plain folders need explicit metadata and new executable workspaces still need approved implementation |

## 1. Request and outcome

- Requested: projects should be inside the tools project, draggable into one
  fitting folder, with integration/indexing automated rather than sibling repos.
- Implemented: `tools/<project>/` owns each full project plus `portal.json`
  metadata and optional `source-files.json`. New `pnpm tools:index` discovers
  manifests and generates a checked-in typed index used by the catalog, search,
  route metadata and shared detail/source views. Portal dev/build automatically
  index before packaging. No central list/import/bundle registration per tool.
  The old sibling `tools/*.files.json` allowlists were moved into projects;
  Media Recover's existing object-format native manifest was retained/extended.
- Acceptance: existing five tools preserve their IDs/slugs/content/order.
  In an isolated staged-tree checkout, a sixth synthetic project was dropped into
  `tools/` without central edits; it appeared in card, route, canonical metadata,
  search and exact source JSON/ZIP in Chromium, Firefox and WebKit.
  Its source throws if executed; discovery/build never executed it.
- Intentional boundaries: raw folders without metadata are explicitly reported,
  not guessed into public entries. No live watcher, auto-install, arbitrary
  project-code execution, service startup, hosting or deployment. Desktop hash
  refresh stays explicit; unknown workspaces, unsafe sources, linked paths,
  nested Git roots and duplicate identities fail rather than silently falling back.
- Contracts: root/tool AGENTS, source allowlists/provenance, and decision
  "self-contained drop-in tool folders". The outer `repos` is just this checkout's
  machine-local parent; no project is moved outside the repository or given a
  new Git root. Tagged old Downloads README path was already stale from rename;
  this task does not create another canonical copy or synchronize sibling sources.

## 2. Change inventory

| Path | Change | Risk |
|---|---|---|
| `scripts/index-tools.mjs` | Discover/validate local manifests and allowlisted UTF-8 source; deterministic generation and read-only stale check | Medium: new build-time ingestion boundary, negative fixtures included |
| `scripts/prepare-tool-sources.mjs` | Discover source bundles, retain native provenance gates, validate before replacing downloads, retire old generated assets | Medium; exact-byte/retirement fixtures |
| `tools/*/portal.json`, `tools/*/source-files.json` | Project-local metadata/allowlists; old sibling allowlists removed | Low; baseline metadata parity checked |
| `packages/portal/src/data/{tools,tool-index}.ts` | Thin typed registry facade and generated index; monogram optional for new metadata-only tools | Low; no unsafe casts/runtime execution |
| Native sync scripts, source tests, source provenance | Read project-local allowlists, preserve Media Recover object format, refresh reviewed hashes | Low; native source-manifest command verified |
| Root/portal package scripts | Index/check commands, formatting globs, typecheck stale gate | Low; dependency lists/lockfile unchanged |
| `tests/tooling/tool-index.test.js` | Add/remove/order, malformed/duplicate/unsafe/link/Git/output/state/non-text and non-execution fixtures | Low; isolated temporary data |
| Root/project docs and AGENTS | Drop-in location, contract/example, commands, boundaries | Low |

Unrelated catalog work was not staged. Its metadata now remains as worktree
changes in the per-project manifests/generated index and registry type, rather
than the deleted central array. Baseline-compatible manifests/index/provenance
were prepared separately in the index and tested in an isolated consumer. The
other task's UI, display versions, scores, descriptions and monogram-removal
edits remain intact, not silently incorporated in this commit.

## 3. Verification evidence

Commands at repository root unless stated. The consumer was a temporary local
shared-object clone populated from the exact staged tree, not a remote checkout.
Existing dependencies were reused via inspected junctions; nothing was installed.

| Check | Result | Exact command / method | Evidence / notes |
|---|---|---|---|
| Unit/source/catalog | PASS | `node --test tests\tooling\tool-index.test.js tests\tooling\tools-catalog.test.js tests\tooling\mediarecover-snapshot.test.js tests\tooling\visory-source.test.js` | 27/27 final worktree and exact staged five-tool consumer; ZIP original bytes and native EOL/provenance preserved |
| Production build | PASS | `pnpm build` | Default Turbopack, TypeScript, discovery/ZIP/standalone prep; five tool projects, four source bundles |
| Browser/accessibility | PASS | `pnpm exec playwright test tools.spec.js mediarecover.spec.js visory.spec.js` | 45/45; selector also matched compact-tools; Chromium/Firefox/WebKit, light/dark, narrow widths, axe, search, downloads, keyboard and dormant services |
| Real drop-in integration | PASS | Session utility `verify-drop-in.mjs`, followed by `--browser-only` after correcting its local Playwright import | Exact staged five-tool source tests; add sixth synthetic self-contained folder; run source prep and `node scripts\run-next.mjs build --webpack` in consumer portal; prepare standalone; test all three engines on loopback port 4187 |
| Drop-in observed output | PASS | Browser assertions: unique card/title, new route/canonical URL, search link, JSON source byte equality, ZIP contains exact CRLF sentinel | No central metadata imports/bundle arrays changed; throwing project source never executed; temporary server stopped in `finally` |
| Native packaging compatibility | PASS | `powershell.exe -NoProfile -File tools\mediarecover\scripts\source-manifest.ps1` | 50 bounded UTF-8 files; preserved object `.files` manifest shape; no native build, recovery or device scan launched |
| Index/check | PASS | `pnpm tools:index:check` | Five projects; read-only comparison normalizes checkout EOL; stale/add/remove behavior separately tested |
| Explicit hash refresh | PASS | `node scripts\sync-mediarecover-source.mjs`; `node scripts\sync-visory-source.mjs` | Reviewed metadata/allowlist changes pinned. Consumer baseline hashes verified separately from pending worktree metadata |
| Typecheck | PASS | `pnpm typecheck` | Includes `index-tools.mjs --check`, then `tsc --noEmit`; consumer six-tool build also typechecked |
| Lint | PASS | `pnpm lint` | Complete configured lint, including new scripts/tests/generated TS |
| Formatting | PASS | `pnpm format:check` | Complete configured format check with project-local JSON manifests |
| Consumer lint/format | PASS | In consumer: `pnpm --config.verifyDepsBeforeRun=false exec eslint packages\portal\src scripts tests\tooling\tool-index.test.js tests\tooling\mediarecover-snapshot.test.js tests\tooling\visory-source.test.js`; same pnpm override with `exec prettier --check packages\portal\src\data scripts tests\tooling\tool-index.test.js tests\tooling\mediarecover-snapshot.test.js tests\tooling\visory-source.test.js` | Exact baseline schema and generated-index format pass; override only accommodates reused dependency directories |
| Repository | PASS | `pnpm check:repository` | 57 JS syntax checks and 13/13 structure tests |
| Bounded credential/sink scan | PASS | `pnpm check:source` | 408 paths before final audit; no reported credential patterns or portal/cleaner executable/HTML assignment sinks; not full security certification |
| Whitespace/scope | PASS | `git diff --cached --check`; task-only staged diff and metadata migration review | Shared-file snapshots exclude pending redesign; source outputs/artifacts/caches not staged |
| Initial failures | FAIL, resolved | Focused commands above | Duplicate self-entry in migrated native allowlist fixed; Node TS import given `.ts`; preserved native object format; two unused destructures removed; README formatted; accidental nested test moved to top level; old tracked-manifest scan passed after staging deletions |
| Consumer bootstrap | PASS with environment caveat | Webpack build using existing dependency junctions | Same external-junction Turbopack limitation already observed in prior task; main default build passes. Consumer tracing warns of 27 outside-root dependency files; standalone checks reuse installed portal dependencies |
| Native runtime/device/account/full suites | NOT RUN | No native app behavior changed | No GUI, USB, real Visory session, signing/publish or full unrelated test suites |
| Performance | N/A | Small local build-time catalog; no watcher/background work | No performance guarantee claimed |

## 4. Risk and quality review

- Public/compatibility: existing tool names/slugs/IDs/order unchanged; source ZIPs
  now include project metadata/allowlist for self-contained drag-in use. Removed
  only old central manifests, not source projects or native script layouts.
  Media Recover object shape/CRLF retained; monograms optional for new projects.
- Security/privacy: no project eval/import/process execution from manifests.
  Explicit UTF-8 allowlists reject unsafe paths, selected secret/runtime/binary
  names, duplicate entries and linked path components. Unknown metadata/workspace
  fields and duplicate IDs/slugs fail. These bounded checks do not replace manual
  review of source before adding it to a public allowlist.
- Accessibility: shared UI rendering/focus/motion/defaults unchanged; selected
  browser and axe tests pass. New project uses same verified detail/source views.
- Disabled behavior: indexing is explicit or part of requested dev/build; no
  idle watcher, hidden native service, external traffic or auto-install.
- Errors/recovery: malformed/unregistered projects are surfaced explicitly.
  Unsafe inputs fail before replacing downloads. Generated-source cleanup targets
  only exact slug JSON/ZIP names inside the generated output folder; never source
  projects. Output-link/ancestor checks prevent redirecting writes.
- Dependencies/performance: unchanged manifests' dependency sections/lockfile;
  existing Prettier generates deterministic typed data. Catalog remains small.
- Cleanup: synthetic project exists only in temporary consumer, not real tools;
  bounded helper server stopped. Remove inspected temporary links before deleting
  the named consumer/scratch folders. Retain unrelated work and ignored native
  artifacts, including locally generated source-manifest evidence.

## 5. Findings and residual risks

| ID | Severity | Finding and evidence | Mitigation / owner | Status |
|---|---|---|---|---|
| R1 | Limitation | A raw folder cannot safely supply honest portal metadata on its own | Explicit `portal.json` contract/example and unregistered-folder warning | Intentional |
| R2 | Limitation | New interactive/native-service integration is not arbitrary executable config | Shared metadata/source page auto-integrates; custom runtime needs approved code/tests | Intentional |
| R3 | Existing | Desktop runtime/account/release validation limits unchanged | Preserve preview/unpublished notices and explicit provenance refresh | Unchanged |
| R4 | Environment | Isolated dependency junctions require Webpack and reused standalone dependencies | Default main build and real six-tool browser acceptance pass; no install or tracing-config changes | Documented |

- Gaps: no live watcher (not intended), native/device/account acceptance, full
  unrelated suites, publishing/deployment or independent security certification.
- Approvals: user authorized local organization/indexing. No external action,
  spending, prerequisites, permissions, accounts or background connection added.
- Rollback: revert the focused integration commit to restore manual registry and
  sibling allowlists, preserving unrelated work and project/native source data.
- Release readiness: `NOT APPLICABLE`; no application release/deployment requested.

## 6. Completion record

- Created before and included with the focused local commit after checks.
- Final commit hash: reported in task completion; no self-edit after committing.
- External actions: `NOT AUTHORIZED`; no push/publish/deployment or account access.

**Limitations:** An agent self-audit is not an independent security review, accessibility certification, or release approval.
