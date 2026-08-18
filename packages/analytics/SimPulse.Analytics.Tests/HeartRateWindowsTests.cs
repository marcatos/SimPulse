using SimPulse.Domain;

namespace SimPulse.Analytics.Tests;

public sealed class HeartRateWindowsTests
{
    [Fact]
    public void Refuses_join_when_timeline_offset_unknown()
    {
        DateTimeOffset start = DateTimeOffset.Parse("2026-08-18T10:00:00Z");
        HeartRateSample[] samples =
        [
            new(new TimestampInstant(start.AddMinutes(1), ClockSource.WorkoutSession), 120)
        ];
        Lap lap = new(
            SessionId.New(),
            1,
            new TimestampInstant(start, ClockSource.SimulatorSession),
            OptionalValue<TimestampInstant>.Available(new TimestampInstant(start.AddMinutes(2), ClockSource.SimulatorSession)),
            OptionalValue<TimeSpan>.Available(TimeSpan.FromMinutes(2)),
            OptionalValue<int>.Unavailable());

        OptionalValue<double> avg = HeartRateWindows.AverageBpmForLap(
            samples,
            lap,
            OptionalValue<TimeSpan>.Unavailable());

        Assert.Equal(DataPresence.Unavailable, avg.Presence);
    }

    [Fact]
    public void Averages_hr_inside_lap_after_applying_offset()
    {
        DateTimeOffset simStart = DateTimeOffset.Parse("2026-08-18T10:00:00Z");
        // Workout clock is 5 seconds ahead of simulator clock.
        TimeSpan offset = TimeSpan.FromSeconds(5);
        HeartRateSample[] samples =
        [
            new(new TimestampInstant(simStart.AddSeconds(5), ClockSource.WorkoutSession), 100),  // maps to sim t=0
            new(new TimestampInstant(simStart.AddSeconds(65), ClockSource.WorkoutSession), 140), // maps to sim t=60
            new(new TimestampInstant(simStart.AddSeconds(125), ClockSource.WorkoutSession), 180) // maps to sim t=120 — outside lap ending at 90s
        ];
        Lap lap = new(
            SessionId.New(),
            1,
            new TimestampInstant(simStart, ClockSource.SimulatorSession),
            OptionalValue<TimestampInstant>.Available(new TimestampInstant(simStart.AddSeconds(90), ClockSource.SimulatorSession)),
            OptionalValue<TimeSpan>.Available(TimeSpan.FromSeconds(90)),
            OptionalValue<int>.Unavailable());

        OptionalValue<double> avg = HeartRateWindows.AverageBpmForLap(
            samples,
            lap,
            OptionalValue<TimeSpan>.Available(offset));

        Assert.True(avg.TryGet(out double value));
        Assert.Equal(120, value, 0); // (100+140)/2
    }

    [Fact]
    public void Event_window_uses_half_window_around_simulator_event()
    {
        DateTimeOffset simEvent = DateTimeOffset.Parse("2026-08-18T10:01:00Z");
        TimeSpan offset = TimeSpan.Zero;
        HeartRateSample[] samples =
        [
            new(new TimestampInstant(simEvent.AddSeconds(-2), ClockSource.WorkoutSession), 110),
            new(new TimestampInstant(simEvent, ClockSource.WorkoutSession), 130),
            new(new TimestampInstant(simEvent.AddSeconds(2), ClockSource.WorkoutSession), 150),
            new(new TimestampInstant(simEvent.AddSeconds(10), ClockSource.WorkoutSession), 200)
        ];
        RaceEvent evt = RaceEvent.Create(
            SessionId.New(),
            RaceEventType.LapComplete,
            new TimestampInstant(simEvent, ClockSource.SimulatorSession));

        OptionalValue<double> avg = HeartRateWindows.AverageBpmAroundEvent(
            samples,
            evt,
            TimeSpan.FromSeconds(3),
            OptionalValue<TimeSpan>.Available(offset));

        Assert.True(avg.TryGet(out double value));
        Assert.Equal(130, value, 0); // 110,130,150 only
    }
}
