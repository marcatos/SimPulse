using SimPulse.Bridge.Core.Ports;

namespace SimPulse.Bridge.Core.Tests;

internal sealed class FakePairingUx : IPairingUx
{
    public string? LastPin { get; private set; }

    public DateTimeOffset? LastExpiresAtUtc { get; private set; }

    public string? LastStatus { get; private set; }

    public event Action? PairNewDeviceRequested;

    public void ShowPin(string pin, DateTimeOffset expiresAtUtc)
    {
        LastPin = pin;
        LastExpiresAtUtc = expiresAtUtc;
    }

    public void ShowStatus(string message)
    {
        LastStatus = message;
    }

    public void RaisePairNewDevice()
    {
        PairNewDeviceRequested?.Invoke();
    }
}
