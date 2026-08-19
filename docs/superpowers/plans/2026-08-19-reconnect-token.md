# KI-006 Reconnect Token Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** After PIN pairing, Bridge reconnect trust requires a per-device opaque token issued once in `pairing.accept` and hashed at rest; DeviceId-only hello is not trusted.

**Architecture:** Extend `HelloMessage` / `PairingAcceptMessage`. Shared `ReconnectToken` helper (32-byte CSPRNG, lowercase hex wire, SHA-256 of raw bytes at rest, fixed-time compare). `ITrustedDeviceStore.AuthorizeReconnectAsync` replaces `IsTrustedAsync`. `PairingCoordinator` issues the token on PIN success and authorizes hello with it. No transport or iOS changes.

**Tech Stack:** .NET 8, `System.Security.Cryptography`, existing `EnvelopeCodec` / xUnit, `JsonFileTrustedDeviceStore` + `InMemoryTrustedDeviceStore`

## Global Constraints

- Branch: rename current `docs/ki-006-reconnect-token` to `feat/reconnect-token` (spec already committed)
- Plane: claim KI-006 In Progress (`1c010b96-5a66-4c9b-b624-e629435d3f8b`)
- Spec: `docs/superpowers/specs/2026-08-19-reconnect-token-design.md`
- Token: 32 raw bytes; wire = lowercase hex 64, no colons; store = SHA-256(**raw bytes**), lowercase hex 64 — never hash the hex string
- Compare hashes with `CryptographicOperations.FixedTimeEquals`
- Never log token, raw bytes, or SHA-256 hash; Information may log `Trusted=` and `TokenPresent=` only
- Legacy store rows with null/empty `reconnectTokenSha256` → not trusted even if a token is sent
- `IsTrustedAsync(deviceId)` is removed
- No Apple UI → screenshots N/A
- Verify: `dotnet test SimPulse.sln --configuration Release` on Windows
- Conventional commits; this plan authorizes per-task commits
- Do not implement IOS-005

## File map

| Path | Role |
| --- | --- |
| `packages/protocol/SimPulse.Protocol/Messages.cs` | `HelloMessage.ReconnectToken`; `PairingAcceptMessage.ReconnectToken` |
| `packages/protocol/SimPulse.Protocol.Tests/EnvelopeCodecTests.cs` | Codec round-trips |
| `.../Application/ReconnectToken.cs` | Create / hex / SHA-256 / `MatchesStoredHash` |
| `.../Ports/Ports.cs` | `TrustedDevice` + store interface |
| `.../Adapters/LocalStores.cs` | In-memory store |
| `.../Adapters/JsonFileTrustedDeviceStore.cs` | JSON persist hash |
| `.../Application/PairingCoordinator.cs` | Issue + authorize |
| `...Tests/ReconnectTokenTests.cs` | Encoding / match / reject |
| `...Tests/JsonFileTrustedDeviceStoreTests.cs` | Hash persist; legacy JSON |
| `...Tests/BridgeCoreTests.cs` | In-memory trust tests |
| `...Tests/PairingCoordinatorTests.cs` | Hello token / DeviceId-only |
| `docs/adr/0014-reconnect-token.md` | ACCEPTED |
| `docs/SECURITY.md`, `KNOWN_ISSUES.md`, `CURRENT_STATE.md`, `BACKLOG.md`, ADR 0003/0013 notes, handoff | Docs |

---

### Task 1: Claim + branch

- [ ] Plane KI-006 → In Progress (id `1c010b96-5a66-4c9b-b624-e629435d3f8b`, state `f4b705bf-43a8-4896-a50e-d4f03d3a8a83`)
- [ ] `git branch -m feat/reconnect-token` if still named `docs/ki-006-reconnect-token`
- [ ] Handoff stub `docs/handoffs/KI-006.md` (IN_PROGRESS, next Task 2)
- [ ] Commit `docs(bridge): claim KI-006 reconnect token`

---

### Task 2: Protocol DTOs (TDD)

**Files:**
- Modify: `packages/protocol/SimPulse.Protocol/Messages.cs`
- Test: `packages/protocol/SimPulse.Protocol.Tests/EnvelopeCodecTests.cs`

**Produces:**

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

JSON: camelCase `reconnectToken`. `DefaultIgnoreCondition.WhenWritingNull` already omits null hello token.

- [ ] **Step 1: Write failing tests** (add to `EnvelopeCodecTests`):

```csharp
[Fact]
public void Round_trips_hello_reconnect_token()
{
    DateTimeOffset sent = DateTimeOffset.Parse("2026-08-18T08:00:00Z");
    HelloMessage hello = new("SimPulse", "phone", "phone-1", "ab".PadRight(64, 'c'));

    string json = EnvelopeCodec.Serialize(MessageTypes.Hello, hello, sent, "hello-token");
    Assert.Contains("reconnectToken", json, StringComparison.Ordinal);

    MessageEnvelope envelope = EnvelopeCodec.Deserialize(json);
    Assert.True(EnvelopeCodec.TryReadPayload(envelope, out HelloMessage? restored));
    Assert.Equal(hello.DeviceId, restored!.DeviceId);
    Assert.Equal(hello.ReconnectToken, restored.ReconnectToken);
}

[Fact]
public void Hello_without_reconnect_token_deserializes_null()
{
    const string json = """
        {
          "protocolVersion": 1,
          "type": "hello",
          "messageId": "abc",
          "sentAtUtc": "2026-08-18T08:00:00Z",
          "payload": { "product": "SimPulse", "role": "phone", "deviceId": "phone-1" }
        }
        """;

    MessageEnvelope envelope = EnvelopeCodec.Deserialize(json);
    Assert.True(EnvelopeCodec.TryReadPayload(envelope, out HelloMessage? hello));
    Assert.Equal("phone-1", hello!.DeviceId);
    Assert.Null(hello.ReconnectToken);
}

[Fact]
public void Round_trips_pairing_accept_reconnect_token()
{
    DateTimeOffset sent = DateTimeOffset.Parse("2026-08-18T08:00:00Z");
    PairingAcceptMessage accept = new("phone-1", sent, "ab".PadRight(64, 'd'));

    string json = EnvelopeCodec.Serialize(MessageTypes.PairingAccept, accept, sent, "acc1");
    Assert.Contains("reconnectToken", json, StringComparison.Ordinal);

    MessageEnvelope envelope = EnvelopeCodec.Deserialize(json);
    Assert.True(EnvelopeCodec.TryReadPayload(envelope, out PairingAcceptMessage? restored));
    Assert.Equal("phone-1", restored!.DeviceId);
    Assert.Equal(64, restored.ReconnectToken.Length);
    Assert.Equal(accept.ReconnectToken, restored.ReconnectToken);
}
```

- [ ] **Step 2: Run Protocol tests — expect compile fail** (`ReconnectToken` missing)

```powershell
dotnet test packages/protocol/SimPulse.Protocol.Tests/SimPulse.Protocol.Tests.csproj --filter FullyQualifiedName~EnvelopeCodecTests
```

- [ ] **Step 3: Update `Messages.cs`** to the records above. Keep existing 3-arg `HelloMessage(...)` compiling via default `ReconnectToken = null`.

- [ ] **Step 4: Protocol tests green.** Bridge may **not** compile until Task 3 (`PairingAcceptMessage` arity). Do not run full solution yet.

- [ ] **Step 5: Commit** `feat(protocol): add reconnectToken to hello and pairing.accept`

---

### Task 3: Token helper + store + coordinator (TDD)

**Files:**
- Create: `apps/windows-bridge/SimPulse.Bridge.Core/Application/ReconnectToken.cs`
- Create: `apps/windows-bridge/SimPulse.Bridge.Core.Tests/ReconnectTokenTests.cs`
- Modify: `apps/windows-bridge/SimPulse.Bridge.Core/Ports/Ports.cs`
- Modify: `apps/windows-bridge/SimPulse.Bridge.Core/Adapters/LocalStores.cs`
- Modify: `apps/windows-bridge/SimPulse.Bridge.Core/Adapters/JsonFileTrustedDeviceStore.cs`
- Modify: `apps/windows-bridge/SimPulse.Bridge.Core/Application/PairingCoordinator.cs`
- Modify: `apps/windows-bridge/SimPulse.Bridge.Core.Tests/JsonFileTrustedDeviceStoreTests.cs`
- Modify: `apps/windows-bridge/SimPulse.Bridge.Core.Tests/BridgeCoreTests.cs` (`TrustedDeviceStoreTests`)
- Modify: `apps/windows-bridge/SimPulse.Bridge.Core.Tests/PairingCoordinatorTests.cs`

**Produces:**

```csharp
public static class ReconnectToken
{
    public const int RawLength = 32;

    public static byte[] CreateRaw(); // RandomNumberGenerator.Fill
    public static string ToHex(ReadOnlySpan<byte> raw); // lowercase
    public static bool TryParseHex(string? hex, out byte[] raw); // length 64, chars [0-9a-f] only
    public static string Sha256Hex(ReadOnlySpan<byte> raw);
    public static bool MatchesStoredHash(string? storedSha256Hex, string? reconnectTokenHex);
    // MatchesStoredHash: false if stored hash null/empty or token parse fails;
    // SHA-256(raw token bytes) vs stored hash via Convert.FromHexString + FixedTimeEquals
}

public sealed record TrustedDevice(
    string DeviceId,
    DateTimeOffset TrustedAtUtc,
    bool Revoked,
    string? ReconnectTokenSha256);

public interface ITrustedDeviceStore
{
    Task<IReadOnlyList<TrustedDevice>> ListAsync(CancellationToken cancellationToken);
    Task TrustAsync(string deviceId, DateTimeOffset trustedAtUtc, string reconnectTokenSha256, CancellationToken cancellationToken);
    Task RevokeAsync(string deviceId, CancellationToken cancellationToken);
    Task<bool> AuthorizeReconnectAsync(string deviceId, string? reconnectTokenHex, CancellationToken cancellationToken);
}
```

`AuthorizeReconnectAsync`: lookup DeviceId; if missing/revoked/`ReconnectTokenSha256` null or empty → `false`; else `ReconnectToken.MatchesStoredHash(stored, token)`.

**Coordinator:**

```csharp
// Hello
connection.DeviceId = hello.DeviceId;
connection.IsTrusted = await _store.AuthorizeReconnectAsync(
    hello.DeviceId, hello.ReconnectToken, cancellationToken);
_logger.LogInformation(
    "Hello trust evaluated. Trusted={Trusted} TokenPresent={TokenPresent}",
    connection.IsTrusted,
    !string.IsNullOrEmpty(hello.ReconnectToken));

// PIN success
byte[] raw = ReconnectToken.CreateRaw();
string tokenHex = ReconnectToken.ToHex(raw);
string tokenSha256 = ReconnectToken.Sha256Hex(raw);
CryptographicOperations.ZeroMemory(raw);
await _store.TrustAsync(request.DeviceId, trustedAt, tokenSha256, cancellationToken);
connection.IsTrusted = true;
await SendAsync(
    connection,
    MessageTypes.PairingAccept,
    new PairingAcceptMessage(request.DeviceId, trustedAt, tokenHex),
    cancellationToken);
```

Never interpolate `tokenHex` / `tokenSha256` into logs.

- [ ] **Step 1: Failing `ReconnectTokenTests`**

```csharp
[Fact]
public void CreateRaw_is_32_bytes()
{
    byte[] raw = ReconnectToken.CreateRaw();
    Assert.Equal(32, raw.Length);
}

[Fact]
public void ToHex_is_64_lowercase()
{
    byte[] raw = Enumerable.Repeat((byte)0xAB, 32).ToArray();
    string hex = ReconnectToken.ToHex(raw);
    Assert.Equal(64, hex.Length);
    Assert.Equal(hex, hex.ToLowerInvariant());
    Assert.DoesNotContain(":", hex, StringComparison.Ordinal);
}

[Fact]
public void MatchesStoredHash_true_for_same_raw_bytes()
{
    byte[] raw = Enumerable.Repeat((byte)0x11, 32).ToArray();
    string hex = ReconnectToken.ToHex(raw);
    string hash = ReconnectToken.Sha256Hex(raw);
    Assert.True(ReconnectToken.MatchesStoredHash(hash, hex));
}

[Fact]
public void MatchesStoredHash_false_for_wrong_token_uppercase_hex_or_legacy_null()
{
    byte[] raw = Enumerable.Repeat((byte)0x11, 32).ToArray();
    string hex = ReconnectToken.ToHex(raw);
    string hash = ReconnectToken.Sha256Hex(raw);
    Assert.False(ReconnectToken.MatchesStoredHash(hash, hex[..^1] + "0"));
    Assert.False(ReconnectToken.MatchesStoredHash(hash, hex.ToUpperInvariant()));
    Assert.False(ReconnectToken.MatchesStoredHash(null, hex));
    Assert.False(ReconnectToken.MatchesStoredHash(hash, null));
}

[Fact]
public void Sha256Hex_hashes_raw_bytes_not_utf8_hex_string()
{
    byte[] raw = Enumerable.Repeat((byte)0x11, 32).ToArray();
    string hex = ReconnectToken.ToHex(raw);
    string ofRaw = ReconnectToken.Sha256Hex(raw);
    string ofUtf8Hex = Convert.ToHexString(SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(hex))).ToLowerInvariant();
    Assert.NotEqual(ofRaw, ofUtf8Hex);
}
```

- [ ] **Step 2: Implement `ReconnectToken` until those tests pass**

```powershell
dotnet test apps/windows-bridge/SimPulse.Bridge.Core.Tests/SimPulse.Bridge.Core.Tests.csproj --filter FullyQualifiedName~ReconnectToken
```

- [ ] **Step 3: Update store interface + both adapters.** `TrustAsync` 4th arg is hash hex. `AuthorizeReconnectAsync` as specified.

Json tests (replace `IsTrustedAsync`):

```csharp
await first.TrustAsync("phone-1", trustedAt, hash, CancellationToken.None);
Assert.True(await first.AuthorizeReconnectAsync("phone-1", tokenHex, CancellationToken.None));
```

Use a known raw buffer in tests (not `CreateRaw`) so expected hash is deterministic.

Legacy file:

```csharp
[Fact]
public async Task Legacy_json_without_hash_is_not_authorized()
{
    using TempStoreFile file = new();
    File.WriteAllText(file.Path, """
        [{"deviceId":"phone-old","trustedAtUtc":"2026-08-18T10:00:00+00:00","revoked":false}]
        """);
    JsonFileTrustedDeviceStore store = new(file.Path, NullLogger<JsonFileTrustedDeviceStore>.Instance);
    string anyToken = ReconnectToken.ToHex(Enumerable.Repeat((byte)0x22, 32).ToArray());
    Assert.False(await store.AuthorizeReconnectAsync("phone-old", anyToken, CancellationToken.None));
}
```

Also: persist does not contain plaintext token; file contains `reconnectTokenSha256` after Trust; revoked still false authorize; missing file empty.

In-memory `TrustedDeviceStoreTests`: same 4-arg Trust + AuthorizeReconnect; revoke still works.

- [ ] **Step 4: Coordinator tests**

Replace `Already_trusted_hello_sets_trusted_without_pin` with:

```csharp
[Fact]
public async Task Hello_with_reconnect_token_sets_trusted_without_pin()
{
    PairingHarness harness = CreateHarness();
    FakeClientConnection paired = new() { DeviceId = "phone-known" };
    await harness.Coordinator.HandleAsync(paired, PairingEnvelope("phone-known", FixedPin), CancellationToken.None);
    Assert.True(EnvelopeCodec.TryReadPayload(
        EnvelopeCodec.Deserialize(paired.Sent[0]),
        out PairingAcceptMessage? accept));
    string token = accept!.ReconnectToken;
    Assert.Equal(64, token.Length);

    FakeClientConnection reconnection = new();
    HelloMessage hello = new("SimPulse", "phone", "phone-known", token);
    await harness.Coordinator.HandleAsync(
        reconnection,
        EnvelopeCodec.Deserialize(EnvelopeCodec.Serialize(MessageTypes.Hello, hello, TrustedAt, "hello-1")),
        CancellationToken.None);

    Assert.True(reconnection.IsTrusted);
    Assert.Empty(reconnection.Sent);
}

[Fact]
public async Task Hello_device_id_only_does_not_trust_after_pair()
{
    // pair first, then hello without token
    Assert.False(reconnection.IsTrusted);
}

[Fact]
public async Task Hello_wrong_token_does_not_trust()
{
    // pair, hello with different 64 hex
}

[Fact]
public async Task Correct_pin_accept_includes_token_and_store_keeps_hash_only()
{
    // accept.ReconnectToken length 64 lowercase
    // AuthorizeReconnectAsync(device, token) true
    // ListAsync()[0].ReconnectTokenSha256 != token and length 64
}
```

Update every `IsTrustedAsync` / 3-arg `TrustAsync` in this file (wrong pin, unknown hello, lockout, second pair, etc.) to `AuthorizeReconnectAsync`. For “not in store” cases pass `null` token. After successful pair, assert authorize with token from accept rather than DeviceId-only.

`Accept_and_reject_logs_do_not_include_pin`: also capture accept token and `Assert.DoesNotContain` that token in log messages.

- [ ] **Step 5: Implement coordinator + stores. Full**

```powershell
dotnet test SimPulse.sln --configuration Release
```

Expected: all green (count ≥ 149 + new tests).

- [ ] **Step 6: Commit** `feat(bridge): require hashed reconnect token on hello`

---

### Task 4: Docs + Plane

**Files:**
- Create: `docs/adr/0014-reconnect-token.md`
- Modify: `docs/adr/0003-bridge-protocol.md` (one line: reconnect proof is ADR 0014)
- Modify: `docs/adr/0013-bridge-tls-kestrel.md` (KI-006 no longer “remains open”; token is 0014)
- Modify: `docs/SECURITY.md` reconnect section
- Modify: `docs/KNOWN_ISSUES.md` KI-006 closed/mitigated; IOS-005 must send token
- Modify: `docs/CURRENT_STATE.md`, `docs/BACKLOG.md` BRIDGE-006 notes, `README.md` if it still says KI-006 planned
- Modify: `docs/handoffs/KI-006.md` complete
- `docs/DEVELOPMENT.md` — one sentence: after pair, clients must persist `pairing.accept.reconnectToken` and send it on hello (IOS-005)

ADR 0014: opaque token vs HMAC vs DeviceId-only; hash at rest; legacy re-pair; not Keychain on Bridge.

- [ ] Commit `docs(bridge): record KI-006 reconnect token`
- [ ] Full `dotnet test SimPulse.sln --configuration Release`
- [ ] Plane comment; **Done only after merge**

---

## Spec coverage

| Spec item | Task |
| --- | --- |
| Hello/accept wire fields | 2 |
| 32-byte token, hex, SHA-256 raw bytes, lowercase | 3 (`ReconnectToken`) |
| FixedTimeEquals | 3 |
| Store hash only; TrustAsync hash arg | 3 |
| AuthorizeReconnect; legacy null hash | 3 |
| Coordinator issue + hello | 3 |
| No token/hash in logs | 3 |
| DeviceId-only / wrong token untrusted | 3 |
| Docs SECURITY / KI-006 / ADR 0014 / IOS-005 contract | 4 |
| No iOS code | all |

## Self-review notes

- Spec diagram said `TryAuthorize`; implement **`AuthorizeReconnectAsync`** only.
- `PairingAcceptMessage` arity change is why Task 2 is Protocol-only and Task 3 immediately updates Bridge.
- Tray UX / PIN window / TLS untouched.
