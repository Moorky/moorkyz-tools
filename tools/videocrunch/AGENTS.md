# Working on Moorkyz Video Crunch

- Keep this a Windows-first, localhost-only application. Do not expose the service to the network, add hosted uploads, or bypass DRM/authentication.
- Read [the architecture](docs/ARCHITECTURE.md) and [contribution guidelines](CONTRIBUTING.md) before changing behavior.
- Keep runtime dependencies at zero. The double-click launcher must bootstrap missing prerequisites on first use; development tools must not block normal app startup.
- Preserve Windows PowerShell 5.1 and Node.js 22 compatibility. Use absolute Windows filesystem paths and URL-relative paths only for HTML/HTTP resources.
- Preserve downloaded bytes. Each export has `original/source.*`, an optional `compressed/video.mp4`, an offline player, and a manifest. Never overwrite or automatically delete an export.
- Never interpolate user input into shell code. Preserve local request protections, file allowlists, realpath containment, and media range requests.
- Surface failures in the UI and manifest. Do not hide errors behind successful states or invented progress percentages.
- For every completed change: update related docs, run `tools\Verify.ps1`, run `tools\Audit.ps1`, and record actual results in `audits/`. Do not claim unperformed checks.
- Commit completed, verified work when the user has requested commits. Stage explicit source paths, never downloaded media, credentials, or local history. Do not commit on failed checks.
- Keep ideas in [ideas/](ideas/README.md), based on its template. The [tools/](tools/README.md) folder is for a few small maintenance scripts, not bundled binaries.
