# tools.moorkyz.com portal

The private Next.js portal is the shared catalog and tool host. It has no
SDK dependency. Native header/navigation, theme and local search remain
independent of the individual tools.

- `/`: cards generated from `src/data/tools.ts`.
- `/tools/toolbar`: small standalone Moorkyz Toolbar Builder with native preview
  and single HTML Copy/Download; no Studio or package generation.
- `/tools/videocrunch`: metadata, complete source and optional native workspace.
- `/tools/directory-cleaner`: rules, downloadable settings/script, non-mutating
  preview and explicitly confirmed archive/move operations through an optional
  localhost service.

Every source bundle is an explicit allowlist; builds produce first-party JSON
and deterministic ZIP assets. The source viewer renders text, never executes it.
The portal keeps its nonce-based CSP. Both local service gateways default off
and acquire no connection or polling until the user enables/opens them.

See [the tool guide](../../docs/portal/modular-tools.md) and
[verification](../../docs/engineering/testing.md). Historical audits remain
unchanged; the old SDK, large Playground and Studio are no longer active scope.

The footer links to the separate [Moorkyz Evidence workspace](https://evidence.moorkyz.com).
This is navigation only: the tools portal does not process, request or host
evidence data, and the destination's availability is managed independently.
