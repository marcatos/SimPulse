using System.Net;
using System.Net.Security;
using System.Net.Sockets;
using System.Net.WebSockets;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

using SimPulse.Bridge.Core.Adapters;
using SimPulse.Bridge.Core.Application;

namespace SimPulse.Bridge.Core.Tests;

public sealed class BridgeTlsPolicyTests
{
    [Theory]
    [InlineData("127.0.0.1")]
    [InlineData("localhost")]
    public void IsLoopbackHost_accepts_supported_loopback_hosts(string host)
    {
        Assert.True(BridgeTlsPolicy.IsLoopbackHost(host));
    }

    [Fact]
    public void EnsureCleartextAllowed_allows_loopback_when_tls_is_disabled()
    {
        BridgeTlsPolicy.EnsureCleartextAllowed("127.0.0.1", tlsEnabled: false);
    }

    [Fact]
    public void EnsureCleartextAllowed_refuses_all_interfaces_when_tls_is_disabled()
    {
        InvalidOperationException error = Assert.Throws<InvalidOperationException>(
            () => BridgeTlsPolicy.EnsureCleartextAllowed("0.0.0.0", tlsEnabled: false));

        Assert.Contains("loopback", error.Message, StringComparison.OrdinalIgnoreCase);
    }
}

public sealed class TlsWebSocketTransportTests
{
    [Fact]
    public async Task Tls_accepts_websocket_with_pinned_fingerprint()
    {
        using TempCertDirectory directory = new();
        FileBridgeCertificateSource certificateSource = new(
            certPath: null,
            certPassword: null,
            directory.Path,
            NullLogger.Instance);
        X509Certificate2 certificate = certificateSource.GetOrCreate();
        int port = GetFreeTcpPort();
        using ILoggerFactory loggerFactory = LoggerFactory.Create(_ => { });
        ClientSessionHub hub = new(loggerFactory.CreateLogger<ClientSessionHub>());
        KestrelWebSocketTransport transport = new(
            "127.0.0.1",
            port,
            certificate,
            certificateSource.Sha256FingerprintHex,
            hub,
            new SystemClock(),
            loggerFactory.CreateLogger<KestrelWebSocketTransport>());

        using CancellationTokenSource cancellation = new(TimeSpan.FromSeconds(15));
        Task run = transport.RunAsync((_, _) => Task.CompletedTask, cancellation.Token);

        using ClientWebSocket client = await ConnectWithRetryAsync(
            new Uri($"wss://127.0.0.1:{port}/ws/"),
            certificateSource.Sha256FingerprintHex,
            cancellation.Token);

        Assert.Equal(WebSocketState.Open, client.State);
        await cancellation.CancelAsync();
        await DrainAsync(run);
    }

    [Fact]
    public async Task Tls_rejects_client_when_pin_mismatches()
    {
        using TempCertDirectory directory = new();
        FileBridgeCertificateSource certificateSource = new(
            certPath: null,
            certPassword: null,
            directory.Path,
            NullLogger.Instance);
        X509Certificate2 certificate = certificateSource.GetOrCreate();
        int port = GetFreeTcpPort();
        using ILoggerFactory loggerFactory = LoggerFactory.Create(_ => { });
        ClientSessionHub hub = new(loggerFactory.CreateLogger<ClientSessionHub>());
        KestrelWebSocketTransport transport = new(
            "127.0.0.1",
            port,
            certificate,
            certificateSource.Sha256FingerprintHex,
            hub,
            new SystemClock(),
            loggerFactory.CreateLogger<KestrelWebSocketTransport>());

        using CancellationTokenSource cancellation = new(TimeSpan.FromSeconds(15));
        Task run = transport.RunAsync((_, _) => Task.CompletedTask, cancellation.Token);
        using ClientWebSocket client = CreatePinnedClient(new string('0', 64));

        await Assert.ThrowsAnyAsync<WebSocketException>(
            () => ConnectAfterListenerStartsAsync(
                client,
                new Uri($"wss://127.0.0.1:{port}/ws/"),
                cancellation.Token));

        await cancellation.CancelAsync();
        await DrainAsync(run);
    }

    private static ClientWebSocket CreatePinnedClient(string expectedFingerprint)
    {
        ClientWebSocket client = new();
        client.Options.RemoteCertificateValidationCallback = (
            _,
            certificate,
            _,
            _) => certificate is not null
                && string.Equals(
                    ComputeFingerprint(certificate),
                    expectedFingerprint,
                    StringComparison.Ordinal);
        return client;
    }

    private static async Task<ClientWebSocket> ConnectWithRetryAsync(
        Uri uri,
        string expectedFingerprint,
        CancellationToken cancellationToken)
    {
        DateTimeOffset deadline = DateTimeOffset.UtcNow.AddSeconds(5);
        Exception? last = null;
        while (DateTimeOffset.UtcNow < deadline)
        {
            ClientWebSocket client = CreatePinnedClient(expectedFingerprint);
            try
            {
                await client.ConnectAsync(uri, cancellationToken);
                return client;
            }
            catch (WebSocketException ex)
            {
                last = ex;
                client.Dispose();
                await Task.Delay(50, cancellationToken);
            }
        }

        throw new TimeoutException($"Could not connect to {uri} within 5s.", last);
    }

    private static async Task ConnectAfterListenerStartsAsync(
        ClientWebSocket client,
        Uri uri,
        CancellationToken cancellationToken)
    {
        await WaitForListenerAsync(uri.Host, uri.Port, cancellationToken);
        await client.ConnectAsync(uri, cancellationToken);
    }

    private static async Task WaitForListenerAsync(
        string host,
        int port,
        CancellationToken cancellationToken)
    {
        DateTimeOffset deadline = DateTimeOffset.UtcNow.AddSeconds(5);
        while (DateTimeOffset.UtcNow < deadline)
        {
            using TcpClient readinessProbe = new();
            try
            {
                await readinessProbe.ConnectAsync(host, port, cancellationToken);
                return;
            }
            catch (SocketException)
            {
                await Task.Delay(50, cancellationToken);
            }
        }

        throw new TimeoutException($"Listener {host}:{port} did not start within 5s.");
    }

    private static int GetFreeTcpPort()
    {
        TcpListener listener = new(IPAddress.Loopback, 0);
        listener.Start();
        int port = ((IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();
        return port;
    }

    private static string ComputeFingerprint(X509Certificate certificate)
    {
        return Convert.ToHexString(SHA256.HashData(certificate.GetRawCertData())).ToLowerInvariant();
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
    }

    private sealed class TempCertDirectory : IDisposable
    {
        public TempCertDirectory()
        {
            Path = System.IO.Path.Combine(
                System.IO.Path.GetTempPath(),
                "simpulse-bridge-tls-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(Path);
        }

        public string Path { get; }

        public void Dispose()
        {
            try
            {
                Directory.Delete(Path, recursive: true);
            }
            catch (IOException)
            {
            }
        }
    }
}
