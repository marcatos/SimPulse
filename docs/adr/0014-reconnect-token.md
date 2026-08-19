# ADR 0014 — Per-device reconnect token

- **Status:** ACCEPTED
- **Date:** 2026-08-19

## Context

PIN pairing originally persisted only a client-chosen DeviceId. A reconnecting client could therefore claim any known trusted DeviceId and receive telemetry without proving that it completed the original pairing. TLS protects the connection and authenticates the Bridge when its certificate is pinned, but it does not prove possession of per-device pairing material.

Reconnect needs a small protocol-v1-compatible proof that can be implemented by the iPhone client without adding a custom challenge protocol or storing plaintext bearer material on the Bridge.

## Decision

- After a successful PIN pair, the Bridge generates a cryptographically random 32-byte opaque reconnect token.
- `pairing.accept.reconnectToken` carries the token once as 64-character lowercase hexadecimal. Clients must persist it and include it in subsequent `hello.reconnectToken` messages.
- The Bridge stores only lowercase SHA-256 of the raw 32 token bytes with the trusted DeviceId. It does not persist the plaintext token.
- Reconnect authorization requires both the DeviceId and a valid token whose raw-byte SHA-256 matches the stored hash using a fixed-time comparison.
- Missing, malformed, uppercase, or incorrect tokens are untrusted. Revoked devices are untrusted.
- Legacy trusted-device rows without `reconnectTokenSha256` remain readable but cannot reconnect; the user must pair again to receive a token.
- The Bridge trusted-device store is not an OS credential vault and does not use Keychain. IOS-005 must store the plaintext token in iOS Keychain, not UserDefaults.
- Tokens, token hashes, and raw token bytes must not be logged.

## Alternatives considered

- **DeviceId-only reconnect:** rejected because DeviceId is client-asserted and provides no proof that the reconnecting client completed PIN pairing.
- **HMAC challenge-response:** stronger replay properties, but it adds challenge state, another round trip, and more protocol surface. A high-entropy bearer token over pinned TLS is sufficient for the current LAN threat model.
- **Store the opaque token on the Bridge:** simpler comparison, but a trusted-device file disclosure would immediately reveal reusable credentials. Hashing the raw token at rest limits that exposure.
- **Use the DeviceId as an HMAC key:** rejected because DeviceId is public, client-selected identity rather than secret key material.

## Consequences

- Knowing a trusted DeviceId is no longer enough to reconnect.
- The reconnect token is a bearer credential. Clients must protect it, and pinned TLS remains required to prevent network disclosure or Bridge impersonation.
- Existing installations require one new PIN pairing per legacy trusted device.
- Revocation continues to invalidate reconnect authorization for that DeviceId.
- IOS-005 now owns Keychain persistence and sending the token on every hello.

## Verification

Protocol tests cover token fields and null legacy hellos. Bridge tests cover generation, lowercase encoding, raw-byte hashing, fixed-time comparison, hash-only persistence, legacy rows, revocation, correct/missing/wrong tokens, and log redaction.
