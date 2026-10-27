# Development Audit: Visory hierarchy and terminal logging foundation

> Observed local evidence. Agent self-audit; final native execution remains blocked.

| Field | Value |
|---|---|
| Audit ID | `2026-10-27T02-05-12Z-visory-hierarchy-and-terminal-logging-foundation` |
| Created (UTC) | `2026-10-27T02:05:12.643Z` |
| Task / issue | Visory hierarchy and terminal logging foundation |
| Auditor | AI assistant using Copilot SDK in VS Code; self-audit |
| Branch / baseline commit | `master`, `e7cbd59`; starting worktree clean |
| Final commit | NOT CREATED: required final native execution is blocked |
| Scope reviewed | Visory hierarchy, bootstrap, diagnostics, reports, desktop wiring, source distribution and focused portal tests |
| Outcome | `BLOCKED` for final native verification; implementation, builds, startup and portal/source checks otherwise completed |

## 1. Request and outcome

- Requested: redo only the Visory hierarchy and logging foundation. A single
  `RUN.cmd` must capture the terminal from startup onward, save each log line,
  timestamp logs/errors and report spans, make reports CSV embedded in HTML/MD,
  and keep regular/error logs Markdown only.
- Implemented: solution/build files, scripts, tests and technical docs under
  `src/`, retaining the existing App/Core boundaries. Root `RUN.cmd` logs before
  prerequisite checks, captures child stdout/stderr and remains attached to the
  console app. Check-only mode does not build, start the UI or contact Visory.
  No native prerequisites are installed.
- Shared C# and bootstrap diagnostics redact before display/storage; every
  physical log line has milliseconds, offset, level, run and area. Writes flush
  to disk and close immediately. DEBUG joins the Markdown log; ERROR/FATAL/FAIL
  also reach a separate Markdown error log.
- CSV results flush per row; file results, generic ERROR rows and RUN summaries
  embed into script-free HTML/Markdown tables. Reports retain timestamps,
  duration, counts, unknown sizes and invariant numbers. Whole-run time includes
  archiving; an archive failure reports completed-with-errors, without
  relabeling successful files. This last refinement is compiled but its new D5
  runtime test could not execute under the machine's application-control policy.
- App lifecycle, settings recovery and scan metadata diagnostics use the same
  logging foundation. No response/media bodies are logged. UI report export is
  CSV/HTML/Markdown, not a JSON log dump; legacy result CSV import preserves old
  artifacts and statistics.
- Evidence: final native compilation; isolated bootstrap tests; earlier native
  suite with 39 executed PASS and one heavy SKIP; final source/bundle checks,
  production portal build and 12 browser checks. These are not a claim that the
  final native suite ran.
- Exclusions: discovery recognition/endpoint repair, Chrome comparison, real
  account testing, prerequisite installs, installer/EXE distribution, deployment,
  publishing, signing and changes to other tools.
- Authority: root/tool AGENTS; current decision register including the
  2026-10-27 foundation decision; user's tagged Visory project plan and explicit
  format/terminal clarification. Current instructions override its proposed
  JSONL/TXT/failed-CSV output.

## 2. Change inventory

| Path | Change | Risk |
|---|---|---|
| `tools/visory-media-downloader/{RUN.cmd,README.md,README.html,.gitignore}` | Single terminal entry, current hierarchy/docs, ignored runtime | Medium: intentional source-first/console workflow |
| `tools/visory-media-downloader/src/{scripts,tests,docs}` and solution/props | Relocated helpers/tests/docs; new startup and persistence fixtures | Medium: project/script references rebuilt |
| `src/MoorkszVisoryMediaDownloader.Core/Diagnostics` | Durable Markdown logger, redactor, CSV and escaped HTML/MD views; retained Reporting namespace | Medium: output schema/lifetime changes |
| Visory Core infrastructure/coordinator and App files | Precise timestamps, lifecycle/error/scan wiring, report export, legacy reading/import and archive-span reporting | Medium: last native test assembly blocked |
| Tool portal/allowlist/provenance and generated portal index | Complete 80-file source bundle, new setup/docs paths and logging formats | Low: exact source/ZIP bytes verified |
| `scripts/sync-visory-source.mjs`, focused tooling/browser tests, decisions | Allow CMD/HTML, reject runtime distribution, assert current entrypoint | Low: no package additions |

## 3. Verification evidence

Commands below ran at the repository root unless stated otherwise.

| Check | Result | Exact command / method | Evidence / notes |
|---|---|---|---|
| Final native build/type safety | PASS | `dotnet build tools\visory-media-downloader\src\MoorkszVisoryMediaDownloader.sln -c Debug --nologo` | All three projects; 0 warnings, 0 errors, after final native edits |
| Final native suite | FAIL to launch / tests NOT RUN | `dotnet run --project tools\visory-media-downloader\src\tests\MoorkszVisoryMediaDownloader.Tests -c Debug --no-build` | FileLoadException: application-control policy blocked test DLL, `0x800711C7`; process exit `-532462766`. Same unchanged normal command confirmed block. No alternate host, policy change, signing or bypass attempted |
| Native blocker diagnosis | PASS, read-only diagnosis | `Get-WinEvent -FilterHashtable @{LogName='Microsoft-Windows-CodeIntegrity/Operational'; StartTime=(Get-Date).AddHours(-1); Id=3077,3033} -MaxEvents 100`, filtered to Visory; `Get-AuthenticodeSignature`, `Get-FileHash`, `Get-Item -Stream *` for the test DLL | Events 3077/3033 explicitly identify Enterprise signing-level requirements. DLL is unsigned, with only its normal data stream. SHA-256 `F34697204009FFFA686FAD08A2BCC9CA1B3BA1F69C9840576E9B988D9DEBD632`. Retrying the identical no-build command after diagnosis still fails under unchanged protection |
| Earlier native regression suite | PASS, intermediate revision only | Same native build and run commands above | Source-tree fingerprint `1d901f951effc786dfdfac41ea0d42beb336c28d3907d2b0220a1dd8dff52732`: 39 scenarios executed PASS, one 4.2 GiB heavy scenario SKIP; runner prints 40/40 including skip. D1-D4 covered per-line reads before disposal, concurrent events, redaction/idempotence, exact HTML/MD cell parity, abrupt exit, CSV row survival, zero-result reporting and legacy import |
| Startup integration | PASS, final scripts | `powershell.exe -NoLogo -NoProfile -File tools\visory-media-downloader\src\scripts\test-startup.ps1` | Isolated fixture: different CWD, terminal output, unique repeat runs, missing SDK stops startup, timestamped errors, CSV contents in HTML/MD, stdout/stderr redaction and nonzero child exit propagation |
| Central entry smoke | PASS | `$env:VISORY_NONINTERACTIVE='1'; & .\tools\visory-media-downloader\RUN.cmd -CheckOnly` | Runtime paths shown; SDK output saved; app not started |
| Portal production/typecheck | PASS | `pnpm build` | Next.js production build and TypeScript pass; final native source included. Last subsequent change was historical-document wording, then source assets regenerated |
| Final provenance/source assets | PASS | `node scripts\sync-visory-source.mjs`; `node scripts\prepare-tool-sources.mjs` | 79 hashed files + provenance = 80 files; ZIP 724462 bytes. Final tree SHA-256 `7e0f3111aeccc2303b9cca596b743f1b53c661c99f86a6f6efe9a192532aa06e` |
| Source/catalog contracts | PASS | `node --test tests\tooling\visory-source.test.js tests\tooling\tools-catalog.test.js` | 12/12, rerun against final assets; ZIP byte equality, EOL rules, provenance and unsafe runtime/link paths |
| Generated index | PASS | `pnpm tools:index:check` | Five tools indexed without executing project code |
| Repository structure | PASS | `pnpm check:repository` | 57 JavaScript syntax checks, 13/13 structure tests |
| Focused lint | PASS | `pnpm exec eslint scripts\sync-visory-source.mjs tests\tooling\visory-source.test.js tests\browser\visory.spec.js` | No findings |
| Focused formatting | PASS | `pnpm exec prettier --check scripts\sync-visory-source.mjs tests\tooling\visory-source.test.js tests\browser\visory.spec.js tools\visory-media-downloader\portal.json tools\visory-media-downloader\source-files.json` | All matched files formatted |
| Browser/portal accessibility | PASS | `pnpm exec playwright test visory.spec.js --workers=1` | 12/12 Chromium/Firefox/WebKit; setup, source download, old URLs, search, themes/narrow layout, existing axe checks and no external page requests. Not desktop accessibility certification |
| Bounded credential/sink scan | PASS | `pnpm check:source` after staging owned renames | 419 paths checked before adding this audit; no selected credential patterns/forbidden portal sinks. Not an independent security audit |
| Performance | PASS on intermediate native revision only | Earlier native scenario 32, same suite command | 100,000-record synthetic inventory test passed; no new performance guarantee |
| Whitespace | PASS | `git diff --check`; `git diff --cached --check` | No whitespace errors; existing portal LF/CRLF warning is not a failure |
| Full root test/browser suites, release publish, real account, desktop GUI | NOT RUN | N/A | Focused scope; final native policy block; no authorized account or release operation |

Resolved intermediate failures:

- Initial persistent file handles prevented default readers from opening live
  files. Corrected to open/append/flush/close each line; D1-D4 and 39 regression
  scenarios then passed.
- Windows PowerShell converted a null File.Replace backup name incorrectly.
  Corrected to its explicit NullString representation; startup regression passes.
- The missing-SDK fixture exposed PATH-dependent PowerShell launch; RUN now uses
  the Windows PowerShell absolute system path and the fixture passes.
- Stale WPF BAML intermediates caused BG1002. Normal
  `dotnet clean tools\visory-media-downloader\src\MoorkszVisoryMediaDownloader.sln -c Debug --nologo -v quiet`
  followed by the normal build resolved this; no native policy was changed.
- A pre-stage source scan saw Git's old deleted paths. After explicitly staging
  the task-owned renames, the unchanged scanner passes. No scanner bypass added.

## 4. Risk and quality review

- Contract: intended new log/report formats and source hierarchy, console
  executable subsystem and CSV/HTML/MD exports. Existing storage/session/scheduler
  identities are unchanged. Legacy artifacts are preserved; no originals or
  downloads are deleted/overwritten by this work.
- Security/privacy: no dependencies, elevation, telemetry, hosts, scheduled tasks,
  signing or installs added. SDK/build output and app events are redacted before
  console/file output. Query/fragment parameters, URL credentials, quoted secret/
  header fields, JWTs and cookies have synthetic coverage. Arbitrary secret prose
  cannot be guaranteed to be recognized; logs may contain identifying paths.
  Reparse-point checks guard new diagnostic outputs. HTML has no scripts or
  external assets, uses escaping and an offline CSP. CSV text is formula-safe.
- Accessibility: existing WPF controls/localization and keyboard/reduced-motion
  behavior retained; new report export controls follow existing patterns.
  Added diagnostics failure messages have English/German entries. Portal tests
  pass; actual desktop interaction was not re-certified.
- Disabled behavior: CheckOnly performs local checks, no build/app/Visory
  connection. Portal indexing never runs native code. Signing in stays opt-in.
  An explicitly requested source build can restore its existing NuGet package.
- Failure/recovery: no logger IO suppression, nonzero child exit stops startup,
  missing prerequisites get actionable terminal/report errors. Fatal writes
  surface to console/UI instead of continuing silently. Each completed Markdown
  line/CSV row gets an OS disk flush. Companion views are replace-written
  snapshots and can be stale after a hard kill. Database/file writes are not
  one transaction. Normal resume appends and re-seeds; invalid CSV fails explicitly.
- Cleanup: all successful tests clean their isolated fixtures. The 21 exact
  leftover fixture directories from the initial failed handle tests were inspected
  and individually resolved/deleted, without glob/root deletion or personal files.
  No helper service remains intentionally running. Real CheckOnly diagnostics
  remain ignored under runtime as run outputs.
- Regression exposure: no discovery recognizer, download transport or new
  endpoint changed. Final small archive-span/error-state refinement is compiled
  but not yet executable-tested due to host policy, which blocks completion.

## 5. Findings and residual risks

| ID | Severity | Finding and evidence | Mitigation / owner | Status |
|---|---|---|---|---|
| V1 | Blocking | Final native test DLL cannot execute under Windows Application Control, `0x800711C7` | Maintainer: run unchanged standard commands in a policy-approved Windows development environment; do not disable/bypass protection | OPEN |
| V2 | Medium | Latest D5 archive error/span assertions and final native localization/error-display edits were not runtime-tested | Re-run full console suite after V1; preceding foundation revision has 39 executed passes | OPEN |
| V3 | Known limitation | Abrupt termination can leave HTML/MD snapshots behind CSV; no end time/atomic multi-file transaction guaranteed | CSV is authoritative; durable completed lines/rows, explicit incomplete state and resume rebuild | DOCUMENTED |
| V4 | Known limitation | Real-account discovery remains unverified and may still find zero recordings | Later dedicated diagnosis phase; no claim of repair here | OUT OF SCOPE |

- Not run: final native suite, newly added D5, desktop GUI/screen reader,
  real Visory account, heavy >4 GiB test, installer/publish/release.
- Required approvals: none used for local source work. Native policy administration,
  installs, release/signing/deployment require separate authorization; none done.
- Recovery: preserve current staged work and ignored run outputs. Review or restore
  individual task-owned paths from `e7cbd59` only with maintainer approval; do not
  reset unrelated work. Existing backup data and old reports were not migrated away.
- Release readiness: `NOT READY`; this is an unreleased foundation, not certification.

## 6. Completion record

- Audit created from the canonical template before any task commit. Task-owned paths
  are staged; no commit is made while final required native verification is blocked.
- A later unrelated untracked `tools/LICENSE` appeared during the follow-up. It
  was left untouched and is not included in the staged task changes.
- Final commit hash: `NOT CREATED - V1/V2 remain open`
- External actions: `NOT AUTHORIZED` unless separately approved.

**Limitations:** An agent self-audit is not an independent security review, accessibility certification, or release approval.
