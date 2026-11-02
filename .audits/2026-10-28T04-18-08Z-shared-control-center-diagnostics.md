# Development Audit: Shared control-center diagnostics

| Field | Value |
|---|---|
| Audit ID | `2026-10-28T04-18-08Z-shared-control-center-diagnostics` |
| Created (UTC) | `2026-10-28T04:18:08Z` |
| Task / issue | Read-only Logs & Errors tab, independent animated terminal handles, full local logs, progress, elapsed time and report-derived averages |
| Auditor | Copilot agent self-audit |
| Branch / baseline commit | `master` / `a59794e`; previous startup work and staged workspace migration already present |
| Final commit | Not created: task changes overlap the already-staged CLI relocation and pre-existing modified/untracked documentation and tests |
| Scope reviewed | Shared native Moorkyz control center, full-current-log CLI option, startup file checks, related native/CLI tests and documentation |
| Outcome | `PASS WITH RISKS` |

## 1. Request and outcome

- Requested: each control center has a terminal with hide/show handles and
  fade/slide animations; all text panels are read-only; Logs & Errors share a tab
  with independently collapsible panes; full logging, progress, session/command
  elapsed time and report-derived averages are important.
- The user supplied two local folders as historical/product guidance. Their
  README.md and README.html files were verified as empty (0 bytes). Only
  high-level directory names were inspected; evidence payloads, private report
  contents and downloaded executables were not read or run. Nothing there was
  modified. No requirement was inferred from inaccessible document text.
- "Both apps" was clarified through the available question tool, but the owner
  was unavailable. The bounded assumption is the shared control center used by
  the two existing launcher identities, not a retrofit of unrelated native apps.
- Implemented one shared Logs & Errors view with independent persistent button
  handles, fade/slide/height transitions, keyboard-accessible activation, focus
  recovery, latest-choice handling and immediate reduced-motion settlement.
- Every diagnostic text box is read-only. The display is a bounded session
  history, while full redacted command output is flushed to unique local files.
  Hiding panes does not stop capture or erase history.
- Activity is indeterminate when the command has no measurable percentage, with
  a static running state under reduced motion. Successful command and logging
  completion fills progress; failure never looks like successful completion.
- Stopwatch-based session/current-last-command elapsed time is shown, without
  wrapping total hours after a day. Versioned duration reports support exact
  same-launcher/project/command averages across sessions, including failed runs.
- The explicit Logs action reads a complete current service-log snapshot via
  `logs --all`, not the previous 80-line/512-KiB-bounded tail.
- Intentional exclusions: arbitrary shell input, remote deployment actions,
  silent watchers, hosted logging, native app rewrites, automatic pruning,
  signing/publishing and changes to supplied personal evidence folders.
- Development/build/test output invoked through this GUI is captured. Independently
  launched apps and deployment pipelines retain their own logging. This is not a
  claim of universal capture of all processes or historical archived files.
- Contracts: root AGENTS, current product decisions, shared GUI guide and existing
  CLI/log-storage behavior. Existing Node/pnpm/Next.js/React/TypeScript pins and
  Windows C#/WPF architecture remain unchanged.

## 2. Change inventory

| Path | Task-owned change | Risk |
|---|---|---|
| `src/.moorkyz/gui/MainWindow.xaml` | Shared Logs & Errors view, read-only panes/handles, progress/timing/report location | Low; native UI only |
| `src/.moorkyz/gui/MoorkyzGui.cs` | View routing, independent animations/focus, timers, output/error buffers, durable journal integration and explicit failures | Medium; command and logging lifecycle |
| `src/.moorkyz/gui/CommandJournal.cs` | Full local redacted logs, unique per-run directories, atomic report publication and validated duration averages | Medium; explicitly requested local persistence |
| `src/.moorkyz/gui/start.ps1` | Compile the new journal with the existing native sources | Low |
| `src/.moorkyz/scripts/moorkyz.cjs` | Add non-truncated streamed `logs --all`; preserve tail/follow behavior and reject conflicting options | Low |
| `src/.moorkyz/scripts/helpers/startup.cjs` | Require the new canonical GUI source during preflight | Low |
| `src/tests/tooling/moorkyz-startup.test.cjs` | Include that source in isolated structure fixtures | Low |
| `src/tests/tooling/moorkyz-gui.test.ps1` | Real native animation, logging, progress, exact average, persistence, failure and minimum-width checks | Low; isolated synthetic fixtures |
| `src/tests/tooling/moorkyz-full-logs.test.cjs` | Full log snapshots beyond the old cap for both CLI identities | Low; isolated temporary logs |
| `src/.moorkyz/gui/README.md`, `src/docs/moorkyz-cli.md` | Explain scope, read-only terminals, full logs, privacy, retention and failure semantics | Low |
| This audit | Verification evidence and limitations | Documentation only |

Pre-existing workspace migration, startup/EXE work and existing documentation
edits are not part of this task's change ownership.

## 3. Verification evidence

| Check | Result | Exact command / method | Evidence / notes |
|---|---|---|---|
| Native/CLI focused suite, final | PASS | `node --test --test-concurrency=1 src\tests\tooling\moorkyz-gui.test.cjs src\tests\tooling\moorkyz-full-logs.test.cjs src\tests\tooling\tools.moorkyz.com-cli.test.cjs` | 14 passed, 0 failed, 0 skipped |
| Shared startup/EXE/full-log compatibility | PASS | `node --test --test-concurrency=1 src\tests\tooling\moorkyz-full-logs.test.cjs src\tests\tooling\moorkyz-startup.test.cjs src\tests\tooling\moorkyz-exe.test.cjs` | 12 passed, 0 failed, 0 skipped |
| Native build/visual capture | PASS | `powershell.exe -NoProfile -STA -File .\src\tests\tooling\moorkyz-gui.test.ps1 -ScreenshotDirectory 'C:\Users\marvi\.copilot\session-state\7ecea607-cb75-46b7-adfe-75fdca9fe180\files\control-center-diagnostics'` | Compiled actual C#/XAML; native assertions pass; Logs & Errors screenshot inspected |
| Repository structure/syntax | PASS | `pnpm --dir src check:repository` | 281 JS syntax checks and 14/14 structural tests |
| Lint | PASS | `node .\src\node_modules\eslint\bin\eslint.js --config .\src\eslint.config.mjs --max-warnings 0 src\.moorkyz\scripts\moorkyz.cjs src\.moorkyz\scripts\helpers\startup.cjs src\tests\tooling\moorkyz-full-logs.test.cjs src\tests\tooling\moorkyz-startup.test.cjs` | Zero warnings/errors |
| Formatting | PASS | `pnpm --dir src exec prettier --check .moorkyz\scripts\helpers\startup.cjs tests\tooling\moorkyz-full-logs.test.cjs tests\tooling\moorkyz-startup.test.cjs .moorkyz\gui\README.md` | All selected supported files match style; native source follows existing conventions |
| Full log fidelity | PASS | Native and full-log fixtures above | Complete 125,000-character output line retained beyond 120,000-character preview cap; entire 680-KiB service log matches exactly for both identities |
| Independent transitions | PASS | Native fixture above | Intermediate height/opacity/translation asserted; collapsed layout releases space; repeated toggles settle latest state |
| Read-only/accessibility/reduced motion | PASS (self-test only) | Native fixture above | All diagnostic text boxes read-only; native button invocation patterns; focus recovery; immediate reduced-motion settlement; no command runs on open |
| Timing/progress/average correctness | PASS | Final native fixture above | Running/completed/failed progress, positive report duration, total-hour nonwrapping at 25 hours, persisted-run count across sessions and exact computed average verified |
| Privacy/storage failure behavior | PASS (self-test only) | Native fixture above | Secrets/credential URLs redacted in files/UI; setup failure prevents launch; junction not followed; malformed report surfaces failure and no invented average |
| Responsive layout | PASS | Native fixture at 940 x 680 | No horizontal page scrolling; terminal text can scroll internally; content remains vertically scrollable |
| Browser/typecheck/full portal build | N/A / NOT RUN | Native WPF and Node tooling only; no web/TS changes | Actual native compilation and UI automation performed; no claim of independent accessibility certification |
| Full repository source gate | NOT RUN | Earlier task established a scanner blocker at a pre-existing moved/deleted audit path | This task does not repair or bypass unrelated migration/scanner behavior |
| Whitespace | PASS | `git diff --check -- src/.moorkyz/gui src/.moorkyz/scripts/moorkyz.cjs src/tests/tooling/moorkyz-gui.test.ps1`; new task files checked against an empty temporary file before completion | No task whitespace errors |

Earlier observed failures:

- The first native run still expected only one fixture file after explicit
  commands. New logs intentionally add local state. The assertion was corrected
  to snapshot before opening the real idle GUI and prove opening adds nothing.
- A later run hit the pre-existing transient inset-animation timing assertion.
  An isolated run passed, as did screenshot/native and final sequential suites.
  Existing interaction timings were not weakened. Native animation fixtures on a
  shared desktop remain susceptible to load/pointer interference.

## 4. Risk and quality review

- Compatibility/defaults: shared native GUI behavior is consistent for both CLI
  identities. Command allowlist and one-command-at-a-time behavior remain.
  Existing tail/follow CLI modes are unchanged; `--all` is an explicit new snapshot.
- Security/privacy: no new dependency, remote endpoint, telemetry, account or
  elevation. Credentials/common secret fields and HTTP(S) URLs are redacted before
  persistence/display; this is not universal sensitive-data detection. Paths and
  filenames can still be private. Downloaded evidence is untouched.
- Storage: unique run directories and CreateNew streams avoid replacing previous
  history. Report publication uses a completed temporary file and non-overwriting
  move. Linked directories/reports are refused. XML DTDs/external resolution are
  disabled. Incomplete runs retain logs without a false completed report.
- Failure handling: log setup failure blocks launch. Stream/report/aggregation/
  disposal failures remain visible and cannot turn a successful child into an
  overall successful logging state. No silent defaults, malformed-report skips,
  recursive reports or automatic cleanup.
- Accessibility: normal native buttons support Space/Enter and UI automation;
  collapse preserves a visible handle and moves focus out of hidden content.
  Windows and UI reduced-motion preferences apply to transitions and activity.
- Resource behavior: no log files are created on idle open; no project service or
  watcher is started. A UI timer updates elapsed time and stops on close. Writes
  and report aggregation occur outside the UI thread after initial setup.
- Performance/retention: screen and pending text buffers are bounded; disk logs
  intentionally retain full output with no automatic pruning. History can grow,
  and same-command averages scan existing small report files. Deliberate manual
  archival is documented; no unrelated retained data is removed.

## 5. Findings and residual risks

| ID | Severity | Finding and evidence | Mitigation / owner | Status |
|---|---|---|---|---|
| DIAG-1 | Low | Supplied README documents contain no text; "both apps" could not be clarified | Apply to existing shared control center only; no guessed application integration | Documented scope assumption |
| DIAG-2 | Low | Full logs increase local disk usage and may contain private metadata beyond common redactions | Local ignored storage, explicit paths, no uploads; manual review/archival | Accepted requested behavior |
| DIAG-3 | Low | Long command previews are capped, not an unlimited RAM terminal | All captured lines retained on disk; explicit truncation notice and full-log location | Intentional responsiveness boundary |
| DIAG-4 | Low | Independent apps/deployment pipelines are not automatically observed | Capture only selected GUI/CLI commands and explicit service snapshot; no remote action added | Documented boundary |
| DIAG-5 | Low | Existing native animation assertions can be timing-sensitive on a shared desktop | Final native checks pass; retain exact evidence and do not claim independent certification | Existing test limitation |
| DIAG-6 | Low | Required CLI edits overlap already-staged relocation from earlier work | Preserve index; do not commit unrelated changes or an incomplete feature | Commit blocked |

- No full portal browser suite, real production deployment, external integration,
  signed release, personal evidence processing or standalone-app redesign was run.
- A screenshot was inspected: terminals fit the shared page width; vertical
  scrolling exposes both panels at small heights, and collapsing a pane reduces
  layout height smoothly.
- Rollback: remove only new journal/full-log test/audit and reverse task hunks,
  preserving prior startup/migration edits. Runtime diagnostic histories remain
  user-owned and are not deleted by rollback.
- Release readiness: `READY WITH CONDITIONS` for local shared control-center use;
  not certification of every independent application's logging or deployment.

## 6. Completion record

- Audit prepared from the current template with observed evidence.
- No commit made: the initial index already contains 26 migration entries,
  including the required CLI destination as an added/renamed path. Documentation
  and native-test files also had pre-existing edits. Committing entire paths would
  absorb earlier work, and committing only the new files would omit required
  wiring. The existing index is left untouched.
- Final commit hash: `NOT CREATED - pre-existing staged migration overlap`.
- External actions: `NOT AUTHORIZED`; no signing, publication, push or deployment.

**Limitations:** An agent self-audit is not an independent security review,
accessibility certification, or release approval.
