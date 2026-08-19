using SimPulse.Bridge.Core.Application;
using SimPulse.Bridge.Core.Ports;

namespace SimPulse.Bridge.Core.Adapters;

public sealed class SystemClock : IClock
{
    public DateTimeOffset UtcNow => DateTimeOffset.UtcNow;
}

public sealed class InMemoryTrustedDeviceStore : ITrustedDeviceStore
{
    private readonly Dictionary<string, TrustedDevice> _devices = new(StringComparer.Ordinal);

    public Task<IReadOnlyList<TrustedDevice>> ListAsync(CancellationToken cancellationToken)
    {
        return Task.FromResult<IReadOnlyList<TrustedDevice>>(_devices.Values.ToArray());
    }

    public Task TrustAsync(
        string deviceId,
        DateTimeOffset trustedAtUtc,
        string reconnectTokenSha256,
        CancellationToken cancellationToken)
    {
        _devices[deviceId] = new TrustedDevice(
            deviceId,
            trustedAtUtc,
            Revoked: false,
            reconnectTokenSha256);
        return Task.CompletedTask;
    }

    public Task RevokeAsync(string deviceId, CancellationToken cancellationToken)
    {
        if (_devices.TryGetValue(deviceId, out TrustedDevice? existing))
        {
            _devices[deviceId] = existing with { Revoked = true };
        }

        return Task.CompletedTask;
    }

    public Task<bool> AuthorizeReconnectAsync(
        string deviceId,
        string? reconnectTokenHex,
        CancellationToken cancellationToken)
    {
        bool trusted = _devices.TryGetValue(deviceId, out TrustedDevice? device)
            && !device.Revoked
            && ReconnectToken.MatchesStoredHash(device.ReconnectTokenSha256, reconnectTokenHex);
        return Task.FromResult(trusted);
    }
}
