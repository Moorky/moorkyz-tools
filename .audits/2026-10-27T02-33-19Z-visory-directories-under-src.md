# Development Audit: Visory src and Data organization

| Field | Value |
|---|---|
| Audit ID | `2026-10-27T02-33-19Z-visory-directories-under-src` |
| Created (UTC) | `2026-10-27T02:33:19Z` |
| Task / issue | Put implementation directories under src and group root JSON metadata under capitalized Data folders |
| Auditor | AI assistant using Copilot SDK in VS Code; self-audit |
| Branch / baseline commit | `master`, `e7cbd59`; substantial pre-existing staged Visory restructuring |
| Final commit | NOT CREATED: task edits overlap pre-existing staged and actively changing work |
| Scope reviewed | Visory directory layout, startup/test/publish paths, notices, source distribution and documentation |
| Outcome | `BLOCKED`; initial src phase verified, follow-up Data logic fixture-tested, final live integration blocked by concurrent edits |

## 1. Request and outcome

- Requested behavior: all directories can go into `src`.
- Scope assumption: the tagged Visory project, not the entire portal. A structured
  clarification was unavailable, so the existing Visory context determined scope.
- Initial phase: the only directory directly under the Visory project root was `src`.
  Moved `third-party`, `artifacts` and `runtime` beneath it. The obsolete root
  `tests` folder contained only generated bin/obj output; retained it under
  `src/artifacts/legacy-test-build` rather than merging with current test builds.
- Updated startup logs, test fixtures and default publish output to stay under
  `src`. The root launcher, README editions, license and metadata remain files at
  the root. User-selected backup folders and profile data locations are unchanged.
- Acceptance evidence: 101 moved files retained identical SHA-256 hashes; source
  and startup tests assert the root-directory contract. The upstream notice's Git
  blob remains `c122c769f250affaa52a431cb6e6a31b2dc00321`.
- Reviewed the neighboring moorkyz-template root guide, README, introduction and
  tool catalog. Reused the current documentation layout; no template adoption or
  unrelated file copying.
- Authority: root/tool AGENTS and current ownership/hierarchy decisions.
- Follow-up request: group root JSON files under capitalized user-facing folders,
  for example `Data/Settings/Settings.json`. The actual root JSON files were portal
  metadata, a public-source allowlist and generated provenance, not saved settings.
- Follow-up implementation: moved those three JSON files into
  `Data/Portal/portal.json` and `Data/Source/{source-files.json,SOURCE-PROVENANCE.json}`.
  Final intended root directories are `Data` and `src`. No speculative Settings
  file was created, and saved application settings were not migrated.
- Shared discovery supports root or Data manifests without changing other tools'
  layouts. It rejects duplicate locations and checks metadata ancestors for links.
  Source entries remain project-root-relative. Updated docs, tests, formatter
  selectors and provenance output handling for the new locations.
- Concurrent-work blocker: during final verification another process removed
  `CMD.cmd`, introduced `RUN.cmd`, replaced `src/tools/engine` with `src/core`, and
  changed report files, project references, documentation and tests. The final
  source refresh now fails because `CMD.cmd` in the allowlist no longer exists;
  startup integration fails at the same missing launcher.
- A structured coordination question was unavailable. Stopped edits to shared
  Visory sources rather than undoing or guessing the concurrent owner's changes.
  The moved provenance is therefore stale and source downloads must not be
  considered finalized until the active restructuring is reconciled.

## 2. Change inventory

| Path | Change | Risk |
|---|---|---|
| `tools/visory-media-downloader/{third-party,artifacts,runtime,tests}` | Non-overwriting directory moves beneath `src`; generated output retained | Low; local paths intentionally change |
| `src/scripts/{startup/run,development/publish}.ps1` | Startup runtime, publish output and license-source paths | Medium; validated build/startup, no release publish |
| `src/tests` | Fixture locations, associated path calculation and startup directory assertion | Low; native/startup tests pass |
| Tool README editions, AGENTS, architecture and notice guide | Document canonical layout and retained generated builds | Low |
| `.gitattributes` | Keep relocated verbatim upstream notice whitespace exemption | Low; exact bytes verified |
| Tool allowlist/provenance and generated portal index | Relocated notice and refreshed content hashes | Low; exact downloadable source tests pass |
| `tests/tooling/visory-source.test.js` | Root-directory regression, publish notice/output references, nested generated-data exclusions | Low |
| Visory `Data/{Portal,Source}` | Relocated all three root JSON files | Medium; final provenance refresh blocked by concurrent launcher move |
| `scripts/{index-tools,prepare-tool-sources,sync-visory-source}.mjs` | Compatible root/Data discovery, duplicate/link rejection, nested provenance output | Medium; nine selected fixture tests pass |
| `tests/tooling/tool-index.test.js`, Visory tooling/browser tests | Data packaging, invalid/duplicate/missing manifests, linked metadata ancestors and expected provenance download path | Low; live browser check not run |
| `package.json`, project automation/tool onboarding/portal/structure docs | Formatter selectors and documented metadata paths | Low |

## 3. Verification evidence

Commands ran from the repository root. Unless a row says follow-up/final, the
results below describe the initial src-only phase, before concurrent edits.
They are not evidence that the final combined tree passes.

| Check | Result | Exact command / method | Evidence / notes |
|---|---|---|---|
| Directory moves | PASS | PowerShell preflight `Test-Path`, reparse-point checks, `Move-Item`, before/after `Get-FileHash -Algorithm SHA256` | Four moves; 101 identical files; no overwrite/deletion |
| Initial incremental build | FAIL, resolved | `dotnet build tools\visory-media-downloader\src\MoorkszVisoryMediaDownloader.sln -c Debug --no-restore --nologo` | Stale WPF intermediates referenced two missing BAML files |
| Rebuild / type safety | PASS | `dotnet build tools\visory-media-downloader\src\MoorkszVisoryMediaDownloader.sln -c Debug --no-restore --nologo -t:Rebuild` | Three projects; zero warnings/errors; no dependency restore |
| Native regression suite | PASS, one opt-in SKIP | `dotnet run --project tools\visory-media-downloader\src\tests\MoorkszVisoryMediaDownloader.Tests -c Debug --no-build` | 41 executed PASS, >4 GiB archive case SKIP; runner prints 42/42 including skip |
| Startup integration | PASS | `powershell.exe -NoProfile -File tools\visory-media-downloader\src\tests\startup\test-startup.ps1` | Isolated fixture: different CWD, repeated runs, missing SDK, terminal/log redaction, CSV/HTML/MD, and directories only under src |
| Source/catalog regression | PASS | `node --test tests\tooling\visory-source.test.js tests\tooling\tools-catalog.test.js` | 14/14; exact source ZIP bytes, provenance, README/CSP/local links and directory contract |
| Generated README | PASS | `node tools\visory-media-downloader\src\scripts\development\readme.mjs` | Regenerated locally; freshness checked by source tests |
| Source regeneration | PASS | `node scripts\sync-visory-source.mjs`; `pnpm tools:index`; `node scripts\prepare-tool-sources.mjs` | Visory: 84 text files, 760382-byte ZIP; tree SHA-256 `d436c2ee0159cd9405ca50d4df665cc6ecd098d00bf071c4030f52c24aea8ff6` |
| Generated index freshness | PASS | `pnpm tools:index:check` | Five indexed projects; no tool code execution |
| Focused lint | PASS | `pnpm exec eslint tests\tooling\visory-source.test.js` | No findings |
| Focused formatting | PASS | `pnpm exec prettier --check tests\tooling\visory-source.test.js tools\visory-media-downloader\source-files.json` | Both files match |
| Repository structure | PASS | `pnpm check:repository` | 58 JavaScript syntax checks, 13/13 structure tests |
| Bounded source scan | PASS | `pnpm check:source` | 427 repository paths; not independent security certification |
| Ignored generated output | PASS | `git check-ignore -- tools/visory-media-downloader/src/artifacts/release tools/visory-media-downloader/src/artifacts/legacy-test-build tools/visory-media-downloader/src/runtime/runs tools/visory-media-downloader/src/.fixtures/example` | All four ignored; source tests reject nested generated paths |
| Notice byte preservation | PASS | Compare `git rev-parse :tools/visory-media-downloader/third-party/wpf-10.0.11-NOTICES.txt` with `git hash-object --no-filters tools\visory-media-downloader\src\third-party\wpf-10.0.11-NOTICES.txt` | Identical blob |
| Browser / accessibility | N/A | No browser UI or stylesheet changes | README generation and local links covered by existing tests |
| Portal production build | NOT RUN | Source-only path/hash change, covered by source/catalog tests | No portal runtime code changes |
| Release publishing | NOT RUN | No EXE/package release requested | Default output/notice references tested without publishing |
| Performance | N/A | No algorithm or workload change | Existing 100,000-recording test passed |
| `git diff --check` | PASS | `git diff --check` | No whitespace errors |
| Follow-up JSON moves | PASS | Preflight `Test-Path`, `Move-Item`, before/after `Get-FileHash -Algorithm SHA256` | Three metadata files moved without byte changes or overwrite |
| Follow-up isolated discovery/provenance tests | PASS | `node --test --test-reporter=spec --test-name-pattern="capitalized Data manifests\|duplicate root/Data\|Data manifest ancestor\|dropping a self-contained\|native object-format\|folders without metadata\|Visory provenance is deterministic\|Visory nested provenance\|Visory allowlist rejects" tests\tooling\tool-index.test.js tests\tooling\visory-source.test.js` | Nine selected tests PASS; fixtures only, no live-source completeness claim |
| Follow-up lint | PASS | `pnpm exec eslint scripts\index-tools.mjs scripts\prepare-tool-sources.mjs scripts\sync-visory-source.mjs tests\tooling\tool-index.test.js tests\tooling\visory-source.test.js` | No findings |
| Follow-up formatting | PASS | `pnpm exec prettier --check package.json scripts\index-tools.mjs scripts\prepare-tool-sources.mjs scripts\sync-visory-source.mjs scripts\README.md tests\tooling\tool-index.test.js tests\tooling\visory-source.test.js tools\visory-media-downloader\Data\Portal\portal.json tools\visory-media-downloader\Data\Source\source-files.json` | All selected files match |
| Final source refresh | FAIL / BLOCKED | `node scripts\sync-visory-source.mjs` | ENOENT on concurrently removed `tools\visory-media-downloader\CMD.cmd`; did not overwrite provenance with partial data |
| Final startup integration | FAIL / BLOCKED | `powershell.exe -NoProfile -File tools\visory-media-downloader\src\tests\startup\test-startup.ps1` | Copy-Item cannot find concurrently removed `CMD.cmd` |
| Final index/assets/build/browser | NOT RUN | Stopped after conflicting concurrent edits | Need reconciled launcher/core/report paths, refreshed complete allowlist/provenance, then normal checks |

## 4. Risk and quality review

- Public contract: deliberate source-layout change; root `CMD.cmd` unchanged.
- Security/privacy: no dependencies, permissions, accounts, remote integration or
  secret changes. Existing local runtime files were moved, not exposed or read for
  content. Source allowlists continue excluding generated data at any depth.
- Accessibility/keyboard/reduced motion: runtime UI and styles unchanged.
- Disabled features: no background service or connection enabled.
- Recovery: preflight rejects destination collisions and reparse points; existing
  generated builds are preserved separately. Test fixtures clean their own data.
- Data settings: existing settings, sessions, backup roots and fallback application
  diagnostics outside the checkout retain their existing locations.
- Unexpected concurrent file preservation: the old root notice path reappeared
  as a zero-byte file after its original verified move. A second clarification
  was unavailable; retained the empty file separately under
  `src/artifacts/legacy-third-party` without touching the complete canonical
  notice in `src/third-party`. Further conflicting changes triggered the stop.

## 5. Findings and residual risks

| ID | Severity | Finding and evidence | Mitigation / owner | Status |
|---|---|---|---|---|
| R1 | Medium | Existing staged restructuring plus active concurrent edits overlap edited source/document paths | Leave index untouched; stop shared-source edits and do not commit someone else's work | Blocked |
| R2 | Low | Initial incremental WPF build had stale missing BAML intermediates | Normal MSBuild Rebuild regenerated outputs; final compilation and native suite pass | Resolved |
| R3 | Low | Opt-in >4 GiB archive test, real account/UI and release publishing not executed | Unrelated to directory-only change; no release or account claims | Disclosed |
| R4 | Medium | Concurrent CMD-to-RUN and engine-to-core changes invalidate the new allowlist's source paths; nested provenance still records intermediate paths | Concurrent owner must finish; reconcile source/download/docs/test paths, regenerate and rerun checks | Blocked |

- No new approval-requiring dependency, data handling or external action.
- Rollback: reverse task-only path/reference changes and non-overwriting directory
  moves, then regenerate README/provenance/index/source assets. Do not reset the
  worktree or discard the pre-existing staged restructuring.
- Release readiness: NOT APPLICABLE; no executable release.

## 6. Completion record

- Audit created after initial verification, then updated with the follow-up
  request's fixture evidence and live integration failures. No completion commit.
- Pre-existing index tree remained
  `a97095ac836eee8cdd2ad85f691efbb6c5651e97`; task changes left unstaged.
- Final commit hash: `NOT CREATED`.
- External actions: `NOT AUTHORIZED`; no push, publish or deployment.

**Limitations:** An agent self-audit is not an independent security review,
accessibility certification or release approval.
