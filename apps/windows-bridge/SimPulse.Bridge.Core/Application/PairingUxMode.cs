namespace SimPulse.Bridge.Core.Application;

public static class PairingUxMode
{
    public static bool UseTray(bool windowsTrayBuild, bool userInteractive, string? trayEnv)
    {
        if (!windowsTrayBuild || !userInteractive)
        {
            return false;
        }

        return !string.Equals(trayEnv, "0", StringComparison.OrdinalIgnoreCase);
    }
}
