# Moorkyz Media Recover

An offline Windows desktop app for safely copying **readable** video/audio from
local folders and Windows Portable Devices (Android USB/MTP). Product repository:
`Moorky/moorkyz-tools`, canonical directory `tools/mediarecover`.
Version **0.1.0, unpublished local build**.

This is not undelete, encryption bypass, forensic extraction, or a guarantee that
a recording is playable. No accounts, network calls, telemetry, local HTTP server,
auto-update, service, administrative privilege, or mandatory third-party package.
The portal at `https://tools.moorkyz.com/tools/mediarecover` is maintained in the
same repository. The desktop application remains independently usable.

## Start

Windows 10/11 x64 with normal WPD/MTP drivers. The portable package includes .NET
and WPF; no .NET installation is required. Extract the **entire ZIP**, then run
`MoorkyzMediaRecover.exe`. It is unsigned and unpublished; Windows may warn.
Do not bypass organizational policy. No installer or elevation is used.

1. Add one or more local folders, or **Detect USB devices**, select one and
   **Add device**. For a phone: unlock it, authorize this PC and choose USB
   **File Transfer / Android Auto**, not charging-only. All accessible internal
   storage and SD folders are enumerated; no `DCIM` assumption.
2. Choose an absolute destination (not a filesystem root).
3. **Scan**. Review counts, unknown/conflicting dates, existing verified files and
   unavailable subtrees. Scanning writes inventory/reports but never copies media.
4. **Start recovery** and confirm the non-destructive operation. Pause/resume
   works while the process is running. **Stop safely** retains complete files and
   incomplete staging. After restart, select a run in **History** and retry it.
   The interrupted file starts from byte zero, including MTP.
5. The exact **Saved to** path is always displayed. Open saved folder, copy path,
   open reports, view failures and verify saved files are real actions.

Settings persist. The main UI is English/German; technical live messages, status
codes and reports remain English. Light, Dark and System palettes are available;
high-contrast colors are honored. Save settings to apply language/theme changes.
The app has no animations (including when reduced motion is enabled), no tray
process, notifications or hidden continuation. Closing during work asks to stop,
continue minimized on the **visible taskbar**, or cancel.

## Safe defaults

- Video and audio extensions from the full request; case insensitive, recursive,
  hidden files optional, no symlinks/junctions/reparse points. Destination,
  `.mediarecover` and application-state subtrees are excluded. Explicitly selected
  UNC shares are allowed; no network share discovery occurs.
- Inclusive start **2026-10-01**, no end bound. Unknown dates are included.
  Creation/last-write metadata is explicitly a filesystem/WPD fallback, **not a
  recording date**. Filesystem dates use UTC calendar dates; WPD DATE values with
  no timezone are provisional. Conflicts across the date range include and flag
  the file. Embedded media capture-date parsing is not implemented.
- Unique staging on the destination filesystem; positive byte count, expected
  length where known, stream-vs-saved SHA-256, flush-to-disk and same-volume
  no-overwrite promotion. Optional **High assurance** reads the source a second
  time. Neither assurance level checks media decodability.
- Stable source-key suffixes prevent same-name collisions. Existing unknown files
  are never overwritten or trusted merely by name/size. Rechecking prior hashes
  detects changed, missing and truncated saved copies. Repair preserves the old
  file, copies suspect bytes/provenance into quarantine and uses a new target.
- Up to four extra transient retries with 2/5/15/45-second backoff and jitter.
  All retries start the file again. Space/access errors are explicit, not green.
- Optional ZIP: new copies from that run + reports, `NoCompression`, ZIP64 via
  .NET, every entry SHA-256 verified before final promotion. Individual files are
  never removed. Partial archives remain `.part` and are not successful ZIPs.

## Data locations and recovery

Preferences and SQLite WAL ledger:
`%LOCALAPPDATA%\Moorkyz\MediaRecover\settings.json` and `state.db`.
Single-app-state and per-destination exclusive locks prevent concurrent writers.

```text
<SAVE_ROOT>\
  <safe-stem>-<source-key>.mp4
  .mediarecover\
    operation.lock
    staging\<unique>.part
    quarantine\<unique>-<suspect-file> + provenance.json
    reports\<yyyy-MM-dd_HH-mm-ss-random>\
      log_<run>.txt
      log_report_<run>.csv
      failed_report_<run>.csv
      summary_<run>.json
      manifest_<run>.csv
    archives\mediarecover_<run>.zip
```

Logs/reports contain private paths and filenames and stay local. Export diagnostics
is redacted by default. Do not share the state database or reports publicly.
Report/archive redirection is not currently implemented.

The ledger records promotion intent and SHA-256 **before** the filesystem move.
On a later scan/recovery, matching promoted bytes are reconciled even if the prior
process died before its final DB update. Unpromoted staging is retained and marked
interrupted, never blindly resumed or renamed into a finished file. SQLite and
filesystem operations are not a single atomic transaction; see `docs/architecture.md`.

**Import legacy backup folder** reads existing media, `.incomplete`, `.damaged`
and optional `S26-Backup-Index.csv` (path/name and hash column variants). It records
local hashes as `LEGACY_UNVERIFIED`, preserves every original and writes an import
receipt in the app-data `imports` directory. A legacy hash is not proof of source
identity. Import never promotes partial files or resumes them by offset.

## Build, tests and portable package

Use an installed **.NET SDK 10.0.303 or compatible stable .NET 10 SDK**, Windows x64.
No NuGet package references are used. Windows `winsqlite3.dll` and WPD are OS APIs;
do not download arbitrary DLLs. Normal SDK runtime/reference-pack restore may use
NuGet when packs are not already installed. The application itself never connects.

```powershell
Set-Location tools\mediarecover # From the repository root.
.\scripts\build.ps1
.\scripts\test.ps1
.\scripts\publish.ps1
.\scripts\verify-package.ps1
.\scripts\smoke.ps1
.\scripts\source-manifest.ps1
```

After editing this tool, refresh its downloadable-source hashes from the
repository root with `node scripts\sync-mediarecover-source.mjs`. This updates
only the local provenance manifest; it does not import a sibling project.

Outputs: `artifacts\portable-win-x64\MoorkyzMediaRecover.exe`,
`artifacts\Moorkyz-Media-Recover-0.1.0-win-x64.zip` and its SHA-256 file.
Publishing here means **local compilation only**, not uploading or releasing.
The directory-based distribution is intentional: WPF/native runtime files must
accompany the EXE; single-file and trimming are disabled.

The dependency-free test executable uses only synthetic fixtures under
`.fixtures` in this repository and removes its own fixtures. The smoke script
launches an isolated app-data profile, exercises real WPF scan/recover actions
against synthetic files, and captures **only this app's window** if supported.
No personal media is inspected. See the audit for exact observed results.

## Current limitations — read before relying on a phone backup

- Genuine WPD COM/resource transport is implemented. The Windows WPD manager was
  exercised with zero connected devices; **no Samsung/Android hardware transfer,
  locked-phone, charging-only or real cable-pull certification has been run**.
  Driver/resource-stream interoperability remains a release blocker.
- Each retry opens new native handles. Automatic endless device polling,
  persistent-ID-to-new-object-ID remapping and automatic reconnect rescan are
  **not implemented**. Reconnect, unlock and explicitly **Scan again**. The new
  scan rematches stable PUIDs. Provisional IDs are deliberately not trusted for
  incremental skipping.
- No embedded media parser/FFprobe/decode checks, content-hash deduplication
  across different source identities, source presets beyond the saved source
  list, selective WPD storage picker, parallel transfers, keep-awake, tray,
  notifications or report/archive folder override.
- Scans stream into SQLite and keep UI logs bounded. Source-key deduplication and
  destination-known-path sets still use memory proportional to file count.
  Interrupted **scans** require a full rescan; interrupted transfers can resume
  a run. No fake ETA or percentage is shown for unknown-size files.
- >4 GiB/ZIP64, clean Windows 10 VM, actual disk-full/ejection, adversarial
  concurrent directory replacement and manual screen-reader testing remain
  unverified. Use trusted destination directories; no software can make a
  removable device durable when it is unplugged before storage flush completes.
- The initial release is a working local vertical slice, **not full acceptance
  of every feature in the 410-line request**. See `docs/verification.md` and audit.

### Verification environment blocker

An initial 12-group synthetic suite passed, including real Windows WPD-manager
enumeration with no device connected. The final strict solution build passed.
The self-contained app launch **and a later rebuilt test-provider assembly** were
blocked by this machine's Windows Application Control policy (`0x800711C7`).
No security setting, signing or alternate execution workaround was attempted.
Consequently no GUI screenshot or final 13-group runtime pass is claimed.
The package is compiled, not certified runnable on this policy-restricted host.
An authorized machine/policy review is required before release.

No Git repository, remote, public release, signing or deployment is created by
the build scripts. License: MIT for this project's code; .NET redistribution
notices are included in the local package.
