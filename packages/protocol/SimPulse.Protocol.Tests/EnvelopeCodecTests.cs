namespace SimPulse.Protocol.Tests;

public sealed class EnvelopeCodecTests
{
    [Fact]
    public void Round_trips_hello_payload()
    {
        DateTimeOffset sent = DateTimeOffset.Parse("2026-08-18T08:00:00Z");
        HelloMessage hello = new("SimPulse", "bridge", "pc-1");

        string json = EnvelopeCodec.Serialize(MessageTypes.Hello, hello, sent, "msg1");
        MessageEnvelope envelope = EnvelopeCodec.Deserialize(json);

        Assert.Equal(1, envelope.ProtocolVersion);
        Assert.Equal(MessageTypes.Hello, envelope.Type);
        Assert.Equal("msg1", envelope.MessageId);
        Assert.True(EnvelopeCodec.TryReadPayload(envelope, out HelloMessage? restored));
        Assert.Equal("pc-1", restored!.DeviceId);
    }

    [Fact]
    public void Round_trips_hello_reconnect_token()
    {
        DateTimeOffset sent = DateTimeOffset.Parse("2026-08-18T08:00:00Z");
        HelloMessage hello = new("SimPulse", "phone", "phone-1", "ab".PadRight(64, 'c'));

        string json = EnvelopeCodec.Serialize(MessageTypes.Hello, hello, sent, "hello-token");
        Assert.Contains("reconnectToken", json, StringComparison.Ordinal);

        MessageEnvelope envelope = EnvelopeCodec.Deserialize(json);
        Assert.True(EnvelopeCodec.TryReadPayload(envelope, out HelloMessage? restored));
        Assert.Equal(hello.DeviceId, restored!.DeviceId);
        Assert.Equal(hello.ReconnectToken, restored.ReconnectToken);
    }

    [Fact]
    public void Hello_without_reconnect_token_deserializes_null()
    {
        const string json = """
            {
              "protocolVersion": 1,
              "type": "hello",
              "messageId": "abc",
              "sentAtUtc": "2026-08-18T08:00:00Z",
              "payload": { "product": "SimPulse", "role": "phone", "deviceId": "phone-1" }
            }
            """;

        MessageEnvelope envelope = EnvelopeCodec.Deserialize(json);
        Assert.True(EnvelopeCodec.TryReadPayload(envelope, out HelloMessage? hello));
        Assert.Equal("phone-1", hello!.DeviceId);
        Assert.Null(hello.ReconnectToken);
    }

    [Fact]
    public void Round_trips_pairing_accept_reconnect_token()
    {
        DateTimeOffset sent = DateTimeOffset.Parse("2026-08-18T08:00:00Z");
        PairingAcceptMessage accept = new("phone-1", sent, "ab".PadRight(64, 'd'));

        string json = EnvelopeCodec.Serialize(MessageTypes.PairingAccept, accept, sent, "acc1");
        Assert.Contains("reconnectToken", json, StringComparison.Ordinal);

        MessageEnvelope envelope = EnvelopeCodec.Deserialize(json);
        Assert.True(EnvelopeCodec.TryReadPayload(envelope, out PairingAcceptMessage? restored));
        Assert.Equal("phone-1", restored!.DeviceId);
        Assert.Equal(64, restored.ReconnectToken.Length);
        Assert.Equal(accept.ReconnectToken, restored.ReconnectToken);
    }

    [Fact]
    public void Ignores_unknown_json_fields()
    {
        const string json = """
            {
              "protocolVersion": 1,
              "type": "hello",
              "messageId": "abc",
              "sentAtUtc": "2026-08-18T08:00:00Z",
              "payload": { "product": "SimPulse", "role": "bridge", "deviceId": "pc-1" },
              "futureField": { "nested": true }
            }
            """;

        MessageEnvelope envelope = EnvelopeCodec.Deserialize(json);
        Assert.Equal("hello", envelope.Type);
        Assert.True(EnvelopeCodec.TryReadPayload(envelope, out HelloMessage? hello));
        Assert.Equal("bridge", hello!.Role);
    }

    [Fact]
    public void Round_trips_pairing_reject_payload()
    {
        DateTimeOffset sent = DateTimeOffset.Parse("2026-08-18T08:00:00Z");
        PairingRejectMessage reject = new("phone-1", "invalid_pin");

        string json = EnvelopeCodec.Serialize(MessageTypes.PairingReject, reject, sent, "rej1");
        MessageEnvelope envelope = EnvelopeCodec.Deserialize(json);

        Assert.Equal(MessageTypes.PairingReject, envelope.Type);
        Assert.True(EnvelopeCodec.TryReadPayload(envelope, out PairingRejectMessage? restored));
        Assert.Equal("phone-1", restored!.DeviceId);
        Assert.Equal("invalid_pin", restored.Reason);
    }

    [Fact]
    public void TryReadPayload_malformed_payload_returns_false_without_throwing()
    {
        const string json = """
            {
              "protocolVersion": 1,
              "type": "hello",
              "messageId": "abc",
              "sentAtUtc": "2026-08-18T08:00:00Z",
              "payload": 123
            }
            """;

        MessageEnvelope envelope = EnvelopeCodec.Deserialize(json);
        bool ok = EnvelopeCodec.TryReadPayload(envelope, out HelloMessage? hello);

        Assert.False(ok);
        Assert.Null(hello);
    }

    [Fact]
    public void Unknown_message_type_is_not_treated_as_known()
    {
        Assert.False(EnvelopeCodec.IsKnownType("simulator.understeer-magic"));
    }

    [Fact]
    public void Older_protocol_version_is_rejected()
    {
        Assert.Equal(ProtocolCompatibility.Rejected, ProtocolCompatibilityRules.Classify(0));
        Assert.Equal(ProtocolCompatibility.Compatible, ProtocolCompatibilityRules.Classify(1));
        Assert.Equal(ProtocolCompatibility.UnknownNewer, ProtocolCompatibilityRules.Classify(2));
    }
}

public sealed class TimeSyncCalculatorTests
{
    [Fact]
    public void Estimates_offset_from_four_timestamps()
    {
        DateTimeOffset t1 = DateTimeOffset.Parse("2026-08-18T08:00:00.000Z");
        DateTimeOffset t2 = DateTimeOffset.Parse("2026-08-18T08:00:00.120Z");
        DateTimeOffset t3 = DateTimeOffset.Parse("2026-08-18T08:00:00.121Z");
        DateTimeOffset t4 = DateTimeOffset.Parse("2026-08-18T08:00:00.040Z");

        TimeSyncEstimate estimate = TimeSyncCalculator.Estimate(t1, t2, t3, t4);

        Assert.Equal(TimeSpan.FromMilliseconds(100.5), estimate.Offset);
        Assert.True(estimate.RoundTrip >= TimeSpan.Zero);
    }
}
