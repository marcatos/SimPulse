using System.Collections.Concurrent;
using System.Diagnostics;
using System.Security.Cryptography;

using Microsoft.Extensions.Logging;

using SimPulse.Bridge.Core.Ports;
using SimPulse.Protocol;

namespace SimPulse.Bridge.Core.Application;

public sealed class PairingCoordinator
{
    public const string InvalidPinReason = "invalid_pin";
    public const string WindowClosedReason = "pairing_window_closed";
    public const string TooManyAttemptsReason = "too_many_attempts";
    public const int MaxFailedAttempts = 5;
    public static readonly TimeSpan WindowDuration = TimeSpan.FromMinutes(5);

    private readonly ITrustedDeviceStore _store;
    private readonly IClock _clock;
    private readonly IPairingPinGenerator _pinGenerator;
    private readonly ILogger<PairingCoordinator> _logger;
    private readonly ConcurrentDictionary<IClientConnection, byte> _connections = new();
    private readonly object _windowLock = new();
    private string? _pin;
    private DateTimeOffset? _windowExpiresAt;
    private int _failedAttempts;
    private bool _locked;

    public PairingCoordinator(
        ITrustedDeviceStore store,
        IClock clock,
        IPairingPinGenerator pinGenerator,
        ILogger<PairingCoordinator> logger)
    {
        _store = store;
        _clock = clock;
        _pinGenerator = pinGenerator;
        _logger = logger;
    }

    public PairingWindowInfo BeginPairingWindow()
    {
        Stopwatch started = Stopwatch.StartNew();
        string pin;
        DateTimeOffset expiresAt;
        lock (_windowLock)
        {
            pin = _pinGenerator.Generate();
            _pin = pin;
            expiresAt = _clock.UtcNow.Add(WindowDuration);
            _windowExpiresAt = expiresAt;
            _failedAttempts = 0;
            _locked = false;
        }

        _logger.LogInformation(
            "Pairing window opened. Pin={Pin} ExpiresAtUtc={ExpiresAtUtc} MaxFailedAttempts={MaxFailedAttempts} ElapsedMs={ElapsedMs} Component={Component}",
            pin,
            expiresAt,
            MaxFailedAttempts,
            started.ElapsedMilliseconds,
            "PairingCoordinator");
        return new PairingWindowInfo(pin, expiresAt);
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

    public bool IsPairingWindowOpen()
    {
        lock (_windowLock)
        {
            return EvaluateWindowUnlocked() == PairingWindowState.Open;
        }
    }

    public event Action? PairingWindowClosed;

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
        connection.IsTrusted = await _store.AuthorizeReconnectAsync(
            hello.DeviceId,
            hello.ReconnectToken,
            cancellationToken);
        _logger.LogInformation(
            "Hello trust evaluated. Trusted={Trusted} TokenPresent={TokenPresent}",
            connection.IsTrusted,
            !string.IsNullOrEmpty(hello.ReconnectToken));
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
        if (!TryConsumePairingPin(request.Pin, out string? rejectReason))
        {
            await SendAsync(
                connection,
                MessageTypes.PairingReject,
                new PairingRejectMessage(request.DeviceId, rejectReason!),
                cancellationToken);
            _logger.LogInformation("Pairing rejected. Reason={Reason}", rejectReason);
            return;
        }

        DateTimeOffset trustedAt = _clock.UtcNow;
        byte[] raw = ReconnectToken.CreateRaw();
        string tokenHex;
        string tokenSha256;
        try
        {
            tokenHex = ReconnectToken.ToHex(raw);
            tokenSha256 = ReconnectToken.Sha256Hex(raw);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(raw);
        }

        await _store.TrustAsync(request.DeviceId, trustedAt, tokenSha256, cancellationToken);
        connection.IsTrusted = true;
        await SendAsync(
            connection,
            MessageTypes.PairingAccept,
            new PairingAcceptMessage(request.DeviceId, trustedAt, tokenHex),
            cancellationToken);
        _logger.LogInformation("Pairing accepted. Trusted={Trusted}", connection.IsTrusted);
    }

    private bool TryConsumePairingPin(string pin, out string? rejectReason)
    {
        bool invalidated = false;
        bool accepted = false;
        rejectReason = null;
        lock (_windowLock)
        {
            PairingWindowState state = EvaluateWindowUnlocked();
            if (state == PairingWindowState.Closed)
            {
                rejectReason = WindowClosedReason;
            }
            else if (state == PairingWindowState.Locked)
            {
                rejectReason = TooManyAttemptsReason;
            }
            else if (!string.Equals(_pin, pin, StringComparison.Ordinal))
            {
                _failedAttempts++;
                if (_failedAttempts >= MaxFailedAttempts)
                {
                    _locked = true;
                    invalidated = true;
                    _logger.LogInformation(
                        "Pairing window locked after failed attempts. FailedAttempts={FailedAttempts} MaxFailedAttempts={MaxFailedAttempts}",
                        _failedAttempts,
                        MaxFailedAttempts);
                }

                rejectReason = InvalidPinReason;
            }
            else
            {
                CloseWindowUnlocked();
                invalidated = true;
                accepted = true;
            }
        }

        if (invalidated)
        {
            PairingWindowClosed?.Invoke();
        }

        return accepted;
    }

    private PairingWindowState EvaluateWindowUnlocked()
    {
        if (_pin is null || _windowExpiresAt is null || _clock.UtcNow >= _windowExpiresAt.Value)
        {
            return PairingWindowState.Closed;
        }

        return _locked ? PairingWindowState.Locked : PairingWindowState.Open;
    }

    private void CloseWindowUnlocked()
    {
        _pin = null;
        _windowExpiresAt = null;
        _failedAttempts = 0;
        _locked = false;
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

    private enum PairingWindowState
    {
        Open,
        Closed,
        Locked
    }
}
