using System.Net.WebSockets;
using System.Text;

using SimPulse.Bridge.Core.Ports;

namespace SimPulse.Bridge.Core.Adapters;

internal sealed class WebSocketClientConnection : IClientConnection, IAsyncDisposable
{
    private readonly WebSocket _socket;
    private readonly SemaphoreSlim _sendLock = new(1, 1);
    private bool _disposed;

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
            if (_disposed || _socket.State != WebSocketState.Open)
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

    public async Task CloseAsync()
    {
        await _sendLock.WaitAsync();
        try
        {
            if (_disposed || _socket.State is not (WebSocketState.Open or WebSocketState.CloseReceived))
            {
                return;
            }

            using CancellationTokenSource closeCts = new(TimeSpan.FromSeconds(2));
            await _socket.CloseOutputAsync(WebSocketCloseStatus.NormalClosure, "closing", closeCts.Token);
        }
        catch (Exception ex) when (ex is WebSocketException or ObjectDisposedException or OperationCanceledException)
        {
        }
        finally
        {
            _sendLock.Release();
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed)
        {
            return;
        }

        try
        {
            await CloseAsync();
        }
        finally
        {
            _disposed = true;
            _socket.Dispose();
            _sendLock.Dispose();
        }
    }
}
