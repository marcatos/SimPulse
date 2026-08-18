using System.Buffers.Binary;
using System.Text;

using SimPulse.Bridge.Core.Adapters.Iracing;

namespace SimPulse.Bridge.Core.Tests;

public sealed class IracingHeaderReaderTests
{
    [Fact]
    public void Reads_session_yaml_when_connected()
    {
        const string yaml = "WeekendInfo:\n  TrackID: 1\n";
        byte[] yamlBytes = Encoding.ASCII.GetBytes(yaml);
        byte[] buffer = new byte[IracingSdkConstants.HeaderSessionInfoOffsetOffset + 4 + yamlBytes.Length];
        BinaryPrimitives.WriteInt32LittleEndian(
            buffer.AsSpan(IracingSdkConstants.HeaderStatusOffset),
            IracingSdkConstants.StatusConnected);
        BinaryPrimitives.WriteInt32LittleEndian(
            buffer.AsSpan(IracingSdkConstants.HeaderSessionInfoLenOffset),
            yamlBytes.Length);
        int yamlOffset = IracingSdkConstants.HeaderMinSize;
        BinaryPrimitives.WriteInt32LittleEndian(
            buffer.AsSpan(IracingSdkConstants.HeaderSessionInfoOffsetOffset),
            yamlOffset);
        yamlBytes.CopyTo(buffer.AsSpan(yamlOffset));

        Assert.True(IracingHeaderReader.TryReadSessionYaml(buffer, out string? parsed, out bool connected));
        Assert.True(connected);
        Assert.Equal(yaml, parsed);
    }

    [Fact]
    public void Reports_disconnected_without_yaml()
    {
        byte[] buffer = new byte[IracingSdkConstants.HeaderMinSize];

        Assert.True(IracingHeaderReader.TryReadSessionYaml(buffer, out string? parsed, out bool connected));
        Assert.False(connected);
        Assert.Null(parsed);
    }
}
