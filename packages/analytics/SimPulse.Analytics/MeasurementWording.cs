namespace SimPulse.Analytics;

/// <summary>
/// Descriptive wording only. SimPulse is not a medical device.
/// </summary>
public static class MeasurementWording
{
    public static string HeartRateChangePercent(double previousBpm, double currentBpm)
    {
        if (previousBpm <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(previousBpm));
        }

        double percent = ((currentBpm - previousBpm) / previousBpm) * 100.0;
        string direction = percent >= 0 ? "increased" : "decreased";
        return $"Heart rate {direction} by {Math.Abs(percent):0.#}% during this interval.";
    }
}
