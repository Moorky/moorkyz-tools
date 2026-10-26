# Modular tools portal

## Approved scope

On 2026-10-23 the user requested a growing tool catalog, complete Moorkyz Video
Crunch integration, common detail pages, and the live moorkyz.com footer. Follow-up
approval selected a **local web service**, with public hosting, authentication,
multi-user workers and infrastructure off. The live site had no discoverable
footer game; the user explicitly selected its current footer without a game
until a reference is supplied.

The homepage is the catalog. `/tools/toolbar`, `/tools/videocrunch`,
`/tools/directory-cleaner` and `/tools/mediarecover` provide
Title, Release Date, Updated On, Description, Use Cases and Setup. Local script
tools additionally show **Tool immediately after Description**: a file hierarchy,
selectable console-style source, copy/download per file and a complete ZIP.
None claims a public release date; the integration date is not a release.
The user subsequently approved removal of the legacy SDK, Studio, examples,
SDK-specific tests and active specifications. Their history and audits are
preserved. `/tools/toolbar` now contains only a compact, independent builder.
The old large Playground and Studio routes intentionally return 404.

Display titles use `Moorkyz <Project Name>` with spaces between words. Each
catalog card and detail heading also shows its stable registry `id`, such as
`moorkyz-tools-video-crunch`, in small muted DM Mono text beneath the title.
These visible identifiers do not change routes, script names or API identifiers.

## Portal branding

The shared header uses the owner's black crossed-tools SVG and the display name
**Moorkyz Tools**. A white backing keeps the black mark visible in both themes.
The same artwork supplies SVG/ICO/PNG favicons, touch/Android icons and social
preview imagery. Browser titles and the web manifest use the display name;
the domain, repository identity and theme-storage key remain unchanged.

## Shared footer

The homepage, tool details and contributing page share the dark Moorkyz footer.
Its centered `made /w` mark and copyright credit use the live moorkyz.com styling:
a 0.9-second fade-in after 0.4 seconds and a single 2.4-second separator glint
after 1.5 seconds. Reduced-motion preferences disable both animations.
The footer stays in document flow rather than covering tool workspaces.
At the default font size it is approximately 56 px tall, a further 20% reduction
from the previous 69.6 px footer. The centered credit sits lower, with both side
links aligned even nearer the bottom at matching font sizes and heights.
On narrow screens the credit sits above the links.
The lower-left `Stay in Touch?` link navigates to `https://moorkyz.com/contact`;
the lower-right `More projects?` link navigates to `https://moorkyz.com/projects`.
Both are un-underlined white text with no icons; only the center credit keeps
its Moorkyz mark. Hover and keyboard focus brighten either link with the same
glow; keyboard focus
also retains a visible outline. All artwork and animation are local, with no new
dependencies or automatic external requests.

## Moorkyz Toolbar Builder

The browser-only builder edits at most eight labels/URLs, light/dark appearance,
top/bottom position and accent border. Native links provide keyboard navigation;
the preview reports clicks without navigating. Copy/Download produces one
self-contained HTML file, at most 8 KiB, with no JavaScript, package dependency,
external fonts or network demand. The portal itself uses independent native
navigation, not this generated toolbar.

## Adding tools

1. Register an entry in
   [`tools.ts`](../../packages/portal/src/data/tools.ts). The catalog, detail
   metadata, use cases, setup and local search derive from this registry.
2. For source-based tools, place only distributable source under `tools/<slug>`
   and add an explicit `tools/<slug>.files.json` allowlist. Register the bundle in
   [`prepare-tool-sources.mjs`](../../scripts/prepare-tool-sources.mjs) and
   reference its name/files in the tool definition. Never include local state,
   downloaded files, credentials, Git internals, caches or dependencies.
3. Add a workspace integration only when needed. Metadata-only tools can use the
   shared detail layout; executable workspaces need their own explicit rendering
   branch and authority boundary, rather than executing manifest-supplied code.
4. Add route, accessibility, disabled-path and actual-workspace tests. Update
   `updatedOn` deliberately when the tool changes.

Development and production builds prepare source JSON and deterministic,
uncompressed ZIP files from original bytes. Generated assets are ignored by Git
and copied into the standalone portal's public assets. Selecting a file fetches
only the first-party source bundle; the viewer renders source as text and never
executes it. Clipboard/download failures remain visible and source stays
selectable. A future distribution license must be settled before distributing
Moorkyz Video Crunch: its original no-selected-license notice is retained and the
parent project's Apache-2.0 license must not be assumed to relicense it.

## Moorkyz Media Recover

`/tools/mediarecover` explains the independent **Moorkyz Media Recover** Windows
desktop application. Its canonical source lives in **tools/mediarecover** within
**Moorky/moorkyz-tools**, alongside the portal and the other tools. Independent
use does not require a separate repository. No remote repository or public
executable release is created. The page states **Not yet released** and offers no fake binary
download. It is not a browser filesystem workspace and has no native HTTP route,
iframe, background connection, upload or service-enable flag.

The desktop source compiled and a self-contained x64 package was produced,
but desktop runtime acceptance is **blocked**: this machine's Windows Application
Control rejects unsigned application assemblies with `0x800711C7`. The earlier
12-group synthetic suite passed before final hardening; the final expanded suite
and WPF launch did not complete. No desktop screenshot, phone-transfer validation
or production-readiness claim is made. Automatic device reconnect, embedded
capture-date parsing and several advanced options remain deferred, as documented
in the desktop README. Do not disable machine policy to run this preview.

The shared sequence remains header/metadata, Description, Tool, Use Cases, Setup,
then Downloads. Workflow, grouped features and candid recovery/integrity limits
are inside Description, before the source browser. Catalog cards, metadata and
local search derive from the registry; search also matches route slugs.

### Canonical source and checked downloads

The editable source under [`tools/mediarecover`](../../tools/mediarecover)
belongs to this repository. All tools use this monorepo by default; a separate
repository requires a concrete need and explicit approval. Its explicit
[`allowlist`](../../tools/mediarecover.files.json) excludes outputs, state,
media, packages and Git internals.
[`SOURCE-PROVENANCE.json`](../../tools/mediarecover/SOURCE-PROVENANCE.json)
records repository/directory ownership, SHA-256 of every included source file
and a deterministic tree hash. This pins local content, not an invented remote
commit or release.
Git attributes disable line-ending conversion for these sources so their checked
bytes survive Windows and non-Windows checkouts. Upstream third-party
notice whitespace is retained verbatim.

After reviewing local source edits, refresh provenance from the repository root:

```powershell
node scripts\sync-mediarecover-source.mjs
pnpm build
node --test tests\tooling\mediarecover-snapshot.test.js tests\tooling\tools-catalog.test.js
pnpm check:source
```

The compatibility-named sync command validates the complete allowlist and rejects
traversal, linked source/output components and non-UTF-8 text. It only updates the
local provenance manifest; external source arguments are rejected, and no source
is copied or overwritten. Source generation refuses content whose recorded
provenance no longer matches its bytes. Builds need neither a sibling project
nor the desktop process. Download all is a source ZIP, never a
portable application package. Windows requirements, state/report paths and USB
authorization steps are documented on the tool page and in the tool's README.

## Moorkyz Directory Cleaner

The [standalone guide](../../tools/directory-cleaner/README.md) describes its
shared engine, CLI, service, receipts and supported bounds. The portal's rule
editor also works with the connection off: download settings and the complete
script ZIP, preview locally, then confirm explicitly.

Rules support `*`, `?`, `**` filename/relative-path patterns, UTC creation date
bounds and strict smaller/larger byte sizes. Conditions use AND. Keep wins;
otherwise the first matching rule applies, and unmatched files are untouched.
Delete always archives; permanent deletion is not a supported action.

```powershell
# Separate service terminal:
pnpm tool:directory-cleaner
# Portal terminal:
$env:TOOLS_MOORKYZ_COM_DIRECTORY_CLEANER_ENABLED = "1"
pnpm --filter @moorkyz-tools/portal dev --hostname 127.0.0.1
```

The default service port is 8766. An explicit native `--port` must match
`TOOLS_MOORKYZ_COM_DIRECTORY_CLEANER_PORT`. The process-local token guards previews,
confirmed application and run status. Preview is non-mutating; apply accepts
only a retained server-side plan ID plus its exact confirmation. Plans expire
after ten minutes, and all files/settings must revalidate before writes.
Archive and move targets are excluded, roots/home/system folders and links are
rejected, destinations never overwrite, and preservation precedes unlinking.
Large apply operations return a run ID immediately instead of tying file work to
an HTTP timeout; polling starts only for an explicitly accepted/resumed run and
is released on disconnect/unmount. Closing the view does not cancel confirmed
work. Failures are explicit, processing stops and synced recovery receipts remain.

## Moorkyz Video Crunch localhost service

The complete tracked source from the provided VideoExports project is imported
under [`tools/videocrunch`](../../tools/videocrunch/), without modifying that
external repository. Its exporter, request token, single-job concurrency,
history, original-byte preservation, path allowlists, range responses and fixed
PowerShell invocation are retained. Relative frontend URLs support both the
original standalone app and its portal mount.

On Windows with Node.js 22+, Windows PowerShell 5.1, yt-dlp and FFmpeg installed:

```powershell
# Terminal 1, repository root:
pnpm tool:videocrunch
# Terminal 2, repository root:
$env:TOOLS_MOORKYZ_COM_VIDEOCRUNCH_ENABLED = "1"
pnpm --filter @moorkyz-tools/portal dev --hostname 127.0.0.1
```

For explicit prerequisite bootstrapping, run
`.\tools\videocrunch\Start-WebVideoCrunch.cmd` instead of Terminal 1.
It can install missing native tools through winget; the portal never installs
anything. Missing tools and failed exports are visible in the native workspace.

The portal gateway `/api/local-tools/videocrunch/...` is off unless
`TOOLS_MOORKYZ_COM_VIDEOCRUNCH_ENABLED=1`. The user must also open the workspace before it
connects or polls. Closing/unmounting the iframe stops its frontend work; an
accepted export continues in the independent service. Reconnection errors are
reported with an explicit retry control.

The upstream target is always `127.0.0.1`, default port 8765. If the native service
uses `--port`, set `TOOLS_MOORKYZ_COM_VIDEOCRUNCH_PORT` to the same valid port in the portal
terminal. The gateway rejects non-loopback Host, mismatched Origin, cross-site
requests, unlisted resources/methods, and oversized job bodies. It forwards only
content type, the process-local job token and byte ranges, never credentials or
cookies. Redirects are refused; unavailable upstreams return explicit 502 errors.
Range/stream/download responses are preserved. Native iframe policy allows
same-origin embedding and its existing inline styles; the portal's nonce-based
CSP remains unchanged.

**Not a hosted service:** do not expose either process publicly or reverse-proxy
the connection. Host checks are defense in depth, not multi-user authentication.
There are no accounts, tenant isolation, remote workers, quotas or hosted storage.
The source viewer and setup page remain available when the gateway is off.

## Verification

```powershell
pnpm build
node --test tests\tooling\tools-catalog.test.js tests\tooling\videocrunch-proxy.test.js tests\tooling\download-json.test.js
pnpm exec playwright test --workers=2
pnpm test:directory-cleaner:browser
pnpm test:videocrunch
pnpm test:videocrunch:browser
```

The separate real-service browser configuration uses isolated synthetic media,
temporary exports/history and loopback fixture ports 8876/8877, with explicit
teardown. It requires the Windows/native prerequisites and runs serially; it is
not a mandatory dependency of the standard browser suite. Moorkyz Directory Cleaner has
a separate real-service browser configuration using disposable files and port 8878. No personal files or third-party videos are used for verification.
