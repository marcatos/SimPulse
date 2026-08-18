using System.Text.Json;

namespace SimPulse.Protocol;

public sealed record MessageEnvelope(
    int ProtocolVersion,
    string Type,
    string MessageId,
    DateTimeOffset SentAtUtc,
    JsonElement Payload);
