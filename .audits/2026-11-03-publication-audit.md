# Publication audit

- Date: 2026-11-03.
- Authority: owner requested audit, commit and remote publication.
- Baseline: `5f1c336`; remote master observed at `9f570b8`.
- Reviewer: Copilot SDK in VS Code; scoped self-review.
- Outcome: publish a verified review branch without changing production.

The current source retains the owner's modular catalog and newer workspace
layout. The preceding repair commit recovered release safeguards/audits and
fixed reproduced validation issues without replacing that product direction.

PASS on the actual original checkout: complete `pnpm --dir src check`,
15 structure checks, 149 unit/tooling tests, 111 cross-browser tests, lint,
formatting, types, build and bounded source checks. Additional reviewed-copy
checks: 13 Windows API, 42 native and 54 focused browser/native tests.

Fresh remote comparison confirms divergent ancestry. Selected credential
checks covered current source and 107 newly reachable blobs without matches.
Five broad path warnings were reviewed: generated catalog data and allowlisted
portal/source-provenance manifests, not personal runtime data. An all-ref bundle
was independently verified. These checks are bounded, not full certification.

Production deployment is enabled remotely, so publish
`recovery/verified-20261103`, not master. No force push, remote merge, account
access, elevated native operation or deployment is included. The local portal
was restored and remains separate from this source publication.
