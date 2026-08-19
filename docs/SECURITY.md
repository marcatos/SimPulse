# Security

## Threat model (Phase 0, lightweight)

| Threat | Impact | Mitigations now | Later |
| --- | --- | --- | --- |
| Malicious LAN device talks to Bridge | Fake session metadata, nuisance | Bridge accepts sessions only after pairing; TLS is default and exposes a certificate fingerprint for client pinning; bind configurable | IOS-005 client pin enforcement |
| Unauthorized Bridge connection | Untrusted PC feeds iPhone | Explicit PIN pairing; persist trusted device IDs; revocation | Device attestation if needed |
| Reconnect impersonation | Attacker claims a known trusted DeviceId without PIN | Reconnect requires a random per-device token; Bridge stores only its SHA-256 hash; fixed-time comparison; revoke invalidates trust | Challenge-response if the threat model expands |
| Replay of protocol messages | Duplicate events | Message IDs; idempotent merge | Sequence numbers + window |
| Spoofed simulator events | Distorted reports | Adapter is local process; paired Bridge is the trust boundary | Signed session summaries |
| Compromised pairing credentials | Attacker becomes trusted | PIN is not stored in the trusted-device file; generated with `RandomNumberGenerator`; valid only during an explicit pairing window (5 minutes, successful pair, or 5 failed attempts) | Rotate pairing material |
| Pairing PIN in file logs | Local reader recovers PIN from `bridge-yyyyMMdd.log` | Coordinator logs `Pin=` at Information when a window opens (required); UX adapters do not log `Pin=`; restrict ACLs on `%LOCALAPPDATA%\SimPulse\logs` (or `SIMPULSE_LOG_DIR`) | Drop PIN from file logs or encrypt the log dir |
| Local data exposure | Biometrics on disk | HealthKit for workouts; Bridge does not persist HR | iOS Data Protection review |
| Sensitive logs | HR in log files | Never log sample payloads; redact IDs if needed | Log scrub tests |

## Pairing window lifecycle (Phase 0)

`BeginPairingWindow()` is called when the Bridge host starts (`Worker.ExecuteAsync`) and again when the tray **Pair new device** command fires (`TrayPairingPresenter`). **Show current PIN** redisplays the last PIN without calling `BeginPairingWindow` and without invalidating it, and only while the window is still open. After a successful pair, 5-minute expiry, or 5-attempt lockout, the current window closes, last PIN is cleared, and **Show current PIN** reports `pairing window closed` until the next explicit open. A process restart is not required. The PIN is not persisted in the trusted-device store; the coordinator Information log line includes `Pin=` and file logs retain it.

## Reconnect trust

After PIN pairing succeeds, the Bridge generates a random 32-byte reconnect token. `pairing.accept.reconnectToken` returns the token once as 64-character lowercase hexadecimal. The trusted-device store persists the client **DeviceId** and lowercase SHA-256 of the raw token bytes, never the plaintext token.

On reconnect, `hello` must carry both the DeviceId and `reconnectToken`. The Bridge decodes the lowercase token, hashes the raw bytes, and compares that hash with the stored value using `CryptographicOperations.FixedTimeEquals`.

Operational rules:

- **Client custody:** IOS-005 must persist the plaintext token in Keychain, not UserDefaults, and send it on every hello.
- **Legacy rows re-pair:** trusted-device rows without `reconnectTokenSha256` cannot reconnect. A new PIN pair issues a token.
- **Reject invalid proof:** missing, malformed, uppercase, or incorrect tokens remain untrusted and receive no telemetry.
- **Revocation:** `RevokeAsync` invalidates the DeviceId/token pair; the user must pair again.
- **No sensitive logs:** the Bridge may log whether a token was present and whether trust succeeded, but never the token, raw bytes, or stored hash.
- **Bearer-token boundary:** the token is replayable if stolen. Pinned TLS protects it in transit; clients must protect it at rest.
- **Explicit local opt-out:** `SIMPULSE_BRIDGE_TLS=0`, `false`, or `off` enables cleartext only when the Bridge host is `127.0.0.1` or `localhost`.

See [ADR 0014](adr/0014-reconnect-token.md).

## Bridge TLS and certificate pinning

The Bridge defaults to `wss://127.0.0.1:8742/ws/` using a persisted self-signed certificate. It logs `TlsCertSha256` at Information when the certificate is ready and when the TLS listener starts. This value is lowercase SHA-256 of the certificate DER with no colons; clients must pin it and reject any mismatch. IOS-005 remains responsible for implementing this check in the iPhone client.

By default the PFX is generated under `%LOCALAPPDATA%\SimPulse\certs`. `SIMPULSE_BRIDGE_CERT_PATH`, `SIMPULSE_BRIDGE_CERT_PASSWORD`, and `SIMPULSE_BRIDGE_CERT_DIR` customize certificate loading and persistence. Passwords and private key material are never logged and must not be committed.

TLS protects transport confidentiality and server identity once a client pins the fingerprint. ADR 0014 adds the separate per-device reconnect proof; IOS-005 must implement both certificate pinning and token custody.

## Defaults

- Pairing is required before telemetry is sent.
- Bridge WebSocket TLS is enabled unless explicitly disabled for loopback-only diagnostics.
- Do not listen on public WAN interfaces by product default. LAN only.
- No analytics SDK, no ads SDK.
- Do not invent cryptography. Use platform TLS and OS credential storage when those steps are implemented.

## Secrets

See `.env.example`. Never commit Apple certificates, App Store Connect keys, pairing stores, or tokens.

## What we are not doing in Phase 0

Mutual TLS, custom crypto protocols, and kernel-level hardening. Unsafe defaults (open unauthenticated WebSocket that streams forever) are also not acceptable.
