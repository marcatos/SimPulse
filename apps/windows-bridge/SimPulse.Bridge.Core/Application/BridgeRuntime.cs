using System.Diagnostics;

using Microsoft.Extensions.Logging;

using SimPulse.Bridge.Core.Ports;
using SimPulse.Domain;
using SimPulse.Protocol;

namespace SimPulse.Bridge.Core.Application;

public sealed class BridgeRuntime
{
    private readonly ISimulatorAdapter _adapter;
    private readonly ILogger<BridgeRuntime> _logger;
    private readonly IClientSessionHub? _hub;
    private readonly IClock? _clock;
    private readonly SessionLifecycleTracker _tracker = new();

    public BridgeRuntime(
        ISimulatorAdapter adapter,
        ILogger<BridgeRuntime> logger,
        IClientSessionHub? hub = null,
        IClock? clock = null)
    {
        _adapter = adapter;
        _logger = logger;
        _hub = hub;
        _clock = clock;
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
                "No simulator source available yet. Subscribing so the adapter can detect it later. ElapsedMs={ElapsedMs}",
                total.ElapsedMilliseconds);
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
                RaceEvent? observed = _tracker.Observe(update.RaceEvent);
                if (observed is null)
                {
                    _logger.LogDebug(
                        "Duplicate race event ignored. SessionId={SessionId} Type={EventType}",
                        update.SessionId,
                        update.RaceEvent.Type);
                    continue;
                }

                events++;
                _logger.LogInformation(
                    "Race event. SessionId={SessionId} Type={EventType} SimulatorId={SimulatorId}",
                    update.SessionId,
                    observed.Type,
                    update.SimulatorId);
                await BroadcastRaceEventAsync(observed, cancellationToken);
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

    private async Task BroadcastRaceEventAsync(RaceEvent raceEvent, CancellationToken cancellationToken)
    {
        if (_hub is null)
        {
            return;
        }

        Stopwatch started = Stopwatch.StartNew();
        RaceEventMessage payload = new(
            raceEvent.SimulatorSessionId.ToString(),
            raceEvent.Type.ToString(),
            raceEvent.Timestamp.Value,
            raceEvent.Attributes.Count == 0 ? null : raceEvent.Attributes);
        DateTimeOffset sentAt = _clock?.UtcNow ?? DateTimeOffset.UtcNow;
        string json = EnvelopeCodec.Serialize(MessageTypes.RaceEvent, payload, sentAt);
        await _hub.BroadcastToTrustedAsync(json, cancellationToken);
        _logger.LogDebug(
            "Race event broadcast finished. SessionId={SessionId} Type={EventType} ElapsedMs={ElapsedMs}",
            raceEvent.SimulatorSessionId,
            raceEvent.Type,
            started.ElapsedMilliseconds);
    }
}
