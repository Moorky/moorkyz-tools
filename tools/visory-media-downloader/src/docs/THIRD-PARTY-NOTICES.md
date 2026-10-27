# Third-party components

## Documentation font

DM Mono Regular is reused from this repository's existing local font asset.
Copyright 2020 The DM Mono Project Authors. Licensed under the SIL Open Font
License 1.1; the complete notice is in
[DMMono-OFL.txt](../utils/documents/fonts/DMMono-OFL.txt).
The text-encoded font is embedded into generated local HTML; no hosted font
service is contacted. Markdown viewers can substitute their own fonts.

## Source dependencies

- **Microsoft.Web.WebView2 1.0.4314.35** (NuGet, Microsoft). The only package
  dependency; it hosts the official Visory web app for sign-in and scanning.
  Licensed under the BSD-style terms in the package `LICENSE.txt`, with notices in
  `NOTICE.txt`. `src\scripts\development\publish.ps1` copies both into the release `licenses` folder.

The test project uses no test framework package.

## Redistributed in the portable EXE

The self-contained single-file EXE redistributes the Microsoft .NET 10 runtime and the
Windows Desktop (WPF, Windows Forms) runtime 10.0.11 under their upstream MIT licenses
and third-party notices. `src\scripts\development\publish.ps1` copies the restored runtime-pack license
and notice files. The WindowsDesktop 10.0.11 runtime pack omits its notices file, so the
verbatim upstream WPF v10.0.11 notice is kept in `src\third-party\wpf-10.0.11-NOTICES.txt`
(source: https://raw.githubusercontent.com/dotnet/wpf/v10.0.11/THIRD-PARTY-NOTICES.TXT).
The script fails instead of producing a package with missing notices.

- https://github.com/dotnet/runtime/blob/main/LICENSE.TXT
- https://github.com/dotnet/runtime/blob/main/THIRD-PARTY-NOTICES.TXT
- https://github.com/dotnet/wpf/blob/main/LICENSE.TXT
- https://github.com/dotnet/wpf/blob/main/THIRD-PARTY-NOTICES.TXT

## Used from Windows, not bundled

- **Microsoft Edge WebView2 Runtime** (Evergreen). Detected at runtime; the app links to
  Microsoft's official download page when it is missing and never installs it silently.
- **winsqlite3.dll** (SQLite shipped with Windows), **BITS** (Background Intelligent
  Transfer Service COM API), **crypt32.dll** (DPAPI), **schtasks.exe** (Task Scheduler).

## Trademarks

Visory is a trademark of its respective owner. This is an unofficial tool that is not
affiliated with, endorsed by or supported by Visory.
