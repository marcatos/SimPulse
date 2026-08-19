import XCTest
@testable import SimPulse

final class SessionDetailTests: XCTestCase {
    func testMockDetailReturnsSortedPointsAndMetrics() async throws {
        let repo = MockSessionRepository()
        let detail = try await repo.sessionDetail(id: "mock-1")
        XCTAssertNotNil(detail)
        XCTAssertEqual(detail?.id, "mock-1")
        XCTAssertEqual(detail?.averageHeartRateBpm, 132)
        XCTAssertEqual(detail?.maximumHeartRateBpm, 161)
        XCTAssertFalse(detail!.heartRatePoints.isEmpty)
        let times = detail!.heartRatePoints.map(\.timestamp)
        XCTAssertEqual(times, times.sorted())
    }

    func testMockDetailSyntheticPointsSpanSessionDuration() async throws {
        let repo = MockSessionRepository()
        let detail = try await repo.sessionDetail(id: "mock-1")
        XCTAssertNotNil(detail)
        let points = detail!.heartRatePoints
        XCTAssertFalse(points.isEmpty)
        XCTAssertEqual(points.first!.timestamp, detail!.startedAt)
        let expectedEnd = detail!.startedAt.addingTimeInterval(detail!.duration)
        XCTAssertEqual(points.last!.timestamp, expectedEnd)
    }

    func testMockDetailUnknownIdReturnsNil() async throws {
        let repo = MockSessionRepository()
        let detail = try await repo.sessionDetail(id: "missing")
        XCTAssertNil(detail)
    }

    func testMockDetailEmptySamplesHasNilHeartRateMetrics() async throws {
        let summary = SessionSummary(
            id: "empty-hr",
            startedAt: Date(timeIntervalSince1970: 1_700_100_000),
            duration: 600,
            averageHeartRateBpm: nil,
            maximumHeartRateBpm: nil,
            activeKilocalories: 50,
            source: .mock
        )
        let emptyDetail = SessionDetail(
            id: "empty-hr",
            startedAt: summary.startedAt,
            duration: summary.duration,
            averageHeartRateBpm: nil,
            maximumHeartRateBpm: nil,
            activeKilocalories: summary.activeKilocalories,
            heartRatePoints: [],
            source: .mock
        )
        let repo = MockSessionRepository(sessions: [summary], detailsById: ["empty-hr": emptyDetail])

        let detail = try await repo.sessionDetail(id: "empty-hr")

        XCTAssertNotNil(detail)
        XCTAssertNil(detail?.averageHeartRateBpm)
        XCTAssertNil(detail?.maximumHeartRateBpm)
        XCTAssertTrue(detail!.heartRatePoints.isEmpty)
    }

    func testDetailPresentationFormatsMetricsFromMockDetail() async throws {
        let repo = MockSessionRepository()
        let detail = try await repo.sessionDetail(id: "mock-1")
        XCTAssertNotNil(detail)

        let presentation = SessionDetailPresentation.from(detail!)

        XCTAssertEqual(presentation.titleText, SessionFormatting.formatStart(detail!.startedAt))
        XCTAssertEqual(presentation.durationText, "01:00:15")
        XCTAssertEqual(presentation.averageHeartRateText, "132")
        XCTAssertEqual(presentation.maximumHeartRateText, "161")
        XCTAssertEqual(presentation.caloriesText, "220")
        XCTAssertTrue(presentation.hasHeartRateChart)
    }

    func testDetailPresentationEmptyHeartRateHasNoChart() {
        let detail = SessionDetail(
            id: "empty-hr",
            startedAt: Date(timeIntervalSince1970: 1_700_100_000),
            duration: 600,
            averageHeartRateBpm: nil,
            maximumHeartRateBpm: nil,
            activeKilocalories: 50,
            heartRatePoints: [],
            source: .mock
        )

        let presentation = SessionDetailPresentation.from(detail)

        XCTAssertEqual(presentation.averageHeartRateText, "--")
        XCTAssertEqual(presentation.maximumHeartRateText, "--")
        XCTAssertEqual(presentation.caloriesText, "50")
        XCTAssertFalse(presentation.hasHeartRateChart)
    }

    func testMetricsCalculatorAverageAndMax() {
        let points = [
            HeartRatePoint(timestamp: Date(timeIntervalSince1970: 1), beatsPerMinute: 100),
            HeartRatePoint(timestamp: Date(timeIntervalSince1970: 2), beatsPerMinute: 140),
        ]
        XCTAssertEqual(HeartRateMetricsCalculator.averageBpm(points), 120)
        XCTAssertEqual(HeartRateMetricsCalculator.maximumBpm(points), 140)
        XCTAssertNil(HeartRateMetricsCalculator.averageBpm([]))
        XCTAssertNil(HeartRateMetricsCalculator.maximumBpm([]))
    }
}
