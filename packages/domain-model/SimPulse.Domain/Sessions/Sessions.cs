namespace SimPulse.Domain;

public static class SimulatorIds
{
    public const string IRacing = "iracing";
    public const string LeMansUltimate = "lmu";
    public const string AssettoCorsa = "ac";
    public const string AssettoCorsaCompetizione = "acc";
    public const string AssettoCorsaEvo = "acevo";
    public const string Automobilista2 = "ams2";
    public const string Rennsport = "rennsport";
}

public sealed record Simulator(string Id, string DisplayName);

public sealed record Track(string Id, string DisplayName, OptionalValue<string> Layout);

public sealed record Vehicle(string Id, string DisplayName, OptionalValue<string> Class);

public enum SimulatorSessionType
{
    Unknown = 0,
    Practice = 1,
    Qualifying = 2,
    Race = 3,
    TimeTrial = 4,
    Other = 5
}

public sealed record SimulatorSession(
    SessionId Id,
    Simulator Simulator,
    OptionalValue<Track> Track,
    OptionalValue<Vehicle> Vehicle,
    OptionalValue<SimulatorSessionType> SessionType,
    TimestampInstant StartedAt,
    OptionalValue<TimestampInstant> EndedAt,
    IReadOnlyList<Lap> Laps,
    IReadOnlyList<RaceEvent> Events);

public sealed record WorkoutSession(
    SessionId Id,
    TimestampInstant StartedAt,
    OptionalValue<TimestampInstant> EndedAt,
    IReadOnlyList<HeartRateSample> HeartRateSamples,
    IReadOnlyList<EnergySample> EnergySamples,
    OptionalValue<int> AverageHeartRateBpm,
    OptionalValue<int> MaximumHeartRateBpm,
    OptionalValue<double> ActiveKilocalories);

/// <summary>
/// Join of a workout and an optional simulator session. TimelineOffset is required
/// before honest cross-timeline correlation (ADR 0004).
/// </summary>
public sealed record DriverSession(
    SessionId Id,
    WorkoutSession Workout,
    OptionalValue<SimulatorSession> Simulator,
    OptionalValue<TimeSpan> TimelineOffset);
