using System.Collections.Concurrent;
using System.Diagnostics;

using Microsoft.Extensions.Logging;

using SimPulse.Bridge.Core.Ports;
using SimPulse.Protocol;

namespace SimPulse.Bridge.Core.Application;

public sealed class PairingCoordinator
{
    public const string InvalidPinReason = "invalid_pin";

    private readonly ITrustedDeviceStore _store;
    private readonly IClock _clock;
    private readonly ILogger<PairingCoordinator> _logger;
    private readonly string _pin;
    private readonly ConcurrentDictionary<IClientConnection, byte> _connections = new();
    private int _pinLogged;

    public PairingCoordinator(
        ITrustedDeviceStore store,
        IClock clock,
        IPairingPinGenerator pinGenerator,
        ILogger<PairingCoordinator> logger)
    {
        _store = store;
        _clock = clock;
        _logger = logger;
        _pin = pinGenerator.Generate();
    }

    public void BeginPairingWindow()
    {
        if (Interlocked.Exchange(ref _pinLogged, 1) == 1)
        {
            return;
        }

        _logger.LogInformation(
            "Pairing window opened. Pin={Pin} Component={Component}",
            _pin,
            "PairingCoordinator");
    }

    public async Task HandleAsync(
        IClientConnection connection,
        MessageEnvelope envelope,
        CancellationToken cancellationToken)
    {
        Stopwatch started = Stopwatch.StartNew();
        _connections.TryAdd(connection, 0);
        _logger.LogDebug(
            "Pairing handle starting. Type={Type} HasDeviceId={HasDeviceId} Component={Component}",
            envelope.Type,
            connection.DeviceId is not null,
            "PairingCoordinator");

        await DispatchAsync(connection, envelope, cancellationToken);

        _logger.LogDebug(
            "Pairing handle finished. Type={Type} Trusted={Trusted} ElapsedMs={ElapsedMs}",
            envelope.Type,
            connection.IsTrusted,
            started.ElapsedMilliseconds);
    }

    public async Task RevokeAsync(string deviceId, CancellationToken cancellationToken)
    {
        Stopwatch started = Stopwatch.StartNew();
        _logger.LogInformation("Revoking trusted device. Component={Component}", "PairingCoordinator");
        await _store.RevokeAsync(deviceId, cancellationToken);
        UntrustLiveConnections(deviceId);
        _logger.LogInformation("Revoke finished. ElapsedMs={ElapsedMs}", started.ElapsedMilliseconds);
    }

    public void Unregister(IClientConnection connection)
    {
        if (_connections.TryRemove(connection, out _))
        {
            _logger.LogDebug("Pairing connection unregistered. Trusted={Trusted}", connection.IsTrusted);
        }
    }

    public bool IsRegistered(IClientConnection connection)
    {
        return _connections.ContainsKey(connection);
    }

    private async Task DispatchAsync(
        IClientConnection connection,
        MessageEnvelope envelope,
        CancellationToken cancellationToken)
    {
        if (envelope.Type == MessageTypes.Hello)
        {
            await HandleHelloAsync(connection, envelope, cancellationToken);
            return;
        }

        if (envelope.Type == MessageTypes.PairingRequest)
        {
            await HandlePairingRequestAsync(connection, envelope, cancellationToken);
        }
    }

    private async Task HandleHelloAsync(
        IClientConnection connection,
        MessageEnvelope envelope,
        CancellationToken cancellationToken)
    {
        if (!EnvelopeCodec.TryReadPayload(envelope, out HelloMessage? hello) || hello is null)
        {
            _logger.LogWarning("Hello payload not parseable.");
            return;
        }

        connection.DeviceId = hello.DeviceId;
        connection.IsTrusted = await _store.IsTrustedAsync(hello.DeviceId, cancellationToken);
        if (connection.IsTrusted)
        {
            _logger.LogInformation("Hello trust evaluated. Trusted={Trusted}", connection.IsTrusted);
            return;
        }

        _logger.LogDebug("Hello trust evaluated. Trusted={Trusted}", connection.IsTrusted);
    }

    private async Task HandlePairingRequestAsync(
        IClientConnection connection,
        MessageEnvelope envelope,
        CancellationToken cancellationToken)
    {
        if (!EnvelopeCodec.TryReadPayload(envelope, out PairingRequestMessage? request) || request is null)
        {
            _logger.LogWarning("Pairing request payload not parseable.");
            return;
        }

        connection.DeviceId = request.DeviceId;
        if (!string.Equals(_pin, request.Pin, StringComparison.Ordinal))
        {
            await SendAsync(connection, MessageTypes.PairingReject, new PairingRejectMessage(request.DeviceId, InvalidPinReason), cancellationToken);
            _logger.LogInformation("Pairing rejected. Reason={Reason}", InvalidPinReason);
            return;
        }

        DateTimeOffset trustedAt = _clock.UtcNow;
        await _store.TrustAsync(request.DeviceId, trustedAt, cancellationToken);
        connection.IsTrusted = true;
        await SendAsync(connection, MessageTypes.PairingAccept, new PairingAcceptMessage(request.DeviceId, trustedAt), cancellationToken);
        _logger.LogInformation("Pairing accepted. Trusted={Trusted}", connection.IsTrusted);
    }

    private void UntrustLiveConnections(string deviceId)
    {
        foreach (IClientConnection connection in _connections.Keys)
        {
            if (string.Equals(connection.DeviceId, deviceId, StringComparison.Ordinal))
            {
                connection.IsTrusted = false;
            }
        }
    }

    private Task SendAsync<TPayload>(
        IClientConnection connection,
        string type,
        TPayload payload,
        CancellationToken cancellationToken)
    {
        string json = EnvelopeCodec.Serialize(type, payload, _clock.UtcNow);
        return connection.SendAsync(json, cancellationToken);
    }
}
