using System.Diagnostics;
using System.Net;
using System.Net.WebSockets;

using Microsoft.Extensions.Logging;

using SimPulse.Bridge.Core.Ports;
using SimPulse.Protocol;

namespace SimPulse.Bridge.Core.Adapters;

public sealed class HttpListenerWebSocketTransport : IBridgeTransport
{
    public const string DefaultHost = "127.0.0.1";
    public const int DefaultPort = 8742;

    private readonly string _host;
    private readonly int _port;
    private readonly IClientSessionHub _hub;
    private readonly IClock _clock;
    private readonly ILogger<HttpListenerWebSocketTransport> _logger;
    private readonly WebSocketMessagePump _pump;

    public HttpListenerWebSocketTransport(
        string host,
        int port,
        IClientSessionHub hub,
        IClock clock,
        ILogger<HttpListenerWebSocketTransport> logger,
        Func<IClientConnection, MessageEnvelope, CancellationToken, Task>? onMessage = null)
    {
        _host = host;
        _port = port;
        _hub = hub;
        _clock = clock;
        _logger = logger;
        _pump = new WebSocketMessagePump(logger, onMessage ?? ((_, _, _) => Task.CompletedTask));
    }

    public async Task RunAsync(
        Func<IClientConnection, CancellationToken, Task> onConnected,
        CancellationToken cancellationToken)
    {
        Stopwatch total = Stopwatch.StartNew();
        string prefix = BuildPrefix(_host, _port);
        using HttpListener listener = new();
        listener.Prefixes.Add(prefix);
        listener.Start();
        _logger.LogInformation(
            "WebSocket transport listening. Prefix={Prefix} Host={Host} Port={Port} Component={Component}",
            prefix,
            _host,
            _port,
            "HttpListenerWebSocketTransport");

        using CancellationTokenRegistration stop = cancellationToken.Register(StopQuietly, listener);
        try
        {
            await AcceptLoopAsync(listener, onConnected, cancellationToken);
        }
        finally
        {
            StopQuietly(listener);
            _logger.LogInformation(
                "WebSocket transport stopped. ElapsedMs={ElapsedMs}",
                total.ElapsedMilliseconds);
        }
    }

    internal static string BuildPrefix(string host, int port)
    {
        string listenerHost = host is "0.0.0.0" or "*" or "+" ? "+" : host;
        return $"http://{listenerHost}:{port}/ws/";
    }

    private async Task AcceptLoopAsync(
        HttpListener listener,
        Func<IClientConnection, CancellationToken, Task> onConnected,
        CancellationToken cancellationToken)
    {
        List<Task> connections = [];
        while (!cancellationToken.IsCancellationRequested && listener.IsListening)
        {
            HttpListenerContext context;
            try
            {
                context = await listener.GetContextAsync();
            }
            catch (Exception ex) when (IsListenerStopped(ex, cancellationToken))
            {
                break;
            }

            connections.Add(HandleContextAsync(context, onConnected, cancellationToken));
        }

        await Task.WhenAll(connections.Select(ObserveAsync));
    }

    private static async Task ObserveAsync(Task task)
    {
        try
        {
            await task;
        }
        catch (Exception)
        {
        }
    }

    private async Task HandleContextAsync(
        HttpListenerContext context,
        Func<IClientConnection, CancellationToken, Task> onConnected,
        CancellationToken cancellationToken)
    {
        try
        {
            if (!context.Request.IsWebSocketRequest)
            {
                context.Response.StatusCode = 400;
                context.Response.Close();
                return;
            }

            HttpListenerWebSocketContext wsContext = await context.AcceptWebSocketAsync(subProtocol: null);
            await HandleSocketAsync(wsContext.WebSocket, onConnected, cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogWarning(ex, "WebSocket accept failed.");
        }
    }

    private async Task HandleSocketAsync(
        WebSocket socket,
        Func<IClientConnection, CancellationToken, Task> onConnected,
        CancellationToken cancellationToken)
    {
        string connectionId = Guid.NewGuid().ToString("N");
        DateTimeOffset acceptedAt = _clock.UtcNow;
        await using WebSocketClientConnection connection = new(socket, connectionId);
        _logger.LogInformation("WebSocket accepted. ConnectionId={ConnectionId}", connectionId);
        _hub.Register(connection);
        using CancellationTokenSource receiveLifetime = new();
        using CancellationTokenRegistration closeOnStop = cancellationToken.Register(
            () => _ = InitiateCloseAsync(connection, receiveLifetime));
        try
        {
            Task connected = onConnected(connection, cancellationToken);
            Task read = _pump.ReadLoopAsync(connection, socket, connectionId, receiveLifetime.Token);
            await Task.WhenAll(connected, read);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
        catch (WebSocketException ex)
        {
            _logger.LogWarning(ex, "WebSocket faulted. ConnectionId={ConnectionId}", connectionId);
        }
        finally
        {
            _hub.Unregister(connection);
            _logger.LogInformation(
                "WebSocket closed. ConnectionId={ConnectionId} Trusted={Trusted} ElapsedMs={ElapsedMs}",
                connectionId,
                connection.IsTrusted,
                (_clock.UtcNow - acceptedAt).TotalMilliseconds);
        }
    }

    private static async Task InitiateCloseAsync(
        WebSocketClientConnection connection,
        CancellationTokenSource receiveLifetime)
    {
        try
        {
            await connection.CloseAsync();
            receiveLifetime.CancelAfter(TimeSpan.FromSeconds(2));
        }
        catch (ObjectDisposedException)
        {
        }
    }

    private static bool IsListenerStopped(Exception ex, CancellationToken cancellationToken)
    {
        return cancellationToken.IsCancellationRequested
            && ex is HttpListenerException or ObjectDisposedException or InvalidOperationException;
    }

    private static void StopQuietly(object? state)
    {
        if (state is not HttpListener listener || !listener.IsListening)
        {
            return;
        }

        try
        {
            listener.Stop();
        }
        catch (ObjectDisposedException)
        {
        }
    }
}
