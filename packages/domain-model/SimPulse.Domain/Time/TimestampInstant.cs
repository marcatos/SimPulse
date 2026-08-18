namespace SimPulse.Domain;

public enum ClockSource
{
    Utc = 0,
    DeviceLocal = 1,
    SimulatorSession = 2,
    WorkoutSession = 3,
    EstimatedUtc = 4
}

/// <summary>
/// Instant plus the clock that produced it. Cross-device equality is not implied.
/// </summary>
public readonly record struct TimestampInstant(
    DateTimeOffset Value,
    ClockSource Source,
    TimeSpan? EstimatedError = null)
{
    public static TimestampInstant UtcNow(DateTimeOffset utc) => new(utc, ClockSource.Utc);
}
