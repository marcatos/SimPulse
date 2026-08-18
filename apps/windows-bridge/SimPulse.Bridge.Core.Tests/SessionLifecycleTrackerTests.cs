using SimPulse.Bridge.Core.Application;
using SimPulse.Domain;

namespace SimPulse.Bridge.Core.Tests;

public sealed class SessionLifecycleTrackerTests
{
    [Fact]
    public void Emits_session_start_once()
    {
        SessionLifecycleTracker tracker = new();
        SessionId id = SessionId.New();
        TimestampInstant t = new(DateTimeOffset.Parse("2026-08-18T10:00:00Z"), ClockSource.SimulatorSession);
        TimestampInstant tLater = new(t.Value.AddSeconds(1), ClockSource.SimulatorSession);
        RaceEvent first = RaceEvent.Create(id, RaceEventType.SessionStart, t);
        RaceEvent second = RaceEvent.Create(id, RaceEventType.SessionStart, tLater);

        Assert.NotNull(tracker.Observe(first));
        Assert.Null(tracker.Observe(second));
    }

    [Fact]
    public void Emits_distinct_lap_completes_and_dedupes_same_lap()
    {
        SessionLifecycleTracker tracker = new();
        SessionId id = SessionId.New();
        TimestampInstant t = new(DateTimeOffset.Parse("2026-08-18T10:00:00Z"), ClockSource.SimulatorSession);
        RaceEvent lap1 = RaceEvent.Create(id, RaceEventType.LapComplete, t, new Dictionary<string, string> { ["lapNumber"] = "1" });
        RaceEvent lap1Again = RaceEvent.Create(id, RaceEventType.LapComplete, t, new Dictionary<string, string> { ["lapNumber"] = "1" });
        RaceEvent lap2 = RaceEvent.Create(id, RaceEventType.LapComplete, t, new Dictionary<string, string> { ["lapNumber"] = "2" });

        Assert.NotNull(tracker.Observe(lap1));
        Assert.Null(tracker.Observe(lap1Again));
        Assert.NotNull(tracker.Observe(lap2));
    }

    [Fact]
    public void Emits_distinct_lap_starts_and_dedupes_same_lap()
    {
        SessionLifecycleTracker tracker = new();
        SessionId id = SessionId.New();
        TimestampInstant t = new(DateTimeOffset.Parse("2026-08-18T10:00:00Z"), ClockSource.SimulatorSession);
        RaceEvent lap1 = RaceEvent.Create(id, RaceEventType.LapStart, t, new Dictionary<string, string> { ["lapNumber"] = "1" });
        RaceEvent lap1Again = RaceEvent.Create(id, RaceEventType.LapStart, t, new Dictionary<string, string> { ["lapNumber"] = "1" });
        RaceEvent lap2 = RaceEvent.Create(id, RaceEventType.LapStart, t, new Dictionary<string, string> { ["lapNumber"] = "2" });

        Assert.NotNull(tracker.Observe(lap1));
        Assert.Null(tracker.Observe(lap1Again));
        Assert.NotNull(tracker.Observe(lap2));
    }

    [Fact]
    public void Lap_starts_with_same_lap_and_different_session_num_are_both_emitted()
    {
        SessionLifecycleTracker tracker = new();
        SessionId id = SessionId.New();
        TimestampInstant t = new(DateTimeOffset.Parse("2026-08-18T10:00:00Z"), ClockSource.SimulatorSession);
        RaceEvent practice = RaceEvent.Create(
            id,
            RaceEventType.LapStart,
            t,
            new Dictionary<string, string> { ["lapNumber"] = "1", ["sessionNum"] = "0" });
        RaceEvent race = RaceEvent.Create(
            id,
            RaceEventType.LapStart,
            t,
            new Dictionary<string, string> { ["lapNumber"] = "1", ["sessionNum"] = "1" });
        RaceEvent raceAgain = RaceEvent.Create(
            id,
            RaceEventType.LapStart,
            t,
            new Dictionary<string, string> { ["lapNumber"] = "1", ["sessionNum"] = "1" });

        Assert.NotNull(tracker.Observe(practice));
        Assert.NotNull(tracker.Observe(race));
        Assert.Null(tracker.Observe(raceAgain));
    }

    [Fact]
    public void Session_end_is_idempotent()
    {
        SessionLifecycleTracker tracker = new();
        SessionId id = SessionId.New();
        TimestampInstant t = new(DateTimeOffset.Parse("2026-08-18T10:00:00Z"), ClockSource.SimulatorSession);
        RaceEvent end = RaceEvent.Create(id, RaceEventType.SessionEnd, t);
        Assert.NotNull(tracker.Observe(end));
        Assert.Null(tracker.Observe(end));
    }
}
