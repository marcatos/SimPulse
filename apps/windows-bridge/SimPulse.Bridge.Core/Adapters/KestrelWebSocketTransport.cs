using System.Diagnostics;
using System.Net;
using System.Net.WebSockets;
using System.Security.Cryptography.X509Certificates;

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

using SimPulse.Bridge.Core.Ports;
using SimPulse.Protocol;

namespace SimPulse.Bridge.Core.Adapters;

public sealed class KestrelWebSocketTransport : IBridgeTransport
{
    private readonly string _host;
    private readonly int _port;
    private readonly X509Certificate2 _certificate;
    private readonly string _fingerprintHex;
    private readonly IClientSessionHub _hub;
    private readonly IClock _clock;
    private readonly ILogger<KestrelWebSocketTransport> _logger;
    private readonly WebSocketMessagePump _pump;
    private readonly Action<IClientConnection> _onDisconnected;

    public KestrelWebSocketTransport(
        string host,
        int port,
        X509Certificate2 certificate,
        string fingerprintHex,
        IClientSessionHub hub,
        IClock clock,
        ILogger<KestrelWebSocketTransport> logger,
        Func<IClientConnection, MessageEnvelope, CancellationToken, Task>? onMessage = null,
        Action<IClientConnection>? onDisconnected = null)
    {
        _host = host;
        _port = port;
        _certificate = certificate;
        _fingerprintHex = fingerprintHex;
        _hub = hub;
        _clock = clock;
        _logger = logger;
        _pump = new WebSocketMessagePump(logger, onMessage ?? ((_, _, _) => Task.CompletedTask));
        _onDisconnected = onDisconnected ?? (_ => { });
    }

    public async Task RunAsync(
        Func<IClientConnection, CancellationToken, Task> onConnected,
        CancellationToken cancellationToken)
    {
        Stopwatch total = Stopwatch.StartNew();
        using IHost host = BuildHost(onConnected, cancellationToken);
        try
        {
            await host.StartAsync(cancellationToken);
            _logger.LogInformation(
                "WebSocket transport listening. TlsEnabled={TlsEnabled} TlsCertSha256={TlsCertSha256} Host={Host} Port={Port} Component={Component}",
                true,
                _fingerprintHex,
                _host,
                _port,
                nameof(KestrelWebSocketTransport));
            await host.WaitForShutdownAsync(cancellationToken);
        }
        finally
        {
            await StopQuietlyAsync(host);
            _logger.LogInformation(
                "WebSocket transport stopped. TlsEnabled={TlsEnabled} Host={Host} Port={Port} ElapsedMs={ElapsedMs}",
                true,
                _host,
                _port,
                total.ElapsedMilliseconds);
        }
    }

    private IHost BuildHost(
        Func<IClientConnection, CancellationToken, Task> onConnected,
        CancellationToken transportCancellation)
    {
        IPAddress address = ResolveAddress(_host);
        return new HostBuilder()
            .ConfigureWebHost(webBuilder =>
            {
                webBuilder.UseKestrel(options => ConfigureEndpoint(options, address));
                webBuilder.Configure(app =>
                {
                    app.UseWebSockets();
                    app.Run(context => HandleRequestAsync(context, onConnected, transportCancellation));
                });
            })
            .Build();
    }

    private void ConfigureEndpoint(KestrelServerOptions options, IPAddress address)
    {
        options.Listen(address, _port, listen => listen.UseHttps(_certificate));
    }

    private async Task HandleRequestAsync(
        HttpContext context,
        Func<IClientConnection, CancellationToken, Task> onConnected,
        CancellationToken cancellationToken)
    {
        if (!context.Request.Path.Equals("/ws", StringComparison.OrdinalIgnoreCase)
            && !context.Request.Path.Equals("/ws/", StringComparison.OrdinalIgnoreCase))
        {
            context.Response.StatusCode = StatusCodes.Status404NotFound;
            return;
        }

        if (!context.WebSockets.IsWebSocketRequest)
        {
            context.Response.StatusCode = StatusCodes.Status400BadRequest;
            return;
        }

        try
        {
            WebSocket socket = await context.WebSockets.AcceptWebSocketAsync();
            await HandleSocketAsync(socket, onConnected, cancellationToken);
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
            _onDisconnected(connection);
            _logger.LogInformation(
                "WebSocket closed. ConnectionId={ConnectionId} Trusted={Trusted} ElapsedMs={ElapsedMs}",
                connectionId,
                connection.IsTrusted,
                (_clock.UtcNow - acceptedAt).TotalMilliseconds);
        }
    }

    private static IPAddress ResolveAddress(string host)
    {
        if (host is "0.0.0.0" or "*" or "+")
        {
            return IPAddress.Any;
        }

        if (string.Equals(host, "localhost", StringComparison.OrdinalIgnoreCase))
        {
            return IPAddress.Loopback;
        }

        if (IPAddress.TryParse(host, out IPAddress? address))
        {
            return address;
        }

        throw new ArgumentException("Kestrel bind host must be an IP address or localhost.", nameof(host));
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

    private static async Task StopQuietlyAsync(IHost host)
    {
        try
        {
            await host.StopAsync(TimeSpan.FromSeconds(2));
        }
        catch (Exception ex) when (ex is OperationCanceledException or ObjectDisposedException)
        {
        }
    }
}
