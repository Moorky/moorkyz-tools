# Development Audit: compact footer and local tool ownership

> Observed task evidence; self-audit, not independent certification.

| Field | Value |
|---|---|
| Audit ID | `2026-10-26T19-01-23Z-compact-footer-and-local-tool-ownership` |
| Created (UTC) | `2026-10-26T19:01:23Z` |
| Task / issue | Match the live Moorkyz footer, reduce height by 35%, add a glowing projects link, and keep canonical tool sources inside this project |
| Auditor | Copilot coding agent; self-audit using Git, Playwright, browser inspection and repository checks |
| Branch / baseline commit | `master` / `b04d46e397468d6f42074880c9d4af4dafa43310` |
| Final commit | Reported in task completion; audit is not edited after committing |
| Scope reviewed | Shared footer, source ownership policy, Media Recover source provenance, directly related tests/docs |
| Outcome | `PASS WITH RISKS`: requested behavior verified; existing native-runtime acceptance limitations remain |

## 1. Request and outcome

- Matched the inspected live `https://www.moorkyz.com/` footer's credit, white
  Moorkyz mark, blue-tinted text glow, separator, 0.9-second fade after 0.4 seconds,
  and one-time 2.4-second separator glint after 1.5 seconds.
- Kept the existing near-black background and Evidence workspace link. The
  footer remains in document flow rather than adopting the reference's fixed
  positioning, avoiding coverage of tool controls.
- Measured the original local footer at 107.2 px at 320, 390 and 1440 px widths.
  The revised footer is 69.6 px: a 35.07% reduction. Automated checks cover
  320, 390, 700, 701 and 1440 px in light/dark themes in three browser engines.
- Added one lower-right `More projects?` anchor containing a white 1 em SVG,
  targeting `https://moorkyz.com/projects`. No underline or browser-default link
  color; hover/focus glows the complete link, with a white keyboard outline.
- All six shared-footer routes are covered, including all four tool details.
- User clarification supersedes the earlier separate Media Recover repository
  assumption: canonical tools live under `tools/` in this repository. Separate
  repositories require a concrete need and approval.
- Existing tool sources were already tracked here; no source directories,
  repositories, personal files or sibling projects were moved or deleted.
- Converted the compatibility-named Media Recover sync command into a local
  provenance refresh: it writes only the manifest, never copies source files,
  and explicitly rejects external-source arguments. Allowlist, UTF-8, SHA-256,
  traversal and link checks remain active.
- Updated source notices, local ownership metadata, maintained docs and root/tool
  instructions. Historical audits and third-party notices are unchanged.
- Excluded unrelated catalog cards, version/score/hero/header refinements and
  concurrent tool-monogram removals from staging.
- Contracts: root AGENTS, current decisions, engineering workflow/testing/coding
  standards, portal guide and Media Recover's safety instructions.

## 2. Change inventory

| Path | Change | Risk |
|---|---|---|
| `packages/portal/src/components/site-footer.tsx` | Reuse local SVG for credit and complete projects link | Low |
| `packages/portal/src/app/tools.css` (footer hunks only) | Compact responsive layout, live fade/glint, hover/focus glow, reduced motion | Low |
| `tests/browser/site-footer.spec.js` | Geometry, themes, all routes, animation frames, keyboard, glow, intercepted destination and axe checks | Low |
| `AGENTS.md`, `docs/decisions/decision-register.md`, `README.md`, `docs/engineering/repository-structure.md`, `docs/portal/modular-tools.md`, `scripts/README.md` | Current monorepo ownership and footer behavior | Low |
| `scripts/sync-mediarecover-source.mjs` | Local-only provenance refresh instead of external-source copying | Low |
| `packages/portal/src/data/tools.ts` (ownership strings only) | Correct setup/source notices | Low |
| `tools/mediarecover/AGENTS.md`, `README.md`, `docs/verification.md` | Correct canonical source and working-directory guidance | Low |
| `tools/mediarecover/source-files.json`, `scripts/source-manifest.ps1`, `SOURCE-PROVENANCE.json` | Local owner/directory metadata and refreshed content hashes | Low |
| `tests/tooling/mediarecover-snapshot.test.js`, `repository-structure.test.cjs`, `tools-catalog.test.js` (ownership assertions only) | Local edits, no source copies, rejected external arguments, parent-owned tool directories | Low |
| `tests/browser/mediarecover.spec.js` | Canonical-source notice/provenance expectations; retain download/privacy coverage | Low |

## 3. Verification evidence

| Check | Result | Exact command / method | Evidence / notes |
|---|---|---|---|
| Build | PASS | `pnpm build` | Production build, TypeScript and standalone source assets generated successfully after local provenance refresh |
| Lint | PASS | `pnpm lint` | Full configured ESLint gate |
| Formatting | PASS | `pnpm exec prettier --check packages\portal\src\components\site-footer.tsx packages\portal\src\app\tools.css packages\portal\src\data\tools.ts scripts\sync-mediarecover-source.mjs tests\tooling\mediarecover-snapshot.test.js tests\tooling\tools-catalog.test.js tests\tooling\repository-structure.test.cjs tests\browser\site-footer.spec.js tests\browser\mediarecover.spec.js` | All task-owned configured code paths passed |
| Typecheck | PASS | `pnpm typecheck` | Portal strict TypeScript check |
| Unit / integration | PASS | `node --test tests\tooling\mediarecover-snapshot.test.js tests\tooling\tools-catalog.test.js tests\tooling\repository-structure.test.cjs` | 24/24 passed, including exact ZIP/source bytes and local ownership |
| Repository checks | PASS | `pnpm check:repository` | Read-only syntax checks and 12 existing structure tests passed; subsequently added ownership test also passes in the 24-test command above |
| Browser / accessibility | PASS | `pnpm exec playwright test site-footer.spec.js mediarecover.spec.js` | Final run 21/21 in Chromium, Firefox and WebKit; footer axe checks in both themes and Media Recover's existing accessibility/privacy/download checks |
| Visual / size | PASS | Browser inspection of live/local footers and generated desktop/mobile screenshots | Local height 69.6 px; centered credit, bottom-right link and no horizontal overflow |
| Local provenance | PASS | `node scripts\sync-mediarecover-source.mjs` | 48 local text files hashed; source files not copied; subsequent build validates the manifest |
| Security / dependency scan | PASS | `pnpm check:source` | 320 paths checked by the repository's bounded scanner; no dependencies, permissions, hosted processing or automatic network activity added |
| Patch whitespace | PASS | `git diff --check` | No whitespace errors; Git reports existing line-ending conversion warnings |
| Initial browser attempts | FAIL, resolved | Earlier runs of `pnpm exec playwright test site-footer.spec.js` and the combined browser command | Fixed pseudo-element animation enumeration and normalized keyframe-property assertions; WebKit keyboard test uses explicit focus because default platform Tab navigation skips native links. Missing generated ZIP files during an overlapping build disappeared after rebuilding |
| Initial ownership unit attempt | FAIL, resolved | `node --test tests\tooling\mediarecover-snapshot.test.js tests\tooling\tools-catalog.test.js` before rebuilding | Old generated download bytes did not match newly edited source; rebuild regenerated them and all exact-byte assertions passed |
| Initial Git submodule inspection | FAIL, alternative passed | `git submodule status` | Host Git/sh could not fork; direct `git ls-files --stage` plus fixture-tested ownership checks confirmed no tool gitlinks or nested root `.git` entries |
| Full aggregate gates | NOT RUN | `pnpm check`, full `pnpm test`, unfiltered `pnpm test:browser`, `pnpm format:check` | Focused coverage plus full lint/build/types/source checks were used; unrelated in-progress work remains outside this task |
| Native Media Recover runtime / hardware | NOT RUN | Native build, synthetic executable, WPF, physical phone and signing tests | No application runtime code changed; earlier Windows Application Control/runtime blockers remain documented and were not bypassed |
| Independent certification / release | N/A | Not authorized or claimed | Self-audit only; no publishing/deployment/remote actions |

## 4. Risk and quality review

- Public contracts/defaults: requested visual change and explicit ownership
  correction. Native app APIs, source licenses, tool IDs/routes, unpublished
  status and opt-in service defaults are unchanged.
- Security/privacy: plain native anchor activates only on user action; SVG/CSS
  are local and CSP-safe. No telemetry, hosted work, remote creation or automatic
  external requests. Hash refresh validates before writing only local provenance.
- Accessibility: native links, decorative hidden SVGs, accessible projects name,
  white focus outline, minimum 24 px mobile targets, no overflow, reduced-motion
  animations/transitions disabled, both themes checked. Sequential Tab was
  verified in Chromium/Firefox; WebKit's default platform setting skips links,
  so explicit focus plus Enter/focus-visible are tested there.
- Disabled-feature behavior: footer and ownership changes start no tool service,
  filesystem scan, native process, poller or connection.
- Errors/recovery: invalid external CLI inputs, unsafe allowlist paths, linked
  paths, binary text and stale hashes produce explicit errors. No source files
  are copied/overwritten by the revised refresh command.
- Performance: CSS-only one-shot animations, small reused inline logo, no client
  state/listeners/dependencies for the footer. No performance certification claimed.
- Unrelated existing and concurrently arriving edits remain unstaged, including
  shared-file hunks. The task does not claim those edits are verified or complete.

## 5. Findings and residual risks

| ID | Severity | Finding and evidence | Mitigation / owner | Status |
|---|---|---|---|---|
| R1 | Info | Existing native desktop runtime/hardware acceptance remains incomplete | Keep preview/unpublished warnings; separate native acceptance work | Unchanged, outside this task |
| R2 | Info | Working tree contains unrelated/concurrent portal changes | Stage only explicit task files and selected ownership/footer hunks | Preserved |
| R3 | Info | An overlapping build initially removed standalone source ZIPs during browser checks | Rebuilt complete assets and reran all 21 targeted browser tests successfully | Resolved for verification |

- Known gaps: no full repository-wide browser/unit aggregate or independent
  accessibility/security certification; no native runtime claim.
- Required approvals: none for these requested reversible changes; repository
  splits, deployment, remote actions and releases still require approval.
- Rollback: revert the focused task commit and rebuild source assets. No user
  source directories were moved/deleted and no remote configuration changed.
- Release readiness: `NOT APPLICABLE`; this is local UI/source-ownership work,
  not authorization to publish the portal or the native preview.

## 6. Completion record

- Audit created before the scoped task commit and included in that commit.
- Final commit hash: reported in task completion after creation.
- External actions: `NOT AUTHORIZED`; no push, merge, repository creation,
  publishing, deployment, signing or prerequisite installation performed.
- Temporary preview helpers are stopped; browser downloads/fixtures use isolated
  test storage and fixture-owned cleanup.

**Limitations:** An agent self-audit is not an independent security review,
accessibility certification or release approval.
