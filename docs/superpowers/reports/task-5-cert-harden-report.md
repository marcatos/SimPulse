# Task 5 — Bridge certificate persistence hardening

Status: **DONE**

## Changes

- Persist generated PFX data through an owner-restricted, uniquely named temporary file.
- Publish the certificate with an atomic same-directory move that cannot overwrite an existing identity.
- If another process wins the create race, discard the generated identity and load the winning PFX from disk.
- Protect both temporary and final files:
  - Windows: disable ACL inheritance and grant full control only to the current user.
  - Linux/macOS: set mode to `0600` (`UserRead | UserWrite`).
- Clear exported PFX bytes from memory after persistence.
- Add regression coverage for Windows/Unix file permissions and concurrent creation consistency.

## Verification

- `dotnet test --filter FullyQualifiedName~BridgeCertificate`
  - PASS — 6 Bridge certificate tests.
- `dotnet test SimPulse.sln --configuration Release`
  - PASS — 149 tests total (6 Domain, 7 Protocol, 9 Analytics, 127 Bridge Core).

The permission and race tests were confirmed failing before the implementation and passing afterward.
