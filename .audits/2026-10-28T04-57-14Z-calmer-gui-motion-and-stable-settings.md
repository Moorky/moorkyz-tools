# Development Audit: Calmer GUI motion and stable settings

> Observed local evidence; scoped agent self-audit.

| Field                    | Value                                                                                          |
| ------------------------ | ---------------------------------------------------------------------------------------------- |
| Audit ID                 | `2026-10-28T04-57-14Z-calmer-gui-motion-and-stable-settings`                                   |
| Created (UTC)            | `2026-10-28T04:57:14.866Z`                                                                     |
| Task / issue             | Calmer GUI motion and stable settings                                                          |
| Auditor                  | Copilot SDK agent, Windows WPF native tests; self-audit                                        |
| Branch / baseline commit | master / a59794e; overlapping pre-existing staged migration and uncommitted GUI work preserved |
| Final commit             | Report in task completion; do not edit this audit after committing                             |
| Scope reviewed           | Native GUI timing, repeated navigation, search settings, reduced motion and regression tests   |
| Outcome                  | `PASS WITH RISKS`; focused commit blocked by overlapping earlier work                          |

## 1. Request and outcome

- Requested behavior: less abrupt visual animations and no unnecessary GUI reload; keep transactions/processing at full speed.
- Implemented behavior: transitions 180 -> 320 ms; spring feedback 420 -> 560 ms. Selecting the current page is a no-op, including no Reports reread. Search hit-limit changes no longer replace the live terminal document or selection. Identical terminal configuration is a no-op. Reduced motion also clears Reports animations.
- Acceptance criteria and evidence: native tests assert exact duration values, unchanged Reports status on repeated selection, document identity and selection preservation for each hit limit 1-10, animation completion, rapid-toggle behavior and immediate reduced-motion settlement. Real fixture commands still run through the same asynchronous path, without animation waits.
- Intentional exclusions: no command delay/throttling, arbitrary reload cache, services, prerequisites or launcher changes in this task. Timestamp changes must still reformat terminal history. Reports still refresh on entering from another page and via explicit Refresh. Earlier reported console-menu issue is not reproduced or repaired by these visual changes.
- Relevant decisions/contracts: [root contract](../AGENTS.md), [current decisions](../src/docs/decisions/decision-register.md), [GUI guide](../src/.moorkyz/gui/README.md).

## 2. Change inventory

| Path                                     | Change                                                                                 | Risk |
| ---------------------------------------- | -------------------------------------------------------------------------------------- | ---- |
| `src/.moorkyz/gui/Theme.xaml`            | Calmer visual transition/spring durations                                              | Low  |
| `src/.moorkyz/gui/MoorkyzGui.cs`         | Idempotent current-page selection; Reports reduced-motion cleanup                      | Low  |
| `src/.moorkyz/gui/TerminalSearch.cs`     | Skip unnecessary terminal render for hit-only/identical configuration                  | Low  |
| `src/tests/tooling/moorkyz-gui.test.ps1` | Timing, no-reload/settings preservation assertions; settle waits reflect new durations | Low  |
| `src/.moorkyz/gui/README.md`             | Explain visual-only timing and preserved content                                       | Low  |
| This audit                               | Task evidence and limitations                                                          | Low  |

## 3. Verification evidence

Use `PASS`, `FAIL`, `NOT RUN` (with reason), or `N/A`. Record exact commands and results.

| Check                        | Result | Exact command / method                                                                                          | Evidence / notes                                                                                            |
| ---------------------------- | ------ | --------------------------------------------------------------------------------------------------------------- | ----------------------------------------------------------------------------------------------------------- |
| Formatting                   | PASS   | `pnpm --dir src exec prettier --check .moorkyz\gui\README.md`                                                   | All matched files passed                                                                                    |
| Typecheck / native compile   | PASS   | Native test command below compiles actual C#/XAML                                                               | No dependencies installed                                                                                   |
| Unit / integration tests     | PASS   | `node --test --test-concurrency=1 .\src\tests\tooling\moorkyz-gui.test.cjs`                                     | Final run 4/4 passed, including native WPF suite (~25 seconds) after settings assertions                    |
| Browser                      | N/A    | Native-only changes                                                                                             | No browser code changed                                                                                     |
| Accessibility                | PASS   | Same native suite                                                                                               | Keyboard/focus/reduced-motion tests passed; not independent certification                                   |
| Build / consumer fixture     | PASS   | Same native suite                                                                                               | Actual native window and isolated command fixtures; EXE loads current GUI source, launcher binary unchanged |
| Security / dependency review | PASS   | Direct scoped diff review                                                                                       | No new dependencies, permissions, network or filesystem handling                                            |
| Performance                  | PASS   | Code inspection and native command fixtures                                                                     | No sleeps/waits added to production command or settings paths; only animation durations changed             |
| Whitespace                   | PASS   | `git -c core.safecrlf=false --no-pager diff --check -- src\.moorkyz\gui src\tests\tooling\moorkyz-gui.test.ps1` | No diagnostics                                                                                              |

## 4. Risk and quality review

- Public contract/default: visual timing is deliberately slower; actual actions still dispatch immediately. Active-page clicks no longer reread disk or replay entrance effects.
- Security/privacy/permissions/secrets: unchanged; no profiles/settings read outside isolated test roots.
- Accessibility/keyboard/reduced motion: reduced motion remains immediate, now also settles Reports transitions; existing keyboard/focus tests pass.
- Disabled-feature resources/network behavior: no watchers or services introduced.
- Error handling/cleanup/recovery: existing explicit settings save failure and rollback remain tested; no catch/default paths added.
- Dependency/performance/regression: same installed WPF/runtime stack, no dependency change. Hit-limit edits no longer rebuild rich terminal documents. Native cleanup remains bounded.

## 5. Findings and residual risks

| ID  | Severity | Finding and evidence                                                                     | Mitigation / owner                       | Status                     |
| --- | -------- | ---------------------------------------------------------------------------------------- | ---------------------------------------- | -------------------------- |
| R1  | Low      | Timestamp changes intentionally reformat live history; this is not a full window restart | Documented behavior                      | Accepted                   |
| R2  | Medium   | Current GUI files overlap prior uncommitted work and pre-existing staged migration       | Preserve index; do not stage baseline    | Commit blocked             |
| R3  | Low      | Earlier console-menu screenshot is separate from visual behavior                         | No claim of launcher repair in this task | Open diagnostic limitation |

- Known gaps/checks not run: no broad portal suite (unaffected); no independent accessibility/security certification. User's installed shell binding remains unverified.
- Required approvals: none for these local GUI changes.
- Rollback/recovery: revert only the task-specific duration, guards and settings-render changes, preserving earlier features/migration.
- Release readiness: `READY WITH CONDITIONS` for local use; no publishing authorization.

## 6. Completion record

- Audit created before the task commit and included in that commit, or reason not committed: generated using `node .\src\.moorkyz\scripts\tools.moorkyz.com.cjs audit "Calmer GUI motion and stable settings"`; filled after native tests. Overlapping pre-existing index/uncommitted GUI baseline prevents safe whole-path staging. No staging or commit performed.
- Final commit hash: `NOT CREATED - preserved overlapping prior work`
- External actions: `NOT AUTHORIZED` unless separately approved.

**Limitations:** An agent self-audit is not an independent security review, accessibility certification, or release approval.
