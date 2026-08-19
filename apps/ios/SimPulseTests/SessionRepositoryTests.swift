import XCTest
@testable import SimPulse

final class SessionRepositoryTests: XCTestCase {
    func testMockRepositoryReturnsSessionsNewestFirst() async throws {
        let older = SessionSummary(
            id: "older",
            startedAt: Date(timeIntervalSince1970: 1_700_000_000),
            duration: 600,
            averageHeartRateBpm: 120,
            maximumHeartRateBpm: 140,
            activeKilocalories: 10,
            source: .mock
        )
        let newer = SessionSummary(
            id: "newer",
            startedAt: Date(timeIntervalSince1970: 1_700_100_000),
            duration: 1_800,
            averageHeartRateBpm: 132,
            maximumHeartRateBpm: 161,
            activeKilocalories: 22,
            source: .mock
        )
        let repository = MockSessionRepository(sessions: [older, newer])

        let listed = try await repository.listSessions()

        XCTAssertEqual(listed.map(\.id), ["newer", "older"])
    }

    func testMockRepositoryCanBeEmpty() async throws {
        let repository = MockSessionRepository(sessions: [])

        let listed = try await repository.listSessions()

        XCTAssertTrue(listed.isEmpty)
    }

    func testListRowPresentationFormatsDurationAndOptionalMetrics() {
        let summary = SessionSummary(
            id: "s1",
            startedAt: Date(timeIntervalSince1970: 1_700_100_000),
            duration: 95,
            averageHeartRateBpm: 132,
            maximumHeartRateBpm: nil,
            activeKilocalories: 22.4,
            source: .healthKit
        )

        let row = SessionListRowPresentation.from(summary)

        XCTAssertEqual(row.durationText, "00:01:35")
        XCTAssertEqual(row.averageHeartRateText, "132")
        XCTAssertEqual(row.maximumHeartRateText, "--")
        XCTAssertEqual(row.caloriesText, "22")
        XCTAssertFalse(row.durationText.isEmpty)
    }

    func testListRowPresentationUsesPlaceholdersWhenMetricsMissing() {
        let summary = SessionSummary(
            id: "s2",
            startedAt: Date(timeIntervalSince1970: 1_700_000_000),
            duration: 0,
            averageHeartRateBpm: nil,
            maximumHeartRateBpm: nil,
            activeKilocalories: nil,
            source: .mock
        )

        let row = SessionListRowPresentation.from(summary)

        XCTAssertEqual(row.durationText, "00:00:00")
        XCTAssertEqual(row.averageHeartRateText, "--")
        XCTAssertEqual(row.maximumHeartRateText, "--")
        XCTAssertEqual(row.caloriesText, "--")
    }
}
