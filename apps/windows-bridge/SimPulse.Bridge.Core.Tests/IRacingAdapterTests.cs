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
        Assert.Equal(ClockSource.Utc, updates[0].CapturedAt.Source);
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

        List<NormalizedSimulatorUpdate> updates = await CollectUntilAsync(
            adapter,
            static _ => false,
            maxUpdates: 1,
            cancelAfter: TimeSpan.FromMilliseconds(200));

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

        List<NormalizedSimulatorUpdate> updates = await CollectUntilAsync(
            adapter,
            static list => list.Exists(u => u.RaceEvent?.Type == RaceEventType.SessionEnd));

        Assert.Contains(updates, u => u.RaceEvent?.Type == RaceEventType.SessionStart);
        Assert.Contains(updates, u => u.RaceEvent?.Type == RaceEventType.SessionEnd);
        Assert.Equal(ClockSource.Utc, updates.First(u => u.RaceEvent is not null).RaceEvent!.Timestamp.Source);
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

        List<NormalizedSimulatorUpdate> updates = await CollectUntilAsync(
            adapter,
            static list => list.Exists(u => u.RaceEvent?.Type == RaceEventType.SessionEnd));

        Assert.Contains(updates, u => u.RaceEvent?.Type == RaceEventType.SessionStart);
        Assert.Contains(updates, u => u.RaceEvent?.Type == RaceEventType.SessionEnd);
        Assert.True(updates.Last(u => u.RaceEvent?.Type == RaceEventType.SessionEnd).SessionSnapshot!.EndedAt.TryGet(out _));
        Assert.Equal(ClockSource.Utc, updates[^1].CapturedAt.Source);
    }

    [Fact]
    public async Task Adapter_resumes_new_session_after_disconnect()
    {
        string yaml = File.ReadAllText(FixturePathHelper.FixturePath("iracing", "session-info-sample.yaml"));
        FakeIracingSharedMemory memory = new(
            open: true,
            [
                new IracingMemorySnapshot(yaml, Connected: true),
                new IracingMemorySnapshot(null, Connected: false),
                new IracingMemorySnapshot(yaml, Connected: true)
            ]);
        IRacingAdapter adapter = new(memory, new SystemClock(), pollInterval: TimeSpan.Zero);

        List<NormalizedSimulatorUpdate> updates = await CollectUntilAsync(
            adapter,
            static list => list.Count(u => u.RaceEvent?.Type == RaceEventType.SessionStart) >= 2);

        List<NormalizedSimulatorUpdate> starts = updates.Where(u => u.RaceEvent?.Type == RaceEventType.SessionStart).ToList();
        Assert.Equal(2, starts.Count);
        Assert.Contains(updates, u => u.RaceEvent?.Type == RaceEventType.SessionEnd);
        Assert.NotEqual(starts[0].SessionId, starts[1].SessionId);
        Assert.All(updates, u => Assert.Equal(ClockSource.Utc, u.CapturedAt.Source));
    }

    private static async Task<List<NormalizedSimulatorUpdate>> CollectUntilAsync(
        IRacingAdapter adapter,
        Func<List<NormalizedSimulatorUpdate>, bool> done,
        int maxUpdates = 16,
        TimeSpan? cancelAfter = null)
    {
        List<NormalizedSimulatorUpdate> updates = [];
        using CancellationTokenSource cts = new(cancelAfter ?? TimeSpan.FromSeconds(2));
        try
        {
            await foreach (NormalizedSimulatorUpdate update in adapter.SubscribeAsync(cts.Token))
            {
                updates.Add(update);
                if (done(updates) || updates.Count >= maxUpdates)
                {
                    break;
                }
            }
        }
        catch (OperationCanceledException) when (cts.IsCancellationRequested)
        {
        }

        return updates;
    }
}
