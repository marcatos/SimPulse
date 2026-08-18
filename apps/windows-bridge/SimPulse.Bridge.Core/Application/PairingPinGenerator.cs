using SimPulse.Bridge.Core.Ports;

namespace SimPulse.Bridge.Core.Application;

public sealed class PairingPinGenerator : IPairingPinGenerator
{
    public string Generate()
    {
        return Random.Shared.Next(0, 1_000_000).ToString("D6");
    }
}
