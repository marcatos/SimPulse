namespace SimPulse.Protocol;

public sealed record HelloMessage(string Product, string Role, string DeviceId);

public sealed record PairingRequestMessage(string DeviceId, string Pin);

public sealed record PairingAcceptMessage(string DeviceId, DateTimeOffset TrustedAtUtc);

public sealed record PairingRejectMessage(string DeviceId, string Reason);

public sealed record HeartbeatMessage(string ConnectionId);

public sealed record TimeSyncRequestMessage(DateTimeOffset ClientSentAtUtc);

public sealed record TimeSyncResponseMessage(
    string RequestId,
    DateTimeOffset ClientSentAtUtc,
    DateTimeOffset ServerReceivedAtUtc,
    DateTimeOffset ServerSentAtUtc);

public sealed record SimulatorSessionSnapshotMessage(
    string SessionId,
    string SimulatorId,
    string? TrackId,
    string? TrackName,
    string? VehicleId,
    string? VehicleName,
    string? SessionType,
    DateTimeOffset StartedAtUtc,
    DateTimeOffset? EndedAtUtc);

public sealed record RaceEventMessage(
    string SessionId,
    string EventType,
    DateTimeOffset OccurredAtUtc,
    IReadOnlyDictionary<string, string>? Attributes);

public sealed record ErrorMessage(string Code, string Message);
