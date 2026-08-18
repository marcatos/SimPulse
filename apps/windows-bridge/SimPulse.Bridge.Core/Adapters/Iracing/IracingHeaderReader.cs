using System.Buffers.Binary;
using System.Text;

namespace SimPulse.Bridge.Core.Adapters.Iracing;

public readonly record struct IracingHeaderLayout(
    int Status,
    bool Connected,
    int SessionInfoUpdate,
    int SessionInfoLen,
    int SessionInfoOffset,
    int NumVars,
    int VarHeaderOffset,
    int NumBuf,
    int BufLen,
    int LatestTickCount,
    int LatestBufOffset);

public static class IracingHeaderReader
{
    public static bool TryReadHeader(
        ReadOnlySpan<byte> buffer,
        out int status,
        out int sessionInfoLen,
        out int sessionInfoOffset,
        out bool connected)
    {
        status = 0;
        sessionInfoLen = 0;
        sessionInfoOffset = 0;
        connected = false;
        if (buffer.Length < IracingSdkConstants.HeaderMinSize)
        {
            return false;
        }

        status = BinaryPrimitives.ReadInt32LittleEndian(buffer[IracingSdkConstants.HeaderStatusOffset..]);
        sessionInfoLen = BinaryPrimitives.ReadInt32LittleEndian(buffer[IracingSdkConstants.HeaderSessionInfoLenOffset..]);
        sessionInfoOffset = BinaryPrimitives.ReadInt32LittleEndian(buffer[IracingSdkConstants.HeaderSessionInfoOffsetOffset..]);
        connected = (status & IracingSdkConstants.StatusConnected) != 0;
        return true;
    }

    public static bool TryReadLayout(ReadOnlySpan<byte> buffer, out IracingHeaderLayout header)
    {
        header = default;
        if (buffer.Length < IracingSdkConstants.HeaderLayoutMinSize)
        {
            return false;
        }

        int status = ReadInt32(buffer, IracingSdkConstants.HeaderStatusOffset);
        int sessionInfoUpdate = ReadInt32(buffer, IracingSdkConstants.HeaderSessionInfoUpdateOffset);
        int sessionInfoLen = ReadInt32(buffer, IracingSdkConstants.HeaderSessionInfoLenOffset);
        int sessionInfoOffset = ReadInt32(buffer, IracingSdkConstants.HeaderSessionInfoOffsetOffset);
        int numVars = ReadInt32(buffer, IracingSdkConstants.HeaderNumVarsOffset);
        int varHeaderOffset = ReadInt32(buffer, IracingSdkConstants.HeaderVarHeaderOffsetOffset);
        int numBuf = ReadInt32(buffer, IracingSdkConstants.HeaderNumBufOffset);
        int bufLen = ReadInt32(buffer, IracingSdkConstants.HeaderBufLenOffset);

        if (!RangesFit(buffer.Length, sessionInfoLen, sessionInfoOffset, numVars, varHeaderOffset, bufLen))
        {
            return false;
        }

        if (!TrySelectLatestVarBuf(buffer, numBuf, bufLen, out int latestTick, out int latestOffset))
        {
            return false;
        }

        header = new IracingHeaderLayout(
            status,
            (status & IracingSdkConstants.StatusConnected) != 0,
            sessionInfoUpdate,
            sessionInfoLen,
            sessionInfoOffset,
            numVars,
            varHeaderOffset,
            numBuf,
            bufLen,
            latestTick,
            latestOffset);
        return true;
    }

    private static bool RangesFit(
        int bufferLength,
        int sessionInfoLen,
        int sessionInfoOffset,
        int numVars,
        int varHeaderOffset,
        int bufLen)
    {
        if (sessionInfoLen < 0 || sessionInfoOffset < 0 || numVars < 0 || varHeaderOffset < 0 || bufLen < 0)
        {
            return false;
        }

        if (sessionInfoLen > 0 && (long)sessionInfoOffset + sessionInfoLen > bufferLength)
        {
            return false;
        }

        return (long)varHeaderOffset + ((long)numVars * IracingSdkConstants.VarHeaderSize) <= bufferLength;
    }

    private static bool TrySelectLatestVarBuf(
        ReadOnlySpan<byte> buffer,
        int numBuf,
        int bufLen,
        out int latestTick,
        out int latestOffset)
    {
        latestTick = 0;
        latestOffset = 0;
        int count = Math.Clamp(numBuf, 1, IracingSdkConstants.MaxBufs);
        bool found = false;
        for (int i = 0; i < count; i++)
        {
            int at = IracingSdkConstants.HeaderVarBufOffset + (i * IracingSdkConstants.VarBufStride);
            int tick = ReadInt32(buffer, at);
            int offset = ReadInt32(buffer, at + 4);
            if (offset < 0 || (long)offset + bufLen > buffer.Length)
            {
                return false;
            }

            if (!found || tick > latestTick)
            {
                found = true;
                latestTick = tick;
                latestOffset = offset;
            }
        }

        return found;
    }

    private static int ReadInt32(ReadOnlySpan<byte> buffer, int offset)
    {
        return BinaryPrimitives.ReadInt32LittleEndian(buffer[offset..]);
    }

    public static bool TryReadSessionYaml(ReadOnlySpan<byte> buffer, out string? yaml, out bool connected)
    {
        yaml = null;
        if (!TryReadHeader(buffer, out _, out int infoLen, out int infoOffset, out connected))
        {
            return false;
        }

        if (!connected || infoLen <= 0 || infoOffset < 0 || infoOffset + infoLen > buffer.Length)
        {
            return true;
        }

        yaml = DecodeYaml(buffer.Slice(infoOffset, infoLen));
        return true;
    }

    public static string DecodeYaml(ReadOnlySpan<byte> bytes)
    {
        int end = bytes.Length;
        while (end > 0 && bytes[end - 1] == 0)
        {
            end--;
        }

        // IRSDK session info is Windows-1252; Latin1 matches 0x00-0xFF without a code-page provider.
        return Encoding.Latin1.GetString(bytes[..end]);
    }
}
