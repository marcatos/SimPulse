using Microsoft.Extensions.Logging;

using SimPulse.Bridge.Core.Ports;

namespace SimPulse.Bridge.Core.Application;

public sealed class TrayPairingPresenter
{
    private const string Component = "TrayPairingPresenter";

    private readonly PairingCoordinator _pairing;
    private readonly IPairingUx _ux;
    private readonly ILogger<TrayPairingPresenter> _logger;

    public TrayPairingPresenter(
        PairingCoordinator pairing,
        IPairingUx ux,
        ILogger<TrayPairingPresenter> logger)
    {
        _pairing = pairing;
        _ux = ux;
        _logger = logger;
        _ux.PairNewDeviceRequested += RequestPairNewDevice;
        _ux.ShowCurrentPinRequested += RequestShowCurrentPin;
    }

    public void OnWindowOpened(string pin, DateTimeOffset expiresAtUtc)
    {
        _ux.ShowPin(pin, expiresAtUtc);
    }

    public void RequestPairNewDevice()
    {
        try
        {
            PairingWindowInfo window = _pairing.BeginPairingWindow();
            _ux.ShowPin(window.Pin, window.ExpiresAtUtc);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Pair new device failed. Component={Component}", Component);
        }
    }

    public void RequestShowCurrentPin()
    {
        try
        {
            _ux.RedisplayLastPin();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Show current PIN failed. Component={Component}", Component);
        }
    }
}
