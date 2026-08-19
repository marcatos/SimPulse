import Foundation

enum SessionSource: String, Sendable, Equatable {
    case mock
    case healthKit
}

/// Local session list item. HealthKit stays the workout source of truth; this is a summary for UI.
struct SessionSummary: Sendable, Equatable, Identifiable {
    var id: String
    var startedAt: Date
    var duration: TimeInterval
    var averageHeartRateBpm: Int?
    var maximumHeartRateBpm: Int?
    var activeKilocalories: Double?
    var source: SessionSource
}
