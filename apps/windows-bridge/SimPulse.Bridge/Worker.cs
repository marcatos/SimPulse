using System.Diagnostics;

using Microsoft.Extensions.Logging;

using SimPulse.Bridge.Core.Application;
using SimPulse.Bridge.Core.Ports;

namespace SimPulse.Bridge;

public sealed class Worker : BackgroundService
{
    private readonly BridgeRuntime _runtime;
    private readonly IBridgeTransport _transport;
    private readonly ILogger<Worker> _logger;

    public Worker(BridgeRuntime runtime, IBridgeTransport transport, ILogger<Worker> logger)
    {
        _runtime = runtime;
        _transport = transport;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        Stopwatch started = Stopwatch.StartNew();
        _logger.LogInformation("Bridge host starting. Component={Component}", "Worker");
        try
        {
            await RunRuntimeAndTransportAsync(stoppingToken);
            _logger.LogInformation(
                "Bridge host completed. ElapsedMs={ElapsedMs}",
                started.ElapsedMilliseconds);
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            _logger.LogInformation(
                "Bridge host cancelled. ElapsedMs={ElapsedMs}",
                started.ElapsedMilliseconds);
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Bridge host failed. ElapsedMs={ElapsedMs}",
                started.ElapsedMilliseconds);
            throw;
        }
    }

    private Task RunRuntimeAndTransportAsync(CancellationToken stoppingToken)
    {
        return LinkedSiblingTasks.RunAsync(
            _runtime.RunAsync,
            cancellationToken => _transport.RunAsync(OnConnectedAsync, cancellationToken),
            stoppingToken);
    }

    private Task OnConnectedAsync(IClientConnection connection, CancellationToken cancellationToken)
    {
        _logger.LogInformation(
            "Client connected. Trusted={Trusted} HasDeviceId={HasDeviceId}",
            connection.IsTrusted,
            connection.DeviceId is not null);
        return Task.CompletedTask;
    }
}
