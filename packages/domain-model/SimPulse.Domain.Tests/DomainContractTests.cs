namespace SimPulse.Domain.Tests;

public sealed class OptionalValueTests
{
    [Fact]
    public void Unavailable_int_does_not_expose_zero_as_a_measurement()
    {
        OptionalValue<int> missing = OptionalValue<int>.Unavailable();

        Assert.Equal(DataPresence.Unavailable, missing.Presence);
        Assert.False(missing.TryGet(out _));
    }

    [Fact]
    public void Available_returns_the_value()
    {
        OptionalValue<int> hr = OptionalValue<int>.Available(142);

        Assert.True(hr.TryGet(out int bpm));
        Assert.Equal(142, bpm);
    }
}

public sealed class SessionIdTests
{
    [Fact]
    public void New_ids_are_unique()
    {
        SessionId a = SessionId.New();
        SessionId b = SessionId.New();

        Assert.NotEqual(a, b);
    }
}

public sealed class CapabilityGateTests
{
    [Fact]
    public void Free_does_not_include_bridge_or_unlimited_history()
    {
        Assert.False(Entitlements.CapabilityGate.UnlimitedHistory(Entitlements.ProductTier.Free));
        Assert.False(Entitlements.CapabilityGate.BridgeSync(Entitlements.ProductTier.Free));
    }

    [Fact]
    public void Premium_unlocks_history_not_bridge()
    {
        Assert.True(Entitlements.CapabilityGate.UnlimitedHistory(Entitlements.ProductTier.Premium));
        Assert.False(Entitlements.CapabilityGate.BridgeSync(Entitlements.ProductTier.Premium));
    }

    [Fact]
    public void Pro_unlocks_bridge()
    {
        Assert.True(Entitlements.CapabilityGate.BridgeSync(Entitlements.ProductTier.Pro));
        Assert.True(Entitlements.CapabilityGate.CsvExport(Entitlements.ProductTier.Pro));
    }
}
