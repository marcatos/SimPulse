namespace SimPulse.Bridge.Core.Adapters;

public static class BridgeTlsPolicy
{
    public static bool IsLoopbackHost(string host)
    {
        return string.Equals(host, "127.0.0.1", StringComparison.OrdinalIgnoreCase)
            || string.Equals(host, "localhost", StringComparison.OrdinalIgnoreCase);
    }

    public static void EnsureCleartextAllowed(string host, bool tlsEnabled)
    {
        if (!tlsEnabled && !IsLoopbackHost(host))
        {
            throw new InvalidOperationException(
                "Cleartext Bridge transport is allowed only on a loopback host.");
        }
    }
}
