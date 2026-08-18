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
}
