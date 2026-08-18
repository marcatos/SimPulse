using SimPulse.Domain;

namespace SimPulse.Analytics;

public static class EnergyMetrics
{
    /// <summary>
    /// HealthKit-style cumulative active energy: the last sample is the session total.
    /// </summary>
    public static OptionalValue<double> SessionActiveKilocalories(IReadOnlyList<EnergySample> samples)
    {
        if (samples.Count == 0)
        {
            return OptionalValue<double>.Unavailable();
        }

        return OptionalValue<double>.Available(samples[^1].ActiveKilocalories);
    }

    public static OptionalValue<double> KilocaloriesPerHour(
        IReadOnlyList<EnergySample> samples,
        TimeSpan duration)
    {
        OptionalValue<double> total = SessionActiveKilocalories(samples);
        if (!total.TryGet(out double kcal) || duration <= TimeSpan.Zero)
        {
            return OptionalValue<double>.Unavailable();
        }

        return OptionalValue<double>.Available(kcal / duration.TotalHours);
    }
}
