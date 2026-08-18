namespace SimPulse.Bridge.Core.Application;

public static class TrayPairingUxText
{
    public const string IconText = "SimPulse Bridge";
    public const string ShowCurrentPin = "Show current PIN";
    public const string PairNewDevice = "Pair new device";
    public const string Exit = "Exit";
    public const string PairingWindowClosed = "pairing window closed";
    public const int NotifyIconTextLimit = 63;

    public static string FormatPinDisplay(string pin, DateTimeOffset expiresAtUtc)
    {
        return $"PIN {pin} expires {expiresAtUtc.UtcDateTime:u}";
    }
}
