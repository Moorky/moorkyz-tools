# Control center task schedule

Updated: 2026-10-28.

This is the next-work schedule for the local Windows control center, not a
Windows Task Scheduler installation or an automatically running agent queue.
Tasks are queued in the execution order below. There are no promised dates,
background jobs, service starts or external actions. Start a task deliberately,
verify its acceptance criteria, then update its status and audit.

See the [gap assessment](control-center-gap-assessment.md) for the reasoning,
layout direction, evidence and boundaries. The [root contract](../../../AGENTS.md)
and [current decisions](../decisions/decision-register.md) remain authoritative.
Historical SDK work is not part of this schedule.

## Status and priority

- **Scheduled**: recorded for the next implementation pass; not started.
- **Decision needed**: a specific design or authorization must be settled first.
- **Blocked**: cannot be completed without its named prerequisite.
- **Verified**: acceptance evidence is recorded, not merely code present.
- **P0**: user-blocking reliability; **P1**: main workflow/layout;
  **P2**: depth, scale and polish.

## Completed checkpoint

The current GUI iteration is verified locally:

- Calmer 320 ms transitions and 560 ms spring feedback; work is not delayed.
- No reload or entrance replay when selecting the current section.
- Search-limit changes preserve the live terminal document and selection.
- Every section returns to its outer scroll position zero.
- First visits stagger panel slide/fade starts by 90 ms, including initial load.
- Leaving, closing or disabling motion settles pending entrance animations.
- Local logs, error capture, reports, source navigation and explicit failures
  retain their regression coverage.

The final combined native/CLI regression run passed **24/24** tests. This is not
confirmation that the owner's previously failing installed launch path is fixed.
The [assessment](control-center-gap-assessment.md#verification-and-confidence)
records the exact command and limits.

## Ordered queue

| Order | ID    | Priority | Status          | Task                                                                       | Prerequisite                                                    |
| ----- | ----- | -------- | --------------- | -------------------------------------------------------------------------- | --------------------------------------------------------------- |
| 1     | CC-01 | P0       | Scheduled       | Diagnose the actual console-menu launch and make GUI entry predictable     | Owner's failing invocation/environment available                |
| 2     | CC-02 | P1       | Scheduled       | Produce and validate the layout hierarchy with the existing style          | None; can be designed while CC-01 awaits evidence               |
| 3     | CC-03 | P1       | Decision needed | Implement the chosen shell/Overview layout                                 | CC-02 layout review                                             |
| 4     | CC-04 | P1       | Scheduled       | Rework Logs & Errors density, settings placement and scrolling             | CC-03 shared layout                                             |
| 5     | CC-05 | P1       | Scheduled       | Make Reports comparison and detail navigation clearer                      | CC-03 shared layout                                             |
| 6     | CC-06 | P1       | Scheduled       | Surface startup error tasks and fix suggestions coherently                 | CC-01 diagnosis; CC-03 shared layout                            |
| 7     | CC-07 | P2       | Decision needed | Connect real tool-owned report measurements                                | Named tool, metadata contract and explicit scope agreed         |
| 8     | CC-08 | P2       | Scheduled       | Measure and improve large-history responsiveness                           | Representative isolated synthetic fixtures                      |
| 9     | CC-09 | P1       | Scheduled       | Validate accessibility, DPI, keyboard and motion across the revised layout | CC-03 through CC-06                                             |
| 10    | CC-10 | P1       | Blocked         | Finish integration/release evidence and isolate implementation commits     | Prior staged migration ownership resolved; affected checks pass |

Priority is not a dependency: CC-09's manual validation is important, but the
revised layout must exist first. Accessibility tests remain required during each
implementation, not postponed until CC-09.

## Acceptance criteria by task

### CC-01: Actual launch-path reliability

- Reproduce the exact failing `.\RUN.cmd` or installed `moorkyz start` path;
  record root, resolved command, headless-mode state, EXE identity and arguments.
- Check stale bindings, inherited `MOORKYZ_HEADLESS`, old binaries and checkout
  selection as hypotheses, not established causes.
- Ordinary desktop launch opens one GUI before checks and never presents the
  old text menu. Explicit headless automation stays supported and tested.
- A bootstrap/check failure is visible and actionable; Retry remains bounded.
- Add a regression for the observed cause. Do not silently edit profiles,
  delete binaries, change execution policy or kill unrelated processes.

### CC-02: Layout proposal and review

- Preserve the approved dark blue/violet palette, materials and restrained motion.
- Provide concrete wide/narrow layouts for Overview, Logs & Errors and Reports,
  including empty, running, failed and history-rich states.
- Make current project, current activity and the next useful action obvious.
- Separate routine operation from development commands and the interaction lab.
- Compare information hierarchy and scrolling, not just colors or screenshots.
- Ask for layout approval before a broad restructuring; no technology migration.

### CC-03: Shell and Overview

- Implement the agreed hierarchy using existing WPF controls and shared styles.
- Keep project selection and activity context compact and legible.
- Make the primary service action distinct from Stop and developer operations;
  no automatic start or destructive behavior.
- Remove misleading introductory copy: startup checks can run on launch, while
  services remain explicitly started. Keep explicit idle inspection accurate.
- Preserve task dispatch, startup gating, keyboard operation and page reuse.
- Verify agreed viewport/DPI cases without sacrificing readable text.

### CC-04: Logs & Errors

- Keep the shared page, independent pane controls, full local capture and searches.
- Prefer a compact activity header and contextual display settings over a large
  settings block preceding the useful output.
- Keep timestamp/severity/reference colors independent of performance accents.
- Choose and test live-tail versus user scroll behavior. Do not steal a user's
  reading position when they intentionally inspect older output.
- Section entry still scrolls to the top; nested terminal behavior is deliberate.
- Save failures remain explicit and restore prior valid settings.

### CC-05: Reports

- Keep the report list primary and Diagnostics/Statistics visible but subordinate.
- Present ID, UTC time, outcome and duration so multiple runs are easy to compare;
  expose remaining fields without forcing every row to become a paragraph wall.
- Keep optional media/file measurements honestly Unavailable until supplied.
- Retain search grammar, exact measurement validation and prior-only baselines.
- Keep the last valid view during refresh if implemented, but clearly mark stale,
  loading and failed states; never disguise partial results as a complete history.
- Maintain keyboard access and readable wrapping at the agreed widths.

### CC-06: Task/error recovery

- Show startup failed attempts, task identity, fix suggestion and retry outcome
  together, linked to exact saved evidence where available.
- Distinguish an operation report, a diagnostic entry and a retryable task.
- Each twice-failed retry cycle preserves a new linked history entry.
- Do not recursively retry reports, blindly rerun side effects or treat failure
  to save an error report as a successful operation.

### CC-07: Tool measurements

- Start with one explicitly selected tool and synthetic/local test inputs.
- Define producer-owned run IDs, UTC times, outcomes and measurement semantics.
- Supply actual video duration/file size only when the producer knows them;
  no arbitrary media probing or invented zero values.
- Keep GUI foreground/background timing distinct from worker/CPU processing time.
- No hosted processing, automatic services or additional dependencies without approval.

### CC-08: Scale

- Benchmark synthetic long logs, many run folders, overlapping search edits and
  refresh during completion; record fixture sizes and elapsed measurements.
- Set measurable budgets from observed baselines before declaring performance good.
- Profile rich-text rebuild and full-history report scanning before optimizing.
- Preserve cancellation, exact source references, strict validation and full disk logs.
- If retention is proposed, require explicit local archive/export decisions;
  no automatic deletion of existing history.

### CC-09: Accessibility and visual acceptance

- Exercise keyboard-only navigation, focus visibility, announced errors and tab order.
- Check Windows animation off, mid-sequence cancellation and rapid section changes.
- Review DPI/text scaling, supported viewport sizes, long paths/messages and
  high-contrast behavior; document failures rather than shrinking text to fit.
- Verify first-visit staggering never gates command execution or hides focused
  controls from a keyboard user. Adjust/cancel decorative motion if needed.
- Record automated evidence separately from manual assistive-technology findings.

### CC-10: Integration and handoff

- Reconcile the pre-existing staged migration explicitly; do not commit it incidentally.
- Run affected repository gates, native regressions and agreed manual checks.
- Verify the actual delivered EXE/RUN path and source/artifact identity.
- Record remaining limitations, rollback and a filled audit per implementation task.
- Make focused local commits only for owned changes; no push, signing, deployment,
  publishing or machine installation without separate approval.

## Schedule maintenance

Keep IDs stable. When starting work, name the task and update its status here.
Record actual evidence and the audit link on completion. Newly discovered
user-blocking defects may move ahead of layout work, but state why; do not
silently convert this queue into unattended execution.
