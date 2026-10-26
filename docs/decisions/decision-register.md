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

## Change authority

Reversible implementation details/tests/docs can proceed autonomously. New
dependencies, security/data handling, public APIs, infrastructure, release,
publishing, deployment and external integration need explicit approval.
Accessibility, protected data, honest verification and dependency gates remain
mandatory. Task audits and focused local commits are required; pushing is not.
