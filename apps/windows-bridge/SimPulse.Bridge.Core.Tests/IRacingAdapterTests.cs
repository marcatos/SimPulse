using SimPulse.Bridge.Core.Adapters;
using SimPulse.Bridge.Core.Ports;
using SimPulse.Domain;

namespace SimPulse.Bridge.Core.Tests;

public sealed class IRacingAdapterTests
{
    [Fact]
    public async Task Adapter_available_when_fake_memory_open()
    {
        string yaml = File.ReadAllText(FixturePathHelper.FixturePath("iracing", "session-info-sample.yaml"));
        FakeIracingSharedMemory memory = new(open: true, yaml: yaml);
        IRacingAdapter adapter = new(memory, new SystemClock(), pollInterval: TimeSpan.Zero);

        Assert.True(await adapter.IsAvailableAsync(CancellationToken.None));

        List<NormalizedSimulatorUpdate> updates = [];
        await foreach (NormalizedSimulatorUpdate update in adapter.SubscribeAsync(CancellationToken.None))
        {
            updates.Add(update);
            if (updates.Count > 3)
            {
                break;
            }
        }

        Assert.NotEmpty(updates);
        Assert.Equal(SimulatorIds.IRacing, updates[0].SimulatorId);
        Assert.Contains(updates, u => u.RaceEvent?.Type == RaceEventType.SessionStart);
        Assert.True(updates[^1].SessionSnapshot!.Track.TryGet(out Track? track));
        Assert.Equal("Okayama International Raceway", track!.DisplayName);
        Assert.True(updates[^1].SessionSnapshot!.Vehicle.TryGet(out Vehicle? vehicle));
        Assert.Equal("Mazda MX-5 Cup", vehicle!.DisplayName);
    }

    [Fact]
    public async Task Adapter_empty_stream_when_memory_unavailable()
    {
        FakeIracingSharedMemory memory = new(open: false);
        IRacingAdapter adapter = new(memory, new SystemClock(), pollInterval: TimeSpan.Zero);

        Assert.False(await adapter.IsAvailableAsync(CancellationToken.None));

        List<NormalizedSimulatorUpdate> updates = [];
        await foreach (NormalizedSimulatorUpdate update in adapter.SubscribeAsync(CancellationToken.None))
        {
            updates.Add(update);
        }

        Assert.Empty(updates);
    }

    [Fact]
    public async Task Adapter_starts_after_memory_becomes_connected()
    {
        string yaml = File.ReadAllText(FixturePathHelper.FixturePath("iracing", "session-info-sample.yaml"));
        FakeIracingSharedMemory memory = new(
            open: true,
            [
                new IracingMemorySnapshot(null, Connected: false),
                new IracingMemorySnapshot(yaml, Connected: true),
                new IracingMemorySnapshot(null, Connected: false)
            ]);
        IRacingAdapter adapter = new(memory, new SystemClock(), pollInterval: TimeSpan.Zero);

        List<NormalizedSimulatorUpdate> updates = [];
        await foreach (NormalizedSimulatorUpdate update in adapter.SubscribeAsync(CancellationToken.None))
        {
            updates.Add(update);
        }

        Assert.Contains(updates, u => u.RaceEvent?.Type == RaceEventType.SessionStart);
        Assert.Contains(updates, u => u.RaceEvent?.Type == RaceEventType.SessionEnd);
    }

    [Fact]
    public async Task Adapter_emits_session_end_when_connection_lost()
    {
        string yaml = File.ReadAllText(FixturePathHelper.FixturePath("iracing", "session-info-sample.yaml"));
        FakeIracingSharedMemory memory = new(
            open: true,
            [
                new IracingMemorySnapshot(yaml, Connected: true),
                new IracingMemorySnapshot(null, Connected: false)
            ]);
        IRacingAdapter adapter = new(memory, new SystemClock(), pollInterval: TimeSpan.Zero);

        List<NormalizedSimulatorUpdate> updates = [];
        await foreach (NormalizedSimulatorUpdate update in adapter.SubscribeAsync(CancellationToken.None))
        {
            updates.Add(update);
        }

        Assert.Contains(updates, u => u.RaceEvent?.Type == RaceEventType.SessionStart);
        Assert.Contains(updates, u => u.RaceEvent?.Type == RaceEventType.SessionEnd);
        Assert.True(updates[^1].SessionSnapshot!.EndedAt.TryGet(out _));
    }
}
