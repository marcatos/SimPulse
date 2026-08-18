using System.Diagnostics;

using Microsoft.Extensions.Logging;

using SimPulse.Bridge.Core.Ports;
using SimPulse.Domain;

namespace SimPulse.Bridge.Core.Application;

public sealed class BridgeRuntime
{
    private readonly ISimulatorAdapter _adapter;
    private readonly ILogger<BridgeRuntime> _logger;

    public BridgeRuntime(ISimulatorAdapter adapter, ILogger<BridgeRuntime> logger)
    {
        _adapter = adapter;
        _logger = logger;
    }

    public async Task RunAsync(CancellationToken cancellationToken)
    {
        Stopwatch total = Stopwatch.StartNew();
        _logger.LogInformation(
            "Bridge runtime starting. SimulatorId={SimulatorId} Component={Component}",
            _adapter.SimulatorId,
            "BridgeRuntime");

        bool available = await _adapter.IsAvailableAsync(cancellationToken);
        _logger.LogInformation(
            "Simulator availability check completed in {ElapsedMs} ms. Available={Available} SimulatorId={SimulatorId}",
            total.ElapsedMilliseconds,
            available,
            _adapter.SimulatorId);

        if (!available)
        {
            _logger.LogWarning(
                "No simulator source available. Fixture path unset or iRacing adapter not implemented. ElapsedMs={ElapsedMs}",
                total.ElapsedMilliseconds);
            await WaitUntilCancelled(cancellationToken);
            _logger.LogInformation("Bridge runtime idle-stop after {ElapsedMs} ms", total.ElapsedMilliseconds);
            return;
        }

        int updates = 0;
        int events = 0;
        SessionId? sessionId = null;
        Stopwatch stream = Stopwatch.StartNew();

        await foreach (NormalizedSimulatorUpdate update in _adapter.SubscribeAsync(cancellationToken))
        {
            updates++;
            sessionId = update.SessionId;
            if (update.RaceEvent is not null)
            {
                events++;
                _logger.LogInformation(
                    "Race event. SessionId={SessionId} Type={EventType} SimulatorId={SimulatorId}",
                    update.SessionId,
                    update.RaceEvent.Type,
                    update.SimulatorId);
            }
        }

        _logger.LogInformation(
            "Bridge runtime finished. SessionId={SessionId} Updates={Updates} Events={Events} StreamMs={StreamMs} TotalMs={ElapsedMs}",
            sessionId,
            updates,
            events,
            stream.ElapsedMilliseconds,
            total.ElapsedMilliseconds);
    }

    private static async Task WaitUntilCancelled(CancellationToken cancellationToken)
    {
        try
        {
            await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
    }
}
