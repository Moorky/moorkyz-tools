# Development Audit: Website and functional Playground first vertical slice

> Evidence below is observed locally. This is an agent self-audit.

| Field | Value |
|---|---|
| Audit ID | `2026-10-17T03-29-13Z-website-playground-first-slice` |
| Created (UTC) | `2026-10-17T03:29:13Z` |
| Task / issue | Begin the website and functional Playground MVP after repository assessment and phased planning |
| Auditor | Copilot agent; local tools, Node and Playwright; not independent certification |
| Branch / baseline commit | `master` / `e84bc37` |
| Final commit | Reported in task completion; this report is not edited after committing |
| Scope reviewed | Portal shell, first Workspace Playground, personalized Vanilla integration, related tests and status documentation |
| Outcome | `PASS WITH RISKS` for the first vertical slice only |

## 1. Request and outcome

- Inspected actual SDK exports, configuration and theme contracts, React mount
  lifecycle, shared generator, Next.js portal/labs, browser tests, product
  specification, decision register and release dossier before implementation.
  Consulted the installed Next.js client-boundary guide.
- Presented the repository assessment and five-phase plan before editing:
  first end-to-end Workspace slice; four applications and 8–12 structural styles;
  editor/geometry/workspace completion; remaining website composition and real
  explorers; MVP hardening while retaining M10/M11 release holds.
- Added the specified compact hero/copy, existing favicon branding, adaptive
  header, skip link and neutral light/violet dark semantic palette.
- Centralized the existing local theme preference. The site navigation, new
  application preview, embedded SDK and generated configuration follow that
  preference without replacing the selected built-in token family.
- Embedded the real `ToolbarMount` adapter with an owned controller, stable host
  callbacks and validated updates rather than remounting on configuration edits.
  The host implements task creation focus, completed-item visibility and clearing
  completed sample tasks. The task application is keyboard accessible, bounded
  to 50 page-local records, and independently resettable.
- Added the eight ordered, mutually exclusive settings sections. Only supported
  fields are editable. Future functionality is described, not represented by
  inert controls or invented SDK configuration keys.
- Added configuration-only history (up to 50 past snapshots), reset, confirmed
  restore/replacement, explicit local save, corrupt/future-version rejection,
  visible storage errors and last-valid-preview behavior. Unapplied JSON is
  preserved when switching accordion sections and gates visual edits/history
  until applied or discarded. No task data or commands are replayed by undo.
- Connected Quick Start to the existing Vanilla generator. Exports contain
  generated source files and integrity metadata in portable versioned JSON.
  Stale async results cannot replace current output or enable stale exports.
  The integration test executes the exported files with the actual SDK.
- Relevant decisions: Q07, Q15, Q18–21, Q28, Q35, Q37–45, Q48, Q52–55,
  Q63–64, Q74, Q79–80. Public configuration and generator contracts remain
  unchanged and in draft status.

## 2. Change inventory

| Path | Change | Risk |
|---|---|---|
| `packages/portal/src/app/page.tsx` | Playground-first hero and personalized Quick Start replace static sample; existing labs retained | Medium: page composition |
| `packages/portal/src/app/layout.tsx`, `globals.css`, `playground.css` | Semantic palette, viewport colors, compact hero, bounded responsive workspace and visible focus | Medium: shared presentation |
| `packages/portal/src/components/portal-shell.tsx` | Shared theme context and synchronized navigation color preference | Medium: existing theme behavior |
| `packages/portal/src/components/site-header.tsx` | Adaptive scroll/focus behavior and listener cleanup | Low |
| `packages/portal/src/components/playground-model.ts` | Shared SDK validation plus constrained host policy and versioned draft parsing | Medium: untrusted data boundary |
| `packages/portal/src/components/functional-playground.tsx` | Local task application, actual SDK preview, history, local draft recovery and personalized generation/export | Medium |
| `tests/browser/playground.spec.js` | Eight new observable-behavior tests across all three engines | Low |
| `tests/browser/portal.spec.js` | Expected dark navigation background follows the new approved visual direction | Low |
| `tests/tooling/playground-model.test.js` | Three boundary tests, including UTF-8 size limit and capability restrictions | Low |
| Portal README, portal specification, release dossier | Accurate slice status, workflow, remaining MVP work and unchanged release holds | Low |

Unrelated pre-existing edits to `tools.moorkyz.com`, `tools.moorkyz.com.cjs`, `tools.moorkyz.com.cmd`,
`tools.moorkyz.com.config.json` and untracked `docs/task/` were not staged or changed.
Generated `next-env.d.ts` route imports temporarily changed during validation
and returned to the initial tracked paths without an authored edit.
No SDK source, package exports, manifests, lockfiles or dependencies changed.

## 3. Verification evidence

The exact browser command (B1) avoids interpreting Windows paths as regexes:

```powershell
pnpm exec playwright test 'playground\.spec\.js|portal\.spec\.js|studio\.spec\.js' --workers=3
```

| Check | Result | Exact command / method | Evidence / notes |
|---|---|---|---|
| Formatting | PASS | `pnpm exec prettier --write` on changed source/test files; `pnpm format:check` | Existing repository format gate passed |
| Lint | PASS | `pnpm lint` | Existing repository lint gate passed |
| Typecheck | PASS | `pnpm typecheck` | SDK, Vanilla, React, Vue, Angular and portal passed |
| Unit / generator | PASS | `node --test tests\tooling\playground-model.test.js packages\sdk\test\generator.test.js` | 8 passed, 0 failures/skips; 3 new model tests plus 5 existing generator tests |
| Browser / accessibility / existing portal | PASS | B1 above | Final JSON report: 75 expected, 0 unexpected, 0 flaky, 0 skipped; 25 each in Chromium/Firefox/WebKit; 24 executions of 8 new cases |
| Build | PASS | `pnpm build` | SDK build, optimized Next.js production build and standalone preparation passed |
| Executable integration | PASS | Exported-project browser case within B1 | Download personalized files, mount real SDK, dispatch `add-task`, verify host status, dispatch `pagehide`, verify toolbar disposal; all three engines |
| CSP / network / host policy | PASS, bounded | Existing portal CSP regression, no-external-request assertions, JSON/command boundary tests | No CSP weakening, arbitrary JavaScript execution, feature activation or host-command escalation added |
| Source scan | PASS, bounded | `pnpm check:source` | 219 selected repository paths in the observed pre-audit run; no selected credential-pattern or forbidden library sink findings; not a comprehensive scan |
| Responsive geometry | PASS | Browser assertion after settled resize | Desktop workspace <=610 CSS px (observed 608 px); 360 px viewport has no document overflow; keyboard and reduced motion checked |
| Visual inspection | PASS, limited | Temporary standalone server on `127.0.0.1:4186`, HTTP 200 readiness, integrated browser screenshots | Inspected 1440 px desktop/light and 360 px mobile/dark; temporary process stopped afterward |
| Whitespace | PASS | Scoped `git diff --check` | No whitespace errors; Git emitted only line-ending normalization warnings |
| Aggregate full suite / coverage / dependency audit | NOT RUN | `pnpm check`, `pnpm test:coverage`, `pnpm audit` | Focused affected-surface checks used instead. SDK coverage and release evidence were not re-certified; dependencies and SDK code unchanged |
| Manual AT/device / independent review / container / approved performance budgets | NOT RUN | Existing M10/M11 acceptance workflow | Remains required; no release readiness or WCAG certification claim |

### Failures investigated and resolved

- The editor test tool did not discover the Node tests; the actual Node test
  runner above executed them successfully.
- Backslash filesystem paths are not valid Playwright regex selectors. The
  first command selected no tests; B1 correctly selected the intended specs.
- The initial browser run found a brand accessible-name mismatch in axe.
  Removed the unnecessary overriding label and preserved the visible space;
  the complete focused cross-browser suite then passed.
- Visual inspection found an excessively tall default workspace. Added a
  608 px desktop workspace with separately scrollable settings/content and
  natural stacked mobile layout, then re-ran affected browser checks.
- One resize assertion initially sampled WebKit before its media/layout update
  settled (74/75). A separate eight-transition experiment reproduced transient
  widths of 509 px before settling to 360 px. The assertion now polls the
  actual document width, without sleeps or relaxed thresholds; the final full
  focused run passed all 75 tests with no retries/flaky results.

## 4. Risk and quality review

- **Contracts/defaults:** no public SDK/API, feature-default or adapter changes.
  All optional features in this demonstration are explicitly disabled, including
  spatial coordination. Existing Studio experimentation is retained.
- **Security/permissions:** parse JSON as data through SDK validation and the
  portal's three-command allowlist. Reject feature activation and placement or
  renderer escape. Bound input to 64 KiB of UTF-8 data and names to documented
  lengths. Imported text is rendered as text, not markup.
- **Privacy:** only explicit configuration saves and the pre-existing theme/
  navigation preferences use local storage. Task titles stay in page memory.
  No telemetry, remote service, new permission, account or upload introduced.
- **Accessibility:** semantic controls, labeled inputs, single-section accordion,
  reachable scroll areas, keyboard SDK actions, focus-revealed header, skip link
  and reduced-motion support. Automated axe checks include light/dark and mobile.
  Automation is not manual WCAG 2.2 AA acceptance.
- **Cleanup:** SDK adapter owns controller disposal; scroll listener removed on
  unmount; generation results ignored after cancellation/supersession; temporary
  download object URLs revoked; temporary inspection server stopped.
- **Recovery:** invalid edits preserve the last valid preview/export; draft
  corruption/unsupported versions never overwrite stored data; replacement and
  reset are explicit; storage errors have visible in-memory/export guidance.
- **Performance:** no new runtime dependency, network demand or background timer.
  Generation reacts to configuration/theme changes, not task edits or polling.
  Numerical performance budgets remain unapproved and unmeasured for this slice.

## 5. Findings and residual risks

| ID | Severity | Finding and evidence | Mitigation / owner | Status |
|---|---|---|---|---|
| W01 | MVP scope | One application and one pill structure, not four applications and 8–12 structural styles | Next template/style vertical slice; six token families are not counted as structural styles | OPEN |
| W02 | MVP scope | Manual variant colors, geometry edit mode/offsets/shapes, full action operations, sidebar resizing and fullscreen remain unavailable | Continue editor/workspace slices; current UI labels these limitations explicitly | OPEN |
| W03 | MVP scope | Remaining website ordering, compact dedicated Studio entry/route, capability/architecture explorers, two-tier footer and vendored DM fonts remain pending | Website completion slice; retain working labs and real assets meanwhile | OPEN |
| W04 | Theme scope | Existing advanced labs retain independent experimental theme controls | Synchronize their custom light/dark variants when completing the Studio shell; do not overwrite experimental palettes | OPEN |
| W05 | Release | Manual accessibility/device acceptance, approved matrix/metrics/budgets, distribution review, independent review, container evidence and release authorization remain outstanding | Existing M10/M11 dossier and owners | HOLD |

- No stable distribution, all-framework generator, complete website MVP,
  independent security review or final production-readiness claim is made.
- Generated integration intentionally supplies a demonstration host, not the
  Playground task application's implementation. Export is portable JSON, not ZIP.
- Further layout polish and the embedded/site dock experience must be reviewed
  with the full responsive/fullscreen workspace before MVP acceptance.
- **Rollback:** revert this task's focused local commit. No SDK data migration,
  external action or deployment occurred. The new local draft namespace can be
  left untouched; previous portal drafts/preferences remain separate.
- **Release readiness: NOT READY.** The first vertical slice is validated, but the
  website MVP and M10/M11 milestone exits are incomplete.

## 6. Completion record

- Audit created before the focused local task commit and included with it.
- Final commit hash: `PENDING` (reported in task completion).
- External actions: `NOT AUTHORIZED`; no push, merge, deployment, publication,
  signing, spending or cross-project integration performed.

**Limitations:** This self-audit is not independent security review,
accessibility certification, distribution clearance or release approval.
