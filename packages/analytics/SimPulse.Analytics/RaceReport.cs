using SimPulse.Domain;

namespace SimPulse.Analytics;

public sealed record RaceReport(
    SessionId SessionId,
    OptionalValue<string> SimulatorDisplayName,
    OptionalValue<string> TrackDisplayName,
    OptionalValue<string> VehicleDisplayName,
    OptionalValue<SimulatorSessionType> SessionType,
    OptionalValue<TimeSpan> Duration,
    OptionalValue<int> LapCount,
    OptionalValue<int> StartPosition,
    OptionalValue<int> FinishPosition,
    OptionalValue<TimeSpan> BestLapTime,
    OptionalValue<double> AverageHeartRateBpm,
    OptionalValue<int> MaximumHeartRateBpm,
    OptionalValue<double> ActiveKilocalories,
    OptionalValue<DateTimeOffset> PeakHeartRateAtUtc,
    OptionalValue<RaceEventType> PeakHeartRateAssociatedEvent,
    IReadOnlyList<HeartRateSample> HeartRateTimeline,
    IReadOnlyList<Lap> Laps);
