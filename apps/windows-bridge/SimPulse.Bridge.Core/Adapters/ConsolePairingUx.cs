using Microsoft.Extensions.Logging;

using SimPulse.Bridge.Core.Ports;

namespace SimPulse.Bridge.Core.Adapters;

public sealed class ConsolePairingUx : IPairingUx
{
    private readonly ILogger<ConsolePairingUx> _logger;

    public ConsolePairingUx(ILogger<ConsolePairingUx> logger)
    {
        _logger = logger;
    }

    public event Action? PairNewDeviceRequested;

    public void ShowPin(string pin, DateTimeOffset expiresAtUtc)
    {
        _logger.LogInformation(
            "Pairing PIN is visible in tray/console. Pin={Pin} ExpiresAtUtc={ExpiresAtUtc} Component={Component}",
            pin,
            expiresAtUtc,
            "ConsolePairingUx");
    }

    public void ShowStatus(string message)
    {
        _logger.LogInformation("{Message} Component={Component}", message, "ConsolePairingUx");
    }

    public void RequestPairNewDevice()
    {
        PairNewDeviceRequested?.Invoke();
    }
}
