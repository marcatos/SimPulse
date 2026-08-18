using System.Buffers.Binary;
using System.Text;

namespace SimPulse.Bridge.Core.Adapters.Iracing;

public static class IracingHeaderReader
{
    public static bool TryReadSessionYaml(ReadOnlySpan<byte> buffer, out string? yaml, out bool connected)
    {
        yaml = null;
        connected = false;
        if (buffer.Length < IracingSdkConstants.HeaderMinSize)
        {
            return false;
        }

        int status = BinaryPrimitives.ReadInt32LittleEndian(buffer[IracingSdkConstants.HeaderStatusOffset..]);
        int infoLen = BinaryPrimitives.ReadInt32LittleEndian(buffer[IracingSdkConstants.HeaderSessionInfoLenOffset..]);
        int infoOffset = BinaryPrimitives.ReadInt32LittleEndian(buffer[IracingSdkConstants.HeaderSessionInfoOffsetOffset..]);
        connected = (status & IracingSdkConstants.StatusConnected) != 0;
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
