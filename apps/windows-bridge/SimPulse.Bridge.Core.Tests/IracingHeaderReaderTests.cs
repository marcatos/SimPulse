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
    public void DecodeYaml_round_trips_latin1_u_umlaut()
    {
        byte[] bytes = [0x4E, 0xFC, 0x72, 0x62, 0x75, 0x72, 0x67];

        string decoded = IracingHeaderReader.DecodeYaml(bytes);

        Assert.Equal("Nürburg", decoded);
        Assert.Equal(bytes, Encoding.Latin1.GetBytes(decoded));
    }

    [Fact]
    public void Reports_disconnected_without_yaml()
    {
        byte[] buffer = new byte[IracingSdkConstants.HeaderMinSize];

        Assert.True(IracingHeaderReader.TryReadSessionYaml(buffer, out string? parsed, out bool connected));
        Assert.False(connected);
        Assert.Null(parsed);
    }

    [Fact]
    public void TryReadHeader_reads_status_and_session_info_offsets()
    {
        byte[] buffer = new byte[IracingSdkConstants.HeaderMinSize];
        BinaryPrimitives.WriteInt32LittleEndian(
            buffer.AsSpan(IracingSdkConstants.HeaderStatusOffset),
            IracingSdkConstants.StatusConnected);
        BinaryPrimitives.WriteInt32LittleEndian(
            buffer.AsSpan(IracingSdkConstants.HeaderSessionInfoLenOffset),
            42);
        BinaryPrimitives.WriteInt32LittleEndian(
            buffer.AsSpan(IracingSdkConstants.HeaderSessionInfoOffsetOffset),
            96);

        Assert.True(IracingHeaderReader.TryReadHeader(
            buffer,
            out int status,
            out int infoLen,
            out int infoOffset,
            out bool connected));
        Assert.Equal(IracingSdkConstants.StatusConnected, status);
        Assert.Equal(42, infoLen);
        Assert.Equal(96, infoOffset);
        Assert.True(connected);
    }

    [Fact]
    public void TryReadHeader_rejects_short_buffer()
    {
        Assert.False(IracingHeaderReader.TryReadHeader(
            new byte[IracingSdkConstants.HeaderMinSize - 1],
            out _,
            out _,
            out _,
            out _));
    }

    [Fact]
    public void TryReadLayout_round_trips_sessionInfoUpdate_and_connected()
    {
        byte[] buffer = new byte[IracingSdkConstants.HeaderLayoutMinSize];
        BinaryPrimitives.WriteInt32LittleEndian(
            buffer.AsSpan(IracingSdkConstants.HeaderStatusOffset),
            IracingSdkConstants.StatusConnected);
        BinaryPrimitives.WriteInt32LittleEndian(
            buffer.AsSpan(IracingSdkConstants.HeaderSessionInfoUpdateOffset),
            11);
        BinaryPrimitives.WriteInt32LittleEndian(
            buffer.AsSpan(IracingSdkConstants.HeaderNumVarsOffset),
            0);
        BinaryPrimitives.WriteInt32LittleEndian(
            buffer.AsSpan(IracingSdkConstants.HeaderVarHeaderOffsetOffset),
            IracingSdkConstants.HeaderLayoutMinSize);
        BinaryPrimitives.WriteInt32LittleEndian(
            buffer.AsSpan(IracingSdkConstants.HeaderNumBufOffset),
            1);
        BinaryPrimitives.WriteInt32LittleEndian(
            buffer.AsSpan(IracingSdkConstants.HeaderBufLenOffset),
            0);
        BinaryPrimitives.WriteInt32LittleEndian(
            buffer.AsSpan(IracingSdkConstants.HeaderVarBufOffset),
            4);
        BinaryPrimitives.WriteInt32LittleEndian(
            buffer.AsSpan(IracingSdkConstants.HeaderVarBufOffset + 4),
            IracingSdkConstants.HeaderLayoutMinSize);

        Assert.True(IracingHeaderReader.TryReadLayout(buffer, out IracingHeaderLayout header));
        Assert.Equal(11, header.SessionInfoUpdate);
        Assert.True(header.Connected);
        Assert.Equal(IracingSdkConstants.StatusConnected, header.Status);
        Assert.Equal(4, header.LatestTickCount);
        Assert.Equal(IracingSdkConstants.HeaderLayoutMinSize, header.LatestBufOffset);
    }

    [Fact]
    public void TryReadLayout_rejects_truncated_buffer()
    {
        Assert.False(IracingHeaderReader.TryReadLayout(
            new byte[IracingSdkConstants.HeaderLayoutMinSize - 1],
            out _));
    }

    [Fact]
    public void TryReadHeader_still_succeeds_on_yaml_only_24_byte_buffer()
    {
        byte[] buffer = new byte[IracingSdkConstants.HeaderMinSize];
        BinaryPrimitives.WriteInt32LittleEndian(
            buffer.AsSpan(IracingSdkConstants.HeaderStatusOffset),
            IracingSdkConstants.StatusConnected);

        Assert.True(IracingHeaderReader.TryReadHeader(
            buffer,
            out int status,
            out _,
            out _,
            out bool connected));
        Assert.Equal(IracingSdkConstants.StatusConnected, status);
        Assert.True(connected);
        Assert.False(IracingHeaderReader.TryReadLayout(buffer, out _));
    }
}
