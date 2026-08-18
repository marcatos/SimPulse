namespace SimPulse.Bridge.Core.Ports;

public interface IClientConnection
{
    string? DeviceId { get; set; }

    bool IsTrusted { get; set; }

    Task SendAsync(string json, CancellationToken cancellationToken);
}

public interface IClientSessionHub
{
    void Register(IClientConnection connection);

    void Unregister(IClientConnection connection);

    Task BroadcastToTrustedAsync(string json, CancellationToken cancellationToken);
}

public interface IBridgeTransport
{
    Task RunAsync(
        Func<IClientConnection, CancellationToken, Task> onConnected,
        CancellationToken cancellationToken);
}
