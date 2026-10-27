# Development Audit: Visory module hierarchy and linked daily reports

> Observed local evidence. Agent self-audit; not independent certification.

| Field | Value |
|---|---|
| Audit ID | `2026-10-27T02-29-08Z-visory-module-hierarchy-and-linked-daily-reports` |
| Created (UTC) | `2026-10-27T02:29:08.436Z` |
| Task / issue | Visory module hierarchy and linked daily reports |
| Auditor | AI assistant using Copilot SDK in VS Code; self-audit |
| Branch / baseline commit | `master`, `e7cbd59`; prior Visory foundation staged by this same task; unrelated untracked `tools/LICENSE` preserved |
| Final commit | Report in task completion; do not edit this audit after committing |
| Scope reviewed | Visory module hierarchy, command entry, template-inspired local README, durable logging, daily reports and line links, source bundles and focused portal checks |
| Outcome | `PASS WITH RISKS`; final native tests execute successfully on the unchanged host policy |

## 1. Request and outcome

- Requested: use moorkyz-template documentation design, no separate static
  website, a root command entry and README.html, meaningful tools/utilities/scripts
  boundaries, and a daily report with ID, human timestamp, SUCCESS/FAIL/ERROR,
  filename, full path, spent time, size, media length, exact log/error links,
  success ratio and message.
- Inspected sibling moorkyz-template read-only: root AGENTS/README.html/index.html
  and designs/README.md. Adopted its main README's quiet, readable documentation
  direction, not its showcase/landing-page application. No template code,
  generator, website runtime or external assets were copied into the product.
- Implemented: `src/tools/desktop` and `src/tools/engine` retain the App/Core
  assembly boundary. Runtime responsibilities remain explicit within engine:
  discovery/providers, download, archive, backup, storage and scheduling.
  Shared helpers are in `src/utils/{diagnostics,documents,system}`, settings in
  `src/config`, translations in `src/localization`, automation in
  `src/scripts/{startup,development}` and tests in `src/tests`.
- Root `CMD.cmd` is the single normal Windows entry. The owner wrote `CMD.run`;
  a clarification form was unavailable, so the pragmatic Windows `.cmd`
  extension was selected and explicitly documented instead of inventing a
  `.run` file association or adding a second launcher. README.html is generated
  from README.md and the same stylesheet embedded into native report output.
- Daily `reports/report.{csv,html,md}` combines recorded local days across runs.
  The twelve requested columns are ordered and tested. IDs use milliseconds plus
  GUID; detailed timestamps retain offset. Unknown values stay blank; failures
  retain their planned absolute destination. Media length is only populated when
  supplied, not guessed from transfer time.
- Daily ratio is SUCCESS / (SUCCESS + FAIL + ERROR). SKIP/RUN stay in detailed
  records without inflating outcome counts. Both Markdown files are linked by
  exact physical line and a same-line `L<number>` anchor. App's latest-report
  command opens the daily report.
- Per-line flushing/redaction/startup capture from the first phase is retained.
  Daily reports are snapshots from authoritative, durable per-run CSVs, not an
  always-connected live website.
- This final evidence supersedes the open native-validation findings V1/V2 in
  the earlier foundation audit. That audit retains what was observed then;
  no old artifact, signing policy or protection was changed to evade a block.
  The required new source layout compiled and its normal native test executable
  ran successfully, including previously unexecuted D5.
- Intentional exclusions: discovery repair, real account testing, new endpoints,
  native installs/media probes, website, public EXE/installer, signing,
  scheduled-task creation, publishing, deployment and pushing.
- Contracts: root/tool AGENTS, current decisions (including the latest hierarchy
  and report refinement), the owner-supplied plan and current chat requirements.

## 2. Change inventory

| Path | Change | Risk |
|---|---|---|
| `tools/visory-media-downloader/CMD.cmd`, README.md/README.html | One terminal entry, generated template-inspired documentation, no separate website | Low; explicit filename choice and offline browser validation |
| `src/tools/{desktop,engine}`, solution/projects, `src/config`, `src/localization` | Responsibility-based source relocation, references updated | Medium; all projects compile, full synthetic suite executes |
| `src/utils/{diagnostics,documents,system}` | Durable Markdown and line anchors, daily aggregation/trace links, shared CSP-hashed presentation | Medium; exact schema/ratios/physical rows tested |
| `src/scripts/{startup,development}`, `src/tests` | Meaningful automation/test placement; README generator and startup fixtures | Low; startup, invalid-argument and freshness checks pass |
| Tool metadata/allowlist/provenance and portal tool index | Current command/source paths and 84-file pinned source bundle | Low; all ZIP bytes and checkout rules verified |
| Root source sync, focused tooling/browser tests, decisions and audits | Source-type allowlist and task acceptance evidence | Low; no dependency changes |

## 3. Verification evidence

Commands ran at the repository root unless stated otherwise.

| Check | Result | Exact command / method | Evidence / notes |
|---|---|---|---|
| Native build / strict typing | PASS | `dotnet build tools\visory-media-downloader\src\MoorkszVisoryMediaDownloader.sln -c Debug --nologo` | All three relocated projects; 0 warnings, 0 errors |
| Full native regressions | PASS with one explicit SKIP | `dotnet run --project tools\visory-media-downloader\src\tests\MoorkszVisoryMediaDownloader.Tests -c Debug --no-build` | 41 scenarios executed PASS, 4.2 GiB heavy scenario SKIP; runner prints 42/42 including skip. Final run 19.8 s. Includes real loopback BITS, no real account |
| D1-D6 acceptance | PASS | Same full suite | Per-line read-before-disposal, concurrent events, redaction/idempotence, exact HTML/MD cells, abrupt-exit durability, legacy import, invalid CSV, full archive span/error state, daily schema and exact file/physical-row anchors. D6 covers multiple days and multiple runs on one day, 25%/100% ratios, SKIP/RUN exclusions, unknown values, stable IDs on resume and both failure trace targets |
| Startup/renamed command | PASS | `powershell.exe -NoLogo -NoProfile -File tools\visory-media-downloader\src\tests\startup\test-startup.ps1` | Isolated fixture; CMD.cmd from different CWD, stdout/stderr, redaction, repeated unique runs, missing SDK stop, timestamped errors and CSV/HTML/MD views |
| Generated README | PASS | `node tools\visory-media-downloader\src\scripts\development\readme.mjs --check` | Tooling tests also verify invalid arguments do not overwrite output, all local README links exist, no script/shared stylesheet loads and browser-normalized CSP hash matches |
| Portal production/typecheck | PASS | `pnpm build` | Next.js production build and TypeScript pass with current typed index/source bundle |
| Source/catalog contracts | PASS | `node --test tests\tooling\visory-source.test.js tests\tooling\tools-catalog.test.js` | 13/13, including exact ZIP/source bytes, current module paths and generated README |
| Source provenance | PASS | `node scripts\sync-visory-source.mjs` as part of final build preparation | 83 hashed files + provenance = 84 source files; ZIP 758120 bytes; tree SHA-256 `1bcd5ff3f64c0a74c8d8e3064d3519b393a158ce46146e08a1717cf344057005` |
| Portal browsers/accessibility | PASS | `pnpm exec playwright test visory.spec.js --workers=1` | 12/12 Chromium/Firefox/WebKit; setup/CMD, downloadable source, retired URLs, search, themes/narrow layouts, existing axe checks and no external page requests |
| Local README browser | PASS | Integrated browser opened `file:///C:/Users/marvi/source/repos/moorkyz-tools/tools/visory-media-downloader/README.html`; Playwright reload/computed-style/layout check | Styled background `rgb(20, 28, 23)` in system dark mode; 390px mobile viewport with 375px document width; seven section headings, zero script/stylesheet assets, zero console errors after correction |
| Repository structure | PASS | `pnpm check:repository` | 58 JavaScript syntax checks; 13/13 structure checks |
| Focused lint | PASS | `pnpm exec eslint scripts\sync-visory-source.mjs tests\tooling\visory-source.test.js tests\browser\visory.spec.js tools\visory-media-downloader\src\scripts\development\readme.mjs` | No findings |
| Focused formatting | PASS | `pnpm exec prettier --check scripts\sync-visory-source.mjs tests\tooling\visory-source.test.js tests\browser\visory.spec.js tools\visory-media-downloader\portal.json tools\visory-media-downloader\source-files.json tools\visory-media-downloader\src\scripts\development\readme.mjs tools\visory-media-downloader\src\utils\documents\document.css` | All matched files formatted |
| Bounded source scan | PASS | `pnpm check:source` after task-owned paths staged | 424 paths before adding this final audit; no selected credential patterns/forbidden portal sinks. Not a complete security audit |
| Performance | PASS, bounded synthetic observation | Native scenario 32, same full test command | 100,000-record inventory test passed in 1369 ms in final run; no new performance guarantee |
| Whitespace / scope | PASS | `git diff --cached --check`; `git diff --check`; staged scope review | Only Visory and directly related registry/sync/tests/decisions/audits staged. Unrelated `tools/LICENSE` left untracked |
| Full root suites / real account / desktop human walkthrough / heavy >4 GiB / publish | NOT RUN | N/A | Focused scope; heavy test opt-in; no account or external/release authorization |

Resolved intermediate findings:

- The first browser check found a CSP stylesheet hash mismatch caused by CRLF
  normalization in the HTML parser. Both generated README and embedded native
  styles now normalize CSS to LF before hashing/rendering. Added a regression
  check and verified the corrected local document in the browser without errors.
- Source sync found the old RUN filename still present after the editor rename.
  Its contents matched the task's new bootstrap; the explicit filesystem rename
  to CMD.cmd was then persisted and startup/provenance checks rerun successfully.
- Initial foundation file-sharing/PowerShell/null-backup and stale BAML issues
  are documented in the earlier audit; current full suite and startup checks
  cover the corrected final implementation.

## 4. Risk and quality review

- Contract: intended new source paths, normal launcher name, generated local
  document and daily report schema. No new service, portal runtime or SDK.
  App/Core namespaces, assembly identities and persisted settings/session/
  scheduler keys stay compatible. Build and startup callers were rewired.
- Security/privacy: no package/native dependencies, elevated permissions, hosted
  processing, accounts, telemetry, installs or policy changes. New CSV/HTML/MD
  passes redaction; HTML is escaped and has no script/external asset load.
  CSV text is spreadsheet-safe. Trace targets are constrained under the chosen
  backup's runs folder, must be existing Markdown files, and have positive line
  references. Reparse points are refused. Styles have a normalized exact CSP hash.
  Arbitrary secret prose cannot be guaranteed to be recognized; logs still
  contain potentially identifying local paths and filenames.
- Accessibility: offline docs use semantic headings/navigation/skip link, focus
  outlines, responsive widths and reduced-motion/print rules. Wide report tables
  have focusable labeled horizontal-scroll regions and scoped column headers.
  Existing app controls/localization remain; desktop screen-reader/high-contrast
  behavior was not independently certified.
- Disabled behavior: no website added. README/report opening makes no requests.
  CheckOnly checks locally without build/app/Visory. Source indexing never
  executes the project. Build restore is the existing explicit NuGet workflow,
  not native prerequisite installation.
- Failure/recovery: each physical Markdown line/CSV result is flushed before
  returning. Exact line anchors survive append/resume. Invalid schema, numbers,
  statuses, IDs and unsafe/missing trace targets fail explicitly. Legacy CSV
  upgrades retain the prior file. Report snapshots can be stale after a hard
  exit; canonical run CSVs remain the regeneration source. Multi-file/database
  persistence is not one atomic transaction and arbitrary hardware/power-loss
  behavior is not certified.
- Cleanup: fixtures are synthetic and disposed, including loopback servers.
  No personal recordings were accessed. Developer refactoring scratch belongs
  to the session, not the source bundle; no background watcher/service added.
  Existing ignored user artifacts and unrelated LICENSE remain untouched.
- Regression exposure: source movement is broad but mechanically wired and
  covered by the full native build/test suite and exact source-bundle checks.
  Daily aggregation reads retained history at snapshot time; it does not
  introduce an always-running indexer or automatic deletion/rotation.

## 5. Findings and residual risks

| ID | Severity | Finding and evidence | Mitigation / owner | Status |
|---|---|---|---|---|
| R1 | Known limitation | Media duration is unknown unless supplied by a result; production discovery currently supplies no proven duration | Empty Length field, no invented zero or automatic codec probe | DOCUMENTED |
| R2 | Known limitation | Plain-text browsers can open Markdown without scrolling to its anchor | Link text includes exact file/physical line; rendered Markdown viewers honor anchors | DOCUMENTED |
| R3 | Known limitation | Snapshots may lag a hard kill, and CSV/database writes are not a single transaction | Durable primary rows, explicit snapshots/incomplete state and regeneration on normal resume | DOCUMENTED |
| R4 | Medium, existing | Real Visory account discovery/download behavior remains unverified | Later dedicated diagnosis phase; no claim of repair or completeness | OUT OF SCOPE |
| R5 | Environment | Earlier unsigned test artifact was blocked; future unsigned builds can still be subject to host policy | Final required source executes normally; no policy bypass or approval claim | OBSERVED / FINAL CHECKS PASS |

- Not run: real account, human desktop keyboard/screen-reader walkthrough, heavy
  >4 GiB test, installer/publish/signing/release, complete root suites.
- Required approvals: no new authority used; external operations remain unapproved.
- Rollback: a focused revert of this task commit after maintainer review;
  preserve ignored runtime/backup data and do not reset unrelated work. Existing
  media and old report originals are never removed by this change.
- Release readiness: `NOT APPLICABLE` to this local, unreleased source foundation.

## 6. Completion record

- Audit created from the canonical template before the focused task commit.
  Commit includes the earlier same-task foundation source/audit plus this
  follow-up, because the foundation was intentionally left uncommitted while its
  then-current native validation was blocked. Final scope excludes unrelated work.
- Final commit hash: reported in task completion; this audit is included in that commit.
- External actions: `NOT AUTHORIZED` unless separately approved.

**Limitations:** An agent self-audit is not an independent security review, accessibility certification, or release approval.
