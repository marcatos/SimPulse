using System.Collections.Concurrent;
using System.Diagnostics;

using Microsoft.Extensions.Logging;

using SimPulse.Bridge.Core.Ports;

namespace SimPulse.Bridge.Core.Application;

public sealed class ClientSessionHub : IClientSessionHub
{
    private readonly ConcurrentDictionary<IClientConnection, byte> _connections = new();
    private readonly ILogger<ClientSessionHub> _logger;

    public ClientSessionHub(ILogger<ClientSessionHub> logger)
    {
        _logger = logger;
    }

    public void Register(IClientConnection connection)
    {
        _connections[connection] = 0;
        _logger.LogInformation(
            "Client registered. Count={Count} Trusted={Trusted}",
            _connections.Count,
            connection.IsTrusted);
    }

    public void Unregister(IClientConnection connection)
    {
        _connections.TryRemove(connection, out _);
        _logger.LogInformation("Client unregistered. Count={Count}", _connections.Count);
    }

    public async Task BroadcastToTrustedAsync(string json, CancellationToken cancellationToken)
    {
        Stopwatch started = Stopwatch.StartNew();
        List<IClientConnection> trusted = _connections.Keys.Where(static c => c.IsTrusted).ToList();
        _logger.LogInformation(
            "Broadcast to trusted starting. Recipients={Recipients} Component={Component}",
            trusted.Count,
            "ClientSessionHub");

        int sent = 0;
        int failed = 0;
        foreach (IClientConnection connection in trusted)
        {
            if (await TrySendAsync(connection, json, cancellationToken))
            {
                sent++;
            }
            else
            {
                failed++;
            }
        }

        _logger.LogInformation(
            "Broadcast to trusted finished. Recipients={Recipients} Sent={Sent} Failed={Failed} ElapsedMs={ElapsedMs}",
            trusted.Count,
            sent,
            failed,
            started.ElapsedMilliseconds);
    }

    private async Task<bool> TrySendAsync(
        IClientConnection connection,
        string json,
        CancellationToken cancellationToken)
    {
        try
        {
            await connection.SendAsync(json, cancellationToken);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Broadcast send failed. Trusted={Trusted}", connection.IsTrusted);
            return false;
        }
    }
}
