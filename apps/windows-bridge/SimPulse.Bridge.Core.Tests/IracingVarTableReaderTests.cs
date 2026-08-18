using System.Buffers.Binary;
using System.Text;

using SimPulse.Bridge.Core.Adapters.Iracing;

namespace SimPulse.Bridge.Core.Tests;

public sealed class IracingVarTableReaderTests
{
    // Official irsdk_header / irsdk_varHeader offsets (plan table).
    private const int SessionInfoUpdateOffset = 12;
    private const int NumVarsOffset = 24;
    private const int VarHeaderOffsetOffset = 28;
    private const int NumBufOffset = 32;
    private const int BufLenOffset = 36;
    private const int VarBufBaseOffset = 48;
    private const int VarBufStride = 16;
    private const int HeaderLayoutMinSize = 112;
    private const int VarHeaderSize = 144;
    private const int VarHeaderNameOffset = 16;
    private const int IrSdkInt = 2;
    private const int IrSdkDouble = 5;

    [Fact]
    public void Reads_typed_values_from_latest_varBuf_by_tickCount()
    {
        byte[] buffer = CreateSyntheticVarTableMmap(
            sessionInfoUpdate: 7,
            buffer0Tick: 10,
            buffer1Tick: 20,
            buffer0Values: (1.0, 99, 0, 1),
            buffer1Values: (123.5, 1, 3, 5));

        Assert.True(IracingHeaderReader.TryReadLayout(buffer, out IracingHeaderLayout header));
        Assert.Equal(7, header.SessionInfoUpdate);
        Assert.True(header.Connected);
        Assert.Equal(4, header.NumVars);
        Assert.Equal(2, header.NumBuf);
        Assert.Equal(32, header.BufLen);
        Assert.Equal(20, header.LatestTickCount);
        Assert.Equal(LatestBufOffset(buffer1: true), header.LatestBufOffset);

        Assert.True(IracingVarTableReader.TryReadVarHeaders(buffer, header, out IracingVarHeader[] vars));
        Assert.Equal(4, vars.Length);
        Assert.True(IracingVarTableReader.TryFind(vars, "SessionTime", out IracingVarHeader sessionTime));
        Assert.True(IracingVarTableReader.TryFind(vars, "SessionNum", out IracingVarHeader sessionNum));
        Assert.True(IracingVarTableReader.TryFind(vars, "DriverCarIdx", out IracingVarHeader driverCarIdx));
        Assert.True(IracingVarTableReader.TryFind(vars, "Lap", out IracingVarHeader lap));

        ReadOnlySpan<byte> row = buffer.AsSpan(header.LatestBufOffset, header.BufLen);
        Assert.True(IracingVarTableReader.TryReadDouble(row, sessionTime, out double sessionTimeValue));
        Assert.True(IracingVarTableReader.TryReadInt(row, sessionNum, out int sessionNumValue));
        Assert.True(IracingVarTableReader.TryReadInt(row, driverCarIdx, out int driverCarIdxValue));
        Assert.True(IracingVarTableReader.TryReadInt(row, lap, out int lapValue));

        Assert.Equal(123.5, sessionTimeValue);
        Assert.Equal(1, sessionNumValue);
        Assert.Equal(3, driverCarIdxValue);
        Assert.Equal(5, lapValue);
    }

    [Fact]
    public void TryReadVarHeaders_rejects_truncated_var_table()
    {
        byte[] buffer = CreateSyntheticVarTableMmap(
            sessionInfoUpdate: 1,
            buffer0Tick: 1,
            buffer1Tick: 2,
            buffer0Values: (0, 0, 0, 0),
            buffer1Values: (0, 0, 0, 0));
        byte[] truncated = buffer[..(HeaderLayoutMinSize + VarHeaderSize)];

        Assert.False(IracingHeaderReader.TryReadLayout(truncated, out _));
        Assert.False(IracingVarTableReader.TryReadVarHeaders(
            truncated,
            new IracingHeaderLayout(
                Status: IracingSdkConstants.StatusConnected,
                Connected: true,
                SessionInfoUpdate: 1,
                SessionInfoLen: 0,
                SessionInfoOffset: 0,
                NumVars: 4,
                VarHeaderOffset: HeaderLayoutMinSize,
                NumBuf: 2,
                BufLen: 32,
                LatestTickCount: 2,
                LatestBufOffset: LatestBufOffset(buffer1: true)),
            out _));
    }

    private static int LatestBufOffset(bool buffer1)
    {
        int varHeadersEnd = HeaderLayoutMinSize + (4 * VarHeaderSize);
        return buffer1 ? varHeadersEnd + 32 : varHeadersEnd;
    }

    private static byte[] CreateSyntheticVarTableMmap(
        int sessionInfoUpdate,
        int buffer0Tick,
        int buffer1Tick,
        (double SessionTime, int SessionNum, int DriverCarIdx, int Lap) buffer0Values,
        (double SessionTime, int SessionNum, int DriverCarIdx, int Lap) buffer1Values)
    {
        int varHeaderOffset = HeaderLayoutMinSize;
        int buffer0Offset = varHeaderOffset + (4 * VarHeaderSize);
        int buffer1Offset = buffer0Offset + 32;
        byte[] buffer = new byte[buffer1Offset + 32];

        BinaryPrimitives.WriteInt32LittleEndian(
            buffer.AsSpan(IracingSdkConstants.HeaderStatusOffset),
            IracingSdkConstants.StatusConnected);
        BinaryPrimitives.WriteInt32LittleEndian(buffer.AsSpan(SessionInfoUpdateOffset), sessionInfoUpdate);
        BinaryPrimitives.WriteInt32LittleEndian(buffer.AsSpan(NumVarsOffset), 4);
        BinaryPrimitives.WriteInt32LittleEndian(buffer.AsSpan(VarHeaderOffsetOffset), varHeaderOffset);
        BinaryPrimitives.WriteInt32LittleEndian(buffer.AsSpan(NumBufOffset), 2);
        BinaryPrimitives.WriteInt32LittleEndian(buffer.AsSpan(BufLenOffset), 32);

        WriteVarBuf(buffer, index: 0, tickCount: buffer0Tick, bufOffset: buffer0Offset);
        WriteVarBuf(buffer, index: 1, tickCount: buffer1Tick, bufOffset: buffer1Offset);

        WriteVarHeader(buffer.AsSpan(varHeaderOffset), IrSdkDouble, offset: 0, count: 1, "SessionTime");
        WriteVarHeader(buffer.AsSpan(varHeaderOffset + VarHeaderSize), IrSdkInt, offset: 8, count: 1, "SessionNum");
        WriteVarHeader(buffer.AsSpan(varHeaderOffset + (2 * VarHeaderSize)), IrSdkInt, offset: 12, count: 1, "DriverCarIdx");
        WriteVarHeader(buffer.AsSpan(varHeaderOffset + (3 * VarHeaderSize)), IrSdkInt, offset: 16, count: 1, "Lap");

        WriteRow(buffer.AsSpan(buffer0Offset, 32), buffer0Values);
        WriteRow(buffer.AsSpan(buffer1Offset, 32), buffer1Values);
        return buffer;
    }

    private static void WriteVarBuf(byte[] buffer, int index, int tickCount, int bufOffset)
    {
        int at = VarBufBaseOffset + (index * VarBufStride);
        BinaryPrimitives.WriteInt32LittleEndian(buffer.AsSpan(at), tickCount);
        BinaryPrimitives.WriteInt32LittleEndian(buffer.AsSpan(at + 4), bufOffset);
    }

    private static void WriteVarHeader(Span<byte> dest, int type, int offset, int count, string name)
    {
        BinaryPrimitives.WriteInt32LittleEndian(dest, type);
        BinaryPrimitives.WriteInt32LittleEndian(dest[4..], offset);
        BinaryPrimitives.WriteInt32LittleEndian(dest[8..], count);
        Encoding.ASCII.GetBytes(name).CopyTo(dest[VarHeaderNameOffset..]);
    }

    private static void WriteRow(
        Span<byte> row,
        (double SessionTime, int SessionNum, int DriverCarIdx, int Lap) values)
    {
        BinaryPrimitives.WriteDoubleLittleEndian(row, values.SessionTime);
        BinaryPrimitives.WriteInt32LittleEndian(row[8..], values.SessionNum);
        BinaryPrimitives.WriteInt32LittleEndian(row[12..], values.DriverCarIdx);
        BinaryPrimitives.WriteInt32LittleEndian(row[16..], values.Lap);
    }
}
