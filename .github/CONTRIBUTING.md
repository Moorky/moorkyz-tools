# Contributing

Read [AGENTS.md](../AGENTS.md), the [current decisions](../docs/decisions/decision-register.md)
and the tool-specific guide before changing behavior.

Keep each tool independently usable, accessible and local-first. Reuse the
catalog/detail/source views. Native operations need explicit opt-in, validated
boundaries, previews where appropriate, clear failures and deterministic cleanup.
Directory Cleaner never permanently deletes or overwrites a file.

Run the relevant [verification commands](../docs/engineering/testing.md), document
actual results in a task-scoped audit, and stage only related changes. Publishing,
deployment and infrastructure require separate approval. Historical audits must
not be rewritten to suggest that old product scope is still current.
