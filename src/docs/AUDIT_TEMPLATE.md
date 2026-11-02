# Development Audit: {{TASK_NAME}}

> Fill in with observed evidence. Never mark an unexecuted check as PASS.

| Field | Value |
|---|---|
| Audit ID | `{{AUDIT_ID}}` |
| Created (UTC) | `{{DATE_UTC}}` |
| Task / issue | {{TASK_NAME}} |
| Auditor | Agent / person and relevant tool (self-audit unless stated otherwise) |
| Branch / baseline commit | |
| Final commit | Report in task completion; do not edit this audit after committing |
| Scope reviewed | |
| Outcome | `PASS / PASS WITH RISKS / BLOCKED / FAIL` |

## 1. Request and outcome

- Requested behavior:
- Implemented behavior:
- Acceptance criteria and evidence:
- Intentional exclusions:
- Relevant decisions/contracts:

## 2. Change inventory

| Path | Change | Risk |
|---|---|---|
| | | |

## 3. Verification evidence

Use `PASS`, `FAIL`, `NOT RUN` (with reason), or `N/A`. Record exact commands and results.

| Check | Result | Exact command / method | Evidence / notes |
|---|---|---|---|
| Formatting / lint | | | |
| Typecheck | | | |
| Unit / integration tests | | | |
| Browser / accessibility | | | |
| Build / consumer fixture | | | |
| Security / dependency review | | | |
| Performance (if relevant) | | | |
| `git diff --check` | | | |

## 4. Risk and quality review

- Public contract, feature-default, and compatibility impact:
- Security, privacy, permissions, and secrets:
- Accessibility, keyboard, and reduced motion:
- Disabled-feature work/resource/network behavior:
- Error handling, cleanup, and recovery:
- Dependency, performance, and regression exposure:

## 5. Findings and residual risks

| ID | Severity | Finding and evidence | Mitigation / owner | Status |
|---|---|---|---|---|
| | | | | |

- Known gaps / checks not run:
- Required approvals:
- Rollback or recovery:
- Release readiness: `READY / READY WITH CONDITIONS / NOT READY / NOT APPLICABLE`

## 6. Completion record

- Audit created before the task commit and included in that commit, or reason not committed:
- Final commit hash: `PENDING`
- External actions: `NOT AUTHORIZED` unless separately approved.

**Limitations:** An agent self-audit is not an independent security review, accessibility certification, or release approval.
