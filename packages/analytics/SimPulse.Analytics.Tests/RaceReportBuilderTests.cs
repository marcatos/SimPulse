using SimPulse.Domain;

namespace SimPulse.Analytics.Tests;

public sealed class RaceReportBuilderTests
{
    [Fact]
    public void Workout_only_session_marks_simulator_fields_unavailable()
    {
        DateTimeOffset start = DateTimeOffset.Parse("2026-08-18T10:00:00Z");
        WorkoutSession workout = new(
            SessionId.New(),
            new TimestampInstant(start, ClockSource.WorkoutSession),
            OptionalValue<TimestampInstant>.Available(new TimestampInstant(start.AddMinutes(20), ClockSource.WorkoutSession)),
            [
                new HeartRateSample(new TimestampInstant(start, ClockSource.WorkoutSession), 100),
                new HeartRateSample(new TimestampInstant(start.AddMinutes(10), ClockSource.WorkoutSession), 140)
            ],
            [new EnergySample(new TimestampInstant(start.AddMinutes(20), ClockSource.WorkoutSession), 22.0)],
            OptionalValue<int>.Unavailable(),
            OptionalValue<int>.Unavailable(),
            OptionalValue<double>.Unavailable());

        DriverSession session = new(
            workout.Id,
            workout,
            OptionalValue<SimulatorSession>.Unavailable(),
            OptionalValue<TimeSpan>.Unavailable());

        RaceReport report = RaceReportBuilder.FromDriverSession(session);

        Assert.Equal(DataPresence.Unavailable, report.SimulatorDisplayName.Presence);
        Assert.Equal(DataPresence.Unavailable, report.TrackDisplayName.Presence);
        Assert.Equal(DataPresence.Unavailable, report.VehicleDisplayName.Presence);
        Assert.Equal(DataPresence.Unavailable, report.PeakHeartRateAssociatedEvent.Presence);
        Assert.True(report.AverageHeartRateBpm.TryGet(out double avg));
        Assert.Equal(120, avg, 0);
        Assert.True(report.MaximumHeartRateBpm.TryGet(out int max));
        Assert.Equal(140, max);
        Assert.True(report.ActiveKilocalories.TryGet(out double kcal));
        Assert.Equal(22.0, kcal);
        Assert.True(report.Duration.TryGet(out TimeSpan duration));
        Assert.Equal(TimeSpan.FromMinutes(20), duration);
        Assert.DoesNotContain("stress", MeasurementWording.HeartRateChangePercent(100, 140), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Without_timeline_offset_does_not_invent_associated_race_event()
    {
        DateTimeOffset start = DateTimeOffset.Parse("2026-08-18T10:00:00Z");
        SessionId id = SessionId.New();
        WorkoutSession workout = new(
            id,
            new TimestampInstant(start, ClockSource.WorkoutSession),
            OptionalValue<TimestampInstant>.Available(new TimestampInstant(start.AddMinutes(5), ClockSource.WorkoutSession)),
            [new HeartRateSample(new TimestampInstant(start.AddMinutes(2), ClockSource.WorkoutSession), 150)],
            Array.Empty<EnergySample>(),
            OptionalValue<int>.Unavailable(),
            OptionalValue<int>.Unavailable(),
            OptionalValue<double>.Unavailable());

        SimulatorSession sim = new(
            id,
            new Simulator(SimulatorIds.IRacing, "iRacing"),
            OptionalValue<Track>.Available(new Track("okayama", "Okayama", OptionalValue<string>.Unavailable())),
            OptionalValue<Vehicle>.Available(new Vehicle("mx5", "MX-5", OptionalValue<string>.Unavailable())),
            OptionalValue<SimulatorSessionType>.Available(SimulatorSessionType.Practice),
            new TimestampInstant(start, ClockSource.SimulatorSession),
            OptionalValue<TimestampInstant>.Available(new TimestampInstant(start.AddMinutes(5), ClockSource.SimulatorSession)),
            [
                new Lap(id, 1, new TimestampInstant(start, ClockSource.SimulatorSession),
                    OptionalValue<TimestampInstant>.Available(new TimestampInstant(start.AddMinutes(2), ClockSource.SimulatorSession)),
                    OptionalValue<TimeSpan>.Available(TimeSpan.FromMinutes(2)),
                    OptionalValue<int>.Available(3))
            ],
            [RaceEvent.Create(id, RaceEventType.LapComplete, new TimestampInstant(start.AddMinutes(2), ClockSource.SimulatorSession))]);

        DriverSession session = new(
            id,
            workout,
            OptionalValue<SimulatorSession>.Available(sim),
            OptionalValue<TimeSpan>.Unavailable());

        RaceReport report = RaceReportBuilder.FromDriverSession(session);

        Assert.True(report.SimulatorDisplayName.TryGet(out string? name));
        Assert.Equal("iRacing", name);
        Assert.True(report.LapCount.TryGet(out int laps));
        Assert.Equal(1, laps);
        Assert.True(report.BestLapTime.TryGet(out TimeSpan best));
        Assert.Equal(TimeSpan.FromMinutes(2), best);
        Assert.Equal(DataPresence.Unavailable, report.PeakHeartRateAssociatedEvent.Presence);
    }
}
