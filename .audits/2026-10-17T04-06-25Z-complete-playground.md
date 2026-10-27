# Development Audit: Complete functional Playground vertical slice

| Field | Value |
|---|---|
| Audit ID | `2026-10-17T04-06-25Z-complete-playground` |
| Created (UTC) | `2026-10-17T04:06:25Z` |
| Task / baseline | Website/Playground MVP continuation / `02f9601` on `master` |
| Auditor | Copilot agent self-audit |
| Scope | Portal-only design model, applications, presentation, editing, recovery and Vanilla export |
| Outcome | PASS WITH RISKS; website composition and final hardening continue next |
| Final commit | Reported in task completion, not edited into this audit afterward |

## 1. Request and outcome

Continued the product-specification MVP rather than stopping after the first
Workspace slice. The durable dependency-ordered plan is
[website-mvp.md](../engineering/website-mvp.md).

Implemented four local stateful applications and ten genuinely structural styles.
The existing SDK still owns rendering, keyboard behavior, schema validation and
lifecycle. Portal-only version-2 design data adds presentation and host-command
bindings without inventing public SDK configuration properties.

Implemented independent template/style selection, favorites/search/categories,
manual variant colors, accessible material controls, constrained geometry,
explicit pointer/keyboard Edit Mode, action add/remove/duplicate/reorder/rename/
icon/tooltip/accessible-name/command operations, context eligibility, real
application reset, bounded configuration history and safe imports.

Autosave now persists configuration/design only, as requested by the product
specification; application contents remain page-local. Valid autosaves restore
automatically; unreadable/newer data is preserved, exportable and replaced only
after explicit confirmation. Prior explicit v1 drafts migrate through the SDK
validator and keep action labels and the selected palette.

Exports reuse the existing Vanilla generator and add validated CSS/presentation/
portable design files. File integrity and the manifest are recomputed. Generated
examples preserve geometry, labels, icons, disabled command boundaries and
cleanup; they do not claim to generate the four mini-applications.

Decisions preserved: Q07/Q15/Q18/Q20/Q28/Q35/Q37–45/Q48/Q52–55/Q63–64/Q74/Q79–80.
No SDK, dependency, public feature-default, architecture or distribution change.

## 2. Change inventory

| Paths | Change | Risk |
|---|---|---|
| Portal `playground-design.ts` | Bounded versioned design validation, legacy recovery, CSS, host decoration and generator composition | Medium |
| Portal `playground-applications.tsx` | Four real local hosts with context eligibility and reset | Medium |
| Portal `functional-playground.tsx`, `playground.css`, page nonce wiring | Connected editor, autosave, history, geometry, responsive sheet/fullscreen and Quick Start | Medium |
| Playground browser/model tests | Contract, adversarial data, behavior and executed integration coverage | Low |
| Portal README and durable plan | Actual status, commands, boundaries and remaining work | Low |

Pre-existing launcher/config edits, untracked product specification and generated
Next declaration changes were excluded from the task commit.

## 3. Verification evidence

| Check | Result | Exact command / method | Observed evidence |
|---|---|---|---|
| Formatting | PASS | `pnpm format:check`; targeted `pnpm exec prettier --write` | Existing gate; no configuration weakened |
| Lint | PASS | `pnpm lint` | Existing full source lint gate |
| Types / build | PASS | `pnpm --filter @tools.moorkyz.com/portal typecheck`; `pnpm build` | Production SDK/Next build and Next TypeScript validation pass |
| Unit / boundary / integrity | PASS | `node --test tests\tooling\playground-design.test.js tests\tooling\playground-model.test.js` | 7 pass; four application/ten style contracts, malicious data, migration and exact generated SHA-256 checks |
| Three-engine browser | PASS | `pnpm exec playwright test 'playground\.spec\.js' --workers=3` | 30 pass; 10 each Chromium/Firefox/WebKit; zero skipped, failed or flaky |
| Accessibility / keyboard | PASS, bounded | Axe in light/dark and mobile; native text selection, command eligibility, keyboard geometry/sheet controls | Part of browser suite; not manual AT certification |
| Executable integration | PASS | Download and serve exported Vanilla files with real SDK in all three engines | Actual action dispatch, icon/geometry rendering and pagehide disposal |
| CSP / host authority | PASS, bounded | Existing nonce retained; unknown-command/CSS-input rejection, incompatible disabled actions, no external-request assertion | No arbitrary JavaScript execution or imported privilege |
| Source scan | PASS, bounded | `pnpm check:source` | 224 selected paths; no selected credential/forbidden library sink findings |
| Whitespace | PASS | `git diff --check` | Only line-ending normalization warnings |
| Full SDK coverage, whole repo/browser regression, dependency audit, final website visuals/performance | NOT RUN in this slice | Next hardening slice | No SDK or dependencies changed; final MVP gates remain pending |

Failures fixed during iteration: strict TypeScript narrowing of validated numeric
properties inside callbacks; intrinsic geometry resize origin; blocked-storage
status overwritten by preference writes; text-selection automation changed to
real keyboard selection; role-based select locators; browser-specific CSS color
serialization now verified by actual rendered RGBA pixels. No assertions were
skipped and no retries or relaxed contrast/geometry thresholds were introduced.

## 4. Risk and quality review

- Versioned JSON is data only; SDK IDs/labels, command allowlists, exact property
  sets, ranges, palette/focus/hover contrast and UTF-8 size are validated.
- Opacity is additionally checked against black/white backdrops; clipped shapes
  affect only a background pseudo-element, never buttons or focus outlines.
- Host authorization is rechecked on dispatch; imported commands cannot add
  host permissions. Ineligible actions remain disabled after collapse/rerender.
- Light-DOM decoration uses an owned child-list observer with disposal. Generated
  examples also disconnect on pagehide. No polling or default telemetry added.
- SDK controller, pointer-capture cancellation, fullscreen listeners/focus,
  superseded generation results and download object URLs have owned cleanup.
- Browser fullscreen denial uses a visible expanded-view fallback, not fake
  success. Settings/code views and valid design state survive responsive changes.
- No dependency, cloud retention, account, external API, remote sharing, CLI
  execution or optional SDK activation introduced.

## 5. Findings and residual risks

- The complete landing composition, local fonts, global search, architecture/
  capability explorers and dedicated Studio are the next slice, not completed
  by the Playground controls. Existing labs still have independent experiments.
- Semantic groups, arbitrary code, advanced workflows and extra framework
  generators remain explicitly unavailable (later platform scope).
- Manual assistive-technology/touch/device acceptance, approved performance/
  platform scope, distribution review, independent review, container evidence
  and release approval remain M10/M11 holds.
- Performance and full-repository checks will be recorded in final MVP hardening.
- Rollback: revert this focused commit. V2 storage uses separate keys; v1 data
  remains untouched. No irreversible migration or external action occurred.
- **Release readiness: NOT READY.** This validates the Playground slice only.

## 6. Completion record

Audit created before and included in the local task commit. Commit hash is
reported at task completion. No push, deploy, publish, signing, spending or
cross-project integration occurred. This self-audit is not independent security
review, WCAG certification or release approval.
