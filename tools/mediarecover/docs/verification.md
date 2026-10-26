# Verification and release conditions

Run from `tools/mediarecover` in the tools repository on Windows using the
installed .NET 10 SDK.

```powershell
dotnet build .\Moorkyz.MediaRecover.sln -c Release --nologo
dotnet run --project .\tests\Moorkyz.MediaRecover.Tests -c Release --no-build
.\scripts\publish.ps1
.\scripts\smoke.ps1
```

The executable harness fails with nonzero exit for any assertion. It tests:
inclusive cutoff/unknown/conflicting/invalid dates; extension/category matching;
recursive Unicode/long paths/hidden/empty/exclusions; real fixture junction cycle;
nested destination exclusion; no-write source handles; same-name/different-byte
collisions and unknown target preservation; incremental hash skip; local
corruption/quarantine/repair; missing target and source-version changes; unknown
size/EOF/zero bytes; partial inventory; fake WPD disconnect/fresh read; access/space
errors; mutation; pause/resume/stop and durable run restart; promotion-before-DB
reconciliation; SQLite/settings reopen; malformed settings; legacy CSV/partials
preservation and traversal rejection; CSV quoting/formula safety; report counters;
verified ZIP and retained originals; actual WPD manager COM device enumeration.

The smoke script starts the actual app with isolated state, uses Windows UI
Automation on its own window, scans/recovers synthetic audio/video, verifies the
saved bytes, checks report paths, captures its window with PrintWindow and exits
the application. It must not capture the desktop or inspect personal media.

## Manual release gate (NOT certified by automated tests)

- Windows 10/11 clean x64 VM without .NET installed: extract whole ZIP, launch,
  run local backup and verify no prerequisite/network prompt.
- Samsung S26 and another Android device: unlocked/authorized/charging-only/locked,
  internal/SD, stale IDs, screen lock, cable reset, reconnect, large unknown sizes.
- Unreadable real ACL subtree, physical drive ejection, genuine disk exhaustion,
  antivirus locking, >4 GiB files and ZIP64 on sufficient isolated storage.
- Every tab and picker with keyboard only, Narrator, 125/150/200% DPI, resized
  window, high contrast, EN/DE, light/dark/system and reduced motion.
- Crash process at staging, verified staging, promoting-before-move and
  promoted-before-DB boundaries; verify durable metadata after actual reboot.

The current crash test injects persisted states; it is not a physical power-loss
test. Space/disconnect failures are provider injections, not actual device damage.
WPD manager enumeration does not certify the resource-stream implementation.
The self-audit records actual commands/results and all remaining gaps.

## Observed host policy blocker

The first 12-group suite passed. After subsequent additions the strict build still
passed, but Windows Application Control refused to load an unsigned rebuilt
provider DLL (`0x800711C7`); the final 13-group suite could not complete. The actual
self-contained WPF EXE was similarly blocked while loading the Core DLL. No app
window or screenshot was produced. No policy settings were changed, no signing
was performed, and no alternate loading mechanism was used to evade the block.
The smoke script is supplied for an authorized Windows test environment; its
described UI steps are **not claims of executed success**.
