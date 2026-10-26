# Development Audit: standalone Moorkyz Media Recover

| Field | Value |
|---|---|
| Audit ID | `2026-10-26T17-28-41Z-mediarecover-desktop` |
| Created (UTC) | `2026-10-26T17:28:41Z` (audit evidence collection; completed after final package verification) |
| Task | Implement the standalone Windows app from the 410-line full request |
| Auditor | Copilot coding agent, self-audit; not independent certification |
| Branch / baseline | N/A: sibling directory did not exist; no Git checkout or baseline |
| Scope | Only `moorkyz-tools-mediarecover`; parent owns portal integration |
| Outcome | **BLOCKED for complete runtime acceptance / release; compiled local deliverables prepared** |

## 1. Request and outcome

Requested a real offline WPF/MVVM Windows application, local and WPD providers,
durable safe transfers, integrity/retry/recovery, reports, settings, history,
legacy import, portable binaries, tests and documentation. The complete prompt
was read in bounded ranges, including continuation ranges where output truncated.
Parent repository AGENTS, current decisions, engineering workflow, testing,
coding/security/dependency policies and task-audit instructions were read.

Implemented:

- Seven buildable strict .NET 10 Windows projects; no NuGet package references.
- Real iterative recursive LocalFolder inventory; Unicode and long native paths,
  hidden toggle, case-insensitive extensions, pattern exclusions, link/junction
  blocking, nested output/state exclusions and exact partial-scan issues.
- Inclusive 2026-10-01 default, unknown dates included, conflicts included/flagged,
  raw metadata/provenance recorded; dates are honest filesystem/WPD fallbacks.
- Real WPD COM manager/content/properties/resource interop and read-only resource
  streams, owning STA threads, negotiated buffers, cancellation boundaries,
  fresh handles per retry; no Shell.CopyHere.
- OS winsqlite3 SQLite WAL/FULL ledger, persisted runs/files/inventory/issues,
  staged streaming, positive EOF and expected length checks, SHA-256, optional
  independent second source read, flush/no-overwrite same-volume promotion and
  promotion-before-DB crash reconciliation.
- Incremental trusted skip, stable collision suffixes, re-verification,
  corruption detection, preserved suspect originals and quarantine/provenance.
  No automatic deletion. No unsafe partial-offset resume.
- Bounded retry/backoff; final device/space/storage failure stops queue scheduling
  rather than hammering every remaining item. Explicit scan/resume is required.
- WPF Overview, Activity, History, Settings; real commands and native pickers,
  actual byte/speed messages, exact saved/report/archive paths, confirmation,
  pause/resume/stop, first-run explanation and Help. Visible taskbar continuation
  on explicit close choice; no tray or hidden process.
- Persisted preferences, EN/DE main labels, Light/Dark/System/high-contrast
  resources, no custom animation; technical statuses/logs remain English.
- CSV/TXT/JSON reports/manifest; local-baseline-only legacy import and receipts;
  optional no-compression ZIP with every-entry SHA-256 verification.
- Build/test/publish/smoke/package-verification/source-manifest scripts, Windows
  CI definition, MIT source license, actual runtime redistribution notices.

**Not full completion of the original acceptance criteria.** Native WPD hardware,
final runtime execution and manual accessibility are not certified; see findings.
The app is not undelete, sandbox bypass, forensic recovery, cloud recovery or a
media decoder. Local binary creation is not a public release.

## 2. Change inventory

All files in `source-files.json` are newly task-owned. No parent file was edited.
The parent began with an unrelated modified portal `next-env.d.ts`; it was left
untouched. No repository initialization, staging or commit was performed.

| Path | Change | Risk |
|---|---|---|
| `src/Moorkyz.MediaRecover.Core/` | Policy, coordinator, integrity and contracts | High: source/output trust and crash boundaries |
| `src/Moorkyz.MediaRecover.Providers.Local/` | Real read-only native/file inventory and streams | High: filesystem edge cases |
| `src/Moorkyz.MediaRecover.Providers.Wpd/` | Actual COM/resource provider | High: hardware interoperability not tested |
| `src/Moorkyz.MediaRecover.Storage/` | Native SQLite and atomic settings replacement | Medium: durable state |
| `src/Moorkyz.MediaRecover.Reporting/` | Reports, ZIP and non-destructive import | Medium: private paths, large files |
| `src/Moorkyz.MediaRecover.App/` | WPF/MVVM controls, labels, resources | Medium: runtime UI blocked by host policy |
| `tests/Moorkyz.MediaRecover.Tests/` | Dependency-free executable suite | Low: isolated synthetic fixtures only |
| `scripts/`, `.github/workflows/` | Local build/verification and unexecuted CI | Medium: packaging |
| README, docs, license/notices, allowlist, audit | Honest limitations and distributable source boundary | Low |
| `artifacts/` | Real generated EXE/runtime directory, ZIP, SHA256 and source manifest | Not distributable source snapshot inputs |

## 3. Verification evidence

Commands below ran from the sibling project root unless explicitly noted.

| Check | Result | Exact command / method | Observed evidence |
|---|---|---|---|
| Initial state | PASS | `Test-Path 'C:\Users\marvi\source\repos\moorkyz-tools-mediarecover'`; `dotnet --list-sdks` | Directory absent; SDKs 9.0.302 and 10.0.303 present |
| Initial compile | FAIL, fixed | `dotnet build .\Moorkyz.MediaRecover.sln -c Release --nologo` | Two viewmodel compile/nullability errors corrected |
| Initial focused suite | FAIL, fixed | `dotnet run --project .\tests\Moorkyz.MediaRecover.Tests -c Release --no-restore` | Deep-path native CreateFile lacked extended prefix; fixed and verified |
| Earlier complete suite | **PASS** | `dotnet run --project .\tests\Moorkyz.MediaRecover.Tests -c Release --no-restore` | All then-existing **12 groups** passed; genuine WPD manager returned 0 devices; no phone opened |
| Formatting | PASS | `dotnet format .\Moorkyz.MediaRecover.sln --no-restore --verbosity quiet`; `dotnet format .\Moorkyz.MediaRecover.sln --verify-no-changes --no-restore --verbosity quiet` | SDK formatter completed; verification clean |
| Final strict build | **PASS** | `.\scripts\build.ps1` | All seven projects built, 0 warnings and 0 errors |
| Final expanded suite | **FAIL — environmental policy block** | `dotnet run --project .\tests\Moorkyz.MediaRecover.Tests -c Release --no-build` | Date group passed, then Windows Application Control refused rebuilt Local provider DLL with `0x800711C7`. Current suite has 13 groups; no full final runtime PASS claimed |
| Real WPF launch/UI smoke | **FAIL — environmental policy block** | `powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\scripts\smoke.ps1` | Process exited `-532462766`; own `.NET Runtime` event identified `FileLoadException`/`0x800711C7` loading Core DLL. `ExecutionPolicy Bypass` applies only to this local PS test script, not Application Control; no application-policy bypass was attempted |
| Screenshot / UI Automation recovery | **NOT RUN** | Remaining steps of `scripts\smoke.ps1` | App could not load its window; no screenshot or GUI action success is claimed |
| Initial package licensing | FAIL, fixed | `.\scripts\publish.ps1` | WindowsDesktop pack uses `LICENSE` and omits notices. Accept license filename and retain upstream WPF v10.0.11 notices explicitly |
| Final self-contained publish | **PASS** | `.\scripts\publish.ps1` | .NET/WindowsDesktop 10.0.11 win-x64 included, trimming/single-file disabled; license/notices copied; 413 files |
| Binary ZIP integrity | **PASS** | `.\scripts\verify-package.ps1` | All 413 archive entry hashes match publish directory; self-contained runtime config and ZIP SHA256 verified |
| Source allowlist/hash | PASS | `.\scripts\source-manifest.ps1` | Explicit bounded UTF-8 file allowlist; per-file and deterministic source-tree SHA256 in `artifacts\source-manifest.json` |
| No network/dependencies/destructive product APIs | PASS, scoped static review | `rg 'HttpClient|TcpClient|Socket|WebRequest|Shell\.CopyHere|File\.Delete|Directory\.Delete|PackageReference' src --glob '*.{cs,csproj}'` | No matches in product source. OS APIs only; no telemetry/server or product cleanup path |
| Fixture/process cleanup | PASS | Inspect `.fixtures`; inspect only MediaRecover process name after launch failure | All synthetic fixtures removed; no app process remained |
| Real device, clean VM, >4 GiB, physical failures | NOT RUN | No authorized hardware/VM fixture | Fake failures and injected persisted states are not hardware/power-loss certification |
| Screen reader / DPI / keyboard / contrast runtime | NOT RUN | Host app policy prevented UI | Semantic native controls, names, focus and static resources implemented; no independent certification |
| Git diff / commit | N/A | `Test-Path .\.git` | False. No baseline and explicit prohibition on git init/commit |
| Portal checks / CI execution | N/A here / NOT RUN remotely | Parent-owned separate integration | No portal files touched; workflow prepared but not uploaded or run |

Final local binary artifacts:

- `artifacts\portable-win-x64\MoorkyzMediaRecover.exe` (extract/use entire directory).
- `artifacts\Moorkyz-Media-Recover-0.1.0-win-x64.zip`, **65,200,918 bytes**.
- ZIP SHA256:
  `b933d3f0efe4cc98423026f26b4563f858c0af72d84cf1f29819468e4a3f27b0`.
- `artifacts\Moorkyz-Media-Recover-0.1.0-win-x64.zip.sha256`.
- `artifacts\source-manifest.json` pins the source allowlist; no remote commit is invented.

The source audit was added after binary packaging; it does not alter binary code.

## 4. Risk and quality review

- **Contracts/defaults:** Product identity exact; standalone native app is independent
  of the portal. Date defaults, source read-only/no-links and unknown inclusion
  match the request. No website filesystem access or browser workspace is added.
- **Privacy/security:** No network code. Explicit UNC selection is normal Windows
  filesystem access. Source handles request read only, deny write sharing locally,
  and validate reparse attributes. Destination media promotion never overwrites.
  Explorer is launched by its OS absolute path, with argument separation. Reports
  stay local and contain private paths; diagnostics omit paths/IDs/raw errors.
- **Accessibility:** WPF controls provide keyboard/focus/UIA semantics and names;
  UI is resizable/scrollable, themes use dynamic resources, high contrast honored.
  No custom motion/fake progress/ETA. Runtime Narrator and DPI testing is pending.
- **Disabled behavior:** ZIP, second source read and throttling do no work while
  off. No tray, keep-awake, service, notifications, telemetry or device polling.
  Detection/scan/recovery are explicit actions.
- **Failure/recovery:** Failed and incomplete files never become successful final
  outputs. Native blocked driver calls cannot be forcibly canceled safely; managed
  waiting and scheduling are bounded where implemented, and native worker threads
  cannot keep an exited process alive. Quarantine and partials are retained.
- **Dependencies/performance:** No mandatory NuGet package or new native install.
  Normal SDK restore downloaded self-contained runtime packs to normal NuGet cache.
  Native interop uses installed OS APIs. Inventory is streamed/paged; source-key
  and known-target sets remain O(file count). Transfers are deliberately serial.

## 5. Findings and residual risks

| ID | Severity | Finding/evidence | Mitigation/owner | Status |
|---|---|---|---|---|
| MR-01 | High / blocker | Windows Application Control blocks unsigned rebuilt app/test assemblies (`0x800711C7`) | Run in authorized test environment or obtain policy approval; never weaken host controls or self-sign without authorization | OPEN |
| MR-02 | High / blocker | WPD manager exercised with zero devices only; COM resource streams not hardware-certified | Authorized S26/Android cable/lock/storage/reconnect tests before reliance/public release | OPEN |
| MR-03 | Medium | No automatic reconnect polling, PUID→new object-ID remapping, selective storage picker or incremental scan checkpoint | User reconnects/unlocks and explicitly rescans; stable PUIDs rematch, provisional versions do not skip | DOCUMENTED |
| MR-04 | Medium | >4 GiB/ZIP64, real ejection/disk-full/ACL, clean VM and reboot/power-loss not tested | Dedicated isolated fixtures/manual release gate | OPEN |
| MR-05 | Medium | Same-user malicious directory swapping can race ancestor checks; metadata-preserving source tampering can evade version checks | Trusted directories; no ACL bypass; use high assurance for an actual second source read | DOCUMENTED |
| MR-06 | Medium | No embedded capture-date parser or media decode validation | Honest fallback provenance; unknown/conflicts included; no playability claims | DOCUMENTED |
| MR-07 | Low–Medium | Main UI EN/DE, technical status/report strings English; manual accessibility not run | Clearly documented; complete localization and Narrator checks before claiming full acceptance | OPEN |
| MR-08 | Low–Medium | No cross-identity content dedupe, legacy-to-source automatic trust reconciliation, report/archive overrides, tray, parallelism or keep-awake | Non-destructive source-key backup; legacy remains untrusted; features honestly deferred | DOCUMENTED |
| MR-09 | Medium | Latest queue/assurance/provisional metadata additions compile but expanded suite cannot execute past host policy block | Re-run full 13 groups on authorized host; earlier 12-group PASS is not represented as final acceptance | OPEN |

- **Required approvals:** Authorized runtime/hardware environment; any future
  signing, publication, release upload, remote creation or new dependency.
- **Rollback:** Preserve any later user backups/state; remove only this new source
  directory/generated artifacts if abandoning the task. Do not delete quarantine,
  imported originals or normal NuGet/shared SDK caches.
- **Release readiness:** **NOT READY for a certified/public release**. Real local
  self-contained package and complete buildable sources exist; runtime acceptance
  remains blocked and original broad scope has documented deferred features.

## 6. Completion record

- Audit follows `docs\AUDIT_TEMPLATE.md` and the loaded task-audit workflow.
- No Git initialization, commit, remote, push, public upload, deployment or signing.
- No system prerequisites installed or security settings changed.
- No unrelated checkout files modified or personal media inspected.
- Source snapshot provenance is an uncommitted local implementation, not a
  fabricated remote revision. Parent independently integrates the portal.

**Limitations:** Self-audit is not an independent security review, accessibility
certification, hardware certification or release approval.
