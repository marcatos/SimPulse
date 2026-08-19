# Task 4 reconnect documentation and Plane report

## Status

DONE

KI-006 reconnect-token documentation is aligned with the implementation on `feat/reconnect-token`. Plane received a progress comment and remains open for the controller to close only after merge.

## Scope completed

- Added accepted ADR 0014 for the per-device opaque reconnect token.
- Documented the choice against DeviceId-only reconnect and HMAC challenge-response.
- Documented 32-byte random token issuance, lowercase-hex wire format, raw-byte SHA-256 at rest, fixed-time comparison, log redaction, revocation, and mandatory legacy re-pair.
- Clarified that the Bridge trusted-device store is not Keychain and never persists the plaintext token.
- Updated ADR 0003 to point reconnect proof to ADR 0014.
- Updated ADR 0013 so its previously unchanged/open KI-006 language no longer conflicts with the implemented token.
- Replaced the DeviceId-only reconnect section in `docs/SECURITY.md` with the implemented token lifecycle and remaining bearer-token boundary.
- Marked KI-006 mitigated on the Bridge in `docs/KNOWN_ISSUES.md`; recorded the remaining IOS-005 Keychain, hello, and certificate-pin work.
- Updated `docs/CURRENT_STATE.md`, BRIDGE-006 and IOS-005 backlog notes, `docs/DEVELOPMENT.md`, and the public README.
- Completed the KI-006 handoff with implementation, verification, migration, and remaining client work.

## Files changed

- `docs/adr/0014-reconnect-token.md` (new)
- `docs/adr/0003-bridge-protocol.md`
- `docs/adr/0013-bridge-tls-kestrel.md`
- `docs/SECURITY.md`
- `docs/KNOWN_ISSUES.md`
- `docs/CURRENT_STATE.md`
- `docs/BACKLOG.md`
- `docs/DEVELOPMENT.md`
- `docs/handoffs/KI-006.md`
- `README.md`
- `docs/superpowers/reports/task-4-reconnect-report.md` (this report)

## Verification

Command:

`dotnet test SimPulse.sln --configuration Release`

Result on Windows 10.0.26200:

- Domain: 6 passed
- Protocol: 10 passed
- Analytics: 9 passed
- Bridge Core: 135 passed
- Total: 160 passed, 0 failed, 0 skipped

Additional checks:

- `git diff --check`: clean.
- IDE documentation lint diagnostics: none.
- Repository search found no current operational docs that still describe KI-006 as planned/open or reconnect as DeviceId-only. Historical specs, plans, and prior handoffs remain unchanged as historical records.
- Apple builds and screenshots: NOT EXECUTED / not applicable; this task changes no Apple UI or source.

## Plane

- Project: `5de718ee-c465-4756-bc10-92ddf8e82604`
- Work item: KI-006 (`1c010b96-5a66-4c9b-b624-e629435d3f8b`)
- Comment created: `ab7a70c2-e49f-465c-955c-becd769a6f02`
- The comment summarizes token issuance, hash-only storage, IOS-005 obligations, and the 160-test result.
- Work item state was not changed. Done waits for merge.
- No token, token hash, PIN, certificate private material, or other secret was posted.

## Commit

`docs(bridge): record KI-006 reconnect token`

The commit contains only the Task 4 documentation and report files. Pre-existing untracked Task 2/3 reviewer artifacts and the Task 4 brief are intentionally excluded.

## Security and performance review

- Documentation matches the implementation: one 32-byte CSPRNG token, SHA-256 of raw bytes at rest, fixed-time comparison, no sensitive token logging.
- The token is explicitly documented as a bearer credential protected in transit by pinned TLS and at rest on iOS by Keychain.
- Legacy rows fail closed and re-pair; no compatibility path silently restores DeviceId-only trust.
- This docs-only task introduces no runtime work, allocations, I/O, polling, or performance change.

## Concerns

- IOS-005 is still required before the iPhone can use the new reconnect contract end to end.
- Existing trusted devices must pair again once because legacy rows have no token hash.
- Plane KI-006 intentionally remains open until merge.

## Final review fixes

- **Files:** `docs/ARCHITECTURE.md` now records reconnect-token SHA-256 storage without plaintext; `docs/BACKLOG.md` now requires IOS-005 to surface connected-but-untrusted sessions as re-pair required.
- **Tests:** `dotnet test SimPulse.sln --configuration Release` — 160 passed, 0 failed, 0 skipped.
- **Commit:** `docs(bridge): document reconnect hash in architecture` (this commit; its SHA is recorded in Git history because a commit cannot contain its own content-addressed SHA).
