# Development Audit: Remove domain from hero

| Field | Value |
|---|---|
| Audit ID | `2026-10-26T18-42-38Z-remove-domain-from-hero` |
| Created (UTC) | `2026-10-26T18:42:38Z` |
| Task / issue | Remove `tools.moorkyz.com` from the homepage hero |
| Auditor | Copilot agent self-audit |
| Branch / baseline commit | `master` / `4480bfa9f512d8a472972b25416d86845dd99fca` |
| Final commit | Report in task completion; do not edit this audit after committing |
| Scope reviewed | Homepage hero label and its browser assertion |
| Outcome | `PASS` |

## 1. Request and outcome

- Requested behavior: Remove `tools.moorkyz.com` from the hero section.
- Implemented behavior: The hero no longer renders the domain eyebrow. A browser test verifies that neither the eyebrow nor the exact domain text appears in the hero.
- Acceptance criteria and evidence: `pnpm build` passed; the focused Chromium browser run passed all 6 tests, including the absence checks.
- Intentional exclusions: Other project branding, metadata, navigation, and domain references remain unchanged.
- Relevant decisions/contracts: `AGENTS.md` and `docs/decisions/decision-register.md`.

## 2. Change inventory

| Path | Change | Risk |
|---|---|---|
| `packages/portal/src/app/page.tsx` | Remove the project-domain eyebrow from the hero. | Low; presentation-only |
| `tests/browser/homepage-refinements.spec.js` | Assert that the hero has no domain label and remove the obsolete spacing measurement. | Low; focused regression coverage |

The page and browser test already had unrelated uncommitted work before this task. The task commit stages only the requested hero-label deletion and audit; the test file is retained in the worktree without staging its existing additions.

## 3. Verification evidence

| Check | Result | Exact command / method | Evidence / notes |
|---|---|---|---|
| Formatting / lint | `PASS` | `pnpm exec prettier --check packages/portal/src/app/page.tsx tests/browser/homepage-refinements.spec.js`; `pnpm exec eslint packages/portal/src/app/page.tsx tests/browser/homepage-refinements.spec.js` | Both completed successfully. |
| Typecheck | `PASS` | `pnpm build` | Next.js production build completed its TypeScript phase successfully. |
| Unit / integration tests | `N/A` | Not run | No unit behavior changed. |
| Browser / accessibility | `PASS` | `pnpm exec playwright test tests/browser/homepage-refinements.spec.js --project=chromium` | 6 tests passed; homepage test includes accessibility analysis and hero absence checks. |
| Build / consumer fixture | `PASS` | `pnpm build` | Optimized production build completed. |
| Security / dependency review | `N/A` | Not run | No security, data-handling, or dependency changes. |
| Performance (if relevant) | `N/A` | Not run | No runtime behavior or performance-sensitive logic changed. |
| `git diff --check` | `PASS` | `git diff --check` | No whitespace errors. |

## 4. Risk and quality review

- Public contract, feature-default, and compatibility impact: No public API or behavior contract changed; only hero presentation changed.
- Security, privacy, permissions, and secrets: No impact.
- Accessibility, keyboard, and reduced motion: Existing keyboard, reduced-motion, and Axe checks passed in the focused browser test.
- Disabled-feature work/resource/network behavior: No impact.
- Error handling, cleanup, and recovery: No impact.
- Dependency, performance, and regression exposure: No dependencies added; a focused browser assertion covers the requested state.

## 5. Findings and residual risks

| ID | Severity | Finding and evidence | Mitigation / owner | Status |
|---|---|---|---|---|
| — | — | No findings. | — | Closed |

- Known gaps / checks not run: The browser test was run on Chromium only.
- Required approvals: None.
- Rollback or recovery: Revert the focused hero-label deletion.
- Release readiness: `NOT APPLICABLE`

## 6. Completion record

- Audit created before the task commit and included in that commit.
- Final commit hash: `PENDING`
- External actions: `NOT AUTHORIZED`.

**Limitations:** An agent self-audit is not an independent security review, accessibility certification, or release approval.
