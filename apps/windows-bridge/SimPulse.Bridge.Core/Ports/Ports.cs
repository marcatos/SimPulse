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

public sealed record TrustedDevice(
    string DeviceId,
    DateTimeOffset TrustedAtUtc,
    bool Revoked,
    string? ReconnectTokenSha256);

public interface ITrustedDeviceStore
{
    Task<IReadOnlyList<TrustedDevice>> ListAsync(CancellationToken cancellationToken);

    Task TrustAsync(
        string deviceId,
        DateTimeOffset trustedAtUtc,
        string reconnectTokenSha256,
        CancellationToken cancellationToken);

    Task RevokeAsync(string deviceId, CancellationToken cancellationToken);

    Task<bool> AuthorizeReconnectAsync(
        string deviceId,
        string? reconnectTokenHex,
        CancellationToken cancellationToken);
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

    void ClearPin();

    event Action? PairNewDeviceRequested;

    event Action? ShowCurrentPinRequested;
}
