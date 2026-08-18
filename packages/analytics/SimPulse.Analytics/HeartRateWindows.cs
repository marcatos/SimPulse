using SimPulse.Domain;

namespace SimPulse.Analytics;

/// <summary>Simulator/workout timeline correlation: workoutTime = simulatorTime + offset.</summary>
public static class HeartRateWindows
{
    public static OptionalValue<double> AverageBpmInSimulatorWindow(
        IReadOnlyList<HeartRateSample> workoutSamples,
        TimestampInstant simulatorWindowStart,
        TimestampInstant simulatorWindowEnd,
        OptionalValue<TimeSpan> timelineOffset)
    {
        if (timelineOffset.Presence != DataPresence.Available)
        {
            return OptionalValue<double>.Unavailable();
        }

        TimeSpan offset = timelineOffset.Value!;
        List<HeartRateSample> filtered = FilterSamplesInSimulatorWindow(
            workoutSamples,
            offset,
            simulatorWindowStart.Value,
            simulatorWindowEnd.Value);

        return HeartRateMetrics.AverageBpm(filtered);
    }

    public static OptionalValue<double> AverageBpmForLap(
        IReadOnlyList<HeartRateSample> workoutSamples,
        Lap lap,
        OptionalValue<TimeSpan> timelineOffset)
    {
        if (timelineOffset.Presence != DataPresence.Available)
        {
            return OptionalValue<double>.Unavailable();
        }

        if (lap.CompletedAt.Presence != DataPresence.Available)
        {
            return OptionalValue<double>.Unavailable();
        }

        return AverageBpmInSimulatorWindow(
            workoutSamples,
            lap.StartedAt,
            lap.CompletedAt.Value!,
            timelineOffset);
    }

    public static OptionalValue<double> AverageBpmAroundEvent(
        IReadOnlyList<HeartRateSample> workoutSamples,
        RaceEvent raceEvent,
        TimeSpan halfWindow,
        OptionalValue<TimeSpan> timelineOffset)
    {
        if (timelineOffset.Presence != DataPresence.Available)
        {
            return OptionalValue<double>.Unavailable();
        }

        DateTimeOffset eventTime = raceEvent.Timestamp.Value;
        TimestampInstant windowStart = new(eventTime - halfWindow, raceEvent.Timestamp.Source);
        TimestampInstant windowEnd = new(eventTime + halfWindow, raceEvent.Timestamp.Source);

        return AverageBpmInSimulatorWindow(
            workoutSamples,
            windowStart,
            windowEnd,
            timelineOffset);
    }

    private static List<HeartRateSample> FilterSamplesInSimulatorWindow(
        IReadOnlyList<HeartRateSample> workoutSamples,
        TimeSpan timelineOffset,
        DateTimeOffset simulatorWindowStart,
        DateTimeOffset simulatorWindowEnd)
    {
        List<HeartRateSample> filtered = [];
        for (int i = 0; i < workoutSamples.Count; i++)
        {
            HeartRateSample sample = workoutSamples[i];
            DateTimeOffset simulatorTime = sample.Timestamp.Value - timelineOffset;
            if (simulatorTime >= simulatorWindowStart && simulatorTime <= simulatorWindowEnd)
            {
                filtered.Add(sample);
            }
        }

        return filtered;
    }
}
