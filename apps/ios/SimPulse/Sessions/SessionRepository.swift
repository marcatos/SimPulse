import Foundation

/// Port for listing Sim Racing sessions without coupling UI to HealthKit.
protocol SessionRepository: Sendable {
    func listSessions() async throws -> [SessionSummary]
}

final class MockSessionRepository: SessionRepository, @unchecked Sendable {
    private let sessions: [SessionSummary]

    init(sessions: [SessionSummary] = MockSessionRepository.sampleSessions) {
        self.sessions = sessions
    }

    func listSessions() async throws -> [SessionSummary] {
        sessions.sorted { $0.startedAt > $1.startedAt }
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
