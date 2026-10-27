# Development Audit: Directory Cleaner and compact standalone Toolbar Builder

> Local implementation self-audit; not independent security or release certification.

| Field | Value |
|---|---|
| Audit ID | `2026-10-23T12-16-42Z-directory-cleaner-and-compact-standalone-toolbar-builder` |
| Created (UTC) | `2026-10-23T12:16:42.216Z` |
| Task / issue | Directory Cleaner and compact standalone Toolbar Builder |
| Auditor | AI assistant using Copilot SDK in VS Code; self-audit |
| Branch / baseline commit | `master` / `71ba040`; initially clean worktree |
| Final commit | Report in task completion; do not edit this audit after committing |
| Scope reviewed | New Directory Cleaner engine/CLI/service/UI; standalone compact builder; explicit retirement of legacy SDK/Studio structure; current docs/instructions/tooling and retained VideoCrunch gateway |
| Outcome | `PASS WITH RISKS`: approved local scope verified; no public hosting/release claim |

## 1. Request and outcome

- Requested behavior: add Directory Cleaner with patterns, Keep/Move, creation
  before/after and smaller/larger file filters; reduce the toolbar creation tool
  to a very small tool and remove its old project structure.
- Explicit clarification/approval: the user chose a standalone compact builder
  and removal of the old SDK, Studio, examples, SDK-specific tests and active
  docs, with replacement portal navigation and preservation of history/audits.
  Cleaner must support **both** local service and script, and Delete must
  **always archive**, not permanently delete or recycle.
- Implemented Cleaner: shared dependency-free Node engine for CLI and loopback
  service, downloadable settings and complete source ZIP, wildcard/date/size
  rule editor, non-mutating preview, exact confirmation, stale-plan preflight,
  archive/move preservation, conflict/link/root guards, asynchronous run status
  and synced recovery receipts. Keep wins; otherwise the first matching rule
  applies. Unmatched files stay untouched. Bounds are strict and dates use UTC.
- Implemented builder: two independent source files, up to eight links, appearance,
  position, accent border, native preview, Copy and one HTML download. No SDK,
  adapters, generator package, Studio or host navigation dependency remains.
  Default output is 1,158 bytes; output is capped at 8 KiB. Default rendered
  workspace is at most 704px wide and below 540px high at desktop test dimensions.
- Complete retirement: the old package, example folders, architecture/contracts/
  design specs, large Playground/Studio pages, related tests, coverage/bundle/
  release-evidence scripts and stale manifest were removed. Ignored generated
  SDK/example leftovers were removed from these exact approved subdirectories.
  The former routes intentionally return 404. Existing audits are unchanged;
  original source/specification versions remain recoverable in Git history.
- Shared portal: native header, local registry search and explicit theme preference
  are independent of tools. Existing footer/separator are preserved. Catalog now
  contains three tools. Source views retain Copy, per-file Download and ZIP.
- Intentional exclusions: hosted cleanup/media workers, accounts, scheduling,
  automatic installs, remote integrations, telemetry, permanent deletion and
  automatic archive removal. No real personal files were cleaned during tests.
- Authority: current decision register records the approved product replacement
  and file-operation semantics. Old Q1-Q80 SDK requirements are historical, not
  active contracts; their removal is intentional, not a compatible SDK release.

## 2. Change inventory

| Path | Change | Risk |
|---|---|---|
| `tools/directory-cleaner/`, source allowlist | Engine, local service, CLI, explicit launcher, example settings, guide, license and private-output ignore rules | High: filesystem operations, mitigated by archive-first confirmation/revalidation |
| `tools/toolbar-builder/`, compact component | Small pure HTML/CSS generator and browser controls | Low: escaped labels/URLs, no executable output |
| Portal Cleaner/model/shared gateway/new route | Typed boundary guards, rule editing, default-off connection, preview/confirmation and active-run polling | Medium: localhost trust/lifecycle boundary |
| Portal shell/header/search/theme/CSS/registry/metadata | Remove SDK navigation and large demo assumptions; retain independent shared UI | Medium: intentional layout/routing replacement |
| Legacy package/examples/specs/Studio/labs/tests/scripts/manifest | Approved removal; existing audit history retained | High: intentional product/API retirement |
| Workspace/package/lock/Next/Docker/CI/Playwright | Two-project workspace, removed unused dependencies/importers, current routes, dedicated native-service gate | Medium: changed build graph |
| Instructions, guides, decisions, validations and this audit | Current product scope, safety, commands and residual limitations | Low |

## 3. Verification evidence

| Check | Result | Exact command / method | Evidence / notes |
|---|---|---|---|
| Locked dependency restoration | PASS | `pnpm install --frozen-lockfile` | Two projects; no new dependencies; removed old SDK/example graph. Initial pruning left an obsolete esbuild approval placeholder; no remaining esbuild dependency, so the retired entry was removed and frozen install passed |
| Complete configured quality gate | PASS | `pnpm check` | Final complete run passed lint, formatting, production build, 42 Node tests, 18 Chromium/Firefox/WebKit browser cases and portal typecheck |
| Build/current routes | PASS | `pnpm build` (executed by `pnpm check`) | Standalone production app; no SDK build/transpile/copy step; builder and both default-off gateway routes present |
| Rule/engine/CLI/API tests | PASS | `pnpm test` (executed by `pnpm check`) | 42 total current tests; includes strict date/size boundaries, AND/Keep/order semantics, actual creation times, no preview writes, archive bytes/receipts, collision/ancestor conflicts, canonical-root and link guards, stale files, copy fallback, partial failure, CLI confirmation/plan exclusivity, independent gateway/token/body guards, source ZIPs and retained generic/video tests |
| Browser/axe/compact export | PASS | `pnpm test:browser` (executed by `pnpm check`) | 18 passed; all three engines; mobile/themes/keyboard/source errors and downloads, native navigation, absent retired routes, disabled zero-native-request paths, compact dimensions, real downloaded HTML opened locally and axe checked |
| Real Cleaner service | PASS | `pnpm test:directory-cleaner:browser` | 6 passed: real disposable files, Keep, confirmed archive/move, exact byte preservation, receipt, disconnect, stale-plan rejection and no stale confirmation reuse |
| Retained real video service | PASS | `pnpm test:videocrunch:browser` | 6 passed: genuine synthetic video export, playback, original download, polling cleanup/reopen and explicit retry through factored gateway |
| Independent Cleaner ZIP interoperability | PASS | Windows `Expand-Archive`, then `Get-FileHash` against every allowlisted source file; remove exact task-owned extraction directory | All 8 final files match SHA-256; LICENSE and safe ignore rules included; no settings/plan/archive data included |
| Exact compact thresholds | PASS | Browser assertions plus `node --input-type=module` invoking `buildToolbar` with default two links | Width <=704px, default desktop height <540px in three engines; default HTML 1,158 bytes, no script/SDK/runtime imports; unit default <3 KiB and generator upper cap 8 KiB |
| Source safety scan | PASS | `pnpm check:source` | 201 paths in final scan; bounded credential patterns and portal/Cleaner executable/HTML-sink checks; includes PowerShell files inherited from VideoCrunch |
| Dependency audit | PASS | `pnpm audit --audit-level=moderate` | No known vulnerabilities; Next.js 16.4.1 retained, no dependency additions |
| Active graph/removal review | PASS | `pnpm list --recursive --depth=0`; explicit filesystem assertions and Node structure test; source searches | Only root and portal projects; no SDK dependency, examples, old spec folders or obsolete build approval; no active old route/contract references |
| Historical preservation | PASS | `git diff --name-only -- docs\audits`; staged equivalent; `git status --short tools\videocrunch` | Existing audits and imported video source unchanged; only this new audit is added |
| `git diff --check` | PASS | `git diff --check`, `git diff --cached --check` | No whitespace errors; normalization notices only |
| Manual/independent/container/Linux certification | NOT RUN | No human AT/touch/device review, independent security review, container run or clean VM was performed | Existing CI definition updated, not executed remotely; no deployment |
| Retired SDK coverage/adapters/bundle gates | N/A | Removed with the user-approved SDK product retirement | Not presented as active gates or as passed tests; new generator output size is explicitly tested |

## 4. Risk and quality review

- Contracts/defaults: approved intentional SDK removal, not a package-compatible
  update. Native portal navigation does not import any tool. Old Studio/Playground
  URLs return 404. Actual repository/domain/infrastructure identifiers are not
  renamed or provisioned. Both service connections stay independently off by default.
- Filesystem safety: no permanent-delete action exists. Preview is read-only.
  Apply accepts an intact stored plan ID plus exact confirmation and re-scans the
  complete snapshot before writes. Roots, home itself, Windows system folders,
  UNC paths, links/junctions, target overwrites and target-as-parent conflicts are
  rejected. Source and output paths are canonicalized, including existing parent
  prefixes of missing output folders. Archive/output folders are excluded.
- Preservation: same-filesystem hard link or exclusive copy with streaming SHA-256
  verification precedes unlinking the source name. Preview identity is rechecked
  immediately before and after preservation. Recovery receipt is created/synced
  before actions and updated per success/failure. Failures stop processing and
  report partial/failed/pending work; no silent rollback or retry.
- Privacy: no browser persistence of source paths/plans. JSON settings and CLI
  plans contain private paths and must be kept local. Default settings/plan/archive
  outputs are ignored; source bundles use explicit allowlists. Existing VideoCrunch
  license/history/privacy boundaries remain unchanged. No shell interpolation.
- Service authority: loopback binding, exact Host/Origin checks, process-local
  tokens, resource/method allowlists and bounded bodies. Gateway forwards only
  tool-specific tokens/content type/ranges, never cookies or authorization.
  No public worker/authentication/tenant-isolation claim.
- Accessibility: native controls and semantic table/folders, visible focus, strict
  typed result guards, UTC/unit labeling, clear errors and reduced motion.
  New controls are disabled until hydration so early input cannot be discarded.
  Exported HTML includes main/title/heading and accessible native navigation.
- Cleanup: no connection until explicitly requested; polling exists only for an
  accepted/resumed run and stops on disconnect/unmount. Confirmed work continues
  in its independently owned process and receipt, not the browser request lifetime.
  Abort controllers and timeouts are cleaned up. Test services and temporary
  filesystem/HTML/ZIP fixtures were closed/removed.
- Performance/dependencies: deterministic wildcard DP avoids regex backtracking;
  previews cap traversal at 10,000 entries/depth 64/100 rules; target conflict
  checks use sorted canonical prefixes rather than a quadratic pairwise scan.
  No new packages. The small builder has no output script or external resource.

## 5. Findings and residual risks

| ID | Severity | Finding and evidence | Mitigation / owner | Status |
|---|---|---|---|---|
| F01 | Medium | Ambiguous "remove everything" could delete the desired tool or retain the SDK | Targeted user choice explicitly approved standalone compact builder plus legacy structure removal | RESOLVED |
| F02 | Medium | Native cleanup could otherwise permanently delete/overwrite or execute stale actions | User chose archive-only; preservation, no-overwrite, exact confirmation, full preflight and receipts tested | RESOLVED FOR LOCAL SCOPE |
| F03 | Medium | Layout CSS initially landed inside reduced-motion scope; width refinement initially targeted a catalog rule | Actual rendered dimensions caught both; default compact rules corrected and width/height assertions pass across engines | RESOLVED |
| F04 | Medium | Early SSR form edits could be reset during hydration | New workspaces gate controls on hydration readiness; all browser input/download paths pass | RESOLVED |
| F05 | Low | Regenerated routes referenced deleted pages during initial typecheck | Removed only stale generated type cache and ran `next typegen`; subsequent build/typechecks pass with generated production imports | RESOLVED |
| F06 | Low | Exported standalone HTML initially lacked main/level-one heading | Added a minimal example main/heading; exported-file axe checks pass in three engines | RESOLVED |
| F07 | Low, environment | Highly parallel local Firefox context teardown produced a protocol error | Local browser default is serial on this shared host; CI retains its prior three-worker policy; final full gates pass | RESOLVED LOCALLY |
| R01 | Medium | Hostile concurrent writers, abrupt power loss, unusual filesystems/ACLs and real multi-volume interruption are not independently certified | Close writers, inspect receipts and preserve both copies on failure; owner/manual review | OPEN |
| R02 | Medium | VideoCrunch still has no selected distribution license; public hosting lacks isolation/quotas/auth | Existing notices/limits retained; hosted flags off and no release/deploy | OPEN, outside local scope |
| R03 | Low | Historical audit links reference removed specs/routes | Preserve evidence unchanged; retrieve exact baseline versions from Git history | INTENTIONAL |

- Known gaps: cross-volume copy fallback was exercised by a controlled EXDEV
  injection on disposable files, not by moving actual user data across physical
  drives. No independent adversarial filesystem/race review, real-device/AT,
  clean-VM bootstrap, remote CI or container certification was performed.
- Required approvals: current retirement and both Cleaner modes/archive policy
  were explicitly approved. Publishing, deployment, hosted services, infrastructure
  changes and VideoCrunch redistribution require separate authorization.
- Recovery: locate originals through the per-run archive receipt; never overwrite
  a newer original while restoring. Disable the corresponding portal flag and
  restart to disconnect the tool. Confirmed native jobs continue safely until the
  process is stopped. A future authorized Git revert can restore retired source;
  no prior history/audits or exported personal files were removed by this task.
- Release readiness: `NOT READY` for public hosting/distribution; verified for
  the explicitly approved local tools scope.

## 6. Completion record

- Audit created with the repository CLI and filled before staging the focused
  task commit. Included with the same implementation/removal commit.
- Final commit hash: reported in task completion, avoiding a post-commit
  self-referential audit edit.
- External actions: `NOT AUTHORIZED` unless separately approved.

**Limitations:** An agent self-audit is not an independent security review, accessibility certification, or release approval.
