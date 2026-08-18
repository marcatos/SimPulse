import Foundation

/// Capability names match C# `SimPulse.Domain.Entitlements.CapabilityGate`.
enum ProductTier: Int, Sendable {
    case free = 0
    case premium = 1
    case pro = 2
}

enum CapabilityGate {
    static let freeHistoryLimit = 5

    static func unlimitedHistory(_ tier: ProductTier) -> Bool { tier.rawValue >= ProductTier.premium.rawValue }
    static func csvExport(_ tier: ProductTier) -> Bool { tier.rawValue >= ProductTier.premium.rawValue }
    static func bridgeSync(_ tier: ProductTier) -> Bool { tier.rawValue >= ProductTier.pro.rawValue }
}
