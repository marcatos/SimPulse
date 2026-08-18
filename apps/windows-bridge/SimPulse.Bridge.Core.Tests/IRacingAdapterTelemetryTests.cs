using SimPulse.Bridge.Core.Adapters;
using SimPulse.Bridge.Core.Ports;
using SimPulse.Domain;

namespace SimPulse.Bridge.Core.Tests;

public sealed class IRacingAdapterTelemetryTests
{
    [Fact]
    public async Task Player_car_and_session_come_from_telemetry_indices()
    {
        string yaml = File.ReadAllText(FixturePathHelper.FixturePath("iracing", "session-info-two-drivers.yaml"));
        FakeIracingSharedMemory memory = new(
            open: true,
            [
                Snapshot(yaml, update: 1, Telemetry(driverCarIdx: 3, sessionNum: 1))
            ]);
        IRacingAdapter adapter = new(memory, new SystemClock(), pollInterval: TimeSpan.Zero);

        List<NormalizedSimulatorUpdate> updates = await CollectUntilAsync(
            adapter,
            static list => list.Exists(u => u.RaceEvent?.Type == RaceEventType.SessionStart));

        SimulatorSession snapshot = updates[^1].SessionSnapshot!;
        Assert.True(snapshot.Vehicle.TryGet(out Vehicle? vehicle));
        Assert.Equal("Mazda MX-5 Cup", vehicle!.DisplayName);
        Assert.NotEqual("Other Car", vehicle.DisplayName);
        Assert.True(snapshot.SessionType.TryGet(out SimulatorSessionType sessionType));
        Assert.Equal(SimulatorSessionType.Race, sessionType);
        Assert.Equal(ClockSource.Utc, updates[0].CapturedAt.Source);
        Assert.Equal(ClockSource.Utc, updates.First(u => u.RaceEvent?.Type == RaceEventType.SessionStart).RaceEvent!.Timestamp.Source);
    }

    [Fact]
    public async Task Same_session_info_update_keeps_cached_vehicle_when_yaml_text_changes()
    {
        string yaml = File.ReadAllText(FixturePathHelper.FixturePath("iracing", "session-info-two-drivers.yaml"));
        string mutated = MutatePlayerCar(yaml, "mutated-car", "Mutated Car");
        FakeIracingSharedMemory memory = new(
            open: true,
            [
                Snapshot(yaml, update: 4, Telemetry(driverCarIdx: 3, sessionNum: 1)),
                Snapshot(mutated, update: 4, Telemetry(driverCarIdx: 3, sessionNum: 1))
            ]);
        IRacingAdapter adapter = new(memory, new SystemClock(), pollInterval: TimeSpan.Zero);

        List<NormalizedSimulatorUpdate> updates = await CollectUntilAsync(
            adapter,
            static list => list.Count(u => u.SessionSnapshot is not null) >= 2);

        Assert.All(
            updates.Where(u => u.SessionSnapshot?.Vehicle.TryGet(out _) == true),
            u =>
            {
                Assert.True(u.SessionSnapshot!.Vehicle.TryGet(out Vehicle? vehicle));
                Assert.Equal("Mazda MX-5 Cup", vehicle!.DisplayName);
            });
    }

    [Fact]
    public async Task Session_info_update_change_reparses_yaml()
    {
        string yaml = File.ReadAllText(FixturePathHelper.FixturePath("iracing", "session-info-two-drivers.yaml"));
        string mutated = MutatePlayerCar(yaml, "mutated-car", "Mutated Car");
        FakeIracingSharedMemory memory = new(
            open: true,
            [
                Snapshot(yaml, update: 4, Telemetry(driverCarIdx: 3, sessionNum: 1)),
                Snapshot(mutated, update: 5, Telemetry(driverCarIdx: 3, sessionNum: 1))
            ]);
        IRacingAdapter adapter = new(memory, new SystemClock(), pollInterval: TimeSpan.Zero);

        List<NormalizedSimulatorUpdate> updates = await CollectUntilAsync(
            adapter,
            static list => list.Exists(u =>
                u.SessionSnapshot?.Vehicle.TryGet(out Vehicle? vehicle) == true &&
                vehicle!.DisplayName == "Mutated Car"));

        Assert.Contains(updates, u =>
            u.SessionSnapshot?.Vehicle.TryGet(out Vehicle? vehicle) == true &&
            vehicle!.DisplayName == "Mazda MX-5 Cup");
        Assert.Contains(updates, u =>
            u.SessionSnapshot?.Vehicle.TryGet(out Vehicle? vehicle) == true &&
            vehicle!.DisplayName == "Mutated Car");
        Assert.Equal(1, updates.Count(u => u.RaceEvent?.Type == RaceEventType.SessionStart));
    }

    [Fact]
    public async Task Lap_increase_emits_start_then_complete_then_start()
    {
        string yaml = File.ReadAllText(FixturePathHelper.FixturePath("iracing", "session-info-sample.yaml"));
        DateTimeOffset startUtc = DateTimeOffset.Parse("2026-08-18T12:00:00Z");
        FakeClock clock = new(startUtc);
        FakeIracingSharedMemory memory = new(
            open: true,
            [
                Snapshot(yaml, update: 1, Telemetry(sessionTime: 5, lap: 1)),
                Snapshot(yaml, update: 1, Telemetry(sessionTime: 70, lap: 2))
            ]);
        IRacingAdapter adapter = new(memory, clock, pollInterval: TimeSpan.Zero);

        List<NormalizedSimulatorUpdate> updates = await CollectUntilAsync(
            adapter,
            static list => list.Count(u => u.RaceEvent?.Type == RaceEventType.LapStart) >= 2);

        List<RaceEvent> events = updates.Where(u => u.RaceEvent is not null).Select(u => u.RaceEvent!).ToList();
        Assert.Equal(
            new[] { RaceEventType.SessionStart, RaceEventType.LapStart, RaceEventType.LapComplete, RaceEventType.LapStart },
            events.Select(e => e.Type).Take(4));
        Assert.Equal("1", events[1].Attributes["lapNumber"]);
        Assert.Equal("1", events[2].Attributes["lapNumber"]);
        Assert.Equal("2", events[3].Attributes["lapNumber"]);
        Assert.Equal(ClockSource.Utc, events[0].Timestamp.Source);
        Assert.Equal(ClockSource.SimulatorSession, events[1].Timestamp.Source);
        Assert.Equal(startUtc + TimeSpan.FromSeconds(5), events[1].Timestamp.Value);
        Assert.Equal(startUtc + TimeSpan.FromSeconds(70), events[3].Timestamp.Value);
        Assert.All(updates, u => Assert.Equal(ClockSource.Utc, u.CapturedAt.Source));

        SimulatorSession last = updates.Last(u => u.RaceEvent?.Type == RaceEventType.LapStart).SessionSnapshot!;
        Assert.Equal(2, last.Laps.Count);
        Assert.Equal(1, last.Laps[0].LapNumber);
        Assert.True(last.Laps[0].CompletedAt.TryGet(out _));
        Assert.Equal(2, last.Laps[1].LapNumber);
        Assert.False(last.Laps[1].CompletedAt.TryGet(out _));
    }

    [Fact]
    public void Fake_wait_for_update_returns_true_immediately()
    {
        FakeIracingSharedMemory memory = new(open: true, yaml: "WeekendInfo:\n");
        Assert.True(memory.WaitForUpdate(TimeSpan.Zero, CancellationToken.None));
    }

    private static IracingMemorySnapshot Snapshot(string yaml, int update, IracingTelemetryValues telemetry)
    {
        return new IracingMemorySnapshot(yaml, Connected: true, update, telemetry);
    }

    private static IracingTelemetryValues Telemetry(
        double? sessionTime = null,
        int? sessionNum = null,
        int? driverCarIdx = null,
        int? lap = null)
    {
        return new IracingTelemetryValues(
            sessionTime is { } time
                ? OptionalValue<double>.Available(time)
                : OptionalValue<double>.Unknown(),
            sessionNum is { } num
                ? OptionalValue<int>.Available(num)
                : OptionalValue<int>.Unknown(),
            driverCarIdx is { } car
                ? OptionalValue<int>.Available(car)
                : OptionalValue<int>.Unknown(),
            lap is { } lapNumber
                ? OptionalValue<int>.Available(lapNumber)
                : OptionalValue<int>.Unknown());
    }

    private static string MutatePlayerCar(string yaml, string carPath, string displayName)
    {
        return yaml
            .Replace("CarPath: mazda mx-5 cup", "CarPath: " + carPath, StringComparison.Ordinal)
            .Replace("CarScreenName: Mazda MX-5 Cup", "CarScreenName: " + displayName, StringComparison.Ordinal);
    }

    private static async Task<List<NormalizedSimulatorUpdate>> CollectUntilAsync(
        IRacingAdapter adapter,
        Func<List<NormalizedSimulatorUpdate>, bool> done,
        int maxUpdates = 16)
    {
        List<NormalizedSimulatorUpdate> updates = [];
        using CancellationTokenSource cts = new(TimeSpan.FromSeconds(2));
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

    private sealed class FakeClock : IClock
    {
        public FakeClock(DateTimeOffset utcNow)
        {
            UtcNow = utcNow;
        }

        public DateTimeOffset UtcNow { get; }
    }
}
