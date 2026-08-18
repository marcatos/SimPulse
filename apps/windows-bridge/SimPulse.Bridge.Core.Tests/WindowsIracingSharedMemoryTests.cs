using Microsoft.Extensions.Logging;

using SimPulse.Bridge.Core.Adapters.Iracing;

namespace SimPulse.Bridge.Core.Tests;

public sealed class WindowsIracingSharedMemoryTests
{
    [Fact]
    public void TryOpen_returns_false_when_map_missing()
    {
        WindowsIracingSharedMemory memory = new();

        bool opened = memory.TryOpen();
        if (opened)
        {
            memory.Close();
            return;
        }

        Assert.False(memory.TryReadSnapshot(out _));
    }

    [Fact]
    public void TryOpen_logs_repeated_misses_at_debug()
    {
        ListLogger<WindowsIracingSharedMemory> logger = new();
        using WindowsIracingSharedMemory memory = new(logger);

        bool first = memory.TryOpen();
        bool second = memory.TryOpen();
        if (first || second)
        {
            memory.Close();
            return;
        }

        Assert.DoesNotContain(
            logger.Entries,
            e => e.Level == LogLevel.Information &&
                 (e.Message.Contains("unavailable", StringComparison.OrdinalIgnoreCase) ||
                  e.Message.Contains("open starting", StringComparison.OrdinalIgnoreCase) ||
                  e.Message.Contains("unsupported", StringComparison.OrdinalIgnoreCase)));
        Assert.Contains(
            logger.Entries,
            e => e.Level == LogLevel.Debug &&
                 (e.Message.Contains("unavailable", StringComparison.OrdinalIgnoreCase) ||
                  e.Message.Contains("unsupported", StringComparison.OrdinalIgnoreCase)));
    }

    [Fact]
    public void WaitForUpdate_returns_false_when_event_missing()
    {
        using WindowsIracingSharedMemory memory = new();
        if (memory.TryOpen())
        {
            memory.Close();
            return;
        }

        Assert.False(memory.WaitForUpdate(TimeSpan.Zero, CancellationToken.None));
    }
}
