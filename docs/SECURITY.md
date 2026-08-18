# Security

## Threat model (Phase 0, lightweight)

| Threat | Impact | Mitigations now | Later |
| --- | --- | --- | --- |
| Malicious LAN device talks to Bridge | Fake session metadata, nuisance | Bridge accepts sessions only after pairing; bind configurable | TLS with pinned cert |
| Unauthorized Bridge connection | Untrusted PC feeds iPhone | Explicit PIN pairing; persist trusted device IDs; revocation | Device attestation if needed |
| Reconnect impersonation (DeviceId-only trust) | Attacker replays a known trusted DeviceId without PIN | Pairing PIN establishes trust once; revoke removes DeviceId from store | Per-device reconnect secret; TLS |
| Replay of protocol messages | Duplicate events | Message IDs; idempotent merge | Sequence numbers + window |
| Spoofed simulator events | Distorted reports | Adapter is local process; paired Bridge is the trust boundary | Signed session summaries |
| Compromised pairing credentials | Attacker becomes trusted | PIN is not persisted; generated with `RandomNumberGenerator`; valid only during an explicit pairing window (5 minutes, successful pair, or 5 failed attempts) | Rotate pairing material |
| Local data exposure | Biometrics on disk | HealthKit for workouts; Bridge does not persist HR | iOS Data Protection review |
| Sensitive logs | HR in log files | Never log sample payloads; redact IDs if needed | Log scrub tests |

## Pairing window lifecycle (Phase 0)

`BeginPairingWindow()` is called **once** when the Bridge host starts (`Worker.ExecuteAsync`). After a successful pair, 5-minute expiry, or 5-attempt lockout, the window stays closed for the remainder of that process lifetime. Opening a new window today requires a **process restart** (or a future tray **Pair new device** action in BRIDGE-007).

## Reconnect trust (Phase 0)

After PIN pairing succeeds, the Bridge persists the client **DeviceId** as trusted. On reconnect, a `hello` message carrying that DeviceId is accepted as trusted **without** re-entering the PIN.

Phase 0 limitations (intentional, documented):

- **DeviceId-only:** trust is keyed only on the client-asserted DeviceId string.
- **Cleartext:** WebSocket traffic is not encrypted; no TLS.
- **No per-device secret:** reconnect does not require a stored token, HMAC, or challenge response.
- **Revocation is the mitigation:** `RevokeAsync` drops the DeviceId from the trusted store; subsequent Hellos with that id are untrusted until a new PIN pair.

Any LAN client that knows (or guesses) a trusted DeviceId can reconnect as that device until it is revoked. Pairing PIN establishes initial trust; it is not re-checked on every connection.

## Defaults

- Pairing is required before telemetry is sent.
- Do not listen on public WAN interfaces by product default. LAN only.
- No analytics SDK, no ads SDK.
- Do not invent cryptography. Use platform TLS and OS credential storage when those steps are implemented.

## Secrets

See `.env.example`. Never commit Apple certificates, App Store Connect keys, pairing stores, or tokens.

## What we are not doing in Phase 0

Mutual TLS, custom crypto protocols, and kernel-level hardening. Unsafe defaults (open unauthenticated WebSocket that streams forever) are also not acceptable.
