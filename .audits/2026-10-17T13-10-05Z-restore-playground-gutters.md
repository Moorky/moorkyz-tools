# Development Audit: Restore the original Playground gutters

| Field | Value |
|---|---|
| Audit ID | `2026-10-17T13-10-05Z-restore-playground-gutters` |
| Created (UTC) | `2026-10-17T13:10:05Z` |
| Task | Undo the Playground widening after the user identified its instruction as a wrong-chat message |
| Auditor | Copilot agent self-audit |
| Branch / baseline | `master` / `c375bf9` |
| Final commit | Reported in completion; no post-commit audit amendment |
| Scope reviewed | Exact width diff, layout regression, current portal guide and MVP plan |
| Outcome | PASS WITH RISKS; restored layout verified, unrelated intermittent scan recorded below |

## 1. Request and outcome

Restored only `.playground-section`'s original outer margin:
`0 clamp(1rem, 2.5vw, 2.5rem)`, as it was before `c375bf9`.
Kept all other QA fixes, sidebar dimensions, SDK contracts, toolbar geometry,
feature defaults, application state and recovery behavior unchanged.

Replaced the withdrawn wider-gutter assertion with exact original gutter checks
at 320px (16px), 1440px (36px) and 1920px (40px), while retaining page containment
and alignment assertions. Updated current documentation; committed historical
audits and Git history were not rewritten.

## 2. Change inventory

| Path | Change | Risk |
|---|---|---|
| `packages/portal/src/app/playground.css` | Restore one original margin declaration | Low |
| `tests/browser/qa-matrix.spec.js` | Assert original responsive gutters instead of widening | Low |
| Portal README and `docs/engineering/website-mvp.md` | Remove current widening claim and record instruction withdrawal | Low |
| This report | Exact scoped evidence and limitations | Low |

Pre-existing edits to `tools.moorkyz.com`, `tools.moorkyz.com.cjs`, `tools.moorkyz.com.cmd` and
`tools.moorkyz.com.config.json` were preserved and excluded.
No file in the other site's repository was edited.

## 3. Verification evidence

| Check | Result | Exact command / method | Evidence / notes |
|---|---|---|---|
| Formatting | PASS | Targeted `pnpm exec prettier --write` and `pnpm format:check` before commit | Existing formatter only |
| Lint | PASS | `pnpm exec eslint tests\browser\qa-matrix.spec.js` | Changed browser test passes |
| Typecheck / production build | PASS | `pnpm build` | SDK and optimized production Next build, including portal TypeScript checks |
| Browser / accessibility / responsive geometry | PASS on final run | Command below | 27 passed, 9 each Chromium/Firefox/WebKit, no skips or retries |
| Unit / SDK coverage / dependency audit | NOT RUN | Not applicable to this CSS-only rollback | No runtime logic, SDK, dependencies or package contracts changed |
| Whitespace | PASS | `git diff --check`; staged whitespace gate before commit | Normalization warnings only |

```powershell
$env:TOOLS_MOORKYZ_COM_FIXTURE_PORT='44973'
$env:TOOLS_MOORKYZ_COM_PORTAL_PORT='44974'
$env:CI='1'
pnpm exec playwright test 'qa-matrix\.spec\.js|visual-regression\.spec\.js' --workers=3
```

The final targeted run covers restored gutters, ten viewport widths, both
themes, large text, all application/style families and rendered geometry.
No product threshold or assertion was weakened.

## 4. Risk and quality review

- No public API, optional-feature activation, network behavior, data handling,
  permission, dependency or persistence change.
- Exact original CSS value restored rather than a new width design.
- Other QA improvements from `c375bf9` remain intact.
- No additional service was left running; Playwright owned its isolated servers.

## 5. Findings and residual risks

An initial broader run passed 26/27 cases but reported low contrast on the
Chromium contribution-page footer during theme transitions. That route does
not contain the changed Playground selector. No contribution/theme code or test
assertion was changed, and the complete unchanged selection then passed 27/27.
This transient observation is retained for follow-up rather than silently
declared fixed by the gutter rollback.

M10/M11 manual accessibility/device, independent review, container and release
authorization holds remain unchanged. No fresh whole-project or performance
certification is claimed by this narrow correction.

Rollback: revert this focused correction commit if explicitly requested.
Release readiness: NOT READY / NOT AUTHORIZED under the existing release dossier.

## 6. Completion record

Audit created before and included with the focused local commit; the hash is
reported in task completion. No push, deployment, publication, signing,
spending, history rewriting or unrelated revert.

This scoped self-audit is not independent security or accessibility certification.
