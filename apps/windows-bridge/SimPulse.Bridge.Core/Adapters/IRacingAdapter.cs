using System.Diagnostics;
using System.Runtime.CompilerServices;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

using SimPulse.Bridge.Core.Adapters.Iracing;
using SimPulse.Bridge.Core.Ports;
using SimPulse.Domain;

namespace SimPulse.Bridge.Core.Adapters;

public sealed class IRacingAdapter : ISimulatorAdapter
{
    private static readonly TimeSpan DefaultPollInterval = TimeSpan.FromMilliseconds(100);

    private readonly IIracingSharedMemory _memory;
    private readonly IClock _clock;
    private readonly ILogger<IRacingAdapter> _logger;
    private readonly TimeSpan _pollInterval;

    public IRacingAdapter(
        IIracingSharedMemory memory,
        IClock clock,
        ILogger<IRacingAdapter>? logger = null,
        TimeSpan? pollInterval = null)
    {
        _memory = memory;
        _clock = clock;
        _logger = logger ?? NullLogger<IRacingAdapter>.Instance;
        _pollInterval = pollInterval ?? DefaultPollInterval;
    }

    public string SimulatorId => SimulatorIds.IRacing;

    public Task<bool> IsAvailableAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        Stopwatch started = Stopwatch.StartNew();
        bool opened = _memory.TryOpen();
        _logger.LogInformation(
            "iRacing availability check completed in {ElapsedMs} ms. Available={Available} Component={Component}",
            started.ElapsedMilliseconds,
            opened,
            nameof(IRacingAdapter));
        return Task.FromResult(opened);
    }

    public async IAsyncEnumerable<NormalizedSimulatorUpdate> SubscribeAsync(
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        Stopwatch total = Stopwatch.StartNew();
        _logger.LogInformation("iRacing subscribe starting. Component={Component}", nameof(IRacingAdapter));
        if (!_memory.TryOpen())
        {
            _logger.LogInformation(
                "iRacing subscribe ended; mmap unavailable. ElapsedMs={ElapsedMs}",
                total.ElapsedMilliseconds);
            yield break;
        }

        StreamState state = new();
        try
        {
            await foreach (NormalizedSimulatorUpdate update in ReadLoopAsync(state, total, cancellationToken))
            {
                yield return update;
            }
        }
        finally
        {
            _memory.Close();
            _logger.LogInformation(
                "iRacing subscribe ended. Updates={Updates} ElapsedMs={ElapsedMs}",
                state.Updates,
                total.ElapsedMilliseconds);
        }
    }

    private async IAsyncEnumerable<NormalizedSimulatorUpdate> ReadLoopAsync(
        StreamState state,
        Stopwatch total,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            if (!_memory.TryReadSnapshot(out IracingMemorySnapshot memory) || !memory.Connected)
            {
                if (state.Snapshot is { } live)
                {
                    state.Updates++;
                    yield return CreateSessionEnd(state.SessionId, live, state.Events);
                    yield break;
                }

                if (!await IdleAsync(cancellationToken))
                {
                    yield break;
                }

                continue;
            }

            if (string.IsNullOrWhiteSpace(memory.SessionYaml))
            {
                if (!await IdleAsync(cancellationToken))
                {
                    yield break;
                }

                continue;
            }

            state.Updates++;
            yield return ApplyYaml(memory.SessionYaml, state, total);

            if (!await IdleAsync(cancellationToken))
            {
                yield break;
            }
        }
    }

    private NormalizedSimulatorUpdate ApplyYaml(string yaml, StreamState state, Stopwatch total)
    {
        Stopwatch parse = Stopwatch.StartNew();
        IracingSessionInfo info = IracingSessionInfoParser.Parse(yaml);
        TimestampInstant at = new(_clock.UtcNow, ClockSource.SimulatorSession);
        _logger.LogDebug(
            "iRacing session YAML parsed in {ElapsedMs} ms. YamlLength={YamlLength}",
            parse.ElapsedMilliseconds,
            yaml.Length);

        if (state.Snapshot is null)
        {
            state.SessionId = SessionId.New();
            RaceEvent start = RaceEvent.Create(state.SessionId, RaceEventType.SessionStart, at);
            state.Events.Add(start);
            state.Snapshot = IracingSessionMapper.ToSnapshot(
                state.SessionId,
                info,
                at,
                OptionalValue<TimestampInstant>.Unknown(),
                state.Events);
            _logger.LogInformation(
                "iRacing session started. SessionId={SessionId} YamlLength={YamlLength} ElapsedMs={ElapsedMs}",
                state.SessionId,
                yaml.Length,
                total.ElapsedMilliseconds);
            return new NormalizedSimulatorUpdate(SimulatorId, state.SessionId, at, start, null, state.Snapshot);
        }

        state.Snapshot = IracingSessionMapper.ToSnapshot(
            state.SessionId,
            info,
            state.Snapshot.StartedAt,
            OptionalValue<TimestampInstant>.Unknown(),
            state.Events);
        return new NormalizedSimulatorUpdate(SimulatorId, state.SessionId, at, null, null, state.Snapshot);
    }

    private NormalizedSimulatorUpdate CreateSessionEnd(
        SessionId sessionId,
        SimulatorSession snapshot,
        List<RaceEvent> events)
    {
        TimestampInstant at = new(_clock.UtcNow, ClockSource.SimulatorSession);
        RaceEvent end = RaceEvent.Create(sessionId, RaceEventType.SessionEnd, at);
        events.Add(end);
        SimulatorSession closed = snapshot with
        {
            EndedAt = OptionalValue<TimestampInstant>.Available(at),
            Events = events.ToArray()
        };
        _logger.LogInformation("iRacing session ended. SessionId={SessionId}", sessionId);
        return new NormalizedSimulatorUpdate(SimulatorId, sessionId, at, end, null, closed);
    }

    private async Task<bool> IdleAsync(CancellationToken cancellationToken)
    {
        try
        {
            if (_pollInterval <= TimeSpan.Zero)
            {
                await Task.Yield();
            }
            else
            {
                await Task.Delay(_pollInterval, cancellationToken);
            }

            return !cancellationToken.IsCancellationRequested;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return false;
        }
    }

    private sealed class StreamState
    {
        public int Updates { get; set; }

        public SessionId SessionId { get; set; }

        public SimulatorSession? Snapshot { get; set; }

        public List<RaceEvent> Events { get; } = [];
    }
}
