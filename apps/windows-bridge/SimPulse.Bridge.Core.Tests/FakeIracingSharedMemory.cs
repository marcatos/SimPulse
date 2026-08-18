using SimPulse.Bridge.Core.Ports;

namespace SimPulse.Bridge.Core.Tests;

internal sealed class FakeIracingSharedMemory : IIracingSharedMemory
{
    private bool _canOpen;
    private IracingMemorySnapshot? _repeating;
    private readonly Queue<IracingMemorySnapshot>? _sequence;

    public FakeIracingSharedMemory(bool open, string? yaml = null, bool connected = true)
    {
        _canOpen = open;
        if (open)
        {
            _repeating = new IracingMemorySnapshot(yaml, connected);
        }
    }

    public FakeIracingSharedMemory(bool open, IReadOnlyList<IracingMemorySnapshot> sequence)
    {
        _canOpen = open;
        _sequence = new Queue<IracingMemorySnapshot>(sequence);
    }

    public void BecomeAvailable(string yaml, bool connected = true)
    {
        _canOpen = true;
        _repeating = new IracingMemorySnapshot(yaml, connected);
    }

    public bool TryOpen()
    {
        return _canOpen;
    }

    public void Close()
    {
    }

    public bool TryReadSnapshot(out IracingMemorySnapshot snapshot)
    {
        if (_sequence is not null)
        {
            return _sequence.TryDequeue(out snapshot);
        }

        if (_repeating is { } repeating)
        {
            snapshot = repeating;
            return true;
        }

        snapshot = default;
        return false;
    }
}
