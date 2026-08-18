using Microsoft.Extensions.Logging;

using SimPulse.Bridge.Core.Ports;

namespace SimPulse.Bridge.Core.Adapters;

public sealed class ConsolePairingUx : IPairingUx
{
    private readonly ILogger<ConsolePairingUx> _logger;
    private string? _lastPin;
    private DateTimeOffset? _lastExpiresAtUtc;

    public ConsolePairingUx(ILogger<ConsolePairingUx> logger)
    {
        _logger = logger;
    }

    public event Action? PairNewDeviceRequested;

    public event Action? ShowCurrentPinRequested;

    public void ShowPin(string pin, DateTimeOffset expiresAtUtc)
    {
        _lastPin = pin;
        _lastExpiresAtUtc = expiresAtUtc;
        _logger.LogInformation(
            "Pairing PIN is visible in tray/console. ExpiresAtUtc={ExpiresAtUtc} Component={Component}",
            expiresAtUtc,
            "ConsolePairingUx");
    }

    public void ShowStatus(string message)
    {
        _logger.LogInformation("{Message} Component={Component}", message, "ConsolePairingUx");
    }

    public void RedisplayLastPin()
    {
        if (_lastPin is null || _lastExpiresAtUtc is null)
        {
            ShowStatus("No pairing PIN is available.");
            return;
        }

        _logger.LogInformation(
            "Current pairing PIN redisplayed. ExpiresAtUtc={ExpiresAtUtc} Component={Component}",
            _lastExpiresAtUtc,
            "ConsolePairingUx");
    }

    public void ClearPin()
    {
        _lastPin = null;
        _lastExpiresAtUtc = null;
    }

    public void RequestPairNewDevice()
    {
        PairNewDeviceRequested?.Invoke();
    }

    public void RequestShowCurrentPin()
    {
        ShowCurrentPinRequested?.Invoke();
    }
}
