import Foundation

struct HeartRatePoint: Equatable, Sendable, Identifiable {
    var id: Date { timestamp }
    let timestamp: Date
    let beatsPerMinute: Double
}

struct SessionDetail: Equatable, Sendable, Identifiable {
    let id: String
    let startedAt: Date
    let duration: TimeInterval
    let averageHeartRateBpm: Int?
    let maximumHeartRateBpm: Int?
    let activeKilocalories: Double?
    let heartRatePoints: [HeartRatePoint]
    let source: SessionSource
}

enum HeartRateMetricsCalculator {
    static func averageBpm(_ points: [HeartRatePoint]) -> Int? {
        guard !points.isEmpty else { return nil }
        let sum = points.reduce(0.0) { $0 + $1.beatsPerMinute }
        return Int((sum / Double(points.count)).rounded())
    }

    static func maximumBpm(_ points: [HeartRatePoint]) -> Int? {
        guard !points.isEmpty else { return nil }
        return points.map(\.beatsPerMinute).max().map { Int($0.rounded()) }
    }
}
