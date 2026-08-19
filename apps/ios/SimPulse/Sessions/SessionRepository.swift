import Foundation

/// Port for listing Sim Racing sessions without coupling UI to HealthKit.
protocol SessionRepository: Sendable {
    func listSessions() async throws -> [SessionSummary]
    func sessionDetail(id: String) async throws -> SessionDetail?
}

final class MockSessionRepository: SessionRepository, @unchecked Sendable {
    private let sessions: [SessionSummary]
    private let detailsById: [String: SessionDetail]?

    init(
        sessions: [SessionSummary] = MockSessionRepository.sampleSessions,
        detailsById: [String: SessionDetail]? = nil
    ) {
        self.sessions = sessions
        self.detailsById = detailsById
    }

    func listSessions() async throws -> [SessionSummary] {
        sessions.sorted { $0.startedAt > $1.startedAt }
    }

    func sessionDetail(id: String) async throws -> SessionDetail? {
        if let detailsById, let detail = detailsById[id] {
            return detail
        }
        guard let summary = sessions.first(where: { $0.id == id }) else {
            return nil
        }
        let points = Self.syntheticHeartRatePoints(for: summary)
        return SessionDetail(
            id: summary.id,
            startedAt: summary.startedAt,
            duration: summary.duration,
            averageHeartRateBpm: HeartRateMetricsCalculator.averageBpm(points),
            maximumHeartRateBpm: HeartRateMetricsCalculator.maximumBpm(points),
            activeKilocalories: summary.activeKilocalories,
            heartRatePoints: points,
            source: summary.source
        )
    }

    private static func syntheticHeartRatePoints(for summary: SessionSummary) -> [HeartRatePoint] {
        let bpms: [Double]
        switch summary.id {
        case "mock-1":
            bpms = [120, 125, 125, 128, 161]
        case "mock-2":
            bpms = [110, 112, 111, 112, 145]
        default:
            bpms = [100, 110, 120, 130, 140]
        }

        let count = bpms.count
        guard count > 1 else {
            return bpms.map {
                HeartRatePoint(timestamp: summary.startedAt, beatsPerMinute: $0)
            }
        }

        let step = summary.duration / Double(count - 1)
        return bpms.enumerated().map { index, bpm in
            HeartRatePoint(
                timestamp: summary.startedAt.addingTimeInterval(step * Double(index)),
                beatsPerMinute: bpm
            )
        }
    }

    static let sampleSessions: [SessionSummary] = [
        SessionSummary(
            id: "mock-1",
            startedAt: Date(timeIntervalSince1970: 1_700_100_000),
            duration: 3_615,
            averageHeartRateBpm: 132,
            maximumHeartRateBpm: 161,
            activeKilocalories: 220,
            source: .mock
        ),
        SessionSummary(
            id: "mock-2",
            startedAt: Date(timeIntervalSince1970: 1_700_000_000),
            duration: 1_800,
            averageHeartRateBpm: 118,
            maximumHeartRateBpm: 145,
            activeKilocalories: 95,
            source: .mock
        )
    ]
}
