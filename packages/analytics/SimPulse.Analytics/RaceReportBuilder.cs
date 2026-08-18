using SimPulse.Domain;

namespace SimPulse.Analytics;

public static class RaceReportBuilder
{
    public static RaceReport FromDriverSession(DriverSession session)
    {
        WorkoutSession workout = session.Workout;
        IReadOnlyList<HeartRateSample> heartRateSamples = workout.HeartRateSamples;
        bool hasSimulator = session.Simulator.TryGet(out SimulatorSession? simulator);

        OptionalValue<string> simulatorDisplayName = OptionalValue<string>.Unavailable();
        OptionalValue<string> trackDisplayName = OptionalValue<string>.Unavailable();
        OptionalValue<string> vehicleDisplayName = OptionalValue<string>.Unavailable();
        OptionalValue<SimulatorSessionType> sessionType = OptionalValue<SimulatorSessionType>.Unavailable();
        OptionalValue<int> lapCount = OptionalValue<int>.Unavailable();
        OptionalValue<int> startPosition = OptionalValue<int>.Unavailable();
        OptionalValue<int> finishPosition = OptionalValue<int>.Unavailable();
        OptionalValue<TimeSpan> bestLapTime = OptionalValue<TimeSpan>.Unavailable();
        IReadOnlyList<Lap> laps = Array.Empty<Lap>();

        if (hasSimulator)
        {
            simulatorDisplayName = OptionalValue<string>.Available(simulator!.Simulator.DisplayName);
            trackDisplayName = ResolveTrackDisplayName(simulator.Track);
            vehicleDisplayName = ResolveVehicleDisplayName(simulator.Vehicle);
            sessionType = simulator.SessionType;
            lapCount = OptionalValue<int>.Available(simulator.Laps.Count);
            startPosition = ResolveStartPosition(simulator.Laps);
            finishPosition = ResolveFinishPosition(simulator.Laps);
            bestLapTime = ResolveBestLapTime(simulator.Laps);
            laps = simulator.Laps;
        }

        OptionalValue<TimeSpan> duration = ResolveDuration(workout);
        OptionalValue<RaceEventType> peakHeartRateAssociatedEvent = ResolvePeakHeartRateAssociatedEvent(
            session,
            heartRateSamples,
            hasSimulator ? simulator : null);

        return new RaceReport(
            session.Id,
            simulatorDisplayName,
            trackDisplayName,
            vehicleDisplayName,
            sessionType,
            duration,
            lapCount,
            startPosition,
            finishPosition,
            bestLapTime,
            HeartRateMetrics.AverageBpm(heartRateSamples),
            HeartRateMetrics.MaximumBpm(heartRateSamples),
            EnergyMetrics.SessionActiveKilocalories(workout.EnergySamples),
            HeartRateMetrics.PeakTimestampUtc(heartRateSamples),
            peakHeartRateAssociatedEvent,
            heartRateSamples,
            laps);
    }

    private static OptionalValue<string> ResolveTrackDisplayName(OptionalValue<Track> track)
    {
        if (track.TryGet(out Track? value))
        {
            return OptionalValue<string>.Available(value.DisplayName);
        }

        return OptionalValue<string>.Unavailable();
    }

    private static OptionalValue<string> ResolveVehicleDisplayName(OptionalValue<Vehicle> vehicle)
    {
        if (vehicle.TryGet(out Vehicle? value))
        {
            return OptionalValue<string>.Available(value.DisplayName);
        }

        return OptionalValue<string>.Unavailable();
    }

    private static OptionalValue<TimeSpan> ResolveDuration(WorkoutSession workout)
    {
        if (!workout.EndedAt.TryGet(out TimestampInstant endedAt))
        {
            return OptionalValue<TimeSpan>.Unavailable();
        }

        TimeSpan duration = endedAt.Value - workout.StartedAt.Value;
        if (duration < TimeSpan.Zero)
        {
            return OptionalValue<TimeSpan>.Unavailable();
        }

        return OptionalValue<TimeSpan>.Available(duration);
    }

    private static OptionalValue<int> ResolveStartPosition(IReadOnlyList<Lap> laps)
    {
        for (int i = 0; i < laps.Count; i++)
        {
            if (laps[i].Position.TryGet(out int position))
            {
                return OptionalValue<int>.Available(position);
            }
        }

        return OptionalValue<int>.Unavailable();
    }

    private static OptionalValue<int> ResolveFinishPosition(IReadOnlyList<Lap> laps)
    {
        for (int i = laps.Count - 1; i >= 0; i--)
        {
            if (laps[i].Position.TryGet(out int position))
            {
                return OptionalValue<int>.Available(position);
            }
        }

        return OptionalValue<int>.Unavailable();
    }

    private static OptionalValue<TimeSpan> ResolveBestLapTime(IReadOnlyList<Lap> laps)
    {
        OptionalValue<TimeSpan> best = OptionalValue<TimeSpan>.Unavailable();

        for (int i = 0; i < laps.Count; i++)
        {
            if (!laps[i].LapTime.TryGet(out TimeSpan lapTime))
            {
                continue;
            }

            if (!best.TryGet(out TimeSpan currentBest) || lapTime < currentBest)
            {
                best = OptionalValue<TimeSpan>.Available(lapTime);
            }
        }

        return best;
    }

    private static OptionalValue<RaceEventType> ResolvePeakHeartRateAssociatedEvent(
        DriverSession session,
        IReadOnlyList<HeartRateSample> heartRateSamples,
        SimulatorSession? simulator)
    {
        if (!session.TimelineOffset.TryGet(out TimeSpan _))
        {
            return OptionalValue<RaceEventType>.Unavailable();
        }

        if (simulator is null)
        {
            return OptionalValue<RaceEventType>.Unavailable();
        }

        if (!HeartRateMetrics.PeakTimestampUtc(heartRateSamples).TryGet(out _))
        {
            return OptionalValue<RaceEventType>.Unavailable();
        }

        // Offset-based event correlation is ANALYTICS-003; never join by raw wall clock.
        return OptionalValue<RaceEventType>.Unavailable();
    }
}
