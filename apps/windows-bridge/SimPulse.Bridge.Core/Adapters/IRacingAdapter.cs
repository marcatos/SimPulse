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
        int updates = 0;
        try
        {
            await foreach (NormalizedSimulatorUpdate update in ReadLoopAsync(total, cancellationToken))
            {
                updates++;
                yield return update;
            }
        }
        finally
        {
            _memory.Close();
            _logger.LogInformation(
                "iRacing subscribe ended. Updates={Updates} ElapsedMs={ElapsedMs}",
                updates,
                total.ElapsedMilliseconds);
        }
    }

    private async IAsyncEnumerable<NormalizedSimulatorUpdate> ReadLoopAsync(
        Stopwatch total,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        IracingLiveSession live = new(_clock, _logger);
        while (!cancellationToken.IsCancellationRequested)
        {
            if (!EnsureOpen())
            {
                if (!await IdleAsync(cancellationToken))
                {
                    yield break;
                }

                continue;
            }

            bool signaled = WaitForData(cancellationToken);
            if (cancellationToken.IsCancellationRequested)
            {
                yield break;
            }

            if (!_memory.TryReadSnapshot(out IracingMemorySnapshot memory) || !memory.Connected)
            {
                if (live.EndIfLive() is { } ended)
                {
                    yield return ended;
                    _memory.Close();
                    _logger.LogInformation(
                        "iRacing subscribe waiting to reconnect. ElapsedMs={ElapsedMs}",
                        total.ElapsedMilliseconds);
                }

                if (!await IdleAsync(cancellationToken))
                {
                    yield break;
                }

                continue;
            }

            if (string.IsNullOrWhiteSpace(memory.SessionYaml))
            {
                if (!signaled && !await IdleAsync(cancellationToken))
                {
                    yield break;
                }

                continue;
            }

            foreach (NormalizedSimulatorUpdate update in live.Apply(memory))
            {
                yield return update;
            }

            if (!signaled && !await IdleAsync(cancellationToken))
            {
                yield break;
            }
        }
    }

    private bool WaitForData(CancellationToken cancellationToken)
    {
        try
        {
            return _memory.WaitForUpdate(_pollInterval, cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return false;
        }
    }

    private bool EnsureOpen()
    {
        return _memory.TryOpen();
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
}
