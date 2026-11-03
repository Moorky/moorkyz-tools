# Agent Scheduler

Read the [repository contract](../AGENTS.md), [agent workflow](docs/engineering/agent-workflow.md),
and [control-center task schedule](docs/engineering/control-center-task-schedule.md)
before claiming work. This file coordinates task ownership; it is not a lock or
authorization for external actions.

## Serial workflow

1. Use a real owner request or an existing task ID from the control-center
   schedule. Record the scope, owner, dependencies, and acceptance evidence;
   do not duplicate the existing backlog or audit history.
2. Keep at most one repository task `IN_PROGRESS`. Take the first `READY` item
   with completed dependencies. Do not take another owner's `CLAIMED`,
   `IN_PROGRESS`, or `PAUSED` task without a recorded handoff.
3. Before editing, re-read applicable instructions, approved decisions, task
   scope, and Git/worktree state. Preserve unrelated staged, unstaged,
   untracked, ignored, and linked-worktree content.
4. Work only within the assigned scope. Use isolated synthetic fixtures; do not
   test against personal files, real accounts, or downloaded media. Preserve
   local-first handling and opt-in, loopback-only service boundaries.
5. Record applicable checks and actual `PASS`, `FAIL`, `NOT RUN`, or `N/A`
   results in a filled task audit. Mark work `DONE` only when acceptance evidence
   is complete. Commit, push, publish, and deploy under the repository contract
   and separate owner authorization.

## States

| State       | Meaning                                                    |
| ----------- | ---------------------------------------------------------- |
| READY       | Scoped and eligible; work has not started.                 |
| CLAIMED     | Reserved by a named owner/session but not active.          |
| IN_PROGRESS | The single active repository task.                         |
| PAUSED      | Explicitly paused; retain the owner and restart condition. |
| BLOCKED     | A concrete input, approval, or prerequisite is missing.    |
| DONE        | Acceptance evidence is recorded.                           |

## Current claims

No product implementation task is claimed or `IN_PROGRESS` in this ledger.
Portfolio-level scheduler adoption is tracked in the portfolio ledger. The dated
control-center backlog remains in its [task schedule](docs/engineering/control-center-task-schedule.md);
revalidate current decisions, prerequisites, and ownership before claiming an
item. Do not copy its rows into this ledger.
