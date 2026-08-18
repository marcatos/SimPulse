using SimPulse.Bridge.Core.Adapters;
using SimPulse.Bridge.Core.Application;
using SimPulse.Bridge.Core.Ports;
using SimPulse.Domain;
using SimPulse.Protocol;

using Microsoft.Extensions.Logging;

namespace SimPulse.Bridge.Core.Tests;

public sealed class FixtureSimulatorAdapterTests
{
    [Fact]
    public async Task Replays_normalized_session_without_iracing()
    {
        string path = FixturePathHelper.FixturePath("telemetry", "iracing-practice-short.json");
        FixtureSimulatorAdapter adapter = new(path);

        Assert.True(await adapter.IsAvailableAsync(CancellationToken.None));
        List<NormalizedSimulatorUpdate> updates = [];
        await foreach (NormalizedSimulatorUpdate update in adapter.SubscribeAsync(CancellationToken.None))
        {
            updates.Add(update);
        }

        Assert.NotEmpty(updates);
        Assert.Equal(SimulatorIds.IRacing, updates[0].SimulatorId);
        Assert.Contains(updates, u => u.RaceEvent?.Type == RaceEventType.SessionStart);
        Assert.Contains(updates, u => u.RaceEvent?.Type == RaceEventType.LapComplete);
        Assert.Contains(updates, u => u.Telemetry is not null);
        SimulatorSession? last = updates[^1].SessionSnapshot;
        Assert.NotNull(last);
        Assert.True(last!.Track.TryGet(out Track? track));
        Assert.Equal("okayama", track!.Id);
        Assert.True(last.Vehicle.TryGet(out Vehicle? vehicle));
        Assert.Equal("mx5-cup", vehicle!.Id);
        Assert.Equal(2, last.Laps.Count);
    }
}

public sealed class TrustedDeviceStoreTests
{
    [Fact]
    public async Task Trust_and_revoke_are_idempotent()
    {
        InMemoryTrustedDeviceStore store = new();
        DateTimeOffset at = DateTimeOffset.Parse("2026-08-18T08:00:00Z");

        await store.TrustAsync("iphone-1", at, CancellationToken.None);
        await store.TrustAsync("iphone-1", at, CancellationToken.None);
        Assert.True(await store.IsTrustedAsync("iphone-1", CancellationToken.None));

        await store.RevokeAsync("iphone-1", CancellationToken.None);
        Assert.False(await store.IsTrustedAsync("iphone-1", CancellationToken.None));
    }
}

public sealed class BridgeRuntimeTests
{
    [Fact]
    public async Task Runtime_replays_fixture_and_completes()
    {
        string path = FixturePathHelper.FixturePath("telemetry", "iracing-practice-short.json");
        FixtureSimulatorAdapter adapter = new(path);
        using ILoggerFactory factory = LoggerFactory.Create(builder => builder.SetMinimumLevel(LogLevel.Debug));
        BridgeRuntime runtime = new(adapter, factory.CreateLogger<BridgeRuntime>());

        await runtime.RunAsync(CancellationToken.None);
    }

    [Fact]
    public async Task IRacing_stub_is_unavailable()
    {
        IRacingAdapter adapter = new();
        Assert.False(await adapter.IsAvailableAsync(CancellationToken.None));
    }

    [Fact]
    public async Task Unavailable_adapter_stops_when_cancelled()
    {
        IRacingAdapter adapter = new();
        using ILoggerFactory factory = LoggerFactory.Create(_ => { });
        BridgeRuntime runtime = new(adapter, factory.CreateLogger<BridgeRuntime>());
        using CancellationTokenSource cts = new(TimeSpan.FromMilliseconds(100));
        await runtime.RunAsync(cts.Token);
    }

    [Fact]
    public async Task Runtime_dedupes_duplicate_race_events_when_logging_and_broadcasting()
    {
        SessionId sessionId = SessionId.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
        TimestampInstant at = new(DateTimeOffset.Parse("2026-08-18T10:00:00Z"), ClockSource.SimulatorSession);
        RaceEvent start = RaceEvent.Create(sessionId, RaceEventType.SessionStart, at);
        ScriptedSimulatorAdapter adapter = new(
            new NormalizedSimulatorUpdate(SimulatorIds.IRacing, sessionId, at, start, null, null),
            new NormalizedSimulatorUpdate(SimulatorIds.IRacing, sessionId, at, start, null, null));
        ListLogger<BridgeRuntime> logger = new();
        ClientSessionHub hub = new(Microsoft.Extensions.Logging.Abstractions.NullLogger<ClientSessionHub>.Instance);
        FakeClientConnection trusted = new() { IsTrusted = true, DeviceId = "phone-1" };
        FakeClientConnection untrusted = new() { IsTrusted = false, DeviceId = "phone-2" };
        hub.Register(trusted);
        hub.Register(untrusted);

        BridgeRuntime runtime = new(adapter, logger, hub, new FixedClock(at.Value));
        await runtime.RunAsync(CancellationToken.None);

        Assert.Equal(1, logger.Entries.Count(e => e.Level == LogLevel.Information && e.Message.Contains("Race event", StringComparison.Ordinal)));
        Assert.Single(trusted.Sent);
        Assert.Empty(untrusted.Sent);
        MessageEnvelope sent = EnvelopeCodec.Deserialize(trusted.Sent[0]);
        Assert.Equal(MessageTypes.RaceEvent, sent.Type);
        Assert.True(EnvelopeCodec.TryReadPayload(sent, out RaceEventMessage? payload));
        Assert.Equal(sessionId.ToString(), payload!.SessionId);
        Assert.Equal(nameof(RaceEventType.SessionStart), payload.EventType);
    }
}

internal sealed class ScriptedSimulatorAdapter : ISimulatorAdapter
{
    private readonly NormalizedSimulatorUpdate[] _updates;

    public ScriptedSimulatorAdapter(params NormalizedSimulatorUpdate[] updates)
    {
        _updates = updates;
    }

    public string SimulatorId => SimulatorIds.IRacing;

    public Task<bool> IsAvailableAsync(CancellationToken cancellationToken)
    {
        return Task.FromResult(true);
    }

    public async IAsyncEnumerable<NormalizedSimulatorUpdate> SubscribeAsync(
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken)
    {
        foreach (NormalizedSimulatorUpdate update in _updates)
        {
            cancellationToken.ThrowIfCancellationRequested();
            yield return update;
        }

        await Task.CompletedTask;
    }
}

internal static class FixturePathHelper
{
    public static string FixturePath(params string[] parts)
    {
        string root = FindRepoRoot();
        return Path.Combine(new[] { root, "tests", "fixtures" }.Concat(parts).ToArray());
    }

    private static string FindRepoRoot()
    {
        DirectoryInfo? current = new(AppContext.BaseDirectory);
        while (current is not null)
        {
            if (File.Exists(Path.Combine(current.FullName, "SimPulse.sln")))
            {
                return current.FullName;
            }

            current = current.Parent;
        }

        throw new DirectoryNotFoundException("Could not locate SimPulse.sln from test output.");
    }
}
