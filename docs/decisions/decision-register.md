# Current product decisions

## 2026-10-23: moorkyz-tools project identity

The project repository is **moorkyz-tools**, hosted at **tools.moorkyz.com**, including
branding, metadata, packages, CLI/configuration filenames, runtime state,
environment variables, deployment references and historical documentation.

- The workspace is `moorkyz-tools`; the portal is `@moorkyz-tools/portal`.
- The local launcher is `.\tools.moorkyz.com.cmd` (POSIX:
  `sh ./tools.moorkyz.com`), configured by `tools.moorkyz.com.config.json`.
  Runtime logs/state live in `.tools.moorkyz.com/`.
- Project environment variables use `TOOLS_MOORKYZ_COM_`. Local-service
  connections remain opt-in and loopback-only; the rename does not enable them.
- Theme preferences use `tools.moorkyz.com-theme`. Existing browser preferences
  are not migrated; the renamed portal starts with its default light theme.
- Repository links target `Moorky/moorkyz-tools`; the public domain is
  `https://tools.moorkyz.com`. GitHub, the Git remote, the checkout directory,
  DNS and deployment infrastructure must use these same identities.
- Historical audit names/references are normalized to the new project identity
  at the user's request. Their original evidence remains in Git history; this
  normalization does not mean historical commands were rerun under the new name.
  Retired SDK package/path project-name segments are normalized to `sdk`;
  the standalone Toolbar Builder keeps its feature-specific name and routes.
- Author/copyright attribution, third-party provenance and the separate
  moorkyz.com footer credit remain unchanged.

## 2026-10-23: modular tools portal

User-approved:

- The landing page is a growing catalog of cards with brief explanations.
- Each tool has title, release date (honest unpublished status where unknown),
  updated date, description, use cases and setup.
- Script tools show a **Tool** section immediately after Description with file
  hierarchy, text-only console source, Copy/Download and complete source ZIP.
- VideoCrunch is integrated as a separately started Windows localhost service.
  Hosted workers, accounts, multi-user features and infrastructure stay off.
- The footer matches the current live moorkyz.com mark/credit and separator.
  Games are deferred until a reference is supplied, as the user explicitly chose.
- Next.js 16.4.1 and its lockfile patch were approved to fix the existing
  16.4.0 security gate. No dependency gate may be bypassed.

## 2026-10-23: replace the legacy SDK product

The user explicitly selected:

> Keep a tiny standalone Toolbar Builder; remove the old SDK, Studio, examples,
> SDK-specific tests and active docs. Replace the portal's SDK navigation.
> Preserve Git history and audit records.

The old Q1-Q80 SDK specifications no longer define active scope. Their exact
versions remain in Git history; historical audit links can refer to those
revisions. Current code must not depend on an unpublished SDK package,
framework adapters, SDK generation or the former Studio.

The remaining builder is a small browser-only link/appearance editor producing
one self-contained HTML file. Shared portal navigation is native and independent.
This is an intentional approved product/API removal, not a compatibility release.

## 2026-10-23: Directory Cleaner

The user requested pattern-based Keep/Move rules, creation date before/after
filters, and size smaller/larger filters, then selected **both** local service and
downloadable script, with **deletions always going to an archive**.

Implementation semantics:

- Conditions within a rule combine with AND. Keep rules always win; otherwise
  the first matching rule applies. Unmatched files remain untouched.
- Date bounds and byte-size bounds are strict. Date fields use UTC midnight,
  clearly labeled. Recursive traversal is optional and off initially.
- Preview does not mutate files. Applying requires the exact preview's explicit
  confirmation and complete preflight revalidation; stale files require a new
  preview.
- Delete means archive, never unlink without a verified preserved destination.
  No overwrite, no symlink/junction traversal, no root/home-directory cleanup.
  Archive and move destinations are excluded from scans.
- Local services remain independently opt-in and loopback-only. No hosting,
  telemetry, automatic installs or unattended scheduled cleanup.

## 2026-10-26: Moorkyz Media Recover

The user supplied the full Media Recover implementation prompt:

- Exact product name **Moorkyz Media Recover**, independent standalone project
  **Moorky/moorkyz-tools-mediarecover**, portal slug **mediarecover**.
- A real Windows desktop runtime, with read-only local-folder and authorized
  WPD/MTP sources, staging, integrity evidence, durable state and explicit reports.
  Recovery does not mean deleted/private phone data extraction.
- The portal's existing shared detail-page sequence is preserved. Workflow,
  grouped features and limitations follow Description, before source/workspace.
- No browser filesystem simulation or new local HTTP service. Public descriptions,
  source and setup remain independent of the desktop process.
- Source distribution is an explicit allowlisted, content-pinned text snapshot
  from the independent project. The source ZIP is not an executable release.
  Downloads say **Not yet released** until a release is actually published.
- No remote repository creation, Git initialization, pushing, publishing,
  deployment, signing or native prerequisite installation is performed.

## 2026-10-26: tool source ownership

The user clarified that all tool directories belong inside the tools project.
Tools remain independently usable, but their canonical source lives under
`tools/` in **Moorky/moorkyz-tools**. Separate repositories are exceptions that
require a concrete need and explicit approval, not the default for a new tool.
Do not create nested Git repositories, submodules or sibling-source sync flows.

This supersedes the earlier Media Recover standalone-repository and read-only
import-snapshot decisions. `tools/mediarecover` is now its editable canonical
source, including its existing desktop solution, tests and build scripts.
Source downloads remain allowlisted and SHA-256 checked; updating local source
requires explicitly refreshing its local provenance manifest. Runtime isolation,
unpublished status, licensing and native validation limitations are unchanged.

## 2026-10-26: Visory Bulk Downloader

The user asked to add the "Visory Bulk Downloader — Portable Windows Edition" brief as a
new tool for the site:

- Product **Visory Bulk Downloader** (portal title **Moorkyz Visory Bulk Downloader**),
  slug **visory-bulk-downloader**, canonical source `tools/visory-bulk-downloader` under
  the tool source ownership decision. No separate repository.
- The brief explicitly selects the stack: C#/.NET 10 WPF, Microsoft Edge WebView2,
  Windows BITS, SQLite, optional Task Scheduler and a portable single-file EXE. The
  desktop project's only package dependency is `Microsoft.Web.WebView2`; SQLite is the
  Windows-supplied `winsqlite3.dll`. The portal gains no dependency.
- Integration is limited to the user's own authorized session in the official Visory web
  app. The app observes only allowlisted Visory/Firebase-project metadata, never invents
  endpoints, labels the tool unofficial and reports real-account behavior as unverified.
- Data stays local: tokens in memory, stored links DPAPI-protected, sign-in profile in
  the Windows user profile with sign-out deletion, redacted reports (full URLs opt-in).
- The portal page explains the tool and serves allowlisted, SHA-256-pinned source only.
  It says **Not yet released**, makes no request to Visory and offers no binary.
- No publishing, signing, pushing, scheduled-task creation or prerequisite installation
  is performed. Application Control blocks on the development machine are reported, not
  bypassed.

## 2026-10-26: Visory Media Downloader identity

The user renamed this tool to exactly **Moorksz Visory Media Downloader**,
including the spelling **Moorksz**. Its portal slug and canonical directory are
`visory-media-downloader` and `tools/visory-media-downloader`; its registry ID is
`moorksz-tools-visory-media-downloader`. The maintainer/editorial usefulness
score is **10/10**, not a measured benchmark or a claim of real-account validation.
Native solution, project namespaces and executable use
`MoorkszVisoryMediaDownloader`. This supersedes the earlier tool identity.
Internal storage, scheduling and single-instance keys retain their original
values to preserve existing settings, sessions and jobs. No user data is moved,
no scheduled task is changed and no runtime or release status is upgraded.
Historical audits/test evidence retain the names actually verified.

## Change authority

Reversible implementation details/tests/docs can proceed autonomously. New
dependencies, security/data handling, public APIs, infrastructure, release,
publishing, deployment and external integration need explicit approval.
Accessibility, protected data, honest verification and dependency gates remain
mandatory. Task audits and focused local commits are required; pushing is not.
