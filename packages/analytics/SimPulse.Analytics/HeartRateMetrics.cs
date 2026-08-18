using SimPulse.Domain;

namespace SimPulse.Analytics;

public static class HeartRateMetrics
{
    public static OptionalValue<double> AverageBpm(IReadOnlyList<HeartRateSample> samples)
    {
        if (samples.Count == 0)
        {
            return OptionalValue<double>.Unavailable();
        }

        double sum = 0;
        for (int i = 0; i < samples.Count; i++)
        {
            sum += samples[i].BeatsPerMinute;
        }

        return OptionalValue<double>.Available(sum / samples.Count);
    }

    public static OptionalValue<int> MaximumBpm(IReadOnlyList<HeartRateSample> samples)
    {
        if (samples.Count == 0)
        {
            return OptionalValue<int>.Unavailable();
        }

        int max = samples[0].BeatsPerMinute;
        for (int i = 1; i < samples.Count; i++)
        {
            if (samples[i].BeatsPerMinute > max)
            {
                max = samples[i].BeatsPerMinute;
            }
        }

        return OptionalValue<int>.Available(max);
    }

    public static OptionalValue<double> PercentileBpm(IReadOnlyList<HeartRateSample> samples, double percentile)
    {
        if (samples.Count == 0)
        {
            return OptionalValue<double>.Unavailable();
        }

        if (percentile < 0 || percentile > 100)
        {
            throw new ArgumentOutOfRangeException(nameof(percentile), "Percentile must be between 0 and 100.");
        }

        int[] values = samples.Select(s => s.BeatsPerMinute).OrderBy(v => v).ToArray();
        if (percentile == 100)
        {
            return OptionalValue<double>.Available(values[^1]);
        }

        double rank = (percentile / 100.0) * (values.Length - 1);
        int low = (int)Math.Floor(rank);
        int high = (int)Math.Ceiling(rank);
        if (low == high)
        {
            return OptionalValue<double>.Available(values[low]);
        }

        double weight = rank - low;
        double interpolated = (values[low] * (1 - weight)) + (values[high] * weight);
        return OptionalValue<double>.Available(interpolated);
    }

    public static OptionalValue<DateTimeOffset> PeakTimestampUtc(IReadOnlyList<HeartRateSample> samples)
    {
        if (samples.Count == 0)
        {
            return OptionalValue<DateTimeOffset>.Unavailable();
        }

        HeartRateSample peak = samples[0];
        for (int i = 1; i < samples.Count; i++)
        {
            if (samples[i].BeatsPerMinute > peak.BeatsPerMinute)
            {
                peak = samples[i];
            }
        }

        return OptionalValue<DateTimeOffset>.Available(peak.Timestamp.Value);
    }
}
