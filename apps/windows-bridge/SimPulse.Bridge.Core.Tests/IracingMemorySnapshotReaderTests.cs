using System.Buffers.Binary;
using System.Text;

using SimPulse.Bridge.Core.Adapters.Iracing;
using SimPulse.Bridge.Core.Ports;
using SimPulse.Domain;

namespace SimPulse.Bridge.Core.Tests;

public sealed class IracingMemorySnapshotReaderTests
{
    [Fact]
    public void Two_arg_snapshot_defaults_update_and_unknown_telemetry()
    {
        IracingMemorySnapshot snapshot = new("yaml", Connected: true);

        Assert.Equal("yaml", snapshot.SessionYaml);
        Assert.True(snapshot.Connected);
        Assert.Equal(0, snapshot.SessionInfoUpdate);
        Assert.Equal(DataPresence.Unknown, snapshot.Telemetry.SessionTime.Presence);
        Assert.Equal(DataPresence.Unknown, snapshot.Telemetry.SessionNum.Presence);
        Assert.Equal(DataPresence.Unknown, snapshot.Telemetry.DriverCarIdx.Presence);
        Assert.Equal(DataPresence.Unknown, snapshot.Telemetry.Lap.Presence);
    }

    [Fact]
    public void Reads_telemetry_from_latest_var_row_when_headers_exist()
    {
        byte[] buffer = CreateVarTable(includeNames: true, sessionTime: 12.25, sessionNum: 1, driverCarIdx: 3, lap: 4);

        Assert.True(IracingMemorySnapshotReader.TryRead(buffer, out IracingMemorySnapshot snapshot));
        Assert.True(snapshot.Connected);
        Assert.Equal(9, snapshot.SessionInfoUpdate);
        Assert.True(snapshot.Telemetry.SessionTime.TryGet(out double sessionTime));
        Assert.Equal(12.25, sessionTime);
        Assert.True(snapshot.Telemetry.SessionNum.TryGet(out int sessionNum));
        Assert.Equal(1, sessionNum);
        Assert.True(snapshot.Telemetry.DriverCarIdx.TryGet(out int carIdx));
        Assert.Equal(3, carIdx);
        Assert.True(snapshot.Telemetry.Lap.TryGet(out int lap));
        Assert.Equal(4, lap);
    }

    [Fact]
    public void Missing_var_names_are_unknown_and_do_not_throw()
    {
        byte[] buffer = CreateVarTable(includeNames: false, sessionTime: 1, sessionNum: 0, driverCarIdx: 0, lap: 0);

        Assert.True(IracingMemorySnapshotReader.TryRead(buffer, out IracingMemorySnapshot snapshot));

        Assert.Equal(DataPresence.Unknown, snapshot.Telemetry.SessionTime.Presence);
        Assert.Equal(DataPresence.Unknown, snapshot.Telemetry.SessionNum.Presence);
        Assert.Equal(DataPresence.Unknown, snapshot.Telemetry.DriverCarIdx.Presence);
        Assert.Equal(DataPresence.Unknown, snapshot.Telemetry.Lap.Presence);
    }

    private static byte[] CreateVarTable(
        bool includeNames,
        double sessionTime,
        int sessionNum,
        int driverCarIdx,
        int lap)
    {
        const int varHeaderOffset = IracingSdkConstants.HeaderLayoutMinSize;
        const int bufLen = 32;
        int buffer0Offset = varHeaderOffset + (4 * IracingSdkConstants.VarHeaderSize);
        int buffer1Offset = buffer0Offset + bufLen;
        byte[] buffer = new byte[buffer1Offset + bufLen];

        BinaryPrimitives.WriteInt32LittleEndian(
            buffer.AsSpan(IracingSdkConstants.HeaderStatusOffset),
            IracingSdkConstants.StatusConnected);
        BinaryPrimitives.WriteInt32LittleEndian(buffer.AsSpan(IracingSdkConstants.HeaderSessionInfoUpdateOffset), 9);
        BinaryPrimitives.WriteInt32LittleEndian(buffer.AsSpan(IracingSdkConstants.HeaderNumVarsOffset), 4);
        BinaryPrimitives.WriteInt32LittleEndian(buffer.AsSpan(IracingSdkConstants.HeaderVarHeaderOffsetOffset), varHeaderOffset);
        BinaryPrimitives.WriteInt32LittleEndian(buffer.AsSpan(IracingSdkConstants.HeaderNumBufOffset), 2);
        BinaryPrimitives.WriteInt32LittleEndian(buffer.AsSpan(IracingSdkConstants.HeaderBufLenOffset), bufLen);

        WriteVarBuf(buffer, 0, tickCount: 1, buffer0Offset);
        WriteVarBuf(buffer, 1, tickCount: 8, buffer1Offset);

        WriteVarHeader(buffer.AsSpan(varHeaderOffset), IracingSdkConstants.VarTypeDouble, 0, includeNames ? "SessionTime" : "OtherTime");
        WriteVarHeader(buffer.AsSpan(varHeaderOffset + IracingSdkConstants.VarHeaderSize), IracingSdkConstants.VarTypeInt, 8, includeNames ? "SessionNum" : "OtherNum");
        WriteVarHeader(buffer.AsSpan(varHeaderOffset + (2 * IracingSdkConstants.VarHeaderSize)), IracingSdkConstants.VarTypeInt, 12, includeNames ? "DriverCarIdx" : "OtherCar");
        WriteVarHeader(buffer.AsSpan(varHeaderOffset + (3 * IracingSdkConstants.VarHeaderSize)), IracingSdkConstants.VarTypeInt, 16, includeNames ? "Lap" : "OtherLap");

        BinaryPrimitives.WriteDoubleLittleEndian(buffer.AsSpan(buffer1Offset), sessionTime);
        BinaryPrimitives.WriteInt32LittleEndian(buffer.AsSpan(buffer1Offset + 8), sessionNum);
        BinaryPrimitives.WriteInt32LittleEndian(buffer.AsSpan(buffer1Offset + 12), driverCarIdx);
        BinaryPrimitives.WriteInt32LittleEndian(buffer.AsSpan(buffer1Offset + 16), lap);
        return buffer;
    }

    private static void WriteVarBuf(byte[] buffer, int index, int tickCount, int bufOffset)
    {
        int at = IracingSdkConstants.HeaderVarBufOffset + (index * IracingSdkConstants.VarBufStride);
        BinaryPrimitives.WriteInt32LittleEndian(buffer.AsSpan(at), tickCount);
        BinaryPrimitives.WriteInt32LittleEndian(buffer.AsSpan(at + 4), bufOffset);
    }

    private static void WriteVarHeader(Span<byte> dest, int type, int offset, string name)
    {
        BinaryPrimitives.WriteInt32LittleEndian(dest, type);
        BinaryPrimitives.WriteInt32LittleEndian(dest[4..], offset);
        BinaryPrimitives.WriteInt32LittleEndian(dest[8..], 1);
        Encoding.ASCII.GetBytes(name).CopyTo(dest[IracingSdkConstants.VarHeaderNameOffset..]);
    }
}
