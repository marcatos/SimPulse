# KI-006 — Per-device reconnect token (hashed at rest)

**Date:** 2026-08-19  
**Status:** Approved  
**Backlog / tracker:** KI-006 (KNOWN_ISSUES); related ADR 0003, SECURITY.md, BRIDGE-006  
**Depends on:** BRIDGE-006 pairing (DONE), KI-003 TLS (DONE). Does **not** include iOS pairing client (IOS-005) or HMAC challenge-response.

## Goal

After PIN pairing, a reconnecting client must prove possession of a **per-device reconnect token** issued once in `pairing.accept`. Knowing only a trusted `DeviceId` is not enough. Windows Bridge + C# protocol + tests only.

## Non-goals

- iOS UI, Keychain storage, or sending the token (IOS-005 consumes this contract)
- Challenge-response HMAC / nonce per connection
- Mutual TLS
- Changing PIN generation, pairing window, or tray UX
- Logging or committing plaintext tokens
- KI-008 WatchConnectivity E2E
- Allowing DeviceId-only reconnect for legacy store rows

## Context

- Today `hello` with a previously trusted `DeviceId` sets `IsTrusted = true` with no proof of possession (KI-006).
- TLS (KI-003) encrypts the wire and pins the server cert; it does not bind the client to a secret.
- Protocol v1 ignores unknown JSON fields. No shipping iOS Bridge client yet — breaking reconnect for old store rows is acceptable.
- User-approved: opaque token (A), re-pair required for rows without hash (1), extend existing `hello` / `pairing.accept` (1).

## Chosen decisions

| Topic | Choice |
| --- | --- |
| Proof | Opaque bearer token, not HMAC |
| Issue | Once, in `pairing.accept.reconnectToken` |
| At rest | SHA-256 of the **raw 32-byte** secret, stored as lowercase hex (64 chars). Wire token is lowercase hex of those same 32 bytes (not a hash of the hex string). |
| Hello | Optional `reconnectToken` string; trust requires present + matching hash |
| Legacy store | Missing `reconnectTokenSha256` → not trusted (must PIN again) |
| iOS | Document contract only |
| Compare | Fixed-time compare of hash bytes, not of plaintext |

**Token encoding (locked):** 32 cryptographically random bytes. On the wire and in `pairing.accept`: lowercase hex, no colons, length 64. Store: SHA-256 over the **raw 32 bytes** (not over the hex string), lowercase hex 64. This matches the KI-003 fingerprint style (hex of a digest) and avoids hex-normalization bugs.

## Architecture

```text
Client                         Bridge
  hello (deviceId, token?)  →  PairingCoordinator
                                 → ITrustedDeviceStore.TryAuthorize(deviceId, token)
  pairing.request (pin)     →  generate 32-byte token
  pairing.accept (token)    ←  store SHA-256(raw bytes); send hex once
```

Hexagonal: protocol DTOs in `SimPulse.Protocol`; store + coordinator in Bridge.Core. No transport change.

### Protocol

```csharp
public sealed record HelloMessage(
    string Product,
    string Role,
    string DeviceId,
    string? ReconnectToken = null);

public sealed record PairingAcceptMessage(
    string DeviceId,
    DateTimeOffset TrustedAtUtc,
    string ReconnectToken);
```

JSON names: camelCase `reconnectToken` (existing envelope codec policy). Missing `reconnectToken` on hello deserializes as null. `PairingAccept` after this change always includes the token for new pairs.

### Ports / store

```csharp
public sealed record TrustedDevice(
    string DeviceId,
    DateTimeOffset TrustedAtUtc,
    bool Revoked,
    string? ReconnectTokenSha256); // lowercase hex SHA-256 of raw 32-byte token; null = legacy

public interface ITrustedDeviceStore
{
    Task<IReadOnlyList<TrustedDevice>> ListAsync(CancellationToken cancellationToken);
    Task TrustAsync(string deviceId, DateTimeOffset trustedAtUtc, string reconnectTokenSha256, CancellationToken cancellationToken);
    Task RevokeAsync(string deviceId, CancellationToken cancellationToken);
    Task<bool> AuthorizeReconnectAsync(string deviceId, string? reconnectTokenHex, CancellationToken cancellationToken);
}
```

`IsTrustedAsync(deviceId)` is **removed**. Tests that previously asserted store trust after PIN must use `AuthorizeReconnectAsync` with the token from `pairing.accept` (or the hash path via a test helper). `ListAsync` may still show DeviceIds for operator/debug; do not log hashes.

`AuthorizeReconnectAsync`:

1. No device, revoked, or `ReconnectTokenSha256` null/empty → `false`.
2. `reconnectTokenHex` null/empty/not 64 lowercase hex → `false`.
3. Decode hex to 32 bytes; SHA-256; compare to stored hash with `CryptographicOperations.FixedTimeEquals`.
4. Never log token, hash, or DeviceId in full if we can avoid it; Information may log `Trusted=true|false` and `TokenPresent=true|false` only.

`JsonFileTrustedDeviceStore`: persist `reconnectTokenSha256`; ignore unknown JSON fields; atomic replace as today. In-memory store for tests updated the same way.

### PairingCoordinator

- **Hello:** set `connection.DeviceId`; `IsTrusted = await AuthorizeReconnectAsync(...)`. Wrong/missing token → untrusted (no pairing lockout; pairing window is PIN-only).
- **Pairing PIN success:** `RandomNumberGenerator.Fill` 32 bytes; hex for accept payload; SHA-256 of raw bytes for `TrustAsync`; send `PairingAcceptMessage` including hex token **once**.
- **Revoke:** unchanged besides store shape; live connections with that DeviceId untrusted.

### Logging / secrets

- Never log `reconnectToken`, raw bytes, or SHA-256 hash.
- Do not write plaintext tokens to the trusted-device JSON file.
- Pairing PIN logging unchanged (existing KI; out of scope to remove `Pin=`).

## Testing (Windows / `dotnet test`)

- Codec: hello with and without `reconnectToken`; accept includes token; unknown extra fields still ignored.
- Pair → accept token length 64 lowercase hex; store file/memory has hash only, not plaintext.
- Hello + correct token → trusted; DeviceId-only → untrusted; wrong token → untrusted.
- Legacy `TrustedDevice` with hash null → untrusted even with any token? **Locked:** null hash → untrusted regardless of token (must re-pair so a new hash is stored). Sending a token cannot resurrect a legacy row.
- Coordinator tests do not print tokens in assertion messages beyond length/format.
- Existing PIN window tests still pass.

## Documentation

- `docs/SECURITY.md` — reconnect is token + DeviceId; hash at rest; legacy re-pair.
- `docs/KNOWN_ISSUES.md` — KI-006 mitigated/closed; IOS-005 must persist token (Keychain) and send it on hello.
- Short ADR `docs/adr/0014-reconnect-token.md` ACCEPTED (opaque token vs HMAC).
- Note on ADR 0003: TLS done (0013); reconnect proof is token (0014).
- `docs/CURRENT_STATE.md`, handoff `docs/handoffs/KI-006.md`.
- Screenshots N/A (no Apple UI).

## IOS-005 contract (do not implement here)

- After `pairing.accept`, store `reconnectToken` in Keychain (not UserDefaults).
- Subsequent `hello` must include the same hex string.
- Loss of token ⇒ user must PIN-pair again.
- Never log the token.

## Risks

- Existing trusted-device files become inert until re-pair (intentional).
- Token in `pairing.accept` is visible to anyone who can read that WebSocket message (TLS default mitigates on the LAN; still do not log it).
- Hex vs raw hashing mismatch — tests must use the locked encoding above.

## Open questions

None. Encoding, legacy behavior, and wire shape are locked.
