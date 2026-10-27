# Development Audit: Website and functional Playground MVP acceptance

> Observed local evidence. This is a self-audit, not independent certification.

| Field | Value |
|---|---|
| Audit ID | `2026-10-17T06-04-23Z-website-mvp-acceptance` |
| Created (UTC) | `2026-10-17T06:04:23Z` |
| Task | Complete the approved website/Playground MVP, not the specification's advanced-platform phases |
| Auditor | Copilot agent; Node, TypeScript, Next.js, Playwright/axe and local rendered-layout inspection |
| Branch / baseline | `master` / `537fb64` (Playground slice), following `02f9601` and SDK baseline `e84bc37` |
| Scope reviewed | Complete landing composition, dedicated Studio/contribution routes, themes/fonts/search/explorers, final Playground hardening, generated consumers, automated acceptance and documentation |
| Outcome | PASS WITH RELEASE HOLDS: local MVP implementation/automated acceptance complete; M10/M11 unchanged |
| Final commit | Reported in task completion; this audit is not amended after committing |

## 1. Request and outcome

Completed the MVP requested in the product specification through incremental
vertical slices, preserving the existing SDK and existing functional Studio.
The [durable plan](../engineering/website-mvp.md) records the delivered scope,
dependencies and explicitly deferred advanced phases.

### Website acceptance

- Compact specified headline/copy and two hero CTAs; no decorative hero artwork.
- Existing brand asset; approximately 64px adaptive header, upward-scroll/focus
  restoration, native skip link and reduced-motion behavior.
- Correct landing order: hero, real Playground, personalized Quick Start,
  unified capability explorer, expandable Studio, documentation, architecture,
  minimal open source, final CTAs and two-tier footer.
- Four applications and ten structural presets from the prior slice remain
  connected to the actual SDK, not a duplicate renderer or mock.
- Local documentation/navigation search has real results and native dialog
  focus/dismissal. Ctrl/Cmd K does not override editor/application inputs or
  reserved modifier combinations.
- Twelve real capability categories share one demonstration area; working
  controls/links open the corresponding implemented SDK experience.
- Selectable architecture layers match actual source/contracts. Framework
  adapters are not misrepresented as extra framework generators.
- `/studio` retains the original configuration/workbench, component, scenario,
  theme, extension, Inspector, starter and versioned-reference functionality.
  An expandable landing preview mounts/disposes a real component lab on demand.
- Global light/dark applies to all mounted SDK previews. Theme Studio preserves
  custom light/dark palettes independently. The site-dock color button changes
  global theme rather than diverging from it.
- `/contributing` supplies genuine local contribution guidance without requiring
  an unpushed GitHub document to be live. Root contributor documentation matches.
- DM Sans and DM Mono are self-hosted with full upstream OFL notices, not loaded
  through an external CDN. Footer includes exact linked `powered by Moorkyz`.

### Final Playground and integration hardening

- Numeric edits allow intermediate typing while preserving the last valid
  geometry. Explicit widths are 320–1100px; automatic docked styles fill the
  actual container. Responsive gaps/offsets keep controls reachable without
  modifying stored master geometry.
- Toolbar heights, silhouettes and target sizes are verified on the actual
  rendered SDK, not just the design data. Polygon clipping affects the
  background pseudo-element, never button hit targets or focus outlines.
- Pointer-up computes the final drag geometry from the pointer event rather
  than stale React render state; one drag creates one history entry. Cancellation
  discards temporary geometry.
- Accessible labels, icons, enabled/context policy and roving Tab entry survive
  SDK rerenders/collapse. Template rebinding preserves custom accessible-name
  suffixes without repeatedly prefixing labels.
- Unapplied JSON cannot be discarded through geometry Edit Mode. Color buffers
  survive unrelated edits and require explicit apply/discard before switching
  the edited variant. Imports are bounded, confirmed, race-protected and undoable.
- Autosave remains design/configuration-only. Corrupt/newer data is never silently
  overwritten. No application document, task/card content or usage history is
  persisted.
- Export uses the existing Vanilla generator with validated presentation inlined
  into `app.js`. This preserves the generator server's narrow public-file
  allowlist; no SDK or server permission expansion was introduced. Exact file
  integrity is recomputed. Tests write the downloaded source to a fresh
  temporary directory and run its actual generated local server against the
  built SDK, in all three browsers.

Relevant decisions preserved: Q07, Q09–10, Q15, Q18–21, Q28, Q35, Q37–45, Q48,
Q52–55, Q63–64, Q74, Q79–80. Public APIs/defaults and release authority did not change.

## 2. Change inventory

| Paths | Change | Risk |
|---|---|---|
| Portal landing/layout and new website CSS | Complete ordered composition, local fonts and semantic responsive surfaces | Medium |
| New Studio and contribution pages | Actual destinations; retained labs rather than rewritten systems | Low |
| Site header/footer, search, website explorers | Real navigation/search/demos, adaptive/focus behavior and progressive Studio mounting | Medium |
| Existing portal shell/labs | Shared global color preference and separate custom theme variants | Medium |
| Playground design/application/editor/CSS | Rendering, export, input/pointer/recovery and keyboard hardening | Medium |
| Browser/model tests and Playwright configuration | Executed generated server; structural screenshots/geometry; whole-MVP regressions; bounded CI concurrency | Low |
| Self-hosted font binaries and complete OFL notices | Requested typography, no runtime CDN or package dependency | Low, reviewed redistribution terms |
| README, contributor guide, status/spec/plan/release dossier and this audit | Accurate current behavior, verification and release holds | Low |

No SDK source, exports, package manifest, lockfile, Docker/deployment workflow or
external repository changed. Pre-existing edits to `tools.moorkyz.com`, `tools.moorkyz.com.cjs`,
`tools.moorkyz.com.cmd`, `tools.moorkyz.com.config.json`, and untracked `docs/task/` remain excluded.
Generated Next declaration changes returned to the tracked production paths
during the normal build; they were not authored or staged.

## 3. Verification evidence

### Exact final aggregate command

The environment is shared, so the final run used isolated ports and required
Playwright to own its servers rather than reuse an unrelated local service:

```powershell
$env:TOOLS_MOORKYZ_COM_FIXTURE_PORT='42973'
$env:TOOLS_MOORKYZ_COM_PORTAL_PORT='42974'
$env:CI='1'
pnpm check
```

CI browser concurrency is bounded to three workers. No test, assertion, timeout,
coverage threshold or feature gate was disabled or weakened.

| Check | Result | Exact command / method | Observed result |
|---|---|---|---|
| Aggregate repository acceptance | PASS | Final command above | Exit 0: lint, format, SDK/production portal build, all Node/browser tests and all consumer typechecks |
| Formatting | PASS | `pnpm format:check`; targeted `pnpm exec prettier --write` | Existing formatting gate |
| Lint | PASS | `pnpm lint` | Existing source/test lint gate |
| Types | PASS | `pnpm typecheck` | SDK, Vanilla, React, Vue, Angular and portal |
| Unit/integration | PASS | `pnpm test` within aggregate | 198 Node tests; 0 failures, skips or cancellations |
| Browser/a11y/regressions | PASS | `pnpm test:browser` within aggregate | 183 expected: 61 each Chromium/Firefox/WebKit; 0 unexpected, skipped or flaky |
| Production build | PASS | `pnpm build` within `pnpm test` | SDK and optimized standalone Next build; `/`, `/studio`, `/contributing` and proxy |
| Executed generated consumers | PASS | Downloaded-project case in browser suite | Real generated server, no mocked asset routes; source files/SDK assets served, dispatch, icon/geometry and disposal verified |
| SDK coverage | PASS | `pnpm test:coverage` | Core branch 94.79%; emitted library line/branch/function 88.64% / 86.03% / 95.21%; existing thresholds retained |
| Bundle/isolation measurement | PASS | `pnpm measure:bundles` | Same SDK consumer baselines; no portal/framework/CLI leakage |
| Dependency advisory check | PASS at execution time | `pnpm audit --json` | 220 resolved dependencies; 0 info/low/moderate/high/critical advisories |
| Bounded source/credential scan | PASS | `pnpm check:source` | 240 selected paths in final run; no selected credential-pattern or forbidden SDK sink findings |
| Structural visual regression | PASS | `visual-regression.spec.js` in aggregate | Ten actual silhouettes, exact heights/radii, full-width behavior, 44px targets, viewport containment, fullscreen dimensions and 320px extreme geometry; screenshot attachments |
| Rendered acceptance inspection | PASS, bounded | Temporary standalone server, HTTP 200, local Chromium screenshots | Inspected full light landing/desktop, mobile dark Playground and mobile Studio; temporary helper stopped |
| Whitespace | PASS | `git diff --check`; staged whitespace gate before commit | No whitespace errors; normalization warnings only |
| Independent security/AT/device/container/distribution acceptance | NOT RUN | Existing M10/M11 process | Remains required; agent evidence does not substitute |

Coverage is the established **SDK** metric scope, not a claim of portal JSX or
whole-workspace coverage. The advisory/source scans are bounded observations,
not independent penetration testing or a comprehensive credential audit.

### Measured local performance

The final browser report includes three navigation samples per engine, one
initial plus two cached, in this shared Windows development environment:

| Engine | Median navigation | Highest sample / reported p95 |
|---|---:|---:|
| Chromium | 82.6ms | 309.4ms |
| Firefox | 134ms | 309ms |
| WebKit | 102ms | 223ms |

With only three samples, the reported p95 is the maximum, not a statistically
robust production percentile. No approved numerical budget, device/network
certification or production-latency promise is inferred.

SDK gzip baselines: configuration 2,560B; dock 17,955B; search 1,634B; AI 798B;
Inspector 385B; generator 6,130B. These remain unchanged. Runtime-disabled work
and byte exclusion remain distinct claims.

### Font provenance and licensing

Unmodified upstream Google Fonts assets, fetched locally on 2026-10-17:

| Asset | Bytes | SHA-256 |
|---|---:|---|
| `DMMono-Regular.ttf` | 49,744 | `55b4c98f123daebb3ed27947ba47b2af00554fc6284d639a540bcef5e6258ad2` |
| `DMSans.ttf` | 240,164 | `8cd08d97e89c24d0aa92edd2f0f4c8ee6195eee9b7c9f154865a58b02f0c1c0d` |

Sources: Google Fonts `ofl/dmmono/DMMono-Regular.ttf` and
`ofl/dmsans/DMSans[opsz,wght].ttf`. Full copyright/OFL texts are committed beside
the binaries (one upstream trailing space was normalized in each notice).
This permits redistribution with notices; the fonts are not
relicensed as Apache-2.0. No mandatory package dependency was added.

### Findings resolved during validation

- Search glyph/shortcut text caused accessible-name mismatches: replaced the
  decorative text glyph with an aria-hidden SVG and kept visible Search text.
- Early Studio/explorer input could precede hydration: existing inert/disabled
  hydration boundaries now protect those controls.
- Render inspection found general presentation rules accidentally nested in a
  mobile media block: corrected scoping and added actual desktop visibility/
  grid/fullscreen regressions. Screenshots were refreshed after the fix.
- Generic asset-route mocking hid an export/server mismatch: presentation is
  now inlined, and the tests launch the real generated server.
- Number editing and pointer-up used overly immediate/stale state: bounded
  editable buffers and final event-coordinate commits preserve valid state.
- Imported incompatible commands remain denied even after a test manually
  re-enables the DOM button; SDK error feedback and the host status are checked.
- Firefox fullscreen can change the actual headless viewport: tests compare the
  rendered fullscreen size to `innerHeight`, not the pre-fullscreen viewport.
- A shared-port run reused a different local service and later lost the fixture
  server. Final acceptance used isolated ports with reuse disabled, without
  terminating unrelated processes. CI's eight-worker run also hit Firefox
  teardown contention; three-worker CI bounds preserve every test and timeout.
- All task-caused failures were resolved. The final aggregate run has no retries,
  skips or reported flakiness. Historical failed attempts are not claimed as PASS.

## 4. Risk and quality review

- **Public contract/defaults:** SDK remains framework-neutral and unchanged.
  Private portal design data is versioned separately and validated through the
  SDK plus portal policy. Optional systems/spatial remain disabled in the
  Playground; existing explicit labs retain their opt-in demonstrations.
- **Security/privacy:** no arbitrary JS, privileged HTML, external uploads,
  telemetry, learning, account or cloud state introduced. Commands remain
  host-owned; design data cannot grant capabilities. CSS values are bounded
  numbers/enum/hex tokens; IDs use SDK validation. Production CSP remains strict.
- **Accessibility:** native labeled controls, roving toolbar entry, disabled
  eligibility, focus continuity, restrained/reduced motion, dialog dismissal,
  responsive target sizes and keyboard geometry/sheet/fullscreen behavior are
  automated. This is not manual WCAG 2.2 AA certification.
- **Persistence:** versioned design-only autosave, explicit saved drafts,
  last-valid preview/export, bounded history, confirmed replacement and preserved
  corrupt/future data. Application contents are page-local.
- **Lifecycle:** owned SDK disposal/observers/listeners, superseded generation/
  import protection, pointer-capture cancellation, download URL revocation,
  fullscreen focus cleanup, test server/worker and temporary-directory cleanup.
- **Dependencies/performance:** no package/lockfile change; font notices retained.
  No background polling or optional runtime work added by disabled systems.
  CI resource bounds change concurrency only, not required acceptance.

## 5. Residual risks, scope and release readiness

| ID | Finding | Mitigation / owner | Status |
|---|---|---|---|
| R01 | Automated checks do not certify real screen readers, touch hardware or the final approved browser/device matrix | Existing M10 manual acceptance | HOLD |
| R02 | Numerical performance budgets/platform and coverage metric scope still need approval | Existing V03/V04 and release dossier | HOLD |
| R03 | Independent release/security review, distribution notices and container-engine execution are not supplied by this session | Existing M10 owners | HOLD |
| R04 | Publication/deployment/signing require separate authorization after M10 | M11 authorization process | HOLD |
| R05 | Full specification includes later-platform scope intentionally outside this MVP | UI/docs label 16+/40+ libraries, professional workflows, cloud sharing, sandboxes and extra generators as future | DEFERRED BY SCOPE |

No known task-caused critical data-loss or authorization defect remains in the
tested MVP. This is a bounded implementation claim, not a security guarantee.

Generated consumers demonstrate host dispatch, not the four application hosts.
Export is portable JSON rather than ZIP/installation. It names DM Sans with a
system fallback; identical self-hosted typography needs the separate OFL assets
and notices as documented. Pixel-perfect OS-independent rendering is not claimed;
visual regression checks geometry/structure and retains screenshots.

**MVP local implementation/automated acceptance: COMPLETE.**
**Distribution/production release readiness: NOT READY / NOT AUTHORIZED.**
M10/M11 holds are unchanged.

Rollback: revert the focused website/acceptance commit and, if needed, the prior
Playground commit. No SDK migration or external action occurred. V2 browser
storage can be retained; v1 data was not overwritten. Unrelated user changes
remain untouched.

## 6. Completion record

- Audit created before the local task commit and included with it.
- Final commit hash: reported in task completion; no post-commit audit amendment.
- External actions: no push, deployment, publication, signing, spending or
  cross-project integration. Only public licensed font downloads and explicitly
  requested advisory retrieval were performed.
- Temporary inspection servers were stopped; generated consumer servers/workers
  and fresh temporary directories were cleaned by tests. Render evidence remains
  in the session artifacts and browser test report, not an unrelated commit.

**Limitations:** Self-audit is not independent security review, accessibility
certification, distribution clearance or release approval.
