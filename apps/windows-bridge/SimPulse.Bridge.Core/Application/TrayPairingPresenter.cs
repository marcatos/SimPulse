using SimPulse.Bridge.Core.Ports;

namespace SimPulse.Bridge.Core.Application;

public sealed class TrayPairingPresenter
{
    private readonly PairingCoordinator _pairing;
    private readonly IPairingUx _ux;

    public TrayPairingPresenter(PairingCoordinator pairing, IPairingUx ux)
    {
        _pairing = pairing;
        _ux = ux;
        _ux.PairNewDeviceRequested += RequestPairNewDevice;
    }

    public void OnWindowOpened(string pin, DateTimeOffset expiresAtUtc)
    {
        _ux.ShowPin(pin, expiresAtUtc);
    }

    public void RequestPairNewDevice()
    {
        PairingWindowInfo window = _pairing.BeginPairingWindow();
        _ux.ShowPin(window.Pin, window.ExpiresAtUtc);
    }
}
