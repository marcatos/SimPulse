using SimPulse.Bridge.Core.Ports;

namespace SimPulse.Bridge.Core.Tests;

internal sealed class FakePairingUx : IPairingUx
{
    public string? LastPin { get; private set; }

    public DateTimeOffset? LastExpiresAtUtc { get; private set; }

    public string? LastStatus { get; private set; }

    public string? LastRedisplayedPin { get; private set; }

    public int ShowPinCount { get; private set; }

    public int RedisplayCount { get; private set; }

    public bool ThrowOnPairNewDevice { get; set; }

    public event Action? PairNewDeviceRequested;

    public event Action? ShowCurrentPinRequested;

    public void ShowPin(string pin, DateTimeOffset expiresAtUtc)
    {
        LastPin = pin;
        LastExpiresAtUtc = expiresAtUtc;
        ShowPinCount++;
    }

    public void ShowStatus(string message)
    {
        LastStatus = message;
    }

    public void RedisplayLastPin()
    {
        RedisplayCount++;
        LastRedisplayedPin = LastPin;
        if (LastPin is null)
        {
            LastStatus = "No pairing PIN is available.";
        }
    }

    public void ClearPin()
    {
        LastPin = null;
        LastExpiresAtUtc = null;
    }

    public void RaisePairNewDevice()
    {
        if (ThrowOnPairNewDevice)
        {
            throw new InvalidOperationException("pair-new-device failed");
        }

        PairNewDeviceRequested?.Invoke();
    }

    public void RaiseShowCurrentPin()
    {
        ShowCurrentPinRequested?.Invoke();
    }
}
