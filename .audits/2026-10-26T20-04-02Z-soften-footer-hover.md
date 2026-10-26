# Development Audit: soften footer hover

| Field | Value |
|---|---|
| Audit ID | `2026-10-26T20-04-02Z-soften-footer-hover` |
| Created (UTC) | `2026-10-26T20:04:02Z` |
| Task / issue | Footer hover highlight is too bright |
| Auditor | Copilot coding agent; self-audit |
| Branch / baseline commit | `master` / `7b199cbdc4f3edc38e1f17d69cdac73c7e03edef` |
| Final commit | Reported at completion; audit not edited afterward |
| Scope reviewed | Footer hover/focus CSS, related browser assertions and documentation |
| Outcome | `PASS` |

## 1. Request and outcome

- Interpreted the request as the footer-link highlight, continuing the current
  footer refinement task. Inspected the tagged homepage and its shared styles.
- Replaced three full-opacity shadow layers plus a drop-shadow filter with
  one 8 px blue-white text shadow at 40% opacity.
- Both side links retain the same subtle hover/focus feedback and 180 ms
  transition; the white keyboard outline remains unchanged.
- Layout, URLs, band height, lift, center credit, fade/glint and reduced-motion
  behavior are unchanged. No new dependencies or runtime logic.
- Updated regression assertions to require the exact restrained shadow and no
  filter on hover/focus. Updated current footer documentation.
- Contracts: root AGENTS, portal instructions and shared-footer guide.

## 2. Change inventory

| Path | Change | Risk |
|---|---|---|
| `packages/portal/src/app/tools.css` (footer hunks only) | Lower-opacity single-layer highlight, no filter | Low |
| `tests/browser/site-footer.spec.js` | Exact hover/focus appearance assertions and hover screenshot | Low |
| `docs/portal/modular-tools.md` (footer paragraph only) | Document restrained highlight | Low |
| This audit | Observed verification evidence | Low |

## 3. Verification evidence

| Check | Result | Exact command / method | Evidence / notes |
|---|---|---|---|
| Build / typecheck | PASS | `pnpm build` | Production build includes strict TypeScript validation |
| Browser / accessibility | PASS | `pnpm exec playwright test site-footer.spec.js` | 12/12 tests across Chromium, Firefox and WebKit; routes, exact highlight, focus/Enter, navigation, geometry, reduced motion and both-theme axe checks |
| Visual review | PASS | Viewed generated Chromium `footer-subtle-contact-hover.png` | Softer glow retains readable white text without the large halo |
| Lint | PASS | `pnpm exec eslint tests\browser\site-footer.spec.js` | Focused changed JavaScript lint |
| Formatting | PASS | `pnpm exec prettier --check packages\portal\src\app\tools.css tests\browser\site-footer.spec.js` | Both changed configured code paths pass |
| Patch whitespace | PASS | `git diff --check` | No whitespace errors; existing EOL warnings only |
| Unit / native tests | N/A | No runtime/native logic changed | Visual behavior covered by production browser checks |
| Full aggregate suite | NOT RUN | `pnpm check`, unfiltered browser/unit suites | Focused gates cover this CSS-only refinement |
| Security / dependency / performance review | N/A | Source inspection | No dependencies, data handling, permissions or network changes; fewer rendering effects |

## 4. Risk and quality review

- Public/default impact: requested visual intensity reduction only.
- Security/privacy/disabled behavior: no new network activity, service or state.
- Accessibility: keyboard focus outline unchanged; hover/focus effects match;
  reduced-motion preferences preserved; automated footer axe checks pass.
- Error handling/cleanup: no new asynchronous paths; Playwright owns its server
  and ignored screenshots. No independent certification claimed.
- Regression exposure: all six shared routes, five widths and both themes tested;
  unrelated existing edits, including new tool work, remain untouched/unstaged.

## 5. Findings and residual risks

| ID | Severity | Finding and evidence | Mitigation / owner | Status |
|---|---|---|---|---|
| R1 | Info | Shared stylesheet/docs contain unrelated work | Stage only task-owned hunks | Preserved |

- Known gaps: no full aggregate suite or manual assistive-technology certification.
- Required approvals: none for this requested reversible UI adjustment.
- Rollback/recovery: revert the focused commit and rebuild.
- Release readiness: `NOT APPLICABLE`; local change, not deployment approval.

## 6. Completion record

- Audit included in the focused local commit; hash reported at completion.
- External actions: `NOT AUTHORIZED`; no push, publishing or deployment.

**Limitations:** Self-audit is not independent security review, accessibility
certification or release approval.
