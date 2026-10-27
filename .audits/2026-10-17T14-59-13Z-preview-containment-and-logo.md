# Development Audit: Contain the preview and replace header artwork

| Field | Value |
|---|---|
| Audit ID | `2026-10-17T14-59-13Z-preview-containment-and-logo` |
| Created (UTC) | `2026-10-17T14:59:13Z` |
| Task | Explain/remove the blank grey development button, fix the escaping preview toolbar, retain customization and use the supplied larger logo |
| Auditor | Copilot agent self-audit |
| Branch / baseline | `master` / `5332278` |
| Final commit | Reported in task completion; not amended into this report afterward |
| Scope reviewed | Actual development page, Next configuration/CSP, preview positioning/lifecycle, header artwork, affected regressions |
| Outcome | PASS for the scoped fixes; M10/M11 holds unchanged |

## 1. Request and outcome

### Blank grey rectangle

Inspected the user's actual development page at `http://localhost:3001`.
The rectangle was the Next.js Dev Tools indicator: its nonce-less development
styles were blocked by the portal's strict CSP, leaving an unstyled button.
Set the installed framework's documented `devIndicators: false` option.
Compile/runtime errors remain available. No CSP, origin, script or style
permission was relaxed and no blanket CSS error suppression was added.
The final local browser inspection found no visible development buttons at the
footer; its screenshot is retained in session artifacts.

### Preview toolbar and controls

Reproduced an 876x474px window where the zero-height sticky preview mount was
constrained at the application's top, painting its actual toolbar above the
application and into the fixed page dock.

Added a real, sized footer inside an application stage below the app header.
Clipped preview rendering to its application; a frame-coalesced guard hides and
makes inert any preview controls whose measured bounds would escape their
stage/viewport or collide with the page dock. It handles delayed SDK mounts,
rerenders, geometry changes, scrolling, resizing, stylesheet load and visibility
restoration. Observers/listeners/pending frames are released on unmount; there
is no continuous polling.

The user explicitly selected **keep customization**. The page dock's existing
customization, theme/collapse preferences and the Playground editor are
retained. No navigation-only redesign, SDK/API/default change or stored-design
migration was made.

### Supplied logo and visible size

Copied the supplied read-only PNG unchanged to
`packages/portal/public/tools.moorkyz.com-logo.png`. Its SHA-256 is
`a1124b2c62a4ecde77df8006b37c3a5730500966c6d664141c7b925f67396356`;
intrinsic size is 512x512px. The attachment itself was not modified.

Initially retained the requested 64px rendering. The user then said the logo
was still too small. The source has substantial empty canvas around its mark;
the frame was increased to 128x64px, using centered cover rendering to trim empty
top/bottom canvas rather than distort the artwork or create a 128px-tall header.
The user's final request was to make it a little smaller: the final frame is
**112x56px**, a 12.5% reduction with the same crop/proportions.
Desktop and 320px phone screenshots confirm the mark itself is visibly larger.
No favicon/footer brand or other-project logo was changed.

## 2. Change inventory

| Paths | Change | Risk |
|---|---|---|
| Portal `next.config.ts` | Disable only the optional development indicator | Low |
| Portal application/editor and Playground CSS | Real stage/footer, clipping, viewport guard and owned cleanup | Medium |
| New `use-preview-dock-visibility.ts` | Measured visibility, resize/mutation/load/scroll handling | Medium |
| Header, supplied PNG and website CSS | Exact supplied artwork and larger visible, fixed-size frame | Low |
| Browser tests | Short-window containment, preserved customization, delayed mount/fallback, source/size and appropriate scrolling before visibility assertions | Low |
| Portal README and this report | Current behavior and actual evidence | Low |

Preserved unrelated launcher/config edits, the pre-existing generated
`next-env.d.ts` development imports, and the separate user-owned untracked
`public/tools.moorkyz.com.png` (same image bytes). Those paths are excluded.
The user edited the header dimensions during the task; the final dimensions
implement the later explicit request to enlarge the visible supplied mark.

## 3. Verification evidence

To avoid deleting the SDK output beneath the user's running dev server, used
the existing already-built, unchanged SDK and built only the portal:

```powershell
Push-Location packages\portal
node scripts\run-next.mjs build
node scripts\prepare-standalone.mjs
Pop-Location
$env:TOOLS_MOORKYZ_COM_FIXTURE_PORT='47973'
$env:TOOLS_MOORKYZ_COM_PORTAL_PORT='47974'
$env:CI='1'
pnpm exec playwright test --workers=3
```

| Check | Result | Exact command / method | Evidence |
|---|---|---|---|
| Production portal build | PASS | Commands above | Optimized Next build and TypeScript; standalone assets prepared |
| All browser regressions | PASS | Command above | 255 passed before the final 12.5% cosmetic logo reduction; 85 each Chromium/Firefox/WebKit; zero failures, skipped or flaky |
| Final cosmetic sizing | PASS | Production rebuild and focused command below after reduction | 15 passed across three engines; exact 112x56px dimensions, navigation, axe and short-window preview controls |
| Node regression suite | PASS | `node --test` | 203 passed, zero failures/skips |
| Lint | PASS | `pnpm lint`; `pnpm exec eslint packages\portal\next.config.ts` | Existing repository gate plus changed config |
| Format | PASS | `pnpm format:check`; targeted Prettier writes | No rules weakened |
| Portal typing | PASS | `pnpm --filter @tools.moorkyz.com/portal typecheck` | Also passed after restoring original generated development imports |
| Actual development page | PASS | Headless local Chromium at `http://localhost:3001` with hydration awaited | No visible Dev Tools button; page customization/Done work; preview contained in short window |
| Supplied artwork | PASS | PNG signature dimensions, SHA-256 and rendered bounds | Exact 512px attachment bytes; final 112x56px rendered frame at desktop and phone widths |
| Visual inspection | PASS, bounded | Reviewed desktop/320px logo, short development preview and clean footer screenshots | No new artwork or page-dock clipping observed in those renders |
| Bounded source scan | PASS | `pnpm check:source` | 254 selected paths including this audit; no selected credential/library sink findings |
| Whitespace | PASS | Scoped/staged `git diff --check` before commit | Normalization warnings only |
| Fresh SDK coverage, dependency audit and all-framework typechecks | NOT RUN | No SDK/dependency/public contract changes | Prior QA evidence remains historical; no fresh certification claimed |

The final browser suite includes the new source/size checks, short 876x474 and
1024x600 windows, mobile containment, page customization, no-ResizeObserver
behavior, all existing geometry/fullscreen/keyboard/axe/consumer cases, and the
restored original Playground gutters.

Final focused command after the last sizing request (same isolated ports):

```powershell
pnpm exec playwright test 'website\.spec\.js|preview-containment\.spec\.js' --grep 'header uses|adaptive header|framework development|preview toolbar stays|preview containment still' --workers=3
```

### Failures investigated

- An initial attempt to test the live development app through `127.0.0.1`
  instead of its recorded `localhost` host failed hydration; the dev log reported
  cross-origin development-resource blocking. Did not broaden allowed origins.
  Retested the actual recorded localhost URL with readiness awaited.
- The first visibility implementation could run before the SDK controller
  created its nodes. The final guard observes owned child-list changes and
  reacquires the current toolbar instead of retaining a missing/stale node.
- Existing tests assumed an offscreen preview remained accessible. Tests now
  scroll to the application before interaction/visible assertions, while mount/
  navigation measurements use actual attached-state checks. Viewport exclusion
  does not weaken any authorization or accessibility checks.
- The initial broader scan found a scrollable-region focus issue during a
  fullscreen/viewport transition. Explicitly scrolled/awaited the stable,
  interactive preview before its axe scan; the final unchanged accessibility
  thresholds and full suite pass.

## 4. Risk and quality review

- No SDK, feature-default, permission, schema, generator or persistence changes.
- Strict CSP remains enforced; the grey-box fix is a supported development-only
  framework setting, not `unsafe-inline` or an origin-policy exception.
- Offstage preview controls become inert rather than visually escaping or
  intercepting pointer/keyboard input. Page customization remains available.
- New lifecycle work is portal-owned and event-driven, frame-coalesced, and
  cleaned up. No timers/polling or disabled SDK subsystem activation.
- Image is user-provided and served locally, without uploads or external
  optimization services. Fixed dimensions reserve layout space.
- The live development server was not stopped or replaced. Test servers were
  owned and cleaned by Playwright. Generated declaration imports were restored
  to their initial development paths and not staged.

## 5. Findings and residual risks

No task-caused failure remains in the checks above. This is not a claim of
whole-device/browser coverage or independent WCAG/security certification.
M10/M11 manual device/AT, independent/distribution review, container evidence and
release approval remain unchanged.

Rollback: revert this focused commit if requested. No user data migration or
external action occurred. Release readiness: NOT READY / NOT AUTHORIZED under
the existing release dossier.

## 6. Completion record

Audit created before and included in the local task commit; final hash is
reported in completion. No push, deployment, publication, signing, spending,
cross-project changes or rewrite of earlier history.

**Limitations:** Scoped self-audit, not independent security review, manual
accessibility certification or release approval.
