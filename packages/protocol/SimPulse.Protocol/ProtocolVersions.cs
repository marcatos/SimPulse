namespace SimPulse.Protocol;

public static class ProtocolVersions
{
    public const int Current = 1;

    public const int MinimumCompatible = 1;
}

public enum ProtocolCompatibility
{
    Compatible = 0,
    UnknownNewer = 1,
    Rejected = 2
}

public static class ProtocolCompatibilityRules
{
    public static ProtocolCompatibility Classify(int protocolVersion)
    {
        if (protocolVersion < ProtocolVersions.MinimumCompatible)
        {
            return ProtocolCompatibility.Rejected;
        }

        if (protocolVersion > ProtocolVersions.Current)
        {
            return ProtocolCompatibility.UnknownNewer;
        }

        return ProtocolCompatibility.Compatible;
    }
}

public static class MessageTypes
{
    public const string Hello = "hello";
    public const string PairingRequest = "pairing.request";
    public const string PairingAccept = "pairing.accept";
    public const string PairingReject = "pairing.reject";
    public const string Heartbeat = "heartbeat";
    public const string TimeSyncRequest = "timesync.request";
    public const string TimeSyncResponse = "timesync.response";
    public const string SimulatorSessionSnapshot = "simulator.session.snapshot";
    public const string RaceEvent = "simulator.race-event";
    public const string TelemetryFrame = "simulator.telemetry";
    public const string Error = "error";
}
