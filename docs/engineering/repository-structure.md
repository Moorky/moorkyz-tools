# Repository structure and template adoption

The portal is a product, not a reusable SDK or a copy of the template showcase.
This hierarchy adopts the separation of concerns in
[Moorky/moorkyz-template](https://github.com/Moorky/moorkyz-template/tree/1c413a3a0e7ea24c39fc995e1a947541a4efac01)
while preserving the approved application, independent tools and safety rules.

```text
moorkyz-tools/
  .github/             Platform-discovered CI and agent integration
  .template/           Shared CLI and read-only maintenance systems
    cli/               Single CLI implementation, README, helpers/
    maintenance/       Checkup entrypoint, README, helpers/
  .audits/             New task verification records
  docs/                Current product and engineering documentation
    AUDIT_TEMPLATE.md  Template, not an audit record
    audits/            Historical implementation evidence (preserved)
  packages/portal/     Next.js application and its own build helpers
  tools/               Independently usable tools and source allowlists
  scripts/             Product-owned source generation/checks
    deployment/        CI deployment client, README and isolated tests
  tests/               Product and repository regression tests
  audits/              Historical operations evidence (preserved)
```

## Ownership and boundaries

- Add tools to the existing catalog, using shared detail/source views. Portal
  navigation must remain independent of any one tool. Do not create another SDK.
- Keep all canonical tool directories in `tools/`, tracked by this repository.
  Each drop-in project keeps its `portal.json` metadata and `source-files.json`
  allowlist inside its own folder. `pnpm tools:index` and portal dev/build discover
  them automatically; see the [project contract](../../tools/README.md).
  Independent use does not imply separate Git ownership. Split a repository only
  for a concrete, explicitly approved need; do not add nested Git roots, submodules
  or sibling-source sync requirements. Media Recover is edited in
  [tools/mediarecover](../../tools/mediarecover), not in an external project.
- Keep app-specific scripts with their workspace, project automation in
  [scripts](../../scripts/README.md), and reusable repository systems in
  [.template](../../.template/README.md). Each tooling folder has a README and
  entrypoint; supporting code belongs under `helpers/`.
- Root CLI files are compatibility entrypoints, not separate implementations.
  Both command identities retain their config names, state directories and
  preferred ports. The Windows profile installer remains explicit and optional.
- Keep active docs in their subject folders and new evidence in
  [.audits](../../.audits/README.md). Historical audits stay at stable paths.
- Source manifests, generated text/ZIP assets and independent native services
  retain content-hash verification, explicit allowlists and opt-in boundaries.

## Useful template systems, without unsafe replacement

The adoption reuses the template's tooling catalog, entrypoint/helper convention,
tracked audit hierarchy and read-only maintenance approach. The repository's
CLI/storage code is retained and shared, not overwritten by upstream code.
Upstream port-conflict and retention behavior differs; adopting it blindly would
change existing startup and recovery behavior.

The approved Node/pnpm versions, package boundaries, local-service defaults and
retention policy remain unchanged. Existing runtime folders (`logs/`, `archive/`,
`trash/`, `.moorkyz/`, `.tools.moorkyz.com/`) remain ignored and excluded from
container contexts. No existing data is moved, cleaned or deleted by checkups.
Do not copy starter presets, claim-template tools, designs, user settings or
deployment systems without an actual approved need.

## Enforced checks

`pnpm check:repository` checks JavaScript syntax without executing source files,
then verifies shared launcher/storage wiring, config-to-workspace references,
quality-gate coverage, maintained documentation links and ignored runtime paths.
`pnpm test:unit` now includes storage and deployment tests that previously needed
separate invocations. `pnpm test` still builds first for tests of generated assets.
`pnpm check` includes repository and bounded source checks alongside the existing
quality gates. CI uses those same commands.

This is a reversible organization change, not authorization to publish, deploy,
change filesystem permissions, install prerequisites or prune retained archives.
