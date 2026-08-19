# Task 3 report — Kestrel TLS transport and cleartext policy

## Status

DONE

## Scope delivered

- Added `Microsoft.AspNetCore.App` as a framework reference for Bridge Core.
- Added `BridgeTlsPolicy` with explicit loopback-only cleartext enforcement.
- Added `KestrelWebSocketTransport` as an `IBridgeTransport` adapter.
- Bound Kestrel HTTPS to the configured host and port using the shared certificate.
- Accepted `/ws` and `/ws/` WebSocket requests and reused the existing connection, hub, and message-pump flow.
- Logged TLS enabled state, certificate SHA-256 fingerprint, host, port, lifecycle, and elapsed time.
- Left certificate ownership with `IBridgeCertificateSource`; the transport never disposes it.
- Kept `HttpListenerWebSocketTransport`, `Program.cs`, environment wiring, and pairing behavior unchanged.

## TDD evidence

1. `BridgeTlsPolicyTests` initially failed to compile because `BridgeTlsPolicy` did not exist.
2. The policy implementation made the four policy cases pass.
3. `TlsWebSocketTransportTests` initially failed to compile because `KestrelWebSocketTransport` did not exist.
4. The first TLS run then exposed a Windows-specific handshake failure:
   `Authentication failed because the platform does not support ephemeral keys`.
5. The existing certificate adapter imported PFX keys with `EphemeralKeySet`; Windows Schannel cannot use those keys for Kestrel server authentication.
6. Changed PFX imports to `UserKeySet | Exportable`, preserving non-elevated per-user operation while enabling Schannel.
7. Pin acceptance, pin rejection, cleartext refusal, certificate-source, and existing cleartext tests pass.

## Tests added

- `IsLoopbackHost_accepts_supported_loopback_hosts`
- `EnsureCleartextAllowed_allows_loopback_when_tls_is_disabled`
- `EnsureCleartextAllowed_refuses_all_interfaces_when_tls_is_disabled`
- `Tls_accepts_websocket_with_pinned_fingerprint`
- `Tls_rejects_client_when_pin_mismatches`

## Verification

Command:

```text
dotnet test SimPulse.sln --configuration Release
```

Result: 139 passed, 0 failed, 0 skipped:

- Domain: 6
- Protocol: 7
- Analytics: 9
- Bridge Core: 117

Focused TLS and certificate run: 10 passed, 0 failed.

## Files changed

- `apps/windows-bridge/SimPulse.Bridge.Core/SimPulse.Bridge.Core.csproj`
- `apps/windows-bridge/SimPulse.Bridge.Core/Adapters/BridgeTlsPolicy.cs`
- `apps/windows-bridge/SimPulse.Bridge.Core/Adapters/KestrelWebSocketTransport.cs`
- `apps/windows-bridge/SimPulse.Bridge.Core/Adapters/FileBridgeCertificateSource.cs`
- `apps/windows-bridge/SimPulse.Bridge.Core.Tests/TlsWebSocketTransportTests.cs`
- `docs/superpowers/reports/task-3-report.md`

## Concerns

None. Task 4 still owns `Program.cs`, environment selection, and public documentation wiring.
