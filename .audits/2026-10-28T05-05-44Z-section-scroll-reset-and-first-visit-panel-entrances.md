# Development Audit: Section scroll reset and first visit panel entrances

> Observed local evidence; agent self-audit.

| Field                    | Value                                                                                      |
| ------------------------ | ------------------------------------------------------------------------------------------ |
| Audit ID                 | `2026-10-28T05-05-44Z-section-scroll-reset-and-first-visit-panel-entrances`                |
| Created (UTC)            | `2026-10-28T05:05:44.952Z`                                                                 |
| Task / issue             | Section scroll reset and first visit panel entrances                                       |
| Auditor                  | Copilot SDK agent; native Windows WPF tests; self-audit                                    |
| Branch / baseline commit | master / a59794e; 26 pre-existing staged migration entries preserved                       |
| Final commit             | Report in task completion; do not edit this audit after committing                         |
| Scope reviewed           | Section scroll reset, staggered initial panel entrances, cleanup, reduced motion and tests |
| Outcome                  | `PASS WITH RISKS`; focused commit blocked by overlapping earlier uncommitted GUI baseline  |

## 1. Request and outcome

- Requested behavior: always return to the top of a section; slide in panel subsections sequentially on first visit, with lighter later navigation.
- Implemented behavior: each section resets its outer scroll position on entry. Per-window visited state enables 90 ms staggered slide/fade starts for the first visit, including the initially loaded page. Panels and individual action/material cards share the 320 ms entrance duration. Subsequent visits retain the lightweight page transition. Leaving a section, disabling motion, or closing settles pending panel animations. No production sleeps or command delays.
- Acceptance criteria/evidence: native tests actually scroll all four sections, leave/return and verify offset zero; verify no panel sequence on return; verify initial Overview includes its project panel and ten action cards; verify first Reports panels start sequentially and reduced motion settles even delayed panels. Existing asynchronous command/settings/error tests still pass.
- Intentional exclusions: inner terminal/search scroll remains independent; current-tab selection remains a no-op. No random animation order, reload, dependency change or launcher modification.
- Relevant decisions/contracts: [root contract](../AGENTS.md), [current decisions](../src/docs/decisions/decision-register.md), [GUI guide](../src/.moorkyz/gui/README.md).

## 2. Change inventory

| Path                                     | Change                                                                                                              | Risk                         |
| ---------------------------------------- | ------------------------------------------------------------------------------------------------------------------- | ---------------------------- |
| `src/.moorkyz/gui/MoorkyzGui.cs`         | Per-window visited sections, outer scroll reset, recursive panel discovery, keyframed stagger and lifecycle cleanup | Low/medium: visual lifecycle |
| `src/tests/tooling/moorkyz-gui.test.ps1` | Actual scrolling, first-page/panel order, no-repeat and reduced-motion assertions                                   | Low                          |
| `src/.moorkyz/gui/README.md`             | Document first-visit/later-visit behavior                                                                           | Low                          |
| This audit                               | Verification evidence and limitations                                                                               | Low                          |

## 3. Verification evidence

Use `PASS`, `FAIL`, `NOT RUN` (with reason), or `N/A`. Record exact commands and results.

| Check                        | Result | Exact command / method                                                                                          | Evidence / notes                                                                                         |
| ---------------------------- | ------ | --------------------------------------------------------------------------------------------------------------- | -------------------------------------------------------------------------------------------------------- |
| Formatting                   | PASS   | `pnpm --dir src exec prettier --check .moorkyz\gui\README.md`                                                   | All matched files passed                                                                                 |
| Typecheck / native compile   | PASS   | Native test command below compiles actual C#/XAML                                                               | Installed WPF compiler, no added packages                                                                |
| Unit / integration tests     | PASS   | `node --test --test-concurrency=1 .\src\tests\tooling\moorkyz-gui.test.cjs`                                     | Final run 4/4 passed, native suite about 27 seconds                                                      |
| Browser                      | N/A    | Native-only changes                                                                                             | No browser code modified                                                                                 |
| Accessibility                | PASS   | Same native suite                                                                                               | Keyboard/focus/reduced-motion coverage retained; no independent certification                            |
| Build / consumer fixture     | PASS   | Same native suite                                                                                               | Actual native windows, isolated command roots; EXE loads current GUI source so launcher binary unchanged |
| Security / dependency review | PASS   | Direct scoped implementation review                                                                             | No new I/O, network, dependencies or permissions                                                         |
| Performance                  | PASS   | Direct code review and native command fixtures                                                                  | Visual clocks only; no waits in commands/settings/search. Panel sequence runs independently of work      |
| Whitespace                   | PASS   | `git -c core.safecrlf=false --no-pager diff --check -- src\.moorkyz\gui src\tests\tooling\moorkyz-gui.test.ps1` | No diagnostics                                                                                           |

## 4. Risk and quality review

- Public/default compatibility: returning sections deliberately lose their prior outer scroll offset; controls and data are reused. First-visit state is per window, not saved.
- Security/privacy/permissions/secrets: no new data handling or permissions; fixtures use isolated temporary roots.
- Accessibility/keyboard/reduced motion: controls retain existing focus and keyboard semantics; disabled motion displays all panels immediately and cancels pending clocks.
- Disabled-feature resources/network behavior: no timer loop, watcher, service or connection added; WPF clocks animate visual properties only.
- Error handling/cleanup/recovery: no new exception-swallowing paths. Leaving/closing cancels panel opacity/translation clocks; reduced-motion cancellation restores base properties.
- Dependency/performance/regression: same installed stack; panel discovery stops at panel boundaries rather than animating every nested label/control. Existing preset/button transforms reused.

## 5. Findings and residual risks

| ID  | Severity | Finding and evidence                                                                        | Mitigation / owner                                        | Status         |
| --- | -------- | ------------------------------------------------------------------------------------------- | --------------------------------------------------------- | -------------- |
| R1  | Low      | Staggered visual first visits intentionally take longer than a single transition            | Processing remains independent; reduced motion available  | Accepted       |
| R2  | Medium   | GUI implementation/tests overlap earlier uncommitted features and staged migration baseline | Preserve index and baseline; no unsafe whole-path staging | Commit blocked |

- Known gaps/checks not run: no broad portal suite (unaffected); no independent security/accessibility certification. Earlier console-menu screenshot is outside this visual task.
- Required approvals: none for local visual changes.
- Rollback/recovery: revert only this task's entrance/visited state and scroll logic with matching docs/tests; preserve prior work.
- Release readiness: `READY WITH CONDITIONS` for local use, no publishing approval.

## 6. Completion record

- Audit created before the task commit and included in that commit, or reason not committed: generated using `node .\src\.moorkyz\scripts\tools.moorkyz.com.cjs audit "Section scroll reset and first visit panel entrances"`; filled after native tests. Existing staged migration and prior uncommitted GUI baseline overlap this task. Nothing staged or committed.
- Final commit hash: `NOT CREATED - preserved overlapping prior work`
- External actions: `NOT AUTHORIZED` unless separately approved.

**Limitations:** An agent self-audit is not an independent security review, accessibility certification, or release approval.
