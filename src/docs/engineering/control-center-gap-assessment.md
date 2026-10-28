# Control center: gaps, layout priorities and next focus

Assessment date: 2026-10-28. Scope: the local Windows Moorkyz control center and
its launchers, not a redesign of every portal tool.

This is an implementation-grounded handoff, not independent security,
accessibility or release certification. It distinguishes existing functionality,
observed gaps and proposed changes. The companion
[task schedule](control-center-task-schedule.md) contains the ordered backlog.
Proposals here do not override the [root contract](../../../AGENTS.md) or
[current product decisions](../decisions/decision-register.md).

## Executive assessment

**The visual language is worth keeping. The information layout needs the next
major investment.** More decorative effects are not the current priority.

The current application has substantial real behavior: native command execution,
bounded startup retries, full local logging, history search, exact source
references, measured timing, validated reports and motion preferences. It is
not merely a mockup. However, the shell still reads partly like a component
showcase and partly like a developer command dashboard. Routine use needs a
clearer hierarchy.

The most urgent reliability gap is separate from design: the owner supplied a
screenshot of RUN presenting the old console checklist/menu after GUI-first
changes. Local isolated launch tests pass, but that does not establish why the
owner's invocation differed. **That report remains unresolved in the actual
owner environment.** It must not be hidden behind a claim that the GUI tests pass.

The user's "ZU layout" feedback is interpreted as dissatisfaction with the GUI
layout. No replacement layout was specified, so the structural direction below
is a proposal for review, not an already-approved redesign.

## What is finished in this iteration

| Area                      | Current implemented behavior                                                        | Confidence boundary                                                          |
| ------------------------- | ----------------------------------------------------------------------------------- | ---------------------------------------------------------------------------- |
| Visual feedback           | 320 ms transitions, 560 ms spring feedback                                          | Native tests; not a universal subjective preference                          |
| Initial section entrance  | Panel slide/fade starts staggered 90 ms, once per section per window                | Initial page, sequence order, cancellation and reduced motion tested         |
| Return navigation         | Outer section scroll resets to top; controls are reused                             | All four sections are actually scrolled and revisited in native tests        |
| Current-section selection | No repeated entrance or Reports reload                                              | Explicit regression assertion                                                |
| Display settings          | Hit limit 1-10, full/compact UTC timestamps; explicit local save                    | Hit-only edits preserve live document/selection; timestamps reformat history |
| Startup                   | GUI-first routing in Windows checkout; hidden checker; action gating and Retry      | Isolated launch tests pass; owner's console-menu report remains open         |
| Command execution         | Existing CLI operations off UI thread, real exit code, one command at a time        | Isolated command fixtures; not real service acceptance                       |
| Logs and errors           | Shared page, independent panes, full redacted disk history, bounded preview         | Capture while hidden, storage failures and redaction fixtures tested         |
| Search/source             | Literal queries, phrases, exclusions, level filter, saved history, exact file/line  | Native search, keyboard and mapping coverage                                 |
| Timing/colors             | Task time/average, error interval/average, adaptive single units, semantic colors   | Exact thresholds and field color assertions                                  |
| Reports                   | Searchable local history; validated XML; outcome/duration/IDs; prior-task baselines | Malformed data and baseline boundaries covered                               |
| Summaries                 | Always-visible Diagnostics/Statistics above Reports; stack at narrower widths       | Native wide/narrow layout assertions                                         |

The native GUI loads current source when opened; an already-running window does
not hot-reload these changes. Reopen it to use the completed iteration.

## 1. Launch reliability needs closure first

### Evidence and limitation

[RUN](../../../RUN.cmd) currently selects the CLI path when
`MOORKYZ_HEADLESS=1`, otherwise preferring the built EXE for a no-argument launch.
The [CLI](../../.moorkyz/scripts/moorkyz.cjs) also deliberately honors that flag.
The [EXE](../../.moorkyz/app/MoorkyzLauncher.cs) invokes the shared launcher.
An installed PowerShell function can independently point at an older checkout.

Inherited headless state, a stale function, an old binary or a different checkout
are plausible explanations, **not proven diagnoses**. The earlier separate
tool shell could not resolve the owner's installed `moorkyz` binding. No profile
or machine environment has been changed.

### Required focus

Capture the actual failing resolution and launch context, using read-only,
non-secret diagnostics. Make the ordinary entrypoints predictably GUI-first
without breaking deliberate CLI automation. Surface bootstrap failures visibly,
including missing prerequisites and policy restrictions. Do not bypass Windows
execution policy, silently modify profiles or install machine tools.

The requested experience is a native window, not a menu asking the owner to open
that window. A batch host can briefly create a console; the windowed EXE remains
the proper no-console entrypoint.

## 2. Layout: retain style, change hierarchy

### What to preserve

- Deep blue/violet palette, clear accent roles and restrained material treatment.
- Legible timestamps, severity, references and textual performance labels.
- Shared control styles, focus treatment and reusable motion implementation.
- Local-first, explicit actions and honest loading/error/Unavailable states.
- Calm transitions that do not introduce sleeps or gate actual processing.

### Observed structural pressure

The [current shell](../../.moorkyz/gui/MainWindow.xaml) opens at 1240 x 870 logical
pixels with a 940 x 680 minimum, a fixed 182-pixel navigation column, generous
outer spacing and ten equal-rank quick-action cards. These are source dimensions,
not evidence that smaller screens or all DPI settings work well.

1. **Routine and developer actions compete.** Start, Status and Stop sit beside
   Build, Test, Lint and Typecheck with similar emphasis.
2. **The main workflow lacks a compact, stable activity context.** Project/action
   selection lives in Overview while activity, settings and results live elsewhere.
3. **Introductory copy consumes prime space.** Decorative headings and lab content
   should not outrank the next useful operation.
4. **Logs start with a relatively large status/settings block.** The useful output
   and search should be easier to reach without removing any evidence.
5. **Reports show many fields as multiline prose.** Complete metadata is valuable,
   but repeated long rows make comparing runs harder.
6. **Nested scrolling needs an explicit policy.** Page, terminal and result-list
   scroll ownership should be understandable, especially with keyboard focus.
7. **Some copy no longer matches startup.** "Nothing starts on open" and checks
   running only on an action are too broad now that launch performs preflight.
   Say that services never auto-start, while startup checks can run.

### Proposed direction, not yet implemented

Use the existing WPF stack; this does not justify a rewrite or a new UI package.

```text
Shared shell
  Compact identity + selected project + current activity
  Navigation: Overview | Logs & Errors | Reports
  Secondary destination: settings / interaction previews

Overview
  Startup/service state and next useful action
  Primary routine controls, with Stop clearly distinct
  Secondary development operations
  Recent outcome / direct links to evidence

Logs & Errors
  Compact activity/outcome strip
  Logs: heading + timing + search + readable output
  Errors: heading + interval + search + readable output
  Display settings available without dominating the page

Reports
  Compact always-visible Diagnostics / Statistics
  Search + result count + explicit refresh
  Scannable report list with essential comparable fields
  Accessible detail surface for complete measurements and source links
```

The interaction lab should remain useful for testing the style system without
being a top-priority operating destination. Decide its final placement during
layout review. Do not remove capabilities just to simplify a screenshot.

### Layout acceptance before implementation

Agree on wide and narrow mock layouts and test them with empty, running,
failed and history-rich content. Include long paths and messages. Preserve
keyboard access and avoid "fixes" that merely shrink text. Verify the supported
viewport and DPI matrix rather than claiming responsiveness from one screenshot.

## 3. Logs and errors need reading-flow polish

Already implemented: read-only rich text, full disk logs, pane controls, search,
source navigation, semantic colors and timing.

Next focus:

- Decide live-tail behavior explicitly. Appending currently renders the bounded
  rich document; preserving a deliberate older reading position needs focused
  tests and possibly implementation work. Hit-limit preservation alone does
  not prove streaming scroll stability.
- Keep source selections and error evidence usable while output arrives.
- Keep current task state visible without pushing the terminals far down-page.
- Distinguish task execution failure from logging/report-storage failure.
- Improve overflow handling for long references without losing copyable evidence.
- Keep errors discoverable even when the main log is noisy or a pane is hidden.

Do not trade full capture or accurate physical line references for a smoother
preview. Animation never represents a measured processing delay.

## 4. Reports need comparison, not more raw fields

[ReportView](../../.moorkyz/gui/ReportView.cs) already validates history, supports
search and computes earlier successful same-task baselines. Its strict failure
behavior is intentional: corrupt history must not silently become partial success.

Missing or deserving attention:

- A clearer scan pattern for time, outcome, duration and task identity.
- A deliberate detail interaction for full IDs, measurements and evidence links.
- Loading currently clears result items before reading completes. Preserving a
  clearly marked previous snapshot could reduce flicker, but a failed refresh
  must still be explicit and must not present stale totals as current.
- Aggregate statistics must remain distinct from the filtered list and
  task-specific performance baselines.
- Unknown or absent measurements need readable explanations, not invented zeros.
- Generic command reporting is not yet a unified producer integration for every tool.

**Video length and file size are not generally populated by these generic
commands.** Optional report fields can be displayed, but real tool producers
still need deliberate integration. Foreground/background fields measure window
activity, not CPU time or media-processing throughput.

## 5. Error tasks need a clearer operational surface

The startup helper stores actionable task histories and bounded retry outcomes;
the GUI exposes startup Retry and local logs/reports. That is not equivalent to a
fully developed task-management page with status, fix suggestion and history
presented together.

Focus on a coherent path from failure to:

1. What failed and which attempt produced the entry.
2. What the owner can do next and what will not be auto-installed.
3. Exact saved evidence.
4. An explicit retry, with one additional attempt after a failure.
5. A new linked entry if that retry cycle also fails twice.

An error report failing to save must itself be visible. Avoid recursive reporting
loops, automatic destructive repairs or indiscriminate re-execution of commands.

The development backlog in the companion schedule is separate from these
runtime error tasks and separate from Windows Task Scheduler.

## 6. Motion is implemented; usability validation remains

The latest behavior meets the requested first-visit/return model. It should not
grow into a larger animation project before layout is settled.

- First visits have ordered panel entrances, including initial Overview.
- Later visits use a lightweight page transition.
- Leaving, closing and disabling motion cancel pending panel effects.
- Return navigation resets the section's outer scroll; nested terminals remain
  independently scrollable.
- Work dispatch, settings saves and logging do not await animation completion.

Remaining focus: fast keyboard navigation during delayed entrances, repeated
navigation under load, long content and focused controls. Ensure decorative
opacity does not make an active keyboard target confusing. Reduced-motion
coverage is necessary but not a substitute for manual usability checks.

## 7. Scale, retention and lifecycle

History search and report aggregation read local saved files. Full logs are not
limited by the preview cap, and there is no automatic history pruning.

Measure before optimizing:

- Many run directories and large combined/error logs.
- Rapid query edits while a command completes.
- Report refresh and settings changes with substantial history.
- Rich-document rebuild costs and UI input responsiveness.
- Repeated window open/close and rapid navigation during entrances.

Use synthetic temporary fixtures, record dataset sizes and timing, then set
measurable budgets. No broad performance claim is supported by a small passing
fixture. If retention/export is added, make it explicit and recoverable; never
quietly delete old history or send it to a hosted service.

## 8. Accessibility and delivery gaps

- Native keyboard/focus/reduced-motion tests exist, but manual assistive-technology,
  high-contrast, touch and DPI/text-scaling checks are still needed.
- Clean Windows prerequisite/bootstrap validation is distinct from this shared
  developer machine; do not uninstall or replace its prerequisites to simulate it.
- Unsigned local EXE generation is not signing, distribution or release approval.
- Existing staged migration and prior uncommitted features must be separated
  before implementation commits can safely absorb them.
- Owner-machine launch acceptance and redesigned-layout review remain release
  conditions, not documentation tasks that can be marked verified.

The [open validation register](open-validations.md) remains relevant. This
assessment does not close its broader portal/tool items.

## Verification and confidence

Final checkpoint command:

```powershell
node --test --test-concurrency=1 .\src\tests\tooling\moorkyz-gui.test.cjs .\src\tests\tooling\moorkyz-exe.test.cjs .\src\tests\tooling\moorkyz-startup.test.cjs .\src\tests\tooling\moorkyz-full-logs.test.cjs .\src\tests\tooling\tools.moorkyz.com-cli.test.cjs
```

Observed result on 2026-10-28: **PASS, 24/24**, approximately 79 seconds.
Coverage includes native GUI compilation/windows, EXE/icon tests, startup retry
fixtures, full log capture and CLI compatibility. Headless startup tests
deliberately exercise the retained automation path; their passing console-menu
comparison is not evidence that an ordinary owner launch should show that menu.

This closure did not start a real managed service, change the owner's profile,
install prerequisites, run a clean-VM trial, perform a broad portal release gate,
or certify accessibility/security independently.

Related implementation audits:

- [GUI-first launch and summaries](../../../.audits/2026-10-28T04-53-56Z-gui-first-launch-and-always-visible-report-summaries.md)
- [Calmer motion and stable settings](../../../.audits/2026-10-28T04-57-14Z-calmer-gui-motion-and-stable-settings.md)
- [Section scroll reset and first entrances](../../../.audits/2026-10-28T05-05-44Z-section-scroll-reset-and-first-visit-panel-entrances.md)

## Recommended focus order

1. Close the owner's real launch discrepancy.
2. Approve a better hierarchy without discarding the style.
3. Implement the shared layout, then Logs & Errors and Reports reading flow.
4. Make failure recovery and runtime tasks clear in that layout.
5. Integrate actual tool measurements deliberately.
6. Measure scale and finish manual accessibility/delivery validation.

Do not add more decorative previews, new dependencies or automatic integrations
to compensate for unclear information architecture.
