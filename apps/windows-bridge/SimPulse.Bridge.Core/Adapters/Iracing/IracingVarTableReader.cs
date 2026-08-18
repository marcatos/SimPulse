using System.Buffers.Binary;
using System.Text;

namespace SimPulse.Bridge.Core.Adapters.Iracing;

public readonly record struct IracingVarHeader(string Name, int Type, int Offset, int Count);

public static class IracingVarTableReader
{
    public static bool TryReadVarHeaders(
        ReadOnlySpan<byte> buffer,
        in IracingHeaderLayout header,
        out IracingVarHeader[] headers)
    {
        headers = [];
        if (header.NumVars < 0 || header.VarHeaderOffset < 0)
        {
            return false;
        }

        long tableEnd = (long)header.VarHeaderOffset + ((long)header.NumVars * IracingSdkConstants.VarHeaderSize);
        if (tableEnd > buffer.Length)
        {
            return false;
        }

        headers = new IracingVarHeader[header.NumVars];
        for (int i = 0; i < header.NumVars; i++)
        {
            int at = header.VarHeaderOffset + (i * IracingSdkConstants.VarHeaderSize);
            ReadOnlySpan<byte> raw = buffer.Slice(at, IracingSdkConstants.VarHeaderSize);
            headers[i] = new IracingVarHeader(
                ReadName(raw),
                ReadInt32(raw, IracingSdkConstants.VarHeaderTypeOffset),
                ReadInt32(raw, IracingSdkConstants.VarHeaderOffsetOffset),
                ReadInt32(raw, IracingSdkConstants.VarHeaderCountOffset));
        }

        return true;
    }

    public static bool TryReadInt(ReadOnlySpan<byte> row, IracingVarHeader header, out int value)
    {
        value = 0;
        if (header.Type != IracingSdkConstants.VarTypeInt || header.Count < 1 || !Fits(row.Length, header.Offset, sizeof(int)))
        {
            return false;
        }

        value = BinaryPrimitives.ReadInt32LittleEndian(row[header.Offset..]);
        return true;
    }

    public static bool TryReadDouble(ReadOnlySpan<byte> row, IracingVarHeader header, out double value)
    {
        value = 0;
        if (header.Type != IracingSdkConstants.VarTypeDouble || header.Count < 1 || !Fits(row.Length, header.Offset, sizeof(double)))
        {
            return false;
        }

        value = BinaryPrimitives.ReadDoubleLittleEndian(row[header.Offset..]);
        return true;
    }

    public static bool TryFind(IReadOnlyList<IracingVarHeader> headers, string name, out IracingVarHeader header)
    {
        for (int i = 0; i < headers.Count; i++)
        {
            if (headers[i].Name == name)
            {
                header = headers[i];
                return true;
            }
        }

        header = default;
        return false;
    }

    private static bool Fits(int rowLength, int offset, int size)
    {
        return offset >= 0 && (long)offset + size <= rowLength;
    }

    private static string ReadName(ReadOnlySpan<byte> varHeader)
    {
        ReadOnlySpan<byte> nameBytes = varHeader.Slice(
            IracingSdkConstants.VarHeaderNameOffset,
            IracingSdkConstants.VarHeaderNameSize);
        int end = nameBytes.IndexOf((byte)0);
        if (end < 0)
        {
            end = nameBytes.Length;
        }

        return Encoding.ASCII.GetString(nameBytes[..end]);
    }

    private static int ReadInt32(ReadOnlySpan<byte> buffer, int offset)
    {
        return BinaryPrimitives.ReadInt32LittleEndian(buffer[offset..]);
    }
}
