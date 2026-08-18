using System.Net;
using System.Net.Sockets;
using System.Net.WebSockets;
using System.Text;

using Microsoft.Extensions.Logging;

using SimPulse.Bridge.Core.Adapters;
using SimPulse.Bridge.Core.Application;
using SimPulse.Bridge.Core.Ports;
using SimPulse.Protocol;

namespace SimPulse.Bridge.Core.Tests;

public sealed class ClientSessionHubTests
{
    [Fact]
    public async Task BroadcastToTrustedAsync_only_delivers_to_trusted_connections()
    {
        using ILoggerFactory factory = LoggerFactory.Create(_ => { });
        ClientSessionHub hub = new(factory.CreateLogger<ClientSessionHub>());
        FakeClientConnection trusted = new() { IsTrusted = true, DeviceId = "trusted-1" };
        FakeClientConnection untrusted = new() { IsTrusted = false, DeviceId = "untrusted-1" };

        hub.Register(trusted);
        hub.Register(untrusted);

        const string payload = "{\"type\":\"simulator.race-event\"}";
        await hub.BroadcastToTrustedAsync(payload, CancellationToken.None);

        Assert.Equal([payload], trusted.Sent);
        Assert.Empty(untrusted.Sent);
    }
}

public sealed class WebSocketTransportTests
{
    [Fact]
    public async Task Accepts_websocket_and_ignores_unknown_type()
    {
        int port = GetFreeTcpPort();
        using ILoggerFactory factory = LoggerFactory.Create(_ => { });
        ClientSessionHub hub = new(factory.CreateLogger<ClientSessionHub>());
        HttpListenerWebSocketTransport transport = new(
            "127.0.0.1",
            port,
            hub,
            new SystemClock(),
            factory.CreateLogger<HttpListenerWebSocketTransport>());

        using CancellationTokenSource cts = new(TimeSpan.FromSeconds(15));
        Task run = transport.RunAsync((_, _) => Task.CompletedTask, cts.Token);

        using ClientWebSocket client = await ConnectWithRetryAsync(
            new Uri($"ws://127.0.0.1:{port}/ws/"),
            cts.Token);

        string unknown = EnvelopeCodec.Serialize("not.a.real.type", new { }, DateTimeOffset.UtcNow);
        await SendTextAsync(client, unknown, cts.Token);

        HelloMessage hello = new("SimPulse", "phone", "test-device");
        string helloJson = EnvelopeCodec.Serialize(MessageTypes.Hello, hello, DateTimeOffset.UtcNow);
        await SendTextAsync(client, helloJson, cts.Token);

        await Task.Delay(200, cts.Token);
        Assert.Equal(WebSocketState.Open, client.State);

        await cts.CancelAsync();
        await DrainAsync(run);
    }

    [Fact]
    public async Task Garbage_hello_payload_does_not_disconnect()
    {
        int port = GetFreeTcpPort();
        using ILoggerFactory factory = LoggerFactory.Create(_ => { });
        ClientSessionHub hub = new(factory.CreateLogger<ClientSessionHub>());
        HttpListenerWebSocketTransport transport = new(
            "127.0.0.1",
            port,
            hub,
            new SystemClock(),
            factory.CreateLogger<HttpListenerWebSocketTransport>());

        using CancellationTokenSource cts = new(TimeSpan.FromSeconds(15));
        Task run = transport.RunAsync((_, _) => Task.CompletedTask, cts.Token);

        using ClientWebSocket client = await ConnectWithRetryAsync(
            new Uri($"ws://127.0.0.1:{port}/ws/"),
            cts.Token);

        const string garbageHello = """
            {"protocolVersion":1,"type":"hello","messageId":"bad","sentAtUtc":"2026-08-18T08:00:00Z","payload":123}
            """;
        await SendTextAsync(client, garbageHello, cts.Token);

        string unknown = EnvelopeCodec.Serialize("not.a.real.type", new { }, DateTimeOffset.UtcNow);
        await SendTextAsync(client, unknown, cts.Token);

        await Task.Delay(200, cts.Token);
        Assert.Equal(WebSocketState.Open, client.State);

        await cts.CancelAsync();
        await DrainAsync(run);
    }

    [Fact]
    public async Task Accepts_second_client_after_first_closes()
    {
        int port = GetFreeTcpPort();
        using ILoggerFactory factory = LoggerFactory.Create(_ => { });
        ClientSessionHub hub = new(factory.CreateLogger<ClientSessionHub>());
        HttpListenerWebSocketTransport transport = new(
            "127.0.0.1",
            port,
            hub,
            new SystemClock(),
            factory.CreateLogger<HttpListenerWebSocketTransport>());

        using CancellationTokenSource cts = new(TimeSpan.FromSeconds(15));
        Task run = transport.RunAsync((_, _) => Task.CompletedTask, cts.Token);
        Uri uri = new($"ws://127.0.0.1:{port}/ws/");

        using ClientWebSocket first = await ConnectWithRetryAsync(uri, cts.Token);
        await first.CloseAsync(WebSocketCloseStatus.NormalClosure, "done", cts.Token);

        using ClientWebSocket second = await ConnectWithRetryAsync(uri, cts.Token);
        Assert.Equal(WebSocketState.Open, second.State);

        await cts.CancelAsync();
        await DrainAsync(run);
    }

    [Fact]
    public async Task Closes_websocket_when_transport_stops()
    {
        int port = GetFreeTcpPort();
        using ILoggerFactory factory = LoggerFactory.Create(_ => { });
        ClientSessionHub hub = new(factory.CreateLogger<ClientSessionHub>());
        HttpListenerWebSocketTransport transport = new(
            "127.0.0.1",
            port,
            hub,
            new SystemClock(),
            factory.CreateLogger<HttpListenerWebSocketTransport>());

        using CancellationTokenSource cts = new(TimeSpan.FromSeconds(15));
        Task run = transport.RunAsync((_, _) => Task.CompletedTask, cts.Token);

        using ClientWebSocket client = await ConnectWithRetryAsync(
            new Uri($"ws://127.0.0.1:{port}/ws/"),
            cts.Token);

        Task<WebSocketReceiveResult> receiveClose = client.ReceiveAsync(new byte[16], CancellationToken.None);
        await cts.CancelAsync();
        WebSocketReceiveResult close = await receiveClose.WaitAsync(TimeSpan.FromSeconds(2));
        Assert.Equal(WebSocketMessageType.Close, close.MessageType);
        await DrainAsync(run);
    }

    private static int GetFreeTcpPort()
    {
        TcpListener listener = new(IPAddress.Loopback, 0);
        listener.Start();
        int port = ((IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();
        return port;
    }

    private static async Task<ClientWebSocket> ConnectWithRetryAsync(Uri uri, CancellationToken cancellationToken)
    {
        DateTimeOffset deadline = DateTimeOffset.UtcNow.AddSeconds(5);
        Exception? last = null;
        while (DateTimeOffset.UtcNow < deadline)
        {
            ClientWebSocket client = new();
            try
            {
                await client.ConnectAsync(uri, cancellationToken);
                return client;
            }
            catch (Exception ex) when (ex is WebSocketException or HttpRequestException)
            {
                last = ex;
                client.Dispose();
                await Task.Delay(50, cancellationToken);
            }
        }

        throw new TimeoutException($"Could not connect to {uri} within 5s.", last);
    }

    private static async Task SendTextAsync(ClientWebSocket client, string json, CancellationToken cancellationToken)
    {
        byte[] bytes = Encoding.UTF8.GetBytes(json);
        await client.SendAsync(bytes, WebSocketMessageType.Text, endOfMessage: true, cancellationToken);
    }

    private static async Task DrainAsync(Task run)
    {
        try
        {
            await run;
        }
        catch (OperationCanceledException)
        {
        }
        catch (HttpListenerException)
        {
        }
        catch (ObjectDisposedException)
        {
        }
    }
}

internal sealed class FakeClientConnection : IClientConnection
{
    public string? DeviceId { get; set; }

    public bool IsTrusted { get; set; }

    public List<string> Sent { get; } = [];

    public Task SendAsync(string json, CancellationToken cancellationToken)
    {
        Sent.Add(json);
        return Task.CompletedTask;
    }
}
