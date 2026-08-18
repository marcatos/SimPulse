using SimPulse.Bridge.Core.Application;

namespace SimPulse.Bridge.Core.Tests;

public sealed class TrayStartupPolicyTests
{
    [Fact]
    public void Ready_timeout_is_five_seconds()
    {
        Assert.Equal(TimeSpan.FromSeconds(5), TrayStartupPolicy.ReadyTimeout);
    }

    [Fact]
    public void Completed_task_is_ready_and_does_not_fall_back()
    {
        TrayStartupOutcome outcome = TrayStartupPolicy.WaitForReady(Task.CompletedTask, TimeSpan.FromSeconds(1));

        Assert.Equal(TrayStartupOutcome.Ready, outcome);
        Assert.False(TrayStartupPolicy.ShouldFallBackToConsole(outcome));
    }

    [Fact]
    public void Faulted_task_falls_back_to_console()
    {
        Task faulted = Task.FromException(new InvalidOperationException("NotifyIcon failed"));

        TrayStartupOutcome outcome = TrayStartupPolicy.WaitForReady(faulted, TimeSpan.FromSeconds(1));

        Assert.Equal(TrayStartupOutcome.Failed, outcome);
        Assert.True(TrayStartupPolicy.ShouldFallBackToConsole(outcome));
    }

    [Fact]
    public void Timed_out_wait_falls_back_to_console()
    {
        Task never = new TaskCompletionSource<bool>().Task;

        TrayStartupOutcome outcome = TrayStartupPolicy.WaitForReady(never, TimeSpan.FromMilliseconds(40));

        Assert.Equal(TrayStartupOutcome.TimedOut, outcome);
        Assert.True(TrayStartupPolicy.ShouldFallBackToConsole(outcome));
    }
}
