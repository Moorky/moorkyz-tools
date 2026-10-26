# Dependencies and distribution

| Component | Requirement | Installation/update | Role |
|---|---|---|---|
| Windows | Windows 10/11 with PowerShell 5.1 | Operating system | Local process/UI environment |
| Node.js | 22+ | winget `OpenJS.NodeJS.LTS` | Built-in HTTP backend and tests |
| yt-dlp | Current compatible release | winget `yt-dlp.yt-dlp` | Authorized site/stream downloads |
| FFmpeg/ffprobe | libx264 and AAC support | winget `Gyan.FFmpeg` | Merge, compression, validation |
| winget | Windows App Installer | Microsoft Store / OS | First-use installation only |
| Git | Development only | Official Git for Windows | Source control and source audit |
| Pester | 3.4 or 4.x, development only | Bundled Windows module / approved installation | CLI regression tests |
| Playwright | Locked npm dev dependency | `npm ci` | Browser tests using installed Microsoft Edge |

The application has **zero npm runtime dependencies** and no frontend build step. Its lockfile records development-only Playwright packages. Microsoft Edge is required only for browser validation, not for end users who prefer another browser. No binaries, vendor installers, or package caches are included in `tools/`.

Automatic setup requests packages from winget's community catalog and accepts the relevant source/package agreements. It does not pin a historical yt-dlp/FFmpeg build because site compatibility changes frequently. Review package provenance and local policies; automatic setup is not a supply-chain certification.

Before redistribution, review licenses from the actual upstream versions. Node.js, yt-dlp, FFmpeg, and FFmpeg's included codecs have their own notices/conditions; FFmpeg distribution obligations vary with build configuration. This repository does not relicense or bundle them.

The repository owner has not yet selected a distribution license for Moorky Video Crunch itself. Do not infer MIT or another open-source license from the presence of source code.
