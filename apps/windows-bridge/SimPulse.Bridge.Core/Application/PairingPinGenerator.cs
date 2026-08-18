using System.Security.Cryptography;

using SimPulse.Bridge.Core.Ports;

namespace SimPulse.Bridge.Core.Application;

public sealed class PairingPinGenerator : IPairingPinGenerator
{
    public string Generate()
    {
        return RandomNumberGenerator.GetInt32(0, 1_000_000).ToString("D6");
    }
}
