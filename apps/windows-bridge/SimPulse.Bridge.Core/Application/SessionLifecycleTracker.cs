using SimPulse.Domain;

namespace SimPulse.Bridge.Core.Application;

public sealed class SessionLifecycleTracker
{
    private readonly HashSet<string> _emitted = new(StringComparer.Ordinal);

    public RaceEvent? Observe(RaceEvent candidate)
    {
        string key = BuildKey(candidate);
        if (!_emitted.Add(key))
        {
            return null;
        }

        return candidate;
    }

    private static string BuildKey(RaceEvent e)
    {
        e.Attributes.TryGetValue("lapNumber", out string? lap);
        return $"{e.SimulatorSessionId}:{e.Type}:{lap ?? ""}";
    }
}
