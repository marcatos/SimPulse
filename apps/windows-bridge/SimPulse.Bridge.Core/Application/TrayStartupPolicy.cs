namespace SimPulse.Bridge.Core.Application;

public enum TrayStartupOutcome
{
    Ready,
    Failed,
    TimedOut
}

public static class TrayStartupPolicy
{
    public static readonly TimeSpan ReadyTimeout = TimeSpan.FromSeconds(5);

    public static bool ShouldFallBackToConsole(TrayStartupOutcome outcome)
    {
        return outcome != TrayStartupOutcome.Ready;
    }

    public static TrayStartupOutcome WaitForReady(Task ready, TimeSpan timeout)
    {
        try
        {
            if (!ready.Wait(timeout))
            {
                return ready.IsCompletedSuccessfully
                    ? TrayStartupOutcome.Ready
                    : TrayStartupOutcome.TimedOut;
            }

            return TrayStartupOutcome.Ready;
        }
        catch (AggregateException)
        {
            return TrayStartupOutcome.Failed;
        }
    }
}
