namespace SimPulse.Bridge.Core.Ports;

public readonly record struct IracingMemorySnapshot(string? SessionYaml, bool Connected);

public interface IIracingSharedMemory
{
    bool TryOpen();

    void Close();

    bool TryReadSnapshot(out IracingMemorySnapshot snapshot);
}
