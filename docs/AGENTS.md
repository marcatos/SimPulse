# Agent operating manual

This project is developed by multiple agents, often in parallel, often with **zero prior chat context**. The repository is the memory. Chat is not.

## Source of truth (highest first)

1. Executable tests and current code behavior
2. Accepted ADRs in `docs/adr/`
3. `docs/ARCHITECTURE.md`
4. `docs/CURRENT_STATE.md`
5. `docs/BACKLOG.md`
6. `docs/handoffs/`
7. Code comments
8. Conversational context

If code and docs disagree, investigate. Do not assume either is correct. Fix the inconsistency.

## Before starting work

1. Read `docs/CURRENT_STATE.md`, `docs/ARCHITECTURE.md`, `docs/BACKLOG.md`, relevant ADRs, `docs/KNOWN_ISSUES.md`, and relevant code/tests.
2. Identify the backlog ID you are working on. If none exists, add one before coding.
3. List files you are likely to change.
4. Check `docs/BACKLOG.md` and `docs/handoffs/` for overlapping IN_PROGRESS work on those files.
5. Prefer tasks with isolated ownership.

## Task claiming

Set the backlog item to `IN_PROGRESS` and write `docs/handoffs/<ID>.md` **before** substantial edits.

One agent owns one backlog ID. Do not silently take over an IN_PROGRESS item; read its handoff first.

## Ownership lanes

| Lane | Typical IDs | Typical paths |
| --- | --- | --- |
| Watch | WATCH-* | `apps/watchos/` |
| iOS | IOS-* | `apps/ios/` |
| Bridge | BRIDGE-* | `apps/windows-bridge/` |
| Protocol / domain | PROTO-*, ANALYTICS-* | `packages/` |
| Infra / tests | INFRA-*, BUG-* | `.github/`, `scripts/`, `tests/` |
| Docs | DOCS-* | `docs/` |

Do not assign two agents to the same files. If unavoidable, name an integration owner in the handoff.

Shared contracts (protocol, domain, persistence schema, public interfaces, architecture) require documentation **before or with** the change. Architecture changes need an ADR. Do not silently supersede an accepted ADR; write a new one with status SUPERSEDED on the old.

## Documentation rules

After a material change, update:

- `docs/CURRENT_STATE.md` (always)
- `docs/BACKLOG.md` status
- `docs/KNOWN_ISSUES.md` if you found or fixed a defect
- `docs/REGRESSIONS.md` if something that worked now does not (or a regression was fixed)
- ADR if architectural
- Handoff for substantial work

## Handoff procedure

Create `docs/handoffs/<task-id>.md` with:

```text
Task
Goal
Status
Files changed
Decisions made
Tests executed
Tests passing
Known failures
Remaining work
Risks
Suggested next action
```

Another agent must continue without the previous conversation.

## Conflict procedure

1. Stop editing the contested file.
2. Record the conflict in the handoff and CURRENT_STATE blocked section.
3. Prefer the accepted contract (ADR / protocol schema) over a local convenience change.
4. Do not overwrite another agent's uncommitted intent. If git history is already on main, integrate; do not force-push.

## Definition of done

A backlog item is DONE only when applicable:

- Implementation complete
- Tests added and passing on an actually available platform
- Documentation updated (CURRENT_STATE, BACKLOG, issues/ADRs/handoff)
- No new unexplained warnings
- Apple/Xcode results recorded as NOT EXECUTED when the platform is missing
- Regression test added when a regression was fixed

## Safety rules

Agents MUST NOT:

- Delete code they do not understand merely to make compilation succeed
- Weaken tests to obtain green CI without justification
- Silently change architecture or public contracts
- Mark untested behavior as tested
- Claim builds succeeded if the platform was unavailable
- Erase known issues
- Overwrite another agent's active work without investigation
- Perform broad refactors unrelated to the assigned task
- Commit secrets, certificates, or pairing stores
- Log raw biometric payloads

If blocked, document the blocker.

## Platforms

This repo must remain useful on Windows without Xcode. Cross-platform .NET tests are the default verification. iOS/watchOS verification happens on macOS later.
