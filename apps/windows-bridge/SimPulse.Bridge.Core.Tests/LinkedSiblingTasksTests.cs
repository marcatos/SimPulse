using SimPulse.Bridge.Core.Application;

namespace SimPulse.Bridge.Core.Tests;

public sealed class LinkedSiblingTasksTests
{
    [Fact]
    public async Task Cancels_sibling_when_one_task_faults()
    {
        using CancellationTokenSource unused = new();
        TaskCompletionSource idle = new();
        bool siblingCancelled = false;

        Task IdleAsync(CancellationToken cancellationToken)
        {
            cancellationToken.Register(() =>
            {
                siblingCancelled = true;
                idle.TrySetResult();
            });
            return idle.Task;
        }

        static Task FaultAsync(CancellationToken cancellationToken)
        {
            return Task.FromException(new InvalidOperationException("listen failed"));
        }

        using CancellationTokenSource timeout = new(TimeSpan.FromSeconds(2));
        InvalidOperationException ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => LinkedSiblingTasks.RunAsync(FaultAsync, IdleAsync, unused.Token).WaitAsync(timeout.Token));

        Assert.Equal("listen failed", ex.Message);
        Assert.True(siblingCancelled);
    }

    [Fact]
    public async Task Cancels_sibling_when_one_task_completes()
    {
        using CancellationTokenSource unused = new();
        TaskCompletionSource idle = new();
        bool siblingCancelled = false;

        Task IdleAsync(CancellationToken cancellationToken)
        {
            cancellationToken.Register(() =>
            {
                siblingCancelled = true;
                idle.TrySetCanceled(cancellationToken);
            });
            return idle.Task;
        }

        static Task CompleteAsync(CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }

        using CancellationTokenSource timeout = new(TimeSpan.FromSeconds(2));
        await LinkedSiblingTasks.RunAsync(CompleteAsync, IdleAsync, unused.Token).WaitAsync(timeout.Token);
        Assert.True(siblingCancelled);
    }
}
