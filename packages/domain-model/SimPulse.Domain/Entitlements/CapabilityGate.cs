namespace SimPulse.Domain.Entitlements;

public enum ProductTier
{
    Free = 0,
    Premium = 1,
    Pro = 2
}

/// <summary>
/// Capability questions for UI and services. StoreKit maps onto ProductTier later.
/// </summary>
public static class CapabilityGate
{
    public const int FreeHistoryLimit = 5;

    public static bool UnlimitedHistory(ProductTier tier) => tier >= ProductTier.Premium;

    public static bool AdvancedCharts(ProductTier tier) => tier >= ProductTier.Premium;

    public static bool SessionComparison(ProductTier tier) => tier >= ProductTier.Premium;

    public static bool ManualSimulatorMetadata(ProductTier tier) => tier >= ProductTier.Premium;

    public static bool CsvExport(ProductTier tier) => tier >= ProductTier.Premium;

    public static bool BridgeSync(ProductTier tier) => tier >= ProductTier.Pro;

    public static bool AutomaticSimulatorDetection(ProductTier tier) => tier >= ProductTier.Pro;

    public static bool PerformanceUnderLoad(ProductTier tier) => tier >= ProductTier.Pro;

    public static bool ShareableSessionCards(ProductTier tier) => tier >= ProductTier.Pro;
}
