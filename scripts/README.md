# Project automation

These scripts belong to this tools portal, not the reusable template systems.

| Location                                             | Responsibility                                         |
| ---------------------------------------------------- | ------------------------------------------------------ |
| [prepare-tool-sources.mjs](prepare-tool-sources.mjs) | Generate allowlisted text/ZIP assets for portal builds |
| [verify-source.mjs](verify-source.mjs)               | Bounded credential and unsafe-rendering source scan    |
| [deployment](deployment/README.md)                   | Approved CI deployment client and isolated tests       |

Source-generation entrypoint paths remain stable for workspace build scripts.
Do not put application workspaces, downloaded sources or runtime data here.
Reusable local lifecycle/storage and checkups live in
[.template](../.template/README.md).
