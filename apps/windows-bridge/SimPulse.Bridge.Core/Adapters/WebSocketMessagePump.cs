using System.Net.WebSockets;
using System.Text;

using Microsoft.Extensions.Logging;

using SimPulse.Bridge.Core.Ports;
using SimPulse.Protocol;

namespace SimPulse.Bridge.Core.Adapters;

internal sealed class WebSocketMessagePump
{
    private const int MaxMessageBytes = 256 * 1024;

    private readonly ILogger _logger;
    private readonly Func<IClientConnection, MessageEnvelope, CancellationToken, Task> _onMessage;

    public WebSocketMessagePump(
        ILogger logger,
        Func<IClientConnection, MessageEnvelope, CancellationToken, Task> onMessage)
    {
        _logger = logger;
        _onMessage = onMessage;
    }

    public async Task ReadLoopAsync(
        IClientConnection connection,
        WebSocket socket,
        string connectionId,
        CancellationToken cancellationToken)
    {
        byte[] buffer = new byte[16 * 1024];
        while (socket.State == WebSocketState.Open && !cancellationToken.IsCancellationRequested)
        {
            string? json = await ReceiveTextAsync(socket, buffer, cancellationToken);
            if (json is null)
            {
                break;
            }

            await DispatchAsync(connection, json, connectionId, cancellationToken);
        }
    }

    private async Task DispatchAsync(
        IClientConnection connection,
        string json,
        string connectionId,
        CancellationToken cancellationToken)
    {
        if (!TryDeserialize(json, connectionId, out MessageEnvelope envelope))
        {
            return;
        }

        if (!EnvelopeCodec.IsKnownType(envelope.Type))
        {
            _logger.LogInformation(
                "Ignoring unknown protocol type. Type={Type} ConnectionId={ConnectionId}",
                envelope.Type,
                connectionId);
            return;
        }

        if (envelope.Type == MessageTypes.Hello)
        {
            ApplyHello(connection, envelope, connectionId);
        }

        await _onMessage(connection, envelope, cancellationToken);
    }

    private bool TryDeserialize(string json, string connectionId, out MessageEnvelope envelope)
    {
        try
        {
            envelope = EnvelopeCodec.Deserialize(json);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Ignoring unreadable envelope. ConnectionId={ConnectionId}", connectionId);
            envelope = default!;
            return false;
        }
    }

    private void ApplyHello(IClientConnection connection, MessageEnvelope envelope, string connectionId)
    {
        if (!EnvelopeCodec.TryReadPayload(envelope, out HelloMessage? hello) || hello is null)
        {
            _logger.LogWarning("Hello payload not parseable. ConnectionId={ConnectionId}", connectionId);
            return;
        }

        connection.DeviceId = hello.DeviceId;
        _logger.LogInformation(
            "Hello received. Role={Role} Product={Product} ConnectionId={ConnectionId}",
            hello.Role,
            hello.Product,
            connectionId);
    }

    private static async Task<string?> ReceiveTextAsync(
        WebSocket socket,
        byte[] buffer,
        CancellationToken cancellationToken)
    {
        using MemoryStream payload = new();
        WebSocketReceiveResult result;
        do
        {
            result = await socket.ReceiveAsync(buffer, cancellationToken);
            if (result.MessageType == WebSocketMessageType.Close)
            {
                return null;
            }

            payload.Write(buffer, 0, result.Count);
            if (payload.Length > MaxMessageBytes)
            {
                throw new InvalidOperationException("WebSocket text frame exceeded 256 KiB.");
            }
        }
        while (!result.EndOfMessage);

        return Encoding.UTF8.GetString(payload.ToArray());
    }
}
