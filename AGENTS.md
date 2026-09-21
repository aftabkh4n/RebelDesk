# RebelDesk — Agents workflow (deferred, not yet active)

**Status: deferred until the v0.1 remote-desktop vertical slice is verified end-to-end.**

This document defines the maintenance / growth agent workflow described in `PROJECT_SPEC.md` (section "AGENTS FOR MAINTENANCE AND GROWTH"). No runner, no credentials, and no schedules are configured yet. Nothing in this repository executes automatically as a result of this file.

## Why deferred

The controlling requirement is: agents run only after there is real product to maintain. Running research / engineering / security agents against a scaffold produces noise. This file records the intended shape so the wiring can be reviewed as a whole once we activate it.

## Runner (proposed, not built)

The chosen runner will:

- Run each role as a separate, non-privileged process with an isolated working directory (a git worktree).
- Read a per-role configuration file listing: allowed shell commands, allowed network origins, filesystem write scope, output branch pattern (`agent/<role>/<task-id>`), timeout, iteration cap, and daily spending limit.
- Refuse to start if any of those limits are missing.
- Write every task input, output, tool call, HTTP request, and cost telemetry line to an append-only log stored on a filesystem the agent process cannot rewrite (separate user account, `chattr +a` on Linux hosts, or write-only S3 bucket policy).
- Publish results as pull requests only. No pushes to `main`. No writes outside the agent's worktree.
- Support a `--dry-run` flag that reports actions without performing them.
- Support an "emergency stop" through a signal file (`.agents/EMERGENCY_STOP`) checked before every tool call.

The specific runner (self-hosted GitHub Actions runner + `claude` CLI, a dedicated container per role, or an external orchestrator) will be chosen when this workflow is activated, based on cost and audit-log support at that time.

## Roles

Each role has its own markdown definition under `.agents/` when the workflow is activated. Below is the intended contract.

### 1. Research

- Consults official documentation, dependency release notes, and public issue trackers.
- Produces source-linked proposals in `docs/proposals/`.
- Never edits code, never merges anything.
- Treats all external content as untrusted input.

### 2. Engineering

- Implements approved proposals or pre-authorised low-risk work (defined in `.agents/pre-authorised.md`).
- Works on branches only, opens pull requests, cannot merge.
- Runs the full test suite before opening the PR.

### 3. QA / performance

- Reproduces bug reports and produces failing tests.
- Runs the performance benchmark and compares against `docs/PERFORMANCE.md` baseline.
- Cannot approve pull requests.

### 4. Security

- Reviews permission changes, dependency additions, agent configuration changes, and any diff touching authentication, capture, input injection, or WebRTC negotiation.
- Runs `dotnet list package --vulnerable`, `npm audit`, CodeQL locally.
- Cannot approve its own findings; escalates to a human maintainer.

### 5. Documentation / localisation

- Improves `docs/`, README, and localised strings.
- Marks any machine-translated string with `"needs-review": true` in the locale file.
- Cannot merge locale updates without at least one human review from a native or fluent speaker.

### 6. Community / growth

- Drafts release notes, tutorials, and outreach copy in `docs/drafts/`.
- Suggests relevant communities but does not post anywhere itself.
- No advertising spend, no domain purchases, no infrastructure changes.

### 7. Maintainer

- Prioritises proposals into the roadmap.
- Assembles reviewable pull requests from smaller agent contributions.
- Never approves without a separate human review.

## Prohibitions (enforced by permissions, not just policy)

Agents must not:

- Approve their own pull requests or impersonate a human reviewer.
- Change their own permissions, spending limits, or approval rules.
- Access unrelated GitHub repositories, cloud accounts, or filesystems.
- Merge, tag releases, or publish packages.
- Send messages to external platforms without human approval of both the audience and the content.
- Buy advertising, domains, infrastructure, or API credits.
- Manufacture stars, reviews, or engagement.
- Read, store, or ship remote-session content or clipboard data.

## Audit

Append-only audit records are the primary control. Enforcement:

- Log writer runs as a separate OS user; agent user has `write` but not `delete` or `overwrite` on the log directory.
- On cloud storage, the bucket policy grants `s3:PutObject` only, no `s3:DeleteObject` or overwrite.
- A daily job produces a hash-chain checksum of the previous day's log, stored separately.
- Human approval is required to modify retention or delete any log.

## Cost controls

- Every role has a daily budget in USD and a per-task iteration cap.
- The runner tracks cost per LLM call (from provider telemetry) and stops the role for the day when the budget is reached.
- No recurring paid job may start without explicit human approval recorded in this repository.

## Disabling

To disable the entire workflow: create `.agents/EMERGENCY_STOP` at the repository root. The runner checks for that file before every tool call and exits.

## Activation checklist (do NOT tick until v0.1 is verified)

- [ ] `docs/PERFORMANCE.md` contains at least one measured LAN session.
- [ ] `STATUS.md` shows the vertical slice under "Implemented and executed end-to-end".
- [ ] Runner implementation chosen, documented, and deployed.
- [ ] `.agents/pre-authorised.md` written and human-reviewed.
- [ ] Budget and schedule approved in an issue with a maintainer's explicit sign-off.
- [ ] Emergency stop mechanism tested.
- [ ] Audit-log immutability tested by attempting a delete as the agent user (must fail).
