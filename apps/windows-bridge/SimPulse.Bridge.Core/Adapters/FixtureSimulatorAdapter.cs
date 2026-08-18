using System.Text.Json;

using SimPulse.Bridge.Core.Ports;
using SimPulse.Domain;

namespace SimPulse.Bridge.Core.Adapters;

public sealed class FixtureSimulatorAdapter : ISimulatorAdapter
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private readonly string _fixturePath;

    public FixtureSimulatorAdapter(string fixturePath)
    {
        _fixturePath = fixturePath;
    }

    public string SimulatorId { get; private set; } = "unknown";

    public Task<bool> IsAvailableAsync(CancellationToken cancellationToken)
    {
        return Task.FromResult(File.Exists(_fixturePath));
    }

    public async IAsyncEnumerable<NormalizedSimulatorUpdate> SubscribeAsync(
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken)
    {
        string json = await File.ReadAllTextAsync(_fixturePath, cancellationToken);
        FixtureDocument fixture = JsonSerializer.Deserialize<FixtureDocument>(json, JsonOptions)
            ?? throw new InvalidOperationException($"Fixture '{_fixturePath}' deserialized to null.");

        SimulatorId = fixture.SimulatorId;
        SessionId sessionId = SessionId.New();
        DateTimeOffset startedAt = fixture.StartedAtUtc;
        Simulator simulator = new(fixture.SimulatorId, fixture.DisplayName);
        OptionalValue<Track> track = fixture.Track is null
            ? OptionalValue<Track>.Unavailable()
            : OptionalValue<Track>.Available(new Track(
                fixture.Track.Id,
                fixture.Track.DisplayName,
                fixture.Track.Layout is null
                    ? OptionalValue<string>.Unavailable()
                    : OptionalValue<string>.Available(fixture.Track.Layout)));
        OptionalValue<Vehicle> vehicle = fixture.Vehicle is null
            ? OptionalValue<Vehicle>.Unavailable()
            : OptionalValue<Vehicle>.Available(new Vehicle(
                fixture.Vehicle.Id,
                fixture.Vehicle.DisplayName,
                fixture.Vehicle.Class is null
                    ? OptionalValue<string>.Unavailable()
                    : OptionalValue<string>.Available(fixture.Vehicle.Class)));
        OptionalValue<SimulatorSessionType> sessionType = ParseSessionType(fixture.SessionType);

        List<Lap> laps = [];
        List<RaceEvent> events = [];
        SimulatorSession snapshot = new(
            sessionId,
            simulator,
            track,
            vehicle,
            sessionType,
            new TimestampInstant(startedAt, ClockSource.Utc),
            OptionalValue<TimestampInstant>.Unknown(),
            laps,
            events);

        foreach (FixtureTick tick in fixture.Ticks.OrderBy(t => t.OffsetMs))
        {
            cancellationToken.ThrowIfCancellationRequested();
            TimestampInstant captured = new(
                startedAt.AddMilliseconds(tick.OffsetMs),
                ClockSource.Utc);

            RaceEvent? raceEvent = MapRaceEvent(sessionId, captured, tick);
            if (raceEvent is not null)
            {
                events.Add(raceEvent);
            }

            TelemetrySample? telemetry = tick.Kind == "Telemetry"
                ? new TelemetrySample(
                    captured,
                    Optional(tick.SpeedMps),
                    Optional(tick.LapDistPct),
                    tick.Gear is null ? OptionalValue<int>.Unavailable() : OptionalValue<int>.Available(tick.Gear.Value),
                    Optional(tick.ThrottlePercent))
                : null;

            if (tick.Kind == "LapComplete" && tick.LapNumber is not null)
            {
                laps.Add(new Lap(
                    sessionId,
                    tick.LapNumber.Value,
                    captured,
                    OptionalValue<TimestampInstant>.Available(captured),
                    tick.LapTimeMs is null
                        ? OptionalValue<TimeSpan>.Unavailable()
                        : OptionalValue<TimeSpan>.Available(TimeSpan.FromMilliseconds(tick.LapTimeMs.Value)),
                    tick.Position is null
                        ? OptionalValue<int>.Unavailable()
                        : OptionalValue<int>.Available(tick.Position.Value)));
            }

            OptionalValue<TimestampInstant> endedAt = tick.Kind == "SessionEnd"
                ? OptionalValue<TimestampInstant>.Available(captured)
                : OptionalValue<TimestampInstant>.Unknown();

            snapshot = snapshot with
            {
                EndedAt = endedAt,
                Laps = laps.ToArray(),
                Events = events.ToArray()
            };

            yield return new NormalizedSimulatorUpdate(
                fixture.SimulatorId,
                sessionId,
                captured,
                raceEvent,
                telemetry,
                snapshot);

            await Task.Yield();
        }
    }

    private static RaceEvent? MapRaceEvent(SessionId sessionId, TimestampInstant timestamp, FixtureTick tick)
    {
        RaceEventType? type = tick.Kind switch
        {
            "SessionStart" => RaceEventType.SessionStart,
            "SessionEnd" => RaceEventType.SessionEnd,
            "LapStart" => RaceEventType.LapStart,
            "LapComplete" => RaceEventType.LapComplete,
            "PitEntry" => RaceEventType.PitEntry,
            "PitExit" => RaceEventType.PitExit,
            "YellowFlag" => RaceEventType.YellowFlag,
            "CheckeredFlag" => RaceEventType.CheckeredFlag,
            "Incident" => RaceEventType.Incident,
            "PositionChange" => RaceEventType.PositionChange,
            _ => null
        };

        if (type is null)
        {
            return null;
        }

        Dictionary<string, string> attributes = new();
        if (tick.LapNumber is not null)
        {
            attributes["lapNumber"] = tick.LapNumber.Value.ToString();
        }

        if (tick.Position is not null)
        {
            attributes["position"] = tick.Position.Value.ToString();
        }

        return RaceEvent.Create(sessionId, type.Value, timestamp, attributes);
    }

    private static OptionalValue<double> Optional(double? value)
    {
        return value is null ? OptionalValue<double>.Unavailable() : OptionalValue<double>.Available(value.Value);
    }

    private static OptionalValue<SimulatorSessionType> ParseSessionType(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return OptionalValue<SimulatorSessionType>.Unknown();
        }

        return Enum.TryParse(value, ignoreCase: true, out SimulatorSessionType parsed)
            ? OptionalValue<SimulatorSessionType>.Available(parsed)
            : OptionalValue<SimulatorSessionType>.Unknown();
    }

    private sealed class FixtureDocument
    {
        public string SimulatorId { get; set; } = "";

        public string DisplayName { get; set; } = "";

        public FixtureNamedEntity? Track { get; set; }

        public FixtureNamedEntity? Vehicle { get; set; }

        public string? SessionType { get; set; }

        public DateTimeOffset StartedAtUtc { get; set; }

        public List<FixtureTick> Ticks { get; set; } = [];
    }

    private sealed class FixtureNamedEntity
    {
        public string Id { get; set; } = "";

        public string DisplayName { get; set; } = "";

        public string? Layout { get; set; }

        public string? Class { get; set; }
    }

    private sealed class FixtureTick
    {
        public int OffsetMs { get; set; }

        public string Kind { get; set; } = "";

        public int? LapNumber { get; set; }

        public int? LapTimeMs { get; set; }

        public int? Position { get; set; }

        public double? SpeedMps { get; set; }

        public double? LapDistPct { get; set; }

        public int? Gear { get; set; }

        public double? ThrottlePercent { get; set; }
    }
}
