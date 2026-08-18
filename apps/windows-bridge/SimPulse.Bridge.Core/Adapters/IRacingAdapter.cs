using SimPulse.Bridge.Core.Ports;
using SimPulse.Domain;

namespace SimPulse.Bridge.Core.Adapters;

/// <summary>
/// Live IRSDK mmap client is BRIDGE-003. This stub keeps iRacing types out of the domain.
/// </summary>
public sealed class IRacingAdapter : ISimulatorAdapter
{
    public string SimulatorId => SimulatorIds.IRacing;

    public Task<bool> IsAvailableAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(false);
    }

    public async IAsyncEnumerable<NormalizedSimulatorUpdate> SubscribeAsync(
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        await Task.Yield();
        yield break;
    }
}
