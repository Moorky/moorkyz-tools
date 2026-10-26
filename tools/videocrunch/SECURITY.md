# Security and privacy

## Intended deployment

Moorkyz Video Crunch is a single-user Windows application bound to `127.0.0.1`, not a remotely exposed download service. Local users and other software running as the same Windows account are inside its trust boundary. Do not forward its port, bind to all interfaces, or reverse-proxy it onto the internet.

The server validates Host/Origin and cross-site fetch metadata, requires a per-process request token for new jobs, validates input, passes arguments without shell interpolation, and serves only allowlisted export files with realpath containment. Generated page HTML is preserved as data but is never served by the web app. Browser content restrictions prevent external script loading.

These controls are not authentication against someone with local account access. User-supplied websites and media remain untrusted inputs to yt-dlp and FFmpeg; keep those external tools updated.

## Data on disk

- Outputs default to the current user's `Desktop\VideoExports`.
- App history/result descriptors are in `%USERPROFILE%\.web-video-crunch`.
- Logs, page URLs, original HTML, and resolved stream URLs may contain temporary credentials or personal information.
- There is no telemetry, account system, hosted storage, or automatic upload.
- `.gitignore` excludes exports, media, local state, common credentials, and dependency caches. Always inspect staged files as well.

## Reporting

For a repository hosted on GitHub with private vulnerability reporting enabled, use its private security reporting feature. Otherwise contact the repository owner privately through an established channel. No owner identity or reporting email has been configured here. Do not post credentials, personal URLs, or sensitive media publicly.

Include affected revision, reproduction steps using synthetic/public-authorized data, expected/actual behavior, and impact. Fixes should add regression coverage and an evidence-based audit entry.

## Scope of automated audits

`tools\Audit.ps1` checks npm dependency advisories (including development packages) and a small set of credential patterns in nonignored source. It does not certify external binaries, installer sources, arbitrary media parsers, OS configuration, or the absence of vulnerabilities. See [audit guidelines](guidelines/AUDITING.md).
