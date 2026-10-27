# Development Audit: Visory interactive reports and performance comparison

> Observed local evidence. Agent self-audit; not independent certification.

| Field | Value |
|---|---|
| Audit ID | `2026-10-27T02-48-30Z-visory-interactive-reports-and-performance-comparison` |
| Created (UTC) | `2026-10-27T02:48:30.217Z` |
| Task / issue | Visory interactive reports and performance comparison |
| Auditor | AI assistant using Copilot SDK in VS Code; self-audit |
| Branch / baseline commit | `master`, `f1fbc0a`; existing coordinated Visory src/Data edits preserved and integrated |
| Final commit | Report in task completion; do not edit this audit after committing |
| Scope reviewed | Offline interactive daily report, weighted comparison math, calendars, styled Markdown diagnostics/report views, local font, shared core/launcher naming and coordinated manifest consumers |
| Outcome | `PASS WITH RISKS`; unknown metrics and viewer-dependent Markdown presentation remain explicit |

## 1. Request and outcome

- Requested: a scrollable report that loads more while scrolling, search, Single
  Tasks / Full Statistics views, timeframe selection limited to recorded dates,
  readable green/red/dark-red statuses and red errors, DM Mono IDs/timestamps,
  a toggle swapping calculated performance columns into the table and comparing
  with all-time averages, a coherent shared Core location, and the explicit
  launcher name RUN.cmd.
- Implemented: single-file HTML snapshot with JSON-escaped data and a CSP-hashed
  embedded module, no server or requests. Search covers all rows; only 50 rows
  render per batch, with scroll and Load more keyboard alternatives.
  Single Tasks retains individual result columns; Full Statistics aggregates
  matching outcomes by day with totals and inclusive From/To dates.
- A local modal calendar greys/disables days outside the snapshot's min/max
  dates and disables unavailable month navigation. Typed impossible, reversed
  or out-of-range values are explicitly invalid, not silently clamped.
  Empty reports disable date selection. Complete CSV/Markdown exports remain
  usable without JavaScript; HTML then shows its first 50 tasks.
- Compare performance swaps size/length columns for seconds per MiB and MiB/s,
  and task ratio for a labeled comparison. All-time measured SUCCESS rows with
  known nonnegative time and positive size determine a size-weighted baseline.
  Query/date filters do not change it. Faster = green, slower = red; equal,
  unknown and undefined rates stay neutral/empty. Zero time never creates
  Infinity. This is processing time, not a pure network speed guarantee.
- SUCCESS is green, FAIL red, ERROR dark red and failure/error messages red.
  DM Mono is text-encoded from the existing repository font and embedded locally
  in HTML. Its SIL-OFL notice is included and copied by publish tooling.
  Markdown logs/report cells carry color spans and monospace code text while
  preserving physical line numbers and anchors; raw/plain app log reading strips
  only generated markup before decoding text.
- Shared non-UI engine moved from `src/tools/engine` to explicit `src/core`;
  desktop stays `src/tools/desktop`. RUN.cmd supersedes the earlier CMD.cmd
  interpretation and all current source/setup/tests are rewired.
- Concurrent work was detected, not reverted: another same-tool chat moved
  implementation/generated folders under src and metadata to Data/Portal and
  Data/Source. A clarification form found the user unavailable; a direct
  coordination handoff established stable ownership. That chat stopped edits/
  native builds/staging and handed final manifest/docs/tests/provenance
  integration here. This commit includes the cohesive src/Data changes and
  required indexer/source/format/doc consumers, not unrelated tools/LICENSE.
- Relevant authority: root/tool AGENTS, current decisions and explicit user
  refinements. No new package, native tool, hosted font, account, permission,
  telemetry, dependency gate exception, release or application-control bypass.
- Exclusions: Visory recording recognition/endpoint repair, real account testing,
  installed fonts, hosted website, media codec probing, signing/publishing.

## 2. Change inventory

| Path | Change | Risk |
|---|---|---|
| `src/utils/documents/{report.mjs,document.css,DocumentHtml.cs,fonts}` | Embedded viewer, batch/search/statistics, bounded calendar, weighted comparison and local DM Mono | Medium: local executable UI, strict CSP and safe DOM tests |
| `src/utils/diagnostics/{DailyReport,CsvReport,DiagnosticLog}.cs` | 50-row preview plus complete exports/data; consistent status/error styling; plain-text decoding and stable line anchors | Medium: output markup, no result schema removal |
| `src/scripts/startup/diagnostics.ps1`, developer README/publish tools | Style/bootstrap parity, local font embedding/notice | Low: no installation/network added |
| `src/core`, solution/project refs, `RUN.cmd`, docs/manifest consumers | Explicit shared core, requested entrypoint, preserved coordinated src/Data layout and provenance | Medium: broad path moves verified through full builds/tests |
| `tests/browser/visory-report.spec.js`, tooling report tests and native fixture/tests | Real file:// coverage in three engines, exact rates/colors/fonts/ranges and retained native behavior | Low: isolated synthetic data only |
| Root index/source scripts, manifest tests, related docs/package format globs | Coordinated support for root or Data manifests, reject duplicates/links, exact source packaging | Medium: other tools retain root-manifest behavior in isolated regression tests |

## 3. Verification evidence

Commands ran at the repository root unless stated otherwise.

| Check | Result | Exact command / method | Evidence / notes |
|---|---|---|---|
| Native compilation / strict typing | PASS | `dotnet build tools\visory-media-downloader\src\MoorkszVisoryMediaDownloader.sln -c Debug --nologo` | Final src/core/desktop/tests, 0 warnings, 0 errors |
| Native full synthetic regression | PASS with one explicit SKIP | `dotnet run --project tools\visory-media-downloader\src\tests\MoorkszVisoryMediaDownloader.Tests -c Debug --no-build` | 41 executed PASS, opt-in 4.2 GiB SKIP; runner prints 42/42 including skip, 14.4 s final run. Line persistence, markup/plain parsing, legacy/resume, exact report cell parity and trace links retained |
| Startup diagnostics integration | PASS | `powershell.exe -NoLogo -NoProfile -File tools\visory-media-downloader\src\tests\startup\test-startup.ps1` | Isolated RUN fixture with copied local style/font, unique runs, alternate CWD, stdout/stderr redaction, failed prerequisites/exit propagation and all report formats |
| Production portal/typecheck | PASS | `pnpm build` | Next.js build and TypeScript pass; current Data manifests/source bundle included |
| Source/catalog/metric unit tests | PASS | `node --test tests\tooling\visory-source.test.js tests\tooling\visory-report.test.js tests\tooling\tools-catalog.test.js` | 18/18. Exact bundled bytes, current paths, generated README/CSP, weighted all-time baselines, blank/zero denominators, all-data search and invalid/impossible dates |
| Data manifest/index regressions | PASS | `node --test tests\tooling\tool-index.test.js` | 13/13: both manifest layouts, root-relative source bytes, duplicate/missing/invalid JSON and ancestor-link refusal, no project-code execution |
| All scoped browser checks | PASS | `pnpm exec playwright test visory.spec.js visory-report.spec.js --workers=1` | 24/24 Chromium/Firefox/WebKit. 12 portal checks plus 12 generated file:// report checks |
| Measurable interactive acceptance | PASS | Same browser command, native synthetic fixture generated by normal `dotnet run ... --no-build -- --report-browser-fixture <disposable-folder>` | 165 rows: initial 50, scroll 100, button 150; query matches oldest not-yet-loaded result; no image/script injection or external requests/errors. Three daily aggregates; bounded multi-month calendar, disabled days/month controls, span/reset/errors. Exact 1.000 and 3.000 s/MiB and 1.000/0.333 MiB/s against unfiltered 2.000 baseline. Computed colors RGB(33,98,52), RGB(180,35,35), RGB(113,27,36); DM Mono loaded; mobile no page overflow. Empty and JavaScript-disabled export fallbacks tested |
| Generated document freshness | PASS | `node tools\visory-media-downloader\src\scripts\development\readme.mjs --check` | README from current Markdown/stylesheet/font; source tests independently check style hash and local references |
| Provenance | PASS | `node scripts\sync-visory-source.mjs` | 87 hashed text files + provenance = 88; source ZIP 945892 bytes; tree SHA-256 `047d7a6e2e31522c66b5eb7b4491db5e98e632c4de6028b172bb51f5190e8972` |
| Generated index | PASS | `pnpm tools:index:check` | Five projects indexed, no code executed |
| Repository structure/syntax | PASS | `pnpm check:repository` | 61 JavaScript syntax checks, 13/13 structure checks |
| Lint | PASS | `pnpm exec eslint tools\visory-media-downloader\src\utils\documents\report.mjs tools\visory-media-downloader\src\scripts\development\readme.mjs tests\browser\visory-report.spec.js tests\tooling\visory-report.test.js tests\tooling\visory-source.test.js tests\browser\visory.spec.js scripts\index-tools.mjs scripts\sync-visory-source.mjs` | No findings |
| Focused formatting | PASS | `pnpm exec prettier --check tools\visory-media-downloader\src\utils\documents\report.mjs tools\visory-media-downloader\src\utils\documents\document.css tools\visory-media-downloader\src\scripts\development\readme.mjs tests\browser\visory-report.spec.js tests\tooling\visory-report.test.js tests\tooling\visory-source.test.js tests\browser\visory.spec.js scripts\index-tools.mjs scripts\sync-visory-source.mjs tools\visory-media-downloader\Data\Portal\portal.json tools\visory-media-downloader\Data\Source\source-files.json` | All matched files formatted |
| Bounded credential/sink scan | PASS | `pnpm check:source` after task paths staged | 442 paths before audit staged; selected credential/injection-sink scan only, not independent review |
| Whitespace / scope | PASS | `git diff --cached --check`; `git diff --check`; explicit staged-path review | Unrelated tools/LICENSE and the other chat's earlier audit are not staged |
| Full root suites, real account, human desktop/screen-reader certification, heavy >4 GiB, publish | NOT RUN | N/A | Focused scope, opt-in heavy test and no external/native release authority |

Resolved intermediate failures:

- Native WPF generated files were stale while simultaneous same-tool work
  adjusted folders. After coordination stopped concurrent builds, the normal
  `dotnet build ... -c Debug -t:Rebuild --nologo` rebuilt intermediates; subsequent
  normal builds pass. No protection was changed.
- Plain-log regex assertions initially expected raw line anchors after markup
  stripping; corrected to assert the decoded line contract, preserving separate
  physical-anchor tests. Full native suite now passes.
- Chromium/Firefox report controls initially had an implicit select label whose
  accessible name included option text. An explicit label corrected that, but
  WebKit also emitted a native-select internal stylesheet CSP warning and did
  not enforce native date underflow consistently. Isolated control probes
  identified the select warning. Replaced with two native radios and a local
  dialog calendar/validated ISO fields. Final all-engine tests have no CSP errors;
  no unsafe-inline, policy disable or CSP bypass was introduced.
- One portal build encountered EBUSY because a browser check was using the same
  standalone build outputs. After that bounded check exited, the unchanged build
  passed. Final browser checks run after production build, not concurrently.
- The temporary test command named a nonexistent `tool-indexing.test.js`
  selector. It was not counted as coverage; the actual `tool-index.test.js`
  was then explicitly run and passed 13 tests.

## 4. Risk and quality review

- Contract: intended report interactivity/markup and corrected launcher/core
  paths; detailed CSV result fields/IDs and log physical rows stay stable.
  Complete CSV/Markdown is retained; HTML becomes a local view over the same
  embedded snapshot. No background watcher or hosted data endpoint.
- Security/privacy: untrusted cells use textContent or HTML/JSON escaping;
  trace links are constrained local Markdown references. Script/style hashes
  use browser-normalized LF. Only embedded font data is allowed, and no
  external script/font/stylesheet/data is fetched. Existing redaction precedes
  persistence/display; generated markup never converts arbitrary log prose to
  active HTML. Font bytes/notice are reused, not remotely downloaded.
- Accessibility: labeled search/radios/buttons, region/table headings, scoped
  headers, keyboard load-more and modal calendar with explicit disabled days,
  arrow-key date navigation/Escape and focus return; responsive horizontal table
  scrolling and reduced-motion/print rules. Text labels/comparison messages
  supplement colors. No independent screen-reader certification claimed.
- Disabled behavior: no JS means a readable 50-task preview plus complete static
  exports. Empty reports disable calendars and show unknown baselines without
  invented rates. CheckOnly does not launch the app or contact Visory.
- Failure/recovery: typed invalid spans show an alert and no misleading
  statistics. Viewer startup errors surface with CSV/Markdown fallback.
  New line styling remains one physical Markdown line per logged line and
  preserves trace targets. Snapshot regeneration retains authoritative CSV
  rows; abrupt-exit multi-file consistency limitation is unchanged.
- Cleanup: browser fixture directories use mkdtemp and native synthetic output,
  removed in afterAll. A separately named WebKit diagnosis fixture and session
  refactoring helper are explicitly cleaned, not root/glob-deleted. Session
  baseline copies/evidence retained for provenance; no personal files touched.
- Performance: only 50 DOM rows per append, but all snapshot data is embedded and
  searched/aggregated in memory. All-time baseline computed once and independent
  of filters. No claim of infinite-size virtualization or incremental disk reads.
- Dependencies/regressions: no package/native/permission changes. Root package
  edits only include capitalized Data manifest formatting globs from the
  coordinated same-tool path work; other tools retain their root layout.

## 5. Findings and residual risks

| ID | Severity | Finding and evidence | Mitigation / owner | Status |
|---|---|---|---|---|
| R1 | Known limitation | Sanitizing Markdown viewers may strip inline colors and substitute their own fonts | Severity/text and code formatting remain; exact DM Mono/colors verified in local HTML | DOCUMENTED |
| R2 | Known limitation | HTML is a snapshot, not an automatically refreshed live report; all rows are in memory | Reload regenerated report for new outcomes; incremental DOM, complete static exports | DOCUMENTED |
| R3 | Known limitation | Real account behavior and unknown media lengths are unchanged | No fabricated metadata or claim of discovery repair | OUT OF SCOPE |
| R4 | Known limitation | All-time comparison measures result processing time, not isolated wire transfer speed | Explicit MiB definition, weighted formula, qualifying rows and constant baseline documented | DOCUMENTED |

- Not run: full root test suites, real Visory account, manual desktop
  screen-reader/high-contrast walkthrough, opt-in >4 GiB test, installer/publish.
- Required approvals: no external actions taken. The user authorized local
  report interactions/appearance; no broader native/install/hosting authority used.
- Recovery: focused revert of this commit after maintainer review; preserve
  ignored personal backup/runtime data, and do not reset unrelated changes.
- Release readiness: `NOT APPLICABLE`; local unreleased development source.

## 6. Completion record

- Audit created before and included in one focused local task commit, including
  the coordinated same-tool Data/src integration that this report now consumes.
- Final commit hash: reported in task completion; no audit rewrite after commit.
- External actions: `NOT AUTHORIZED` unless separately approved.

**Limitations:** An agent self-audit is not an independent security review, accessibility certification, or release approval.
