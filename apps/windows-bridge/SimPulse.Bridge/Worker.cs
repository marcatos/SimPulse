using Microsoft.Extensions.Logging;

using SimPulse.Bridge.Core.Adapters;
using SimPulse.Bridge.Core.Application;
using SimPulse.Bridge.Core.Ports;

namespace SimPulse.Bridge;

public sealed class Worker : BackgroundService
{
    private readonly BridgeRuntime _runtime;
    private readonly ILogger<Worker> _logger;

    public Worker(BridgeRuntime runtime, ILogger<Worker> logger)
    {
        _runtime = runtime;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        System.Diagnostics.Stopwatch started = System.Diagnostics.Stopwatch.StartNew();
        _logger.LogInformation("Bridge host starting. Component={Component}", "Worker");
        try
        {
            await _runtime.RunAsync(stoppingToken);
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
}
