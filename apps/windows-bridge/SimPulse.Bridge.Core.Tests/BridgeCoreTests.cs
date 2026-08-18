using SimPulse.Bridge.Core.Adapters;
using SimPulse.Bridge.Core.Application;
using SimPulse.Bridge.Core.Ports;
using SimPulse.Domain;

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
