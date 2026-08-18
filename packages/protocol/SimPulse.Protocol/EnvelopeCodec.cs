using System.Text.Json;
using System.Text.Json.Serialization;

namespace SimPulse.Protocol;

public static class EnvelopeCodec
{
    public static readonly JsonSerializerOptions Options = CreateOptions();

    public static string Serialize<TPayload>(
        string type,
        TPayload payload,
        DateTimeOffset sentAtUtc,
        string? messageId = null)
    {
        JsonElement element = JsonSerializer.SerializeToElement(payload, Options);
        MessageEnvelope envelope = new(
            ProtocolVersions.Current,
            type,
            messageId ?? Guid.NewGuid().ToString("N"),
            sentAtUtc,
            element);
        return JsonSerializer.Serialize(envelope, Options);
    }

    public static MessageEnvelope Deserialize(string json)
    {
        MessageEnvelope? envelope = JsonSerializer.Deserialize<MessageEnvelope>(json, Options);
        if (envelope is null)
        {
            throw new InvalidOperationException("Protocol envelope deserialized to null.");
        }

        return envelope;
    }

    public static bool TryReadPayload<TPayload>(MessageEnvelope envelope, out TPayload? payload)
    {
        try
        {
            payload = envelope.Payload.Deserialize<TPayload>(Options);
            return payload is not null;
        }
        catch (JsonException)
        {
            payload = default;
            return false;
        }
        catch (NotSupportedException)
        {
            payload = default;
            return false;
        }
    }

    public static bool IsKnownType(string type)
    {
        return type is MessageTypes.Hello
            or MessageTypes.PairingRequest
            or MessageTypes.PairingAccept
            or MessageTypes.PairingReject
            or MessageTypes.Heartbeat
            or MessageTypes.TimeSyncRequest
            or MessageTypes.TimeSyncResponse
            or MessageTypes.SimulatorSessionSnapshot
            or MessageTypes.RaceEvent
            or MessageTypes.TelemetryFrame
            or MessageTypes.Error;
    }

    private static JsonSerializerOptions CreateOptions()
    {
        return new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            PropertyNameCaseInsensitive = true,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
            WriteIndented = false
        };
    }
}
