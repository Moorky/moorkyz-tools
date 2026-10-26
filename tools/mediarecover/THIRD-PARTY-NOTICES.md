# Third-party components

This project's source has no NuGet package dependencies.

The self-contained binary package redistributes Microsoft .NET 10 runtime and
Windows Desktop/WPF runtime components, under their upstream licenses and third-party
notices. `scripts\publish.ps1` copies the installed/restored runtime-pack
`LICENSE`/`LICENSE.TXT` and `THIRD-PARTY-NOTICES.TXT` into the package.
The WindowsDesktop 10.0.11 package omits its notices file, so the verbatim upstream
WPF v10.0.11 notice is retained in `third-party\wpf-10.0.11-NOTICES.txt`.
Source: https://raw.githubusercontent.com/dotnet/wpf/v10.0.11/THIRD-PARTY-NOTICES.TXT
The script fails rather than declaring a redistributable package complete when
runtime license/notices cannot be found. Upstream license sources:

- https://github.com/dotnet/runtime/blob/main/LICENSE.TXT
- https://github.com/dotnet/runtime/blob/main/THIRD-PARTY-NOTICES.TXT
- https://github.com/dotnet/wpf/blob/main/LICENSE.TXT
- https://github.com/dotnet/wpf/blob/main/THIRD-PARTY-NOTICES.TXT

Windows `winsqlite3.dll`, WPD COM, `kernel32.dll` and `ole32.dll` are called through
OS interfaces and are **not** bundled. Their availability and Windows licensing
are the responsibility of the installed operating system. Do not copy DLLs from
untrusted sources. No FFmpeg, FFprobe, community SQLite package or other native
prerequisite is installed or included.
