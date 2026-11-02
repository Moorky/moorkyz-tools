# Development Audit: Recover and integrate Moorkyz Folder Unlock

| Field | Value |
|---|---|
| Created UTC | 2026-11-02 |
| Baseline | `610fe5d`; substantial pre-existing staged and unstaged restructuring |
| Auditor | Copilot SDK in VS Code; scoped self-audit |
| Outcome | PASS WITH RISKS; module integrated, native elevated acceptance pending |

## 1. Request and outcome

The owner asked to evaluate the recovered Unlock utility, retain it only if
useful, and complete a properly namespaced standalone tools module.

The useful scope is identifying applications holding a working folder open and
optionally requesting normal closure after the user saves and confirms.
Registered `moorkyz-tools-folder-unlock`, slug/folder `folder-unlock`, display
name Moorkyz Folder Unlock. Uses the existing drop-in project contract and
generated catalog/source viewer, not a new local service or central registry.

The original prototype is preserved outside repos with the unknown-folder
recovery material. Its hard-coded personal target, automatic elevation/download/
EULA acceptance, permanent deletion, recursive ACL changes and forced process
termination were deliberately not carried into the integrated module. File
organization belongs to the existing recoverable Directory Cleaner.

## 2. Changes and preservation

- Added the standalone PowerShell entrypoint, explicit launcher, helper,
  documentation, first-party license, source allowlist and catalog metadata.
- Added disposable dual-host safety/behavior fixtures and repository test adapter.
- Regenerated the catalog and eight-file source JSON/ZIP with existing tools.
- Original source, Git metadata and staged state were copied and SHA-256 verified
  under the external `repo-recovery-20261102-1353` preservation directory.
- Existing restructuring, drafts, native GUI work and other staged paths are not
  part of this commit. No old documentation layout was blindly restored.

## 3. Verification

| Check | Result | Command / evidence |
|---|---|---|
| Two PowerShell hosts | PASS | `node --test src\tests\tooling\folder-unlock.test.cjs`; Windows PowerShell 5.1 and PowerShell 7, 32 assertions each. |
| Discovery/source regressions | PASS | Folder Unlock and `tool-index.test.js`: 15 tests, including exact allowlists and rejected unsafe inputs. |
| Portal build | PASS | `pnpm --dir src build`; Next.js standalone output generated with six tools. |
| Typecheck | PASS | `pnpm --dir src typecheck`. |
| Lint | PASS | `pnpm --dir src lint`. |
| Full unit suite | PASS with environment qualification | 160/160 tests. The session injects `NoDefaultCurrentDirectoryInExePath`; two existing tests invoke bare `RUN.cmd`. Retest used normal cmd lookup in the child PowerShell only; no persistent environment or product code changed. |
| Task-owned formatting | PASS | Prettier check of new adapter, manifests and generated index. |
| Whole-repository formatting | FAIL, pre-existing | `src/.template/README.md`, `src/tests/tooling/manual-housekeeping.test.cjs`, and `src/tests/tooling/repository-structure.test.cjs`; unrelated edits preserved. |
| Bounded source scan | PASS | `pnpm --dir src check:source`; no reported selected credential signatures or forbidden portal/cleaner sinks. |
| Live browser | PASS | Loopback production route `/tools/folder-unlock`, title, prerequisite/limitation text, accessible source selection and ZIP link inspected. |
| Actual download bytes | PASS | All eight live JSON entries exactly match canonical text files; JSON and ZIP return HTTP 200. |
| Elevated live Handle scan/closure | NOT RUN | This session is not elevated. No elevation, license acceptance or unrelated application closure was attempted. |

The original provided Handle executable has a valid Microsoft signature and
the current account already has an EULA acceptance record. Neither the binary
nor that account-specific state is bundled or committed. Microsoft's official
Handle documentation confirms the CSV options, elevation requirement and the
risks of raw handle closure; the latter operation is intentionally absent.

## 4. Quality and boundaries

Default behavior is inspection. Preview starts no scanner; normal closure needs
both confirmation and an exact PID phrase. A fresh scan plus process creation
time, current-user/session ownership and main-window eligibility are rechecked.
Protected processes and uncertain observations are not actionable. No force
termination, deletion, ACL mutation, background service or network client added.
Scanner failures/timeouts are explicit, not success-shaped "unlocked" results.

Tests use temporary folders and process doubles, never real user applications.
The source viewer does not execute recovered code. No new package dependency,
telemetry, permission grant, deployment or publication was introduced.

## 5. Completion and residual risks

This is an unpublished utility, not certified for elevated real-world integration.
Users must obtain Microsoft Handle and review its separate license themselves.
Full repository formatting and branch divergence belong to pre-existing work.
Keep that work and its index intact; commit only this audit and Folder Unlock
source, tests and generated catalog. Report the resulting hash at completion.
