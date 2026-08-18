namespace SimPulse.Protocol;

public sealed record TimeSyncEstimate(TimeSpan Offset, TimeSpan RoundTrip);

/// <summary>
/// NTP-style offset from a four-timestamp exchange. Large RTT means the offset is untrusted.
/// </summary>
public static class TimeSyncCalculator
{
    public static TimeSyncEstimate Estimate(
        DateTimeOffset clientSentAt,
        DateTimeOffset serverReceivedAt,
        DateTimeOffset serverSentAt,
        DateTimeOffset clientReceivedAt)
    {
        TimeSpan roundTrip = (clientReceivedAt - clientSentAt) - (serverSentAt - serverReceivedAt);
        TimeSpan offset = TimeSpan.FromTicks(
            ((serverReceivedAt - clientSentAt) + (serverSentAt - clientReceivedAt)).Ticks / 2);
        return new TimeSyncEstimate(offset, roundTrip);
    }
}
