---
name: task-audit
description: "Create a scoped implementation audit with real verification evidence and prepare the task's local commit. Use after any repository file changes and before committing."
---

# Task Audit

Use this workflow after implementation and applicable checks, before the task commit. Follow the root `AGENTS.md` and the relevant specifications.

1. Inspect the actual task diff and changed paths. Exclude pre-existing or unrelated user changes.
2. Compare the change with approved decisions, contracts, acceptance criteria, and affected tests.
3. Run appropriate existing checks. Record exact commands and actual outcomes; use `NOT RUN` with a reason or `N/A` when a check does not apply.
4. Review security, privacy, dependencies, permissions, accessibility, failure handling, cleanup, and disabled-feature behavior as relevant.
5. Create `.audits/YYYY-MM-DDTHH-MM-SSZ-short-task-slug.md` from `docs/AUDIT_TEMPLATE.md`, preferably with `moorkyz audit "task title"`. Historical reports in `docs/audits/` and `audits/` remain at their original paths.
6. Fill the audit with observed evidence, identified findings, residual risks, incomplete work, and rollback notes. Do not include secrets or claim independent review.
7. If a task-caused required check fails, fix it or report the task blocked; do not record a successful completion commit.
8. When checks pass and Git state allows, stage only task-owned paths and the audit, inspect the staged change, and make one focused local commit. Report the hash. Never push, merge, publish, deploy, or rewrite history without authorization.

If the repository has no commit baseline or the initial files are all pre-existing untracked work, do not force a commit by staging that baseline. State the blocker and leave user data intact.
