using SimPulse.Domain;

namespace SimPulse.Bridge.Core.Ports;

public interface IClock
{
    DateTimeOffset UtcNow { get; }
}

public sealed record NormalizedSimulatorUpdate(
    string SimulatorId,
    SessionId SessionId,
    TimestampInstant CapturedAt,
    RaceEvent? RaceEvent,
    TelemetrySample? Telemetry,
    SimulatorSession? SessionSnapshot);

public interface ISimulatorAdapter
{
    string SimulatorId { get; }

    Task<bool> IsAvailableAsync(CancellationToken cancellationToken);

    IAsyncEnumerable<NormalizedSimulatorUpdate> SubscribeAsync(CancellationToken cancellationToken);
}

public sealed record TrustedDevice(string DeviceId, DateTimeOffset TrustedAtUtc, bool Revoked);

public interface ITrustedDeviceStore
{
    Task<IReadOnlyList<TrustedDevice>> ListAsync(CancellationToken cancellationToken);

    Task TrustAsync(string deviceId, DateTimeOffset trustedAtUtc, CancellationToken cancellationToken);

    Task RevokeAsync(string deviceId, CancellationToken cancellationToken);

    Task<bool> IsTrustedAsync(string deviceId, CancellationToken cancellationToken);
}

public interface IPairingPinGenerator
{
    string Generate();
}

public interface IPairingUx
{
    void ShowPin(string pin, DateTimeOffset expiresAtUtc);

    void ShowStatus(string message);

    void RedisplayLastPin();

    event Action? PairNewDeviceRequested;

    event Action? ShowCurrentPinRequested;
}
