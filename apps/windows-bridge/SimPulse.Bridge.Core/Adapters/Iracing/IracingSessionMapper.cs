using SimPulse.Domain;

namespace SimPulse.Bridge.Core.Adapters.Iracing;

internal static class IracingSessionMapper
{
    public static SimulatorSession ToSnapshot(
        SessionId sessionId,
        IracingSessionInfo info,
        TimestampInstant startedAt,
        OptionalValue<TimestampInstant> endedAt,
        IReadOnlyList<RaceEvent> events)
    {
        return new SimulatorSession(
            sessionId,
            new Simulator(SimulatorIds.IRacing, "iRacing"),
            MapTrack(info),
            MapVehicle(info),
            MapSessionType(info.SessionType),
            startedAt,
            endedAt,
            Array.Empty<Lap>(),
            events.ToArray());
    }

    private static OptionalValue<Track> MapTrack(IracingSessionInfo info)
    {
        bool hasId = info.TrackId.TryGet(out string? id);
        bool hasName = info.TrackDisplayName.TryGet(out string? name);
        if (!hasId && !hasName)
        {
            return OptionalValue<Track>.Unavailable();
        }

        string trackId = hasId ? id! : name!;
        string display = hasName ? name! : trackId;
        return OptionalValue<Track>.Available(new Track(trackId, display, OptionalValue<string>.Unavailable()));
    }

    private static OptionalValue<Vehicle> MapVehicle(IracingSessionInfo info)
    {
        bool hasId = info.VehicleId.TryGet(out string? id);
        bool hasName = info.VehicleDisplayName.TryGet(out string? name);
        if (!hasId && !hasName)
        {
            return OptionalValue<Vehicle>.Unavailable();
        }

        string vehicleId = hasId ? id! : name!;
        string display = hasName ? name! : vehicleId;
        return OptionalValue<Vehicle>.Available(new Vehicle(vehicleId, display, OptionalValue<string>.Unavailable()));
    }

    private static OptionalValue<SimulatorSessionType> MapSessionType(OptionalValue<string> raw)
    {
        if (!raw.TryGet(out string? value) || string.IsNullOrWhiteSpace(value))
        {
            return OptionalValue<SimulatorSessionType>.Unavailable();
        }

        if (Enum.TryParse(value, ignoreCase: true, out SimulatorSessionType parsed) &&
            parsed != SimulatorSessionType.Unknown)
        {
            return OptionalValue<SimulatorSessionType>.Available(parsed);
        }

        return OptionalValue<SimulatorSessionType>.Available(Classify(value));
    }

    private static SimulatorSessionType Classify(string value)
    {
        if (value.Contains("qualif", StringComparison.OrdinalIgnoreCase))
        {
            return SimulatorSessionType.Qualifying;
        }

        if (value.Contains("practice", StringComparison.OrdinalIgnoreCase) ||
            value.Contains("testing", StringComparison.OrdinalIgnoreCase))
        {
            return SimulatorSessionType.Practice;
        }

        if (value.Contains("race", StringComparison.OrdinalIgnoreCase))
        {
            return SimulatorSessionType.Race;
        }

        if (value.Contains("time", StringComparison.OrdinalIgnoreCase))
        {
            return SimulatorSessionType.TimeTrial;
        }

        return SimulatorSessionType.Other;
    }
}
