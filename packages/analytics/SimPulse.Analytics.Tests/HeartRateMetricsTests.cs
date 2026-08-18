using SimPulse.Domain;

namespace SimPulse.Analytics.Tests;

public sealed class HeartRateMetricsTests
{
    [Fact]
    public void Empty_samples_are_unavailable_not_zero()
    {
        OptionalValue<double> average = HeartRateMetrics.AverageBpm(Array.Empty<HeartRateSample>());
        OptionalValue<int> max = HeartRateMetrics.MaximumBpm(Array.Empty<HeartRateSample>());

        Assert.Equal(DataPresence.Unavailable, average.Presence);
        Assert.Equal(DataPresence.Unavailable, max.Presence);
    }

    [Fact]
    public void Computes_average_max_and_peak_time_from_fixture_like_samples()
    {
        DateTimeOffset start = DateTimeOffset.Parse("2026-08-18T10:00:00Z");
        HeartRateSample[] samples =
        [
            Sample(start, 0, 95),
            Sample(start, 30, 120),
            Sample(start, 60, 142)
        ];

        Assert.True(HeartRateMetrics.AverageBpm(samples).TryGet(out double avg));
        Assert.Equal(119, avg, 0);
        Assert.True(HeartRateMetrics.MaximumBpm(samples).TryGet(out int max));
        Assert.Equal(142, max);
        Assert.True(HeartRateMetrics.PeakTimestampUtc(samples).TryGet(out DateTimeOffset peak));
        Assert.Equal(start.AddSeconds(60), peak);
        Assert.True(HeartRateMetrics.PercentileBpm(samples, 50).TryGet(out double p50));
        Assert.Equal(120, p50);
    }

    [Fact]
    public void Wording_does_not_claim_stress()
    {
        string text = MeasurementWording.HeartRateChangePercent(100, 125);
        Assert.Contains("increased by 25%", text, StringComparison.Ordinal);
        Assert.DoesNotContain("stress", text, StringComparison.OrdinalIgnoreCase);
    }

    private static HeartRateSample Sample(DateTimeOffset start, int seconds, int bpm)
    {
        return new HeartRateSample(
            new TimestampInstant(start.AddSeconds(seconds), ClockSource.WorkoutSession),
            bpm);
    }
}

public sealed class EnergyMetricsTests
{
    [Fact]
    public void Uses_last_cumulative_sample_as_session_total()
    {
        DateTimeOffset start = DateTimeOffset.Parse("2026-08-18T10:00:00Z");
        EnergySample[] samples =
        [
            new EnergySample(new TimestampInstant(start, ClockSource.WorkoutSession), 2.0),
            new EnergySample(new TimestampInstant(start.AddMinutes(20), ClockSource.WorkoutSession), 18.5)
        ];

        Assert.True(EnergyMetrics.SessionActiveKilocalories(samples).TryGet(out double kcal));
        Assert.Equal(18.5, kcal);
        Assert.True(EnergyMetrics.KilocaloriesPerHour(samples, TimeSpan.FromMinutes(20)).TryGet(out double rate));
        Assert.Equal(55.5, rate, 1);
    }
}
