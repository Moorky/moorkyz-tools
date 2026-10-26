# Development Audit: Moorkyz Media Recover desktop preparation and portal integration

| Field | Value |
|---|---|
| Audit ID | `2026-10-26T17-43-32Z-mediarecover-integration` |
| Created (UTC) | `2026-10-26T17:43:32Z` |
| Task / issue | User-supplied Moorkyz Media Recover full implementation prompt |
| Auditor | Copilot agent self-audit; desktop implementation agent evidence plus directly executed portal/build/package checks |
| Branch / baseline commit | `master`; initial `75ebf05`; isolated task-only acceptance based on `be3a313` |
| Final commit | Focused unreleased-preview/source integration commit prepared after isolated acceptance; report hash in completion, not by editing this audit after commit |
| Scope reviewed | Independent desktop project, curated source distribution, mediarecover catalog/detail/search, tests and documentation |
| Outcome | `BLOCKED` for full desktop delivery; task-focused portal acceptance `PASS`; final broad rerun exposes an existing Directory Cleaner/WebKit contrast failure |

## 1. Request and outcome

- Requested: a real local-first Windows desktop application named **Moorkyz Media
  Recover**, independently maintained as **Moorky/moorkyz-tools-mediarecover**, and
  an explanation-first portal page at `/tools/mediarecover`.
- Prepared the independent project at `C:\Users\marvi\source\repos\moorkyz-tools-mediarecover`.
  It was not initialized as a Git repository, pushed, signed or published.
- Desktop implementation includes local recursion, actual read-only WPD COM
  resource-stream code, SQLite state, staged no-overwrite copying, SHA-256,
  independent second-read option, retry, quarantine, crash reconciliation,
  reports, legacy import, optional verified ZIP and WPF/MVVM screens/settings.
  These are implementation claims, not a successful final runtime acceptance.
- A seven-project Release build and self-contained portable package passed.
  Earlier desktop tests passed 12 groups. Subsequent hardening was compiled and
  packaged, but the final 13-group test run and GUI launch were rejected by
  Windows Application Control, error `0x800711C7`. There is no desktop screenshot.
- Portal implementation preserves Description -> Tool -> Use Cases -> Setup,
  adds workflow/features/limits inside Description and honest Downloads after
  Setup, and has no executable workspace or native service for this tool.
- Source distribution is 48 original text inputs plus generated provenance.
  Per-file hashes and deterministic tree SHA-256 pin the exact local snapshot;
  no remote commit or public release is invented.
- Relevant authority: root AGENTS, current decisions, modular-tools guide,
  engineering/testing/security policies and the user's supplied prompt.

## 2. Change inventory

| Path | Change | Risk |
|---|---|---|
| `.gitattributes` | Preserve pinned snapshot bytes without EOL conversion; keep the exact upstream WPF notice whitespace | Cross-platform provenance must survive Git storage/checkout |
| `packages/portal/src/data/tools.ts` | Media Recover metadata, workflow, limits, source allowlist, unpublished downloads | Honest capability and release labeling |
| `packages/portal/src/app/tools/[slug]/page.tsx` | Optional explanation groups, source provenance notice and Downloads | Shared layout compatibility |
| `packages/portal/src/app/tools.css` | New feature-list spacing and workflow subheadings only | Preserve unrelated concurrent card styling |
| `packages/portal/src/components/portal-search.tsx` | Match route slugs as well as titles and summaries | Local-only discoverability |
| `tools/mediarecover.files.json`, `tools/mediarecover/` | Explicit buildable UTF-8 source snapshot with provenance | No binaries, recordings, state or caches |
| `scripts/sync-mediarecover-source.mjs` | Deliberate snapshot refresh, path/link/text checks and hashes | Never execute source; reject linked outputs |
| `scripts/prepare-tool-sources.mjs` | Verify pinned bytes before bundling | Fail explicitly on drift |
| `scripts/verify-source.mjs` | Include C#/XAML/project source in bounded credential scan | Not a comprehensive security audit |
| `tests/browser/mediarecover.spec.js`, related catalog tests | Workflow/order, real source downloads, search, no native requests, themes/responsive/axe | Three browser engines |
| `tests/tooling/mediarecover-snapshot.test.js` | Tamper/path/binary/link rejection and deterministic provenance | Isolated temporary fixtures |
| README, decisions, modular-tools guide | Identity, independent ownership, source refresh and runtime blocker | No deployment claims |
| Sibling standalone directory | Desktop source, tests, CI, package, documentation and its self-audit | Outside portal Git; runtime acceptance blocked |

## 3. Verification evidence

Commands below were executed from this repository unless otherwise stated.

| Check | Result | Exact command / method | Evidence / notes |
|---|---|---|---|
| Initial focused regression | PASS | `pnpm exec playwright test tools.spec.js compact-tools.spec.js --project=chromium --workers=1` | 8 passed before later concurrent branding/card changes |
| Snapshot import | PASS | `node scripts\sync-mediarecover-source.mjs C:\Users\marvi\source\repos\moorkyz-tools-mediarecover` | 48 original text files plus provenance; tree `1ecf338fe65df3383e7e3f8428d1b3c7b9a96646dc76c753b35cdfb83f2733c3` |
| Focused unit/source bundle tests | PASS | `node --test tests\tooling\mediarecover-snapshot.test.js tests\tooling\tools-catalog.test.js` | 9 passed, including exact JSON/ZIP bytes and original file membership |
| Production portal build | PASS | `pnpm build` | All 3 source bundles prepared; Media Recover 49 entries, 244723 ZIP bytes; Next.js build/type phase passed |
| Lint | PASS | `pnpm lint` | No lint failures |
| Formatting | PASS | `pnpm format:check` | All configured files passed; final browser test also checked with `pnpm exec prettier --check tests\browser\mediarecover.spec.js` |
| Strict types | PASS | `pnpm typecheck` | `tsc --noEmit` passed |
| Source checks | PASS | `pnpm check:source` | Final run checked 313 paths; bounded credential/sink scan, not independent security certification |
| Complete configured gate | FAIL | `pnpm check` | Repository checks 12 passed, formatting/lint/build and unit tests 103 passed; browser phase 37 passed / 14 failed, so chained final type/source steps did not run; these were run separately |
| Complete gate failure analysis | FAIL, investigated | Targeted reads of failing test output | Concurrent branding artwork threshold, catalog footer wording and homepage-title expectations failed. Initial new-test title expectations and theme synchronization were corrected; final focused tests below passed. Full mixed-worktree gate was not rerun or represented as green |
| Final focused portal browser acceptance | PASS | `pnpm exec playwright test mediarecover.spec.js --workers=1` | 9 passed across Chromium/Firefox/WebKit; workflow/order, source Copy/Download controls and ZIP, search, exact theme colors, axe, 320/390/1440px, no native/external startup requests |
| Browser visual evidence | PASS | Test-generated `mediarecover-tool-page-light.png` and `mediarecover-tool-page-dark.png` under each engine's test-results folder | Portal screenshots only, not WPF screenshots |
| Independent snapshot build | PASS | `dotnet build .\tools\mediarecover\Moorkyz.MediaRecover.sln -c Release --artifacts-path C:\Users\marvi\.copilot\session-state\6ec39897-cef9-4778-8d80-e94e89212421\files\mediarecover-snapshot-build` | 7 projects; 0 warnings/errors; no sibling-project dependency. Temporary compilation outputs removed afterward |
| Standalone build, delegated evidence | PASS | `.\scripts\build.ps1` from sibling project | Final source compiled with 0 warnings/errors |
| Standalone formatting, delegated evidence | PASS | `dotnet format .\Moorkyz.MediaRecover.sln --verify-no-changes --no-restore --verbosity quiet` from sibling | Final formatting verification passed |
| Earlier desktop suite, delegated evidence | PASS, earlier revision only | `dotnet run --project .\tests\Moorkyz.MediaRecover.Tests -c Release --no-restore` from sibling | 12 groups; dates, recursive local safety, transfer/integrity/collision, unknown size, retry, mutation, pause/cancel, crash state, SQLite/settings, migration, reports/ZIP, WPD manager enumeration with zero devices |
| Final expanded desktop suite, delegated evidence | FAIL, host policy | `dotnet run --project .\tests\Moorkyz.MediaRecover.Tests -c Release --no-build` from sibling | First date group passed; provider DLL load blocked by `0x800711C7`; 13-group suite incomplete |
| WPF launch, delegated evidence | FAIL, host policy | `powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\scripts\smoke.ps1` from sibling | Script launched the EXE with `--smoke` and isolated fixture state; Core assembly rejected before a window appeared. This attempt preceded final packaging; the final ZIP was not launch-tested |
| Portable publish, delegated evidence | PASS | `.\scripts\publish.ps1` from sibling | Final executable changes built/published; only audit/source allowlist added afterward |
| Portable package independent hash verification | PASS | `& C:\Users\marvi\source\repos\moorkyz-tools-mediarecover\scripts\verify-package.ps1` | All 413 ZIP entry hashes and self-contained runtime metadata passed |
| Portable ZIP digest | PASS | `Get-FileHash -Algorithm SHA256 C:\Users\marvi\source\repos\moorkyz-tools-mediarecover\artifacts\Moorkyz-Media-Recover-0.1.0-win-x64.zip` | `b933d3f0efe4cc98423026f26b4563f858c0af72d84cf1f29819468e4a3f27b0` |
| Hardware/manual acceptance | NOT RUN | Real phone transfers, clean VM, physical drive/USB failures, >4 GiB, manual accessibility/High DPI | No authorized fixture hardware; host policy blocks current executable acceptance |
| Dependency changes | N/A | No new mandatory third-party package/native dependencies added | Existing SDK and built-in Windows SQLite/WPD APIs; no prerequisite installation |
| Whitespace | PASS | `git diff --check` | No whitespace errors; Git emitted existing LF/CRLF notices |
| Isolated dependency setup | PASS | `pnpm install --offline --frozen-lockfile` in the temporary task-only worktree | Needed after shared dependency junctions correctly failed workspace verification; 126 cached packages, zero downloads, lockfile/supply-chain policies passed; no shared installation modified |
| Complete isolated configured gate | PASS | `$env:TOOLS_MOORKYZ_COM_PORTAL_PORT = '4297'; pnpm check` from `C:\Users\marvi\.copilot\session-state\6ec39897-cef9-4778-8d80-e94e89212421\files\mediarecover-acceptance` | Repository checks 12/12, unit tests 103/103, browser tests 45/45 (Chromium/Firefox/WebKit), build, lint, formatting, types and source scan all passed |
| Snapshot Git-byte regression | PASS | `node --test tests\tooling\mediarecover-snapshot.test.js` | 4/4 including filtered/unfiltered Git object hashes for every pinned file |
| Isolated full rerun after byte-preservation fix | FAIL, existing unrelated UI | Same isolated `pnpm check` command | Repository checks 12/12, unit tests 104/104, lint/format/build passed; browser tests 44/45 passed. The broad existing WebKit accessibility test reported dark-theme Directory Cleaner rule-label contrast (`#201b2b` on `#14101d`), not Media Recover content |
| Existing failure smallest recheck | FAIL, reproduced | `pnpm exec playwright test tools.spec.js --project=webkit --grep 'remain accessible'` with port 4297 in isolated worktree | Same Directory Cleaner contrast failure. No unrelated cleaner implementation, contrast threshold or axe exclusion changed |
| Final isolated types/source | PASS | `pnpm typecheck` and `pnpm check:source` after the failed broad rerun | Both separately completed successfully |
| Isolated acceptance materialization | PASS | Detached worktree at `be3a313`, selected task-only Git patch, explicit source allowlist copy | Concurrent card fields, card CSS, catalog tests and card documentation excluded. Canonical Git bytes exported with `git -c core.autocrlf=false -c core.eol=lf archive` to avoid an existing CRLF-sensitive CI-workflow test; tests themselves unchanged |
| Commit | PREPARED | Selected task-only diff hunks and explicit new source paths | Unrelated working-tree changes remain unstaged; desktop runtime blocker is documented, not presented as resolved |

An initial Playwright invocation with backslash-separated test filters found no
tests; it was replaced by filename regex filters. Initial focused theme checks
were tightened to wait for actual computed colors before axe; no axe rules,
contrast thresholds or accessibility checks were disabled.
The isolated `pnpm check` initially stopped at a pre-existing test requiring an LF
in the CI workflow, because Windows checkout conversion produced CRLF. Restoring
canonical Git blob bytes fixed materialization; no repository line-ending policy,
test expression or global Git setting was changed.

## 4. Risk and quality review

- Public contract: one new desktop-only catalog route; existing workspace
  branches, native connection defaults and CSP remain unchanged.
- Privacy: source browsing is lazy, same-origin and text-only. No browser device
  scanning, account, upload, hosted processing, telemetry or HTTP service added.
- Data safety: standalone implementation uses isolated tests, source read-only
  handles, staging, no-overwrite promotion, preservation/quarantine and ledger
  reconciliation. Final post-hardening behavior is compiled but not accepted at
  runtime; important user media must not be used to validate it.
- Accessibility: final portal-focused automated checks passed in three engines,
  both themes and narrow layouts. This does not certify the WPF UI or unrelated
  concurrent branding changes.
- Error handling: source drift and malformed manifests fail explicitly. Desktop
  runtime policy errors and incomplete feature/hardware acceptance are exposed
  in the page, guide, desktop README and audit.
- Source ownership: edit the independent application and refresh the pinned
  snapshot; builds do not require the sibling app or process to exist.
- Cleanup: test fixtures clean themselves; temporary snapshot compilation
  directory was removed. Real portable artifacts were retained intentionally.
- Worktree: initial unrelated next-env change was never staged or overwritten.
  Concurrent repository refactoring commits and branding/card work were preserved.
  No unrelated source or asset edits were reverted or included in a task commit.
- Follow-up acceptance isolated the task's exact commit scope rather than treating
  unrelated mixed-worktree failures as permanent blockers. The entire configured
  gate passed there; only selected task-owned hunks are eligible for staging.
- Staging inspection found a real distribution bug: Git's inherited Markdown
  line-ending normalization would change originally pinned CRLF bytes on a fresh
  checkout. Snapshot-specific attributes now prohibit conversion, and a regression
  test compares Git-filtered and unfiltered object hashes for all 49 entries.
  Snapshot whitespace attributes explicitly permit original CRLF line endings
  while retaining Git's normal trailing-space and space-before-tab checks.
  The upstream WPF legal notice is intentionally byte-preserved, including two
  existing trailing-whitespace lines; its exact path alone disables Git whitespace
  warnings. First-party code whitespace checks remain enabled.

## 5. Findings and residual risks

| ID | Severity | Finding and evidence | Mitigation / owner | Status |
|---|---|---|---|---|
| MR-1 | High delivery blocker | Windows Application Control rejects unsigned app/provider assemblies; final desktop tests and GUI cannot run | Maintainer must use an authorized execution/signing environment; do not weaken machine policy | Open |
| MR-2 | High validation gap | Genuine WPD resource-stream implementation is not validated against a phone; manager enumeration saw zero devices | Authorized synthetic/device acceptance before release | Open |
| MR-3 | Medium scope gap | Automatic reconnect/PUID remapping, embedded capture dates, decode probing, cross-identity deduplication, tray/notifications/keep-awake, report-directory overrides, parallelism and full technical-message translation deferred | Candid desktop README and portal limitations; no production claim | Open |
| MR-4 | Medium integration risk | Mixed-worktree overlap resolved by isolation; a later broad rerun exposed existing Directory Cleaner/WebKit dark-theme contrast, reproduced in the smallest selector | Preserve unrelated cleaner code; report broad gate failure without weakening tests. Task-specific 9-browser and source tests pass | Existing unrelated UI issue open |
| MR-5 | Medium integrity limit | A saved hash is not proof of source playability/authenticity; MTP per-file retry is not byte-range resume | Explicit assurance labels and page limitations | Documented |

- Required approvals: no remote creation/push/release/deployment/signing/system
  policy change is authorized. Hardware and clean-environment acceptance remain.
- Rollback: remove only Media Recover registry/explanation/bundle additions, not
  concurrent branding/card work. Retain standalone source and portable output.
  Never remove user media, reports, ledger or quarantine as portal rollback.
- Release readiness: **NOT READY**. Source/package preparation and focused portal
  acceptance are not equivalent to the user's full desktop acceptance criteria.

## 6. Completion record

- This audit is included in the focused unreleased-preview/source integration
  commit after task-specific acceptance, one complete isolated gate pass, and
  transparent reporting of the later unrelated Directory Cleaner/WebKit failure.
  This is not a fully green final broad-gate, desktop release-readiness or
  successful runtime claim.
- Final commit hash: report in task completion; do not edit audit after commit.
- External actions: `NOT AUTHORIZED / NOT PERFORMED`.

**Limitations:** An agent self-audit is not independent security review,
accessibility certification, release approval or evidence of a successful final
desktop runtime.
