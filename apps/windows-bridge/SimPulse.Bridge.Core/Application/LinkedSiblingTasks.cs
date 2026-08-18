namespace SimPulse.Bridge.Core.Application;

public static class LinkedSiblingTasks
{
    public static async Task RunAsync(
        Func<CancellationToken, Task> first,
        Func<CancellationToken, Task> second,
        CancellationToken cancellationToken)
    {
        using CancellationTokenSource linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        Task left = first(linked.Token);
        Task right = second(linked.Token);
        Task completed = await Task.WhenAny(left, right);
        await linked.CancelAsync();
        await Task.WhenAll(ObserveAsync(left), ObserveAsync(right));
        await completed;
    }

    private static async Task ObserveAsync(Task task)
    {
        try
        {
            await task;
        }
        catch (Exception)
        {
        }
    }
}
