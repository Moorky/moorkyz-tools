# CI release follow-up audit

Owner request: repair critical release defects, audit, commit/push and verify deployment. Scoped self-audit by Codex.

PASS: 111 portal browser cases across Chromium, Firefox and WebKit; native report project build (zero errors/warnings) and 4 report Chromium cases; repository structure 14 tests, unit suite 143 tests, build, lint, typecheck, formatting and bounded source scan. Fixed explicit repository cwd for native fixtures launched from src. Linux CI had run the Windows-specific VideoCrunch API cases as part of the generic suite; those cases remain required in videocrunch-windows and are now exposed separately as test:videocrunch:api. No assertion disabled, no application security guard relaxed. The native report Windows job remains a prerequisite of container/deploy. Original source checkout is preserved; this release lives in the separate task checkout.

Production revision matching remains pending until GitHub and Coolify finish. No credentials or private user data included. Restore the previous verified image or revert this commit if deployment fails.
