# Moorkyz Folder Unlock

A standalone Windows folder-lock inspector with an optional, explicitly confirmed
**normal application close request**. It identifies file handles, not permission
problems, and never guarantees that a folder can be renamed, moved or deleted.
Windows PowerShell 5.1 and PowerShell 7 are supported.

## Why keep this tool?

It answers "which application still has this working folder open?" before a user
moves or archives it. Directory Cleaner handles recoverable file organization;
Folder Unlock does not duplicate that tool's cleanup operations.

The recovered Moorky-Unlock prototype had a hard-coded personal target, automatic
elevation/download/license acceptance, ambiguous scan failures, stale permission
markers, and optional permanent deletion. This version deliberately replaces those
behaviors. No permissions, ownership, file contents or stored completion state are
changed. There is no force-termination or raw handle-closing operation.

## Setup and inspection

1. Download [Microsoft Sysinternals Handle](https://learn.microsoft.com/en-us/sysinternals/downloads/handle)
   yourself and extract it outside this source folder. The executable is not bundled.
2. Read its license and run it separately to accept it if you agree. Microsoft
   requires administrative privileges for Handle; open an elevated PowerShell
   yourself. This tool neither elevates nor accepts a license on your behalf.
3. Use the executable matching Windows: `handle64.exe` (x64), `handle64a.exe`
   (ARM64), or `handle.exe` (x86). Its Microsoft signature is checked.
4. Choose an explicit, existing local working folder:

```powershell
.\Unlock-Folder.ps1 -Target 'C:\Work\example' -DryRun
.\Unlock-Folder.ps1 -Target 'C:\Work\example' -HandlePath 'C:\Tools\Handle\handle64.exe'
.\Unlock-Folder.ps1 -Target 'C:\Work\example' -HandlePath 'C:\Tools\Handle\handle64.exe' -Json -NonInteractive
```

`-DryRun` and `-WhatIf` validate the target but never start Handle or another
process. They need neither elevation nor an installed scanner.

## Optional normal close

Save work in the application first. Use a PID from the current scan:

```powershell
.\Unlock-Folder.ps1 -Target 'C:\Work\example' -HandlePath 'C:\Tools\Handle\handle64.exe' -CloseProcessId 12345
```

Confirm PowerShell's prompt, then type the exact `CLOSE 12345` phrase. The tool
rescans, verifies process creation time, ownership and session, and requests normal
closure of that one application's main window. An application can refuse, prompt
to save, or remain running. The tool does not click prompts or force an exit.
`-NonInteractive` forbids closure. A final scan reports remaining handles.

## Boundaries and troubleshooting

- Local, explicit folder paths only. No roots, UNC/device paths, wildcards,
  junctions, symlinks, profile roots or Windows/application installation folders.
- Process names are never used as termination selectors. System/shell processes,
  another user's processes, and applications without a main window are ineligible.
- The scanner times out after 30 seconds. Only the launched scanner is stopped.
- Missing prerequisites, access failures and unknown output are explicit errors,
  not "unlocked" results. A process disappearing during inspection is reported
  as unavailable and cannot be selected.
- No HTTP server, portal connection, downloads, telemetry, stored personal paths,
  ACL changes, deletion or bundled third-party executable.
- Scan results are a momentary observation. Locks can reappear. Save and close
  applications manually or restart Windows when a protected process is involved.
- `Run.cmd` forwards the same arguments; it does not change execution policy.

## Verification

From the tool directory:

```powershell
powershell.exe -NoProfile -File .\tests\Verify.ps1
pwsh -NoProfile -File .\tests\Verify.ps1
```

Fixtures cover path boundaries, CSV parsing, error reporting, preview behavior,
process-identity changes and normal-close behavior without touching user data or
stopping real applications. Actual elevated Handle integration must also be
checked by a user before treating this unpublished utility as a release.

First-party code follows the parent repository's Apache-2.0 license. Microsoft's
Handle executable and license are separate and are not redistributed here.
