# Architecture and trust boundaries

Seven .NET 10 Windows projects, no package references:

- **Core**: typed contracts, models, path/date/filter policy, coordinator,
  streaming transfer, hashing, retry and reconciliation.
- **Providers.Local**: iterative recursive inventory; read-only Windows file
  handles with `OPEN_REPARSE_POINT`, handle attribute validation and extended
  Unicode/UNC paths. Read sharing denies concurrent write/delete while open.
- **Providers.Wpd**: native Windows Portable Devices COM interop, a dedicated
  owning STA per session, bounded inventory channel, read-only device access,
  `WPD_RESOURCE_DEFAULT`/`IPortableDeviceResources.GetStream`, negotiated read
  buffer (bounded 4 KiB–1 MiB). No Shell automation or HTTP.
- **Storage**: OS `winsqlite3` P/Invoke; WAL + FULL synchronous, serialized access,
  UTF-8 SQL escaping/JSON, paged reads, file/per-run records and scan issues.
- **Reporting**: UTF-8 BOM CSV/TXT, JSON summaries, manifest, no-overwrite ZIP
  creation followed by every-entry SHA-256 verification; legacy importer.
- **App**: WPF MVVM, native folder dialogs, commands and bounded activity view.
- **Tests**: dependency-free executable with synthetic fixtures/fake provider.

## State transitions and crash compensation

`Eligible → Transferring → Verifying → Promoting → Verified`.
Failures retain a typed code, remediation, retry count and staging path.
AlreadyVerified is only a run-level classification after rereading a prior target
and matching the recorded SHA-256, source identity and metadata version.

1. Persist unique staging/target intent before reading.
2. Stream from source without source mutation. Unknown length is allowed, but a
   positive EOF result is mandatory; known lengths must agree. Recheck source
   metadata before/after reading. Check destination free space every chunk for
   local volumes (16 MiB reserve); UNC writes rely on explicit I/O errors.
3. Flush staging; compare stream and disk SHA-256. High assurance makes an actual
   independent source read and compares its SHA-256, then rechecks metadata.
4. Persist `Promoting` with length/hash and final target; `File.Move` without
   overwrite on the same volume; persist `Verified`.
5. On next scan/recovery, reconcile `Promoting` against final-file length/hash.
   Interrupted transfer/verification cannot be promoted. Old `.part` files stay
   preserved. Retrying creates new staging; offset resume is deliberately absent.

SQLite plus filesystem promotion cannot be one transaction. A successful flush
does not overcome faulty hardware or a device ejecting before its write cache
is durable. A driver can block inside a synchronous COM call; user cancellation
stops scheduling and bounds managed waits, but cannot safely terminate an
arbitrary kernel/USB driver. STA threads are background threads and cannot keep
the process alive after Exit.

## Identity, dates and exclusions

Local source identity hashes canonical case-insensitive absolute path. Version
uses length, UTC modification and creation ticks. WPD prefers persistent unique
ID plus device ID; missing PUID, size or usable dates is explicitly provisional and disables trusted
incremental skipping. Metadata-stable content changes that deliberately preserve
all timestamps/length are not detectable without another source read; use High
assurance or rescan into a separate backup for that threat model.

File names are normalized/sanitized, reserved DOS names escaped, excessively long
segments shortened, and a stable identity hash added. No-overwrite promotion is
the final guard against collisions. Layout can be flat, by media type or tree.
The destination cannot contain a source; a destination inside the source is
excluded. Reparse points and every existing path ancestor are checked.

Metadata contains raw values, property names and timezone descriptions. Filesystem
dates are UTC; WPD FILETIME is UTC and WPD DATE with no zone is provisional.
Neither claims to be embedded capture time. Unknown/bogus dates are included by
default; conflicting in-range/out-of-range fields include and flag.

Static links/junctions are blocked. Existing same-user processes maliciously
swapping directory ancestors between validation and access remain a residual
TOCTOU risk; use trusted source/destination directories, not hostile shared
folders. No privilege escalation or ACL bypass is attempted.

## Reconnect, retries and errors

Transient failures get at most four extra attempts (2, 5, 15, 45 seconds + jitter).
Device operations construct fresh COM sessions. The UI reports waiting/retry and
guides unlock/authorization/File Transfer/cable checks. After exhausted retries
or changed object IDs, reconnect and explicitly scan again. Automatic hardware
polling and persistent-ID remapping without rescan are not implemented.

Disk full, permission denial, zero bytes, length/hash mismatch, source mutation,
missing source, links and scan failures are never represented as verified.
Repair does not overwrite/delete suspect files: copy to quarantine with provenance,
retain the old target and promote the replacement under a new collision-safe name.
No quarantine cleanup action is currently provided.

## Privacy and resources

No sockets, hosted page processing, credentials, updater, telemetry or filesystem
actions from a remote site. Connections to an explicitly chosen UNC source are
normal Windows filesystem operations, not application telemetry.
State/reports contain private metadata. Redacted diagnostics omit device IDs,
media paths, filenames and full error strings. Native OS APIs do not constitute
bundled dependencies. The portable package includes actual .NET redistribution
notices. Source bundle inputs are explicit, text-only and independent of build
output or runtime state.
