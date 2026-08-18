using SimPulse.Domain;

namespace SimPulse.Bridge.Core.Ports;

public readonly record struct IracingTelemetryValues(
    OptionalValue<double> SessionTime,
    OptionalValue<int> SessionNum,
    OptionalValue<int> DriverCarIdx,
    OptionalValue<int> Lap)
{
    public static IracingTelemetryValues Unknown() => new(
        OptionalValue<double>.Unknown(),
        OptionalValue<int>.Unknown(),
        OptionalValue<int>.Unknown(),
        OptionalValue<int>.Unknown());
}

public readonly record struct IracingMemorySnapshot(
    string? SessionYaml,
    bool Connected,
    int SessionInfoUpdate,
    IracingTelemetryValues Telemetry)
{
    public IracingMemorySnapshot(string? SessionYaml, bool Connected)
        : this(SessionYaml, Connected, 0, IracingTelemetryValues.Unknown())
    {
    }
}

public interface IIracingSharedMemory
{
    bool TryOpen();

    void Close();

    bool TryReadSnapshot(out IracingMemorySnapshot snapshot);

    bool WaitForUpdate(TimeSpan timeout, CancellationToken cancellationToken);
}
