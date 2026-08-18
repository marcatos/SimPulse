using System.Diagnostics;
using System.Globalization;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

using SimPulse.Bridge.Core.Ports;
using SimPulse.Domain;

namespace SimPulse.Bridge.Core.Adapters.Iracing;

internal sealed class IracingLiveSession
{
    private readonly IClock _clock;
    private readonly ILogger _logger;
    private readonly List<RaceEvent> _events = [];
    private readonly List<Lap> _laps = [];
    private SessionId _sessionId;
    private DateTimeOffset _sessionStartUtc;
    private int _cachedUpdate;
    private string _cachedYaml = "";
    private int? _cachedDriverCarIdx;
    private int? _cachedSessionNum;
    private IracingSessionInfo? _info;
    private SimulatorSession? _snapshot;
    private int? _observedLap;
    private int? _observedSessionNum;

    public IracingLiveSession(IClock clock, ILogger? logger = null)
    {
        _clock = clock;
        _logger = logger ?? NullLogger.Instance;
    }

    public IEnumerable<NormalizedSimulatorUpdate> Apply(IracingMemorySnapshot memory)
    {
        Stopwatch started = Stopwatch.StartNew();
        List<RaceEvent> emitted = [];
        if (_snapshot is null)
        {
            BeginSession(memory, emitted);
        }
        else
        {
            RefreshInfoIfNeeded(memory);
        }

        ResetLapsIfSessionChanged(memory);
        AppendLapEvents(memory, emitted);
        RebuildSnapshot();
        _logger.LogDebug(
            "iRacing live apply finished in {ElapsedMs} ms. Events={EventCount} SessionInfoUpdate={SessionInfoUpdate}",
            started.ElapsedMilliseconds,
            emitted.Count,
            memory.SessionInfoUpdate);

        if (emitted.Count == 0)
        {
            yield return ToUpdate(null);
            yield break;
        }

        foreach (RaceEvent ev in emitted)
        {
            yield return ToUpdate(ev);
        }
    }

    public NormalizedSimulatorUpdate? EndIfLive()
    {
        if (_snapshot is null)
        {
            return null;
        }

        TimestampInstant at = TimestampInstant.UtcNow(_clock.UtcNow);
        RaceEvent end = RaceEvent.Create(_sessionId, RaceEventType.SessionEnd, at);
        _events.Add(end);
        SimulatorSession closed = _snapshot with
        {
            EndedAt = OptionalValue<TimestampInstant>.Available(at),
            Events = _events.ToArray()
        };
        NormalizedSimulatorUpdate update = new(
            SimulatorIds.IRacing,
            _sessionId,
            at,
            end,
            null,
            closed);
        _logger.LogInformation("iRacing session ended. SessionId={SessionId}", _sessionId);
        Reset();
        return update;
    }

    private void BeginSession(IracingMemorySnapshot memory, List<RaceEvent> emitted)
    {
        Stopwatch parse = Stopwatch.StartNew();
        _sessionId = SessionId.New();
        TimestampInstant at = TimestampInstant.UtcNow(_clock.UtcNow);
        _sessionStartUtc = at.Value;
        AcceptYaml(memory);
        RaceEvent start = RaceEvent.Create(_sessionId, RaceEventType.SessionStart, at);
        _events.Add(start);
        emitted.Add(start);
        _logger.LogInformation(
            "iRacing session started. SessionId={SessionId} YamlLength={YamlLength} ParseMs={ElapsedMs}",
            _sessionId,
            memory.SessionYaml?.Length ?? 0,
            parse.ElapsedMilliseconds);
    }

    private void RefreshInfoIfNeeded(IracingMemorySnapshot memory)
    {
        int? carIdx = Identity(memory.Telemetry.DriverCarIdx);
        int? sessionNum = Identity(memory.Telemetry.SessionNum);
        bool updateChanged = memory.SessionInfoUpdate != _cachedUpdate;
        bool identityChanged = carIdx != _cachedDriverCarIdx || sessionNum != _cachedSessionNum;
        if (!updateChanged && !identityChanged)
        {
            return;
        }

        Stopwatch parse = Stopwatch.StartNew();
        if (updateChanged)
        {
            AcceptYaml(memory);
            _logger.LogDebug(
                "iRacing session YAML re-parsed in {ElapsedMs} ms. SessionInfoUpdate={SessionInfoUpdate} YamlLength={YamlLength}",
                parse.ElapsedMilliseconds,
                memory.SessionInfoUpdate,
                memory.SessionYaml?.Length ?? 0);
            return;
        }

        _info = IracingSessionInfoParser.Parse(_cachedYaml, carIdx, sessionNum);
        _cachedDriverCarIdx = carIdx;
        _cachedSessionNum = sessionNum;
        _logger.LogDebug(
            "iRacing session identity re-resolved from cached YAML in {ElapsedMs} ms. SessionNum={SessionNum} DriverCarIdx={DriverCarIdx} YamlLength={YamlLength}",
            parse.ElapsedMilliseconds,
            sessionNum,
            carIdx,
            _cachedYaml.Length);
    }

    private void AcceptYaml(IracingMemorySnapshot memory)
    {
        int? carIdx = Identity(memory.Telemetry.DriverCarIdx);
        int? sessionNum = Identity(memory.Telemetry.SessionNum);
        _cachedUpdate = memory.SessionInfoUpdate;
        _cachedYaml = memory.SessionYaml ?? "";
        _cachedDriverCarIdx = carIdx;
        _cachedSessionNum = sessionNum;
        _info = IracingSessionInfoParser.Parse(_cachedYaml, carIdx, sessionNum);
    }

    private static int? Identity(OptionalValue<int> value)
    {
        return value.TryGet(out int resolved) ? resolved : null;
    }

    private void ResetLapsIfSessionChanged(IracingMemorySnapshot memory)
    {
        if (!memory.Telemetry.SessionNum.TryGet(out int sessionNum))
        {
            return;
        }

        if (_observedSessionNum is null)
        {
            _observedSessionNum = sessionNum;
            return;
        }

        if (sessionNum == _observedSessionNum.Value)
        {
            return;
        }

        int previous = _observedSessionNum.Value;
        _observedSessionNum = sessionNum;
        _observedLap = null;
        _laps.Clear();
        _logger.LogInformation(
            "iRacing session num changed; lap tracking reset. SessionId={SessionId} PreviousSessionNum={PreviousSessionNum} SessionNum={SessionNum}",
            _sessionId,
            previous,
            sessionNum);
    }

    private void AppendLapEvents(IracingMemorySnapshot memory, List<RaceEvent> emitted)
    {
        if (!memory.Telemetry.Lap.TryGet(out int lap) || lap < 1)
        {
            return;
        }

        TimestampInstant at = EventTime(memory.Telemetry);
        if (_observedLap is null)
        {
            AddLapStart(lap, at, emitted);
            _observedLap = lap;
            return;
        }

        if (lap <= _observedLap.Value)
        {
            return;
        }

        CompleteLap(_observedLap.Value, at, emitted);
        AddLapStart(lap, at, emitted);
        _observedLap = lap;
    }

    private TimestampInstant EventTime(IracingTelemetryValues telemetry)
    {
        if (telemetry.SessionTime.TryGet(out double sessionTime))
        {
            return new TimestampInstant(
                _sessionStartUtc + TimeSpan.FromSeconds(sessionTime),
                ClockSource.SimulatorSession);
        }

        return TimestampInstant.UtcNow(_clock.UtcNow);
    }

    private void AddLapStart(int lapNumber, TimestampInstant at, List<RaceEvent> emitted)
    {
        RaceEvent ev = LapEvent(RaceEventType.LapStart, lapNumber, at);
        _events.Add(ev);
        emitted.Add(ev);
        _laps.Add(new Lap(
            _sessionId,
            lapNumber,
            at,
            OptionalValue<TimestampInstant>.Unknown(),
            OptionalValue<TimeSpan>.Unavailable(),
            OptionalValue<int>.Unavailable()));
        _logger.LogInformation(
            "iRacing lap event. SessionId={SessionId} Type={EventType} LapNumber={LapNumber}",
            _sessionId,
            ev.Type,
            lapNumber);
    }

    private void CompleteLap(int lapNumber, TimestampInstant at, List<RaceEvent> emitted)
    {
        RaceEvent ev = LapEvent(RaceEventType.LapComplete, lapNumber, at);
        _events.Add(ev);
        emitted.Add(ev);
        for (int i = 0; i < _laps.Count; i++)
        {
            if (_laps[i].LapNumber == lapNumber)
            {
                _laps[i] = _laps[i] with { CompletedAt = OptionalValue<TimestampInstant>.Available(at) };
                break;
            }
        }

        _logger.LogInformation(
            "iRacing lap event. SessionId={SessionId} Type={EventType} LapNumber={LapNumber}",
            _sessionId,
            ev.Type,
            lapNumber);
    }

    private RaceEvent LapEvent(RaceEventType type, int lapNumber, TimestampInstant at)
    {
        return RaceEvent.Create(
            _sessionId,
            type,
            at,
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["lapNumber"] = lapNumber.ToString(CultureInfo.InvariantCulture)
            });
    }

    private void RebuildSnapshot()
    {
        _snapshot = IracingSessionMapper.ToSnapshot(
            _sessionId,
            _info!,
            new TimestampInstant(_sessionStartUtc, ClockSource.Utc),
            OptionalValue<TimestampInstant>.Unknown(),
            _events,
            _laps);
    }

    private NormalizedSimulatorUpdate ToUpdate(RaceEvent? raceEvent)
    {
        return new NormalizedSimulatorUpdate(
            SimulatorIds.IRacing,
            _sessionId,
            TimestampInstant.UtcNow(_clock.UtcNow),
            raceEvent,
            null,
            _snapshot);
    }

    private void Reset()
    {
        _sessionId = default;
        _sessionStartUtc = default;
        _cachedUpdate = 0;
        _cachedYaml = "";
        _cachedDriverCarIdx = null;
        _cachedSessionNum = null;
        _info = null;
        _snapshot = null;
        _observedLap = null;
        _observedSessionNum = null;
        _events.Clear();
        _laps.Clear();
    }
}
