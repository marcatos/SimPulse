import XCTest
@testable import SimPulse

final class RecordingWorkoutDataSource: WorkoutDataSource, @unchecked Sendable {
    private(set) var startCount = 0
    private(set) var stopCount = 0
    private let continuation: AsyncStream<WorkoutSnapshot>.Continuation
    let snapshots: AsyncStream<WorkoutSnapshot>

    init() {
        let stream = AsyncStream<WorkoutSnapshot>.makeStream()
        snapshots = stream.stream
        continuation = stream.continuation
    }

    func start() async throws {
        startCount += 1
        continuation.yield(
            WorkoutSnapshot(
                elapsed: 0,
                currentHeartRateBpm: nil,
                averageHeartRateBpm: nil,
                maximumHeartRateBpm: nil,
                activeKilocalories: nil,
                isRunning: true
            )
        )
    }

    func stop() async throws {
        stopCount += 1
        continuation.finish()
    }
}

final class WorkoutSessionControllerTests: XCTestCase {
    func testStartBeginsTheDataSourceSession() async throws {
        let source = RecordingWorkoutDataSource()
        let controller = WorkoutSessionController(source: source)

        try await controller.startSimRacing()

        XCTAssertEqual(source.startCount, 1)
        XCTAssertEqual(source.stopCount, 0)
    }

    func testEndStopsTheDataSourceSession() async throws {
        let source = RecordingWorkoutDataSource()
        let controller = WorkoutSessionController(source: source)

        try await controller.startSimRacing()
        try await controller.endSimRacing()

        XCTAssertEqual(source.startCount, 1)
        XCTAssertEqual(source.stopCount, 1)
    }

    func testCompanionUnreachableDoesNotEndTheSession() async throws {
        let source = RecordingWorkoutDataSource()
        let controller = WorkoutSessionController(source: source)

        try await controller.startSimRacing()
        controller.companionReachabilityDidChange(false)

        XCTAssertEqual(source.startCount, 1)
        XCTAssertEqual(source.stopCount, 0)
    }

    func testStartWhileRunningIsRejected() async throws {
        let source = RecordingWorkoutDataSource()
        let controller = WorkoutSessionController(source: source)

        try await controller.startSimRacing()

        do {
            try await controller.startSimRacing()
            XCTFail("expected already running")
        } catch WorkoutSessionError.alreadyRunning {
            XCTAssertEqual(source.startCount, 1)
        }
    }
}
