namespace SimPulse.Domain;

public sealed record HeartRateSample(TimestampInstant Timestamp, int BeatsPerMinute);

public sealed record EnergySample(TimestampInstant Timestamp, double ActiveKilocalories);

/// <summary>
/// Simulator-independent telemetry point. Fields use OptionalValue to avoid fake precision.
/// </summary>
public sealed record TelemetrySample(
    TimestampInstant Timestamp,
    OptionalValue<double> SpeedMetersPerSecond,
    OptionalValue<double> LapDistancePercent,
    OptionalValue<int> Gear,
    OptionalValue<double> ThrottlePercent);
