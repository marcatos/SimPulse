using SimPulse.Bridge.Core.Ports;
using SimPulse.Domain;

namespace SimPulse.Bridge.Core.Adapters.Iracing;

public static class IracingMemorySnapshotReader
{
    public static bool TryRead(ReadOnlySpan<byte> buffer, out IracingMemorySnapshot snapshot)
    {
        if (IracingHeaderReader.TryReadLayout(buffer, out IracingHeaderLayout layout))
        {
            snapshot = new IracingMemorySnapshot(
                ReadYaml(buffer, layout),
                layout.Connected,
                layout.SessionInfoUpdate,
                ReadTelemetry(buffer, layout));
            return true;
        }

        if (!IracingHeaderReader.TryReadSessionYaml(buffer, out string? yaml, out bool connected))
        {
            snapshot = default;
            return false;
        }

        int update = buffer.Length >= IracingSdkConstants.HeaderSessionInfoOffsetOffset
            ? System.Buffers.Binary.BinaryPrimitives.ReadInt32LittleEndian(
                buffer[IracingSdkConstants.HeaderSessionInfoUpdateOffset..])
            : 0;
        snapshot = new IracingMemorySnapshot(yaml, connected, update, IracingTelemetryValues.Unknown());
        return true;
    }

    private static string? ReadYaml(ReadOnlySpan<byte> buffer, in IracingHeaderLayout layout)
    {
        if (!layout.Connected || layout.SessionInfoLen <= 0 || layout.SessionInfoOffset < 0)
        {
            return null;
        }

        if ((long)layout.SessionInfoOffset + layout.SessionInfoLen > buffer.Length)
        {
            return null;
        }

        return IracingHeaderReader.DecodeYaml(buffer.Slice(layout.SessionInfoOffset, layout.SessionInfoLen));
    }

    private static IracingTelemetryValues ReadTelemetry(ReadOnlySpan<byte> buffer, in IracingHeaderLayout layout)
    {
        if (layout.NumVars <= 0 ||
            !IracingVarTableReader.TryReadVarHeaders(buffer, layout, out IracingVarHeader[] headers))
        {
            return IracingTelemetryValues.Unknown();
        }

        if (layout.BufLen <= 0 ||
            layout.LatestBufOffset < 0 ||
            (long)layout.LatestBufOffset + layout.BufLen > buffer.Length)
        {
            return IracingTelemetryValues.Unknown();
        }

        ReadOnlySpan<byte> row = buffer.Slice(layout.LatestBufOffset, layout.BufLen);
        return new IracingTelemetryValues(
            ReadDouble(row, headers, "SessionTime"),
            ReadInt(row, headers, "SessionNum"),
            ReadInt(row, headers, "DriverCarIdx"),
            ReadInt(row, headers, "Lap"));
    }

    private static OptionalValue<int> ReadInt(ReadOnlySpan<byte> row, IReadOnlyList<IracingVarHeader> headers, string name)
    {
        if (!IracingVarTableReader.TryFind(headers, name, out IracingVarHeader header) ||
            !IracingVarTableReader.TryReadInt(row, header, out int value))
        {
            return OptionalValue<int>.Unknown();
        }

        return OptionalValue<int>.Available(value);
    }

    private static OptionalValue<double> ReadDouble(ReadOnlySpan<byte> row, IReadOnlyList<IracingVarHeader> headers, string name)
    {
        if (!IracingVarTableReader.TryFind(headers, name, out IracingVarHeader header) ||
            !IracingVarTableReader.TryReadDouble(row, header, out double value))
        {
            return OptionalValue<double>.Unknown();
        }

        return OptionalValue<double>.Available(value);
    }
}
