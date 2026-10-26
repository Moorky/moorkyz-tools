# Development Audit: Add Visory Bulk Downloader desktop tool

> Recorded from observed local verification. This is an agent self-audit.

| Field | Value |
|---|---|
| Audit ID | `2026-10-26T20-13-50Z-add-visory-bulk-downloader-desktop-tool` |
| Created (UTC) | `2026-10-26T20:13:50.121Z` |
| Task / issue | "New tool for the site": implement the *Visory Bulk Downloader — Portable Windows Edition* brief |
| Auditor | AI assistant using Copilot SDK in VS Code; self-audit |
| Branch / baseline commit | `master`, `73f3b705c981d139a0cb8e7fd70bd110b9f1eb28`; worktree contained another task's uncommitted catalog/footer work and new audits |
| Final commit | Report in task completion; do not edit this audit after committing |
| Scope reviewed | `tools/visory-bulk-downloader` (desktop source, tests, scripts, docs), its allowlist/provenance tooling, portal registry entry, source bundling, tests and docs |
| Outcome | `PASS WITH RISKS` — implemented and verified with synthetic data; real Visory behavior unverified; final-location desktop runtime checks blocked by Windows Application Control |

## 1. Request and outcome

- Requested behavior: a production-quality portable Windows app (C#/.NET 10 WPF, WebView2,
  BITS, HttpClient, SQLite, ZIP64, tray, optional Task Scheduler) that backs up the cloud
  recordings accessible from the user's authenticated Visory session, with exact logging,
  reports, archives, tests, docs and a portable EXE; plus a new tool on the site.
- Implemented behavior: `tools/visory-bulk-downloader` with Core (provider contract,
  Visory/Firebase observation parser, winsqlite3 state, hybrid BITS/HttpClient engine,
  logging contract, CSV/JSON reports, ZIP64 archives, coordinator, demo provider), the WPF
  hub (Dashboard, Recordings, Downloads, Archives, Reports, Settings; light/dark/high-contrast;
  English/German; tray; single instance; WebView2 sign-in/scan window), a console test runner
  with a loopback HTTP server, publish scripts and the five required documents.
  The portal adds `/tools/visory-bulk-downloader` with explanation, limitations, setup and
  allowlisted SHA-256-pinned source, stating **Not yet released**.
- Acceptance criteria and evidence: section 3. Demo workflow verified end to end in the GUI
  (36 synthetic recordings, verified 41-entry ZIP). 35/35 automated desktop scenarios and the
  4.2 GiB ZIP64 test passed on identical code before relocation.
- Intentional exclusions: real Visory sign-in or downloads (no authorized session), publishing,
  signing, pushing, scheduled-task creation, prerequisite installation, policy changes.
- Relevant decisions/contracts: root `AGENTS.md`; decision register "tool source ownership"
  (canonical source under `tools/`, no sibling repositories) and the new
  "2026-10-26: Visory Bulk Downloader" entry. The project was first created in a sibling
  folder and moved byte-for-byte into `tools/` after re-reading that decision; the sibling
  folder (never a Git repository) was deleted.

## 2. Change inventory

| Path | Change | Risk |
|---|---|---|
| `tools/visory-bulk-downloader/**` (70 files) | New desktop solution, tests, scripts, docs, notices, `SOURCE-PROVENANCE.json` | Medium: unverified against real Visory; unsigned binaries |
| `tools/visory-bulk-downloader.files.json` | Text-only source allowlist | Low |
| `scripts/sync-visory-source.mjs` | Provenance create/verify/update with path, link and UTF-8 checks | Low |
| `scripts/prepare-tool-sources.mjs` | Adds the bundle and verifies its provenance before packaging | Low |
| `.gitattributes` | `-text` for the tool, verbatim WPF notices | Low |
| `packages/portal/src/data/tools.ts` | Registry entry and allowlist import (committed in the `HEAD` schema; see 4) | Low |
| `tests/tooling/visory-source.test.js`, `tests/browser/visory.spec.js` | New provenance, EOL, page, download, search and axe tests | Low |
| `tests/browser/tools.spec.js` | Catalog card count 4 → 5 | Low |
| `docs/portal/modular-tools.md`, `docs/decisions/decision-register.md`, `README.md` | Tool documentation and decision record | Low |

## 3. Verification evidence

Desktop commands ran in the tool folder; portal commands in the repository root unless
"commit tree" (a temporary detached worktree containing exactly the committed tree).

| Check | Result | Exact command / method | Evidence / notes |
|---|---|---|---|
| Desktop build | PASS | `dotnet build VisoryBulkDownloader.sln -c Release` and `-c Debug` | 0 warnings, 0 errors (warnings are errors), before and after relocation |
| Desktop automated suite | PASS (pre-relocation) | `dotnet run --project tests\VisoryBulkDownloader.Tests -c Debug --no-build` | 35/35 executed, 1 opt-in skipped, 15.4 s; code unchanged afterwards (only Markdown) |
| 4.2 GiB ZIP64 | PASS | `VBD_HEAVY_TESTS=1`, filter `25b` | written, re-read and SHA-256 verified (10.7 s) |
| Desktop suite, Release binaries / canonical location | BLOCKED (environment) | same command, `-c Release`; and `-c Debug` in `tools/` | Application Control refused rebuilt test/Core DLLs (`0x800711C7`, Code Integrity 3077) before tests ran; no bypass attempted |
| Real BITS transfer | PASS | suite scenario S5 | BITS COM job against loopback server, HEAD probe, Complete, hash |
| GUI smoke (published single-file EXE) | PASS | Windows UI Automation + `PrintWindow` captures of the app window only | dark/German and light/English; demo scan → backup 36/36 → verified ZIP; reports/archives pages; clean exit; ~4 s cold start |
| GUI fixes re-verified | PASS | Debug build of the same source | WebView2 window loaded the official Visory login page (no sign-in); sign-out leaves 0 session folders after 25 s and after restart |
| Release package | PASS | `scripts\publish.ps1` (twice) | one EXE + docs + licenses + `SHA256SUMS.txt`; identical bytes across clean publishes |
| Final release EXE launch | BLOCKED (environment) | `Start-Process` | Application Control blocked the unsigned binary; no GUI claim for that exact file |
| Formatting / lint | PASS | `pnpm format:check`; `pnpm lint` | main checkout and commit tree |
| Typecheck | PASS | `pnpm typecheck` | main checkout and commit tree |
| Unit / integration tests | PASS with 1 unrelated failure | `node --test tests/tooling/visory-source.test.js tests/tooling/tools-catalog.test.js tests/tooling/mediarecover-snapshot.test.js`; commit tree `pnpm test:unit` | 16/16; commit tree 110/111 — only the existing CRLF-sensitive CI workflow regex test fails in a fresh Windows worktree (workflows untouched) |
| Repository checks | PASS with same unrelated failure | commit tree `pnpm check:repository` | 12/13; "tools are tracked by the parent repository" passes |
| Browser / accessibility | PASS with 1 flaky unrelated | `pnpm exec playwright test tests/browser/visory.spec.js` (`--repeat-each=2`, Chromium/Firefox/WebKit); commit tree: visory, tools, mediarecover specs | Visory 18/18; commit tree 32/33 — WebKit `tools.spec.js:229` (Directory Cleaner contrast) failed once and then passed 3/3 alone. Full `pnpm test:browser` NOT RUN (other task's in-progress specs) |
| Build / consumer fixture | PASS | `pnpm build` (main checkout and commit tree) | bundle `visory-bulk-downloader`: 70 files, provenance verified |
| Security / dependency review | PASS (scoped) | `pnpm check:source`; review; search for `AIza`, `phc_`, `apiKey` in the tool | no credential patterns; only new package `Microsoft.Web.WebView2 1.0.4314.35` in the desktop project; portal unchanged |
| Performance | PASS | suite scenario 32 | 100,000 recordings: merge 0.66 s, load 0.35 s, filter 16 ms |
| `git diff --check` | PASS | `git diff --cached --check HEAD` on the private index | clean |

## 4. Risk and quality review

- Public contract, feature-default, and compatibility impact: new route and registry entry
  only. The committed entry uses the `HEAD` schema (`monogram: "vb"`,
  `version: "0.1.0 development preview"`). The working tree keeps the same entry adapted to
  the other task's uncommitted schema (no monogram, `catalogDescription`, editorial
  `usefulness: 8`, display version `1.0.0`); that remains theirs to commit.
- Security, privacy, permissions, and secrets: no telemetry or uploads; tokens in memory;
  links DPAPI-protected; WebView2 profile kept in the Windows profile with verified deletion;
  redacted reports; HTTPS/host/path allowlists with per-hop redirect checks; no elevation.
- Accessibility, keyboard, and reduced motion: portal page passes axe in both themes and three
  engines; desktop uses named controls, live regions, Ctrl+1…6, reduced-motion handling and a
  high-contrast palette. Screen-reader and keyboard-only walkthroughs NOT RUN.
- Disabled-feature work/resource/network behavior: the portal page makes no request to Visory
  or other origins (tested). Desktop scheduling is off by default and never enabled in tests.
- Error handling, cleanup, and recovery: classified retries, safe Range resume, disk-full and
  network pauses, restart reconciliation, archive read-back verification; test fixtures,
  temporary worktrees, sibling folder and test sessions removed.
- Dependency, performance, and regression exposure: WebView2 NuGet approved by the brief's
  explicit stack and recorded in the decision register.

## 5. Findings and residual risks

| ID | Severity | Finding and evidence | Mitigation / owner | Status |
|---|---|---|---|---|
| VB-01 | High | Real Visory discovery, downloads and link renewal are unverified (no authorized session) | Validate with the user's account following `VISORY_INTEGRATION.md` | OPEN |
| VB-02 | High (environment) | Application Control blocks unsigned rebuilt DLLs/EXE (`0x800711C7`) | Run in an authorized environment or sign builds after approval; never weaken policy | OPEN |
| VB-03 | Medium | Scheduled scans, BITS continuation after exit/logoff, clean-VM and missing-WebView2 runs not executed | Manual validation on a test machine | OPEN |
| VB-04 | Low | WebKit `tools.spec.js:229` intermittent contrast failure on Directory Cleaner | Existing test owner | OPEN (unrelated) |

- Known gaps / checks not run: see VB-01–VB-03, full browser suite, high-contrast/screen-reader runtime.
- Required approvals: code signing, publishing a release and any scheduled-task enabling on real machines.
- Rollback or recovery: revert this commit; the tool folder is self-contained.
- Release readiness: `NOT READY` (development preview, unsigned, unverified against Visory).

## 6. Completion record

- Audit created before the task commit and included in that commit. The commit is built
  from a private index (HEAD + task paths) so concurrent uncommitted work stays untouched.
- Final commit hash: `PENDING` (reported in task completion).
- External actions: `NOT AUTHORIZED` — nothing pushed, published, signed or installed.

**Limitations:** An agent self-audit is not an independent security review, accessibility certification, or release approval.
