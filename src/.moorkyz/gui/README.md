# Moorkyz local control GUI

This is the Windows front end for the repository-management CLI, not a
portal page, a Visory redesign, or a separately published tool.

From the intended project root:

```powershell
node .\moorkyz gui
# Uses the other launcher's configuration/state instead:
node .\src\.moorkyz\scripts\tools.moorkyz.com.cjs gui
```

Requires the existing Node runtime, Windows PowerShell 5.1 and Windows' built-in
WPF assemblies. No NuGet package, service, browser bridge, profile edit or
execution-policy change is added. A policy or Application Control error remains
an error; the launcher never bypasses it.

## What works

- Visual transitions take 320 ms and spring feedback 560 ms. They never delay
  command dispatch, processing, logging or settings persistence. Navigation reuses
  the existing page and clicking its current tab does not replay its entrance or
  reload Reports. Changing only the search hit limit keeps the live terminal
  document, selection and scroll position intact; timestamp changes still
  reformat the displayed history. Reduced motion remains immediate.
- Returning to a section resets its outer scroll position to the top without
  rebuilding its controls. On its first visit per window, panel subsections
  slide up and fade in with 90 ms staggered starts (including the initial page).
  Later visits use only the lightweight page transition. Navigating away or
  disabling motion immediately settles pending panel entrances; none of these
  effects delay commands, searches or settings saves.
- Select a configured project, then explicitly run Start, Status, Restart, Stop,
  Logs, Doctor, Build, Test, Lint or Typecheck through the existing CLI.
- Commands run outside the UI thread, retain their real exit code, and show
  read-only stdout/stderr in the shared **Logs & Errors** tab. Each terminal has
  an independent persistent Hide/Show handle. Height, opacity and translation
  animate together so the layout reflows; rapid toggles follow the latest choice.
  Windows reduced-motion settings and the Interface motion switch settle these
  transitions immediately. Focus returns to the handle when its pane is hidden.
  Only one command runs at a time.
- Both streams are retained on disk without the preview's 120,000-character cap.
  Hiding a terminal never stops capture. Terminal history survives subsequent
  commands in the same window, with explicit notices when old preview text is
  omitted. stderr also has its own read-only pane/file and remains visible in
  the combined log.
- Both terminals use the same local saved-history search component. The default
  is **5 hits**; the shared **Search hits** setting accepts every integer from
  **1 to 10**. Search runs off the UI thread after a short debounce, with
  cancellation when the query changes. All terms must match, case-insensitively;
  use `"quoted phrases"`, `-exclusions` and `level:ERROR` (or `level:INFO`).
  Punctuation is literal, not a regular expression. Runs are searched newest
  first, then physical file-line order; the limit is a display cap, not a total
  match count. Search includes saved history beyond the bounded live preview.
- Click a terminal line or select a search hit to reveal the file/line and
  directory icon buttons. The file icon opens a **read-only context excerpt**
  of the actual saved file, selecting its exact numbered physical line. Very
  long viewer lines are explicitly shortened; the full file stays unchanged.
  The directory icon selects that file in Windows Explorer. Unsaved logging
  failures cannot offer a saved-file location. Ctrl+F from a terminal focuses
  its query; Enter/Shift+Enter cycles hits and Escape clears the query. Hiding
  a pane also hides its search controls and results, without stopping capture.
- **Timestamp (UTC)** switches terminal and search-hit display between
  `[yyyy-MM-dd HH:mm:ss] [LEVEL] message` and `[HH:mm:ss] [LEVEL] message`.
  Full date/time is the default. Disk logs and exact-source excerpts always
  retain full UTC dates for traceability. Changing settings explicitly saves
  them locally to `.moorkyz/control-center/settings.xml`; opening the window
  alone creates no settings file. Settings save failures are reported in Errors
  and restore the previous choices.
- Progress shows activity while a command runs (no invented percentage), success
  only after successful command/log completion, and a distinct failure status.
  Logs show **Time spent**, then **Average**, for the current/last task; no
  session-total clock is displayed. Completed reports
  supply the average duration for the same launcher, project and command across
  sessions, including failed commands. Reduced motion disables the indeterminate
  animation without stopping timing or capture.
- Errors show **Time since last error**, then **Average interval** between
  errors in this window/session, measured with a monotonic clock. No-error and
  single-error states explicitly report unavailable statistics rather than
  invented zero averages. Replaying a recent error in the preview does not
  count it again. Each command report also saves its own error count, interval
  sample count and average interval; session-wide and per-command samples are
  different scopes.
- Both timing displays use one unit only: seconds below 120 seconds, minutes
  below 120 minutes, hours below 120 hours, then days. For example `119.9 s`,
  `2 m`, `2 h`, `5 d`. Values are truncated to one decimal rather than rounded
  prematurely across a boundary. Days remain the largest unit (no ambiguous
  month/year conversions). Statistics are neutral violet; no arbitrary
  good/bad performance score is inferred from duration.
- Read-only terminals render colored text fields, not ANSI escape sequences or
  executable markup. Timestamps always use the same muted color; TRACE/DEBUG
  are gray, INFO/SUCCESS/PASS green, WARN/WARNING amber and
  ERROR/FATAL/CRITICAL/FAIL red. Unknown levels use gray. stdout beginning with
  a recognized `[LEVEL]` keeps that severity; ordinary stdout is INFO and stderr
  always ERROR. Structured stdout error levels also enter the Errors stream.
  Diagnostics use blue, statistics violet and file references teal.
  Outcomes/report locations use labeled PASS/success green or FAIL/error red;
  severity/outcome text remains present so color is never the only signal.
- New saved entries include a small reference such as `[a1b2c3d4:L17]` after
  their level. It combines the run ID suffix with the canonical combined-log
  physical line; the same reference is retained in the error file and preview
  replays. The exact-source icon/tooltip remains authoritative and maps to the
  correct physical line of either file; short suffixes are convenience labels,
  not globally unique identifiers.
- The Logs action requests the complete current managed-service log snapshot,
  not only the old 80-line tail. It does not start a permanent follower or read
  unrelated projects' files.
- Bare `moorkyz`, `moorkyz start`, `moorkyz launch`, RUN and the EXE open the GUI
  first in this Windows checkout. The shared startup checker then runs as a
  hidden, fully logged child, with the existing bounded retries/frozen-lockfile
  dependency repair. Project actions stay disabled until it passes; failures
  show a **Retry startup checks** button and saved error/report history.
  No service is automatically selected. Explicit `moorkyz gui` remains an idle
  inspection entrypoint; `MOORKYZ_HEADLESS=1` preserves console automation and
  is set explicitly for GUI children to prevent recursive windows.
  Closing while a command is active is
  blocked with a visible explanation. Closing an idle GUI does not stop existing
  managed services; use Stop deliberately. No install, archive, scheduled task,
  arbitrary shell command or machine-wide prerequisite installer is exposed.
- **Reports** is a separate local page. Compact **Diagnostics** and
  **Statistics** summaries stay visible above the primary report list: side by
  side at widths of at least 700 logical pixels, stacked below that width.
  There are no summary hide/show or reorder controls to manage.
- The primary report list has an editable search bar and read-only items showing
  full run ID, UTC timestamp, PASS/FAIL success, time spent, video duration, file
  size, foreground UI time and background time. It shares the 1-10 hit limit,
  timestamp display preference and literal AND/phrase/exclusion/`level:PASS` or
  `level:FAIL` search grammar with the terminals. Newest UTC starts appear first;
  the status gives displayed and total matching counts. Search includes
  displayed metadata, IDs and task/project/launcher names. Enter selects the
  next result; Escape/Clear clears the query. Refresh reads saved files without
  starting commands. Opening Reports or completing a command while it is visible
  also refreshes it; there is no filesystem watcher or background polling.
- Report row accents and their message text use the same performance color.
  Baseline is the arithmetic mean of **earlier successful** reports for the same
  launcher/project/task, excluding the current result and failed samples:
  green **Faster** at <=90% of baseline; neutral violet **Typical** above 90%
  through 110%; amber **Slower** above 110% through 125%; red **Much slower**
  above 125%. Failed reports are red **Failed** regardless of speed. Missing or
  zero baselines are neutral **No baseline**. These are comparison labels, not
  quality certification or a numerical score; a faster unrelated workload is
  not necessarily a better tool result. Timestamps, severity and reference fields
  retain their fixed semantic colors independently of the message accent.
- New command reports measure **Time on UI** while this control-center window
  is active and not minimized, and **Time in background** otherwise. They sum to
  the measured task duration; they are not CPU/worker execution times. Older
  reports without measurements explicitly show **Unavailable**. Generic
  commands do not invent video durations or output sizes. A tool-produced
  version-1 report can supply finite nonnegative `videoDurationSeconds` and
  whole-number `fileSizeBytes`; these are displayed without probing media or
  arbitrary user files. Foreground/background fields are `uiSeconds` and
  `backgroundSeconds`, both required together and validated against duration.
- Reports are read off the UI thread, with debounce/cancellation for changed
  searches. Incomplete folders are counted separately. Malformed or unsupported
  reports, invalid measurements and linked paths clear the results and expose
  a path-bearing error; statistics become unavailable rather than showing a
  partial success-shaped aggregate. Correct/remove the affected file deliberately
  and Refresh. The Statistics section summarizes **all validated reports**, not
  only the currently filtered hits; mixed-task averages are clearly aggregate
  durations, not performance baselines.
- Drag quick-action cards to reorder them for the current window. Alt + arrow
  keys gives the same reorder operation without dragging. Reordering does not
  execute a command or persist preferences.
- The Interaction lab offers three presets in each family: hover, press, drag
  and move. Badges and icon buttons use the same feedback system as other
  buttons. Material samples use real WPF 3D rotations, a fine tiled texture,
  an embossed imprint and pointer-positioned warm sunlight.
- The cursor-following drag preview uses a damped spring with velocity, so it
  trails acceleration, brakes on direction changes, and settles rather than
  teleporting. Its rendering subscription exists only during a drag and is
  removed on completion/cancellation.
- Windows' animation preference is respected. The Interface motion switch
  immediately clears active effects without removing focus/press/hover feedback
  or either reordering method.

## Shared implementation

- [Theme.xaml](Theme.xaml): central palette, spacing, radii, focus treatment and
  reusable button/card/badge/icon/select/input/scrollbar styles and diagnostic
  field/category colors.
- [MainWindow.xaml](MainWindow.xaml): native window layout, workspace and lab.
- [MoorkyzGui.cs](MoorkyzGui.cs): shared control factories, interaction presets,
  project/action binding, animated terminals, progress/timing and asynchronous
  CLI output and cleanup.
- [CommandJournal.cs](CommandJournal.cs): full local command logs, redaction,
  durable duration reports and report-derived averages.
- [TerminalSearch.cs](TerminalSearch.cs): shared search, source navigation,
  timestamp display, bounded preview/source mapping and local settings.
- [ReportView.cs](ReportView.cs): validated local report loading, searchable
  report list, responsive always-visible summaries and performance accents.
- [Motion.cs](Motion.cs): inertial drag preview and reusable 3D material cards.
- [start.ps1](start.ps1): the STA native bootstrap. The CLI supplies validated
  projects and the selected entrypoint, preserving each launcher's namespace.

The GUI loads the existing [public icon set](../public). When copying optional
GUI support with the CLI, include both this directory and that public directory.
The portable CLI itself continues to work without GUI support.

## Full logs and reports

Logs are created only when an action is selected, under
`.moorkyz/control-center/runs/<UTC timestamp>-<unique ID>/` in the selected checkout:

- `logs.log`: complete captured stdout/stderr, with full UTC timestamps, levels
  and compact source references, including command context. Failed commands
  replay recent stderr in the preview after completion so a large stdout burst
  cannot hide it; replayed lines retain their original file/line references and
  are not duplicated on disk.
- `errors.log`: all captured stderr separately.
- `report.xml`: versioned outcome, launcher, command, project, UTC start/end,
  measured duration, exit code, error/interval counts, average error interval
  (empty when fewer than two errors), measured foreground/background durations
  and relative log-file references. It is published
  only after writing finishes; incomplete runs keep their logs without pretending
  to be completed reports.

The read-only report-location panel exposes the directory so paths can be
selected/copied. File logs are flushed as lines arrive. Common credential fields,
Bearer values and HTTP(S) URLs are redacted before either persistence or display.
Do not deliberately emit secrets: this is not a universal sensitive-data detector;
filenames and other metadata can still be private.

GUI command output (including Build/Test and CLI-reported development activity)
is captured. The selected service's own current log is loaded on the explicit
Logs action; it is a snapshot, not an always-on observer. Existing service logs
and deployment pipelines keep their own logging. No remote deployment command,
new integration or automatic service watcher is added. Independently launched
desktop tools are not silently changed by this GUI update.

No automatic pruning, rotation or deletion is applied to this diagnostic history.
It can grow; review/archive it deliberately. Log setup failures prevent running
the command. Write/flush/report/aggregation failures are surfaced in Errors and
the run status, and a successful child process cannot hide a logging failure.
Malformed existing reports are not silently skipped when calculating averages.
Symlink/junction log destinations are rejected. Logs remain local and ignored by
Git; they are not public evidence bundles or deployments.

## Verification

```powershell
node --test --test-concurrency=1 src\tests\tooling\moorkyz-gui.test.cjs src\tests\tooling\moorkyz-full-logs.test.cjs
```

The Windows-only native fixture compiles the actual source and XAML, checks all
twelve preset choices and visible hover/press states, 3D and reduced-motion
cleanup, native button automation, reordering, spring lag/braking/settling,
bounded errors and real CLI GUI launch/close. It also checks read-only panels,
independent fade/slide/reflow, focus and reduced-motion behavior, output capture
while hidden, full disk logs beyond the preview cap, progress/timing, exact
120-unit formatting thresholds, colored fields and references, measured error
intervals, duration
reports, averages across sessions, redaction and explicit storage/report failures.
Report tests additionally cover empty history, side-by-side/stacked summary
layout, foreground/background time accounting, optional tool metadata,
legacy missing measurements, exact performance boundaries and prior-only
baselines, row/message color agreement, search limits/cancellation, malformed
XML/measurements, linked histories and unchanged report bytes.
Launcher tests open real native windows for bare/start/launch commands before
failed preflight, verify the visible Retry action, and check successful startup
gating and the internal headless worker. No real service is started.
Search checks cover defaults and configurable limits, literal AND/phrase/
exclusion/level matching, stale-query cancellation, Enter/Escape navigation,
exact combined/error-file line selection, read-only source excerpts, timestamp
display/persistence, unchanged saved-file bytes and junction refusal.
Commands use synthetic temporary files; tests never start a real project service
or touch account/media data.

This is a first working UI pass. It does not claim independent accessibility
certification, real-account validation, a published desktop release, or a final
Moorkyz design. Text scaling/assistive-technology and real pointer-drag feel still
benefit from owner review.
