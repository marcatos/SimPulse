using System.Net.WebSockets;
using System.Text;

using SimPulse.Bridge.Core.Ports;

namespace SimPulse.Bridge.Core.Adapters;

internal sealed class WebSocketClientConnection : IClientConnection
{
    private readonly WebSocket _socket;
    private readonly SemaphoreSlim _sendLock = new(1, 1);

    public WebSocketClientConnection(WebSocket socket, string connectionId)
    {
        _socket = socket;
        ConnectionId = connectionId;
    }

    public string ConnectionId { get; }

    public string? DeviceId { get; set; }

    public bool IsTrusted { get; set; }

    public async Task SendAsync(string json, CancellationToken cancellationToken)
    {
        byte[] bytes = Encoding.UTF8.GetBytes(json);
        await _sendLock.WaitAsync(cancellationToken);
        try
        {
            if (_socket.State != WebSocketState.Open)
            {
                return;
            }

            await _socket.SendAsync(bytes, WebSocketMessageType.Text, endOfMessage: true, cancellationToken);
        }
        finally
        {
            _sendLock.Release();
        }
    }
}
