# Preserve the pending tools workspace restructuring

- Date: 2026-11-02.
- Baseline: `887c2ce`.
- Authority: owner explicitly requested committing all remaining unstaged work,
  including existing staged changes.
- Reviewer: Copilot SDK in VS Code; scoped self-review.
- Outcome: checkpoint with known unfinished work, not release certification.

## Scope and preservation

Commit the existing root/source/tooling restructuring, relocated historical
audits and documentation, Windows GUI/startup work, tests and configuration.
These changes predate the Folder Unlock recovery commit. No old path is restored
merely to hide a legitimate relocation, and draft audit placeholders are not
treated as completed verification.

All pending bytes, original index and tracked patch were preserved and SHA-256
verified outside repos before staging. The owner explicitly authorizes recording
this existing state; this is not a new product redesign or a history rewrite.

The nested `moorkyz-site/moorkyz-toolbar` folder was another obsolete incomplete
copy of this repository, not a new submodule. Its 3,212 Git objects and modified
storage content already exist in this Git store. Its entire directory was
preserved outside repos rather than accidentally staged as a parent gitlink.

## Verification

- PASS: `pnpm --dir src check:repository`; syntax of 69 JavaScript files and
  14 structure/fixture tests.
- PASS: `pnpm --dir src check:source`; bounded scan of 567 repository paths.
- PASS: prior current-state recovery checks: build, types, lint, live Folder
  Unlock page/source downloads and all 160 unit tests with normal child cmd lookup.
- PASS: pending credential-signature scan; credential-shaped strings are
  deliberate synthetic redaction/rejection fixtures, not production credentials.
- PASS: `git diff --check HEAD`.
- FAIL, retained baseline: whole-repository formatting flags three existing
  files; no unrelated formatting rewrite was applied.
- NOT RUN: a fresh full cross-browser/native application release certification.

## Remaining work

Existing migration task states, historical audit drafts and branch divergence
remain visible. The earlier recovery audit records the exact Windows command
lookup qualification and elevated Folder Unlock acceptance limit.
No secrets, generated dependency/build trees, runtime data, push or deployment
are included. A checkpoint does not turn unfinished work into a completed release.
