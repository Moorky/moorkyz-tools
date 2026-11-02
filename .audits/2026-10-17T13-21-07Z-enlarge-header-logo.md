# Development Audit: Enlarge the tools.moorkyz.com header logo

| Field | Value |
|---|---|
| Audit ID | `2026-10-17T13-21-07Z-enlarge-header-logo` |
| Created (UTC) | `2026-10-17T13:21:07Z` |
| Task | Make the tools.moorkyz.com website header logo much bigger |
| Auditor | Copilot agent self-audit |
| Branch / baseline | `master` / `d908498` |
| Final commit | Reported in task completion; no post-commit audit amendment |
| Scope reviewed | Shared header, existing branding assets, responsive header tests and portal guide |
| Outcome | PASS for this presentation change; existing release holds unchanged |

## 1. Request and outcome

Confirmed the target with the user: this project's header logo, not another
project. Increased the image from 28x28px to 56x56px (twice each dimension,
four times the rendered area). Reused the matching existing 192x192px asset
instead of upscaling the 32px favicon.

The shared header applies this consistently on home, Studio and contribution
routes. No wordmark, navigation action, footer, favicon, Playground width or
SDK behavior changed. Explicit image dimensions reserve layout space. Existing
responsive wrapping keeps navigation available on narrow screens.

## 2. Change inventory

| Path | Change | Risk |
|---|---|---|
| `packages/portal/src/components/site-header.tsx` | Larger image with existing higher-resolution source | Low |
| `tests/browser/website.spec.js` | Actual size, asset decode, containment, navigation, focus and header axe checks | Low |
| `packages/portal/README.md` | Document rendered size and local asset | Low |
| This report | Scope, observed verification and limitations | Low |

Preserved pre-existing launcher/config changes and the generated
`packages/portal/next-env.d.ts` development-route imports. The production build
regenerated that file; its initial development imports were restored afterward
and it is excluded from the task commit.

## 3. Verification evidence

| Check | Result | Exact command / method | Evidence |
|---|---|---|---|
| Formatting / lint | PASS | Targeted `pnpm exec prettier --write`; `pnpm exec eslint packages\portal\src\components\site-header.tsx tests\browser\website.spec.js`; final `pnpm format:check` | Existing tooling |
| Types / production build | PASS | `pnpm build`; portal typecheck after restoring generated declaration | SDK/Next production build and portal TypeScript |
| Browser / accessibility | PASS | Command below | 6 passed, Chromium/Firefox/WebKit; no retries/skips |
| Rendered inspection | PASS | Reviewed attached header screenshots at 320px and 1440px | Sharp larger logo; desktop header stays compact; 320px navigation wraps rather than clips |
| Unit / full SDK coverage / dependency audit | NOT RUN | Presentation-only change | No SDK logic, dependency, schema, permissions or persistence changes |
| Whitespace | PASS | `git diff --check`; staged whitespace gate | No task-owned whitespace errors |

```powershell
$env:TOOLS_MOORKYZ_COM_FIXTURE_PORT='45973'
$env:TOOLS_MOORKYZ_COM_PORTAL_PORT='45974'
$env:CI='1'
pnpm exec playwright test 'website\.spec\.js' --grep 'header logo|adaptive header' --workers=3
```

The new case checks all three routes at 320/390/768/1440px, actual rendered
56px dimensions, the decoded 192px source, containment, visible navigation,
home-link focus and header axe findings. The existing adaptive-header/reflow
case also passes. No hidden controls or relaxed size assertions were used.

## 4. Risk and quality review

- Accessible home-link text and decorative image alt treatment remain unchanged.
- Same-origin, already-owned branding asset; no new dependency or remote request.
- Fixed dimensions preserve image layout space; the 320px header may use two
  rows through the existing responsive layout.
- No listeners, permissions, optional resources, data or stored preferences changed.
- The logo file is larger than the favicon, intentionally reusing the existing
  higher-resolution asset for sharp 56px rendering.

## 5. Findings and residual risks

No task-caused failures remain in the checks above. This focused change does not
re-certify whole-project performance, independent security or manual WCAG/device
acceptance. M10/M11 holds remain unchanged.

Rollback: revert this focused task commit. No data migration or external action
occurred. Release readiness remains NOT READY / NOT AUTHORIZED under the dossier.

## 6. Completion record

Audit created before and included with the focused local commit. Final hash is
reported in completion. No push, deploy, publication, signing, spending,
history rewrite or change to another repository.

This self-audit is not independent security or accessibility certification.
