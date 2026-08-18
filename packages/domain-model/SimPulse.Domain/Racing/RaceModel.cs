namespace SimPulse.Domain;

public enum RaceEventType
{
    SessionStart = 0,
    SessionEnd = 1,
    LapStart = 2,
    LapComplete = 3,
    PitEntry = 4,
    PitExit = 5,
    PositionChange = 6,
    Incident = 7,
    YellowFlag = 8,
    CheckeredFlag = 9
}

public sealed record RaceEvent(
    SessionId SimulatorSessionId,
    RaceEventType Type,
    TimestampInstant Timestamp,
    IReadOnlyDictionary<string, string> Attributes)
{
    public static RaceEvent Create(
        SessionId sessionId,
        RaceEventType type,
        TimestampInstant timestamp,
        IReadOnlyDictionary<string, string>? attributes = null)
    {
        return new RaceEvent(
            sessionId,
            type,
            timestamp,
            attributes ?? new Dictionary<string, string>());
    }
}

public sealed record Lap(
    SessionId SimulatorSessionId,
    int LapNumber,
    TimestampInstant StartedAt,
    OptionalValue<TimestampInstant> CompletedAt,
    OptionalValue<TimeSpan> LapTime,
    OptionalValue<int> Position);

public sealed record SessionMarker(
    string Label,
    TimestampInstant Timestamp,
    OptionalValue<string> Note);
