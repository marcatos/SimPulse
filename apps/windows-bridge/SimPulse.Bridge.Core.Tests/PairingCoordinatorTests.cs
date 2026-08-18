using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

using SimPulse.Bridge.Core.Adapters;
using SimPulse.Bridge.Core.Application;
using SimPulse.Bridge.Core.Ports;
using SimPulse.Protocol;

namespace SimPulse.Bridge.Core.Tests;

public sealed class PairingCoordinatorTests
{
    private const string FixedPin = "123456";
    private static readonly DateTimeOffset TrustedAt = DateTimeOffset.Parse("2026-08-18T10:00:00Z");

    [Fact]
    public async Task Wrong_pin_sends_reject_and_does_not_trust()
    {
        PairingHarness harness = CreateHarness();
        FakeClientConnection connection = new() { DeviceId = "phone-1" };
        MessageEnvelope request = PairingEnvelope("phone-1", "000000");

        await harness.Coordinator.HandleAsync(connection, request, CancellationToken.None);

        Assert.False(connection.IsTrusted);
        Assert.False(await harness.Store.IsTrustedAsync("phone-1", CancellationToken.None));
        Assert.Single(connection.Sent);
        MessageEnvelope sent = EnvelopeCodec.Deserialize(connection.Sent[0]);
        Assert.Equal(MessageTypes.PairingReject, sent.Type);
        Assert.True(EnvelopeCodec.TryReadPayload(sent, out PairingRejectMessage? reject));
        Assert.Equal("phone-1", reject!.DeviceId);
        Assert.Equal("invalid_pin", reject.Reason);
    }

    [Fact]
    public async Task Correct_pin_trusts_and_sends_accept()
    {
        PairingHarness harness = CreateHarness();
        FakeClientConnection connection = new() { DeviceId = "phone-1" };
        MessageEnvelope request = PairingEnvelope("phone-1", FixedPin);

        await harness.Coordinator.HandleAsync(connection, request, CancellationToken.None);

        Assert.True(connection.IsTrusted);
        Assert.True(await harness.Store.IsTrustedAsync("phone-1", CancellationToken.None));
        Assert.Single(connection.Sent);
        MessageEnvelope sent = EnvelopeCodec.Deserialize(connection.Sent[0]);
        Assert.Equal(MessageTypes.PairingAccept, sent.Type);
        Assert.True(EnvelopeCodec.TryReadPayload(sent, out PairingAcceptMessage? accept));
        Assert.Equal("phone-1", accept!.DeviceId);
        Assert.Equal(TrustedAt, accept.TrustedAtUtc);
    }

    [Fact]
    public async Task Already_trusted_hello_sets_trusted_without_pin()
    {
        PairingHarness harness = CreateHarness();
        await harness.Store.TrustAsync("phone-known", TrustedAt, CancellationToken.None);
        FakeClientConnection connection = new();
        HelloMessage hello = new("SimPulse", "phone", "phone-known");
        MessageEnvelope envelope = EnvelopeCodec.Deserialize(
            EnvelopeCodec.Serialize(MessageTypes.Hello, hello, TrustedAt, "hello-1"));

        await harness.Coordinator.HandleAsync(connection, envelope, CancellationToken.None);

        Assert.Equal("phone-known", connection.DeviceId);
        Assert.True(connection.IsTrusted);
        Assert.Empty(connection.Sent);
    }

    [Fact]
    public async Task Unknown_hello_leaves_connection_untrusted()
    {
        PairingHarness harness = CreateHarness();
        FakeClientConnection connection = new();
        HelloMessage hello = new("SimPulse", "phone", "phone-unknown");
        MessageEnvelope envelope = EnvelopeCodec.Deserialize(
            EnvelopeCodec.Serialize(MessageTypes.Hello, hello, TrustedAt, "hello-unknown"));

        await harness.Coordinator.HandleAsync(connection, envelope, CancellationToken.None);

        Assert.Equal("phone-unknown", connection.DeviceId);
        Assert.False(connection.IsTrusted);
        Assert.False(await harness.Store.IsTrustedAsync("phone-unknown", CancellationToken.None));
        Assert.Empty(connection.Sent);
    }

    [Fact]
    public async Task Non_pairing_messages_do_not_log_information()
    {
        ListLogger<PairingCoordinator> logger = new();
        PairingCoordinator coordinator = new(
            new InMemoryTrustedDeviceStore(),
            new FixedClock(TrustedAt),
            new FixedPinGenerator(FixedPin),
            logger);
        coordinator.BeginPairingWindow();
        logger.Entries.Clear();

        HeartbeatMessage heartbeat = new("conn-1");
        MessageEnvelope heartbeatEnvelope = EnvelopeCodec.Deserialize(
            EnvelopeCodec.Serialize(MessageTypes.Heartbeat, heartbeat, TrustedAt, "hb-1"));
        TimeSyncRequestMessage timeSync = new(TrustedAt);
        MessageEnvelope timeSyncEnvelope = EnvelopeCodec.Deserialize(
            EnvelopeCodec.Serialize(MessageTypes.TimeSyncRequest, timeSync, TrustedAt, "ts-1"));

        await coordinator.HandleAsync(new FakeClientConnection(), heartbeatEnvelope, CancellationToken.None);
        await coordinator.HandleAsync(new FakeClientConnection(), timeSyncEnvelope, CancellationToken.None);

        Assert.DoesNotContain(logger.Entries, e => e.Level == LogLevel.Information);
        Assert.Contains(logger.Entries, e => e.Level == LogLevel.Debug && e.Message.Contains("Pairing handle", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Revoke_blocks_trust_and_broadcast_to_that_device()
    {
        PairingHarness harness = CreateHarness();
        FakeClientConnection connection = new() { DeviceId = "phone-1" };
        using ILoggerFactory factory = LoggerFactory.Create(_ => { });
        ClientSessionHub hub = new(factory.CreateLogger<ClientSessionHub>());
        hub.Register(connection);

        await harness.Coordinator.HandleAsync(
            connection,
            PairingEnvelope("phone-1", FixedPin),
            CancellationToken.None);
        connection.Sent.Clear();

        await harness.Coordinator.RevokeAsync("phone-1", CancellationToken.None);

        Assert.False(connection.IsTrusted);
        Assert.False(await harness.Store.IsTrustedAsync("phone-1", CancellationToken.None));

        await hub.BroadcastToTrustedAsync("{\"type\":\"simulator.race-event\"}", CancellationToken.None);
        Assert.Empty(connection.Sent);
    }

    [Fact]
    public async Task Unregister_drops_connection_so_revoke_does_not_touch_it()
    {
        PairingHarness harness = CreateHarness();
        FakeClientConnection connection = new() { DeviceId = "phone-gone" };

        await harness.Coordinator.HandleAsync(
            connection,
            PairingEnvelope("phone-gone", FixedPin),
            CancellationToken.None);
        Assert.True(harness.Coordinator.IsRegistered(connection));
        Assert.True(connection.IsTrusted);
        connection.Sent.Clear();

        harness.Coordinator.Unregister(connection);
        Assert.False(harness.Coordinator.IsRegistered(connection));

        await harness.Coordinator.RevokeAsync("phone-gone", CancellationToken.None);

        Assert.True(connection.IsTrusted);
        Assert.Empty(connection.Sent);
        Assert.False(await harness.Store.IsTrustedAsync("phone-gone", CancellationToken.None));
    }

    [Fact]
    public async Task Pairing_request_before_window_is_rejected()
    {
        PairingHarness harness = CreateHarness(beginWindow: false);
        FakeClientConnection connection = new() { DeviceId = "phone-1" };

        await harness.Coordinator.HandleAsync(connection, PairingEnvelope("phone-1", FixedPin), CancellationToken.None);

        Assert.False(connection.IsTrusted);
        Assert.False(await harness.Store.IsTrustedAsync("phone-1", CancellationToken.None));
        AssertReject(connection, "phone-1", PairingCoordinator.WindowClosedReason);
    }

    [Fact]
    public async Task Expired_pairing_window_rejects_even_with_correct_pin()
    {
        MutableClock clock = new(TrustedAt);
        PairingHarness harness = CreateHarness(clock, beginWindow: true);
        FakeClientConnection connection = new() { DeviceId = "phone-1" };

        clock.UtcNow = TrustedAt.Add(PairingCoordinator.WindowDuration).AddSeconds(1);
        await harness.Coordinator.HandleAsync(connection, PairingEnvelope("phone-1", FixedPin), CancellationToken.None);

        Assert.False(connection.IsTrusted);
        AssertReject(connection, "phone-1", PairingCoordinator.WindowClosedReason);
    }

    [Fact]
    public async Task Five_wrong_pins_lock_window_against_correct_pin()
    {
        PairingHarness harness = CreateHarness();
        for (int i = 0; i < PairingCoordinator.MaxFailedAttempts; i++)
        {
            FakeClientConnection failed = new() { DeviceId = $"phone-bad-{i}" };
            await harness.Coordinator.HandleAsync(
                failed,
                PairingEnvelope(failed.DeviceId!, "000000"),
                CancellationToken.None);
            AssertReject(failed, failed.DeviceId!, PairingCoordinator.InvalidPinReason);
        }

        FakeClientConnection locked = new() { DeviceId = "phone-late" };
        await harness.Coordinator.HandleAsync(
            locked,
            PairingEnvelope("phone-late", FixedPin),
            CancellationToken.None);

        Assert.False(locked.IsTrusted);
        Assert.False(await harness.Store.IsTrustedAsync("phone-late", CancellationToken.None));
        AssertReject(locked, "phone-late", PairingCoordinator.TooManyAttemptsReason);
    }

    [Fact]
    public async Task Successful_pair_closes_window_so_same_pin_requires_new_window()
    {
        PairingHarness harness = CreateHarness();
        FakeClientConnection first = new() { DeviceId = "phone-1" };
        await harness.Coordinator.HandleAsync(first, PairingEnvelope("phone-1", FixedPin), CancellationToken.None);
        Assert.True(first.IsTrusted);

        FakeClientConnection second = new() { DeviceId = "phone-2" };
        await harness.Coordinator.HandleAsync(second, PairingEnvelope("phone-2", FixedPin), CancellationToken.None);

        Assert.False(second.IsTrusted);
        AssertReject(second, "phone-2", PairingCoordinator.WindowClosedReason);

        harness.Coordinator.BeginPairingWindow();
        FakeClientConnection third = new() { DeviceId = "phone-3" };
        await harness.Coordinator.HandleAsync(third, PairingEnvelope("phone-3", FixedPin), CancellationToken.None);
        Assert.True(third.IsTrusted);
        Assert.True(await harness.Store.IsTrustedAsync("phone-3", CancellationToken.None));
    }

    [Fact]
    public async Task Malformed_pairing_payload_does_not_throw()
    {
        PairingHarness harness = CreateHarness();
        FakeClientConnection connection = new();
        const string json = """
            {"protocolVersion":1,"type":"pairing.request","messageId":"bad","sentAtUtc":"2026-08-18T08:00:00Z","payload":123}
            """;

        await harness.Coordinator.HandleAsync(
            connection,
            EnvelopeCodec.Deserialize(json),
            CancellationToken.None);

        Assert.False(connection.IsTrusted);
        Assert.Empty(connection.Sent);
    }

    [Fact]
    public void BeginPairingWindow_logs_pin_each_time_window_opens()
    {
        ListLogger<PairingCoordinator> logger = new();
        PairingCoordinator coordinator = new(
            new InMemoryTrustedDeviceStore(),
            new FixedClock(TrustedAt),
            new FixedPinGenerator(FixedPin),
            logger);

        coordinator.BeginPairingWindow();
        coordinator.BeginPairingWindow();

        List<string> pinLogs = logger.Entries
            .Where(e => e.Level == LogLevel.Information && e.Message.Contains(FixedPin, StringComparison.Ordinal))
            .Select(e => e.Message)
            .ToList();
        Assert.Equal(2, pinLogs.Count);
        Assert.All(pinLogs, message => Assert.Contains("Pairing window opened", message, StringComparison.Ordinal));
    }

    [Fact]
    public async Task Accept_and_reject_logs_do_not_include_pin()
    {
        ListLogger<PairingCoordinator> logger = new();
        PairingCoordinator coordinator = new(
            new InMemoryTrustedDeviceStore(),
            new FixedClock(TrustedAt),
            new FixedPinGenerator(FixedPin),
            logger);
        coordinator.BeginPairingWindow();
        logger.Entries.Clear();

        await coordinator.HandleAsync(
            new FakeClientConnection { DeviceId = "phone-ok" },
            PairingEnvelope("phone-ok", FixedPin),
            CancellationToken.None);
        await coordinator.HandleAsync(
            new FakeClientConnection { DeviceId = "phone-bad" },
            PairingEnvelope("phone-bad", "000000"),
            CancellationToken.None);

        Assert.DoesNotContain(
            logger.Entries,
            e => e.Message.Contains(FixedPin, StringComparison.Ordinal));
    }

    [Fact]
    public void Pairing_pin_generator_returns_six_digits()
    {
        PairingPinGenerator generator = new();
        for (int i = 0; i < 32; i++)
        {
            string pin = generator.Generate();
            Assert.Equal(6, pin.Length);
            Assert.True(pin.All(char.IsDigit));
        }
    }

    private static PairingHarness CreateHarness(bool beginWindow = true)
    {
        return CreateHarness(new FixedClock(TrustedAt), beginWindow);
    }

    private static PairingHarness CreateHarness(IClock clock, bool beginWindow)
    {
        InMemoryTrustedDeviceStore store = new();
        PairingCoordinator coordinator = new(
            store,
            clock,
            new FixedPinGenerator(FixedPin),
            NullLogger<PairingCoordinator>.Instance);
        if (beginWindow)
        {
            coordinator.BeginPairingWindow();
        }

        return new PairingHarness(coordinator, store);
    }

    private static void AssertReject(FakeClientConnection connection, string deviceId, string reason)
    {
        Assert.Single(connection.Sent);
        MessageEnvelope sent = EnvelopeCodec.Deserialize(connection.Sent[0]);
        Assert.Equal(MessageTypes.PairingReject, sent.Type);
        Assert.True(EnvelopeCodec.TryReadPayload(sent, out PairingRejectMessage? reject));
        Assert.Equal(deviceId, reject!.DeviceId);
        Assert.Equal(reason, reject.Reason);
    }

    private static MessageEnvelope PairingEnvelope(string deviceId, string pin)
    {
        PairingRequestMessage request = new(deviceId, pin);
        string json = EnvelopeCodec.Serialize(MessageTypes.PairingRequest, request, TrustedAt, "pair-1");
        return EnvelopeCodec.Deserialize(json);
    }

    private sealed record PairingHarness(PairingCoordinator Coordinator, InMemoryTrustedDeviceStore Store);
}

internal sealed class FixedClock : IClock
{
    public FixedClock(DateTimeOffset utcNow)
    {
        UtcNow = utcNow;
    }

    public DateTimeOffset UtcNow { get; }
}

internal sealed class MutableClock : IClock
{
    public MutableClock(DateTimeOffset utcNow)
    {
        UtcNow = utcNow;
    }

    public DateTimeOffset UtcNow { get; set; }
}

internal sealed class FixedPinGenerator : IPairingPinGenerator
{
    private readonly string _pin;

    public FixedPinGenerator(string pin)
    {
        _pin = pin;
    }

    public string Generate() => _pin;
}

internal sealed class ListLogger<T> : ILogger<T>
{
    public List<(LogLevel Level, string Message)> Entries { get; } = [];

    public IDisposable BeginScope<TState>(TState state)
        where TState : notnull
        => NullScope.Instance;

    public bool IsEnabled(LogLevel logLevel) => true;

    public void Log<TState>(
        LogLevel logLevel,
        EventId eventId,
        TState state,
        Exception? exception,
        Func<TState, Exception?, string> formatter)
    {
        Entries.Add((logLevel, formatter(state, exception)));
    }

    private sealed class NullScope : IDisposable
    {
        public static readonly NullScope Instance = new();

        public void Dispose()
        {
        }
    }
}
