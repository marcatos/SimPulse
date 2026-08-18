import Foundation

struct HeartRateSample: Sendable, Equatable {
    var timestamp: Date
    var beatsPerMinute: Int
}

struct WorkoutSnapshot: Sendable, Equatable {
    var elapsed: TimeInterval
    var currentHeartRateBpm: Int?
    var averageHeartRateBpm: Int?
    var maximumHeartRateBpm: Int?
    var activeKilocalories: Double?
    var isRunning: Bool
}

/// Boundary that lets domain logic run without HealthKit on Windows or in unit tests.
protocol WorkoutDataSource: Sendable {
    func start() async throws
    func stop() async throws
    var snapshots: AsyncStream<WorkoutSnapshot> { get }
}

final class MockWorkoutDataSource: WorkoutDataSource, @unchecked Sendable {
    private let continuation: AsyncStream<WorkoutSnapshot>.Continuation
    let snapshots: AsyncStream<WorkoutSnapshot>

    init(samples: [WorkoutSnapshot] = []) {
        let stream = AsyncStream<WorkoutSnapshot>.makeStream()
        snapshots = stream.stream
        continuation = stream.continuation
        for sample in samples {
            continuation.yield(sample)
        }
    }

    func start() async throws {}

    func stop() async throws {
        continuation.finish()
    }
}
