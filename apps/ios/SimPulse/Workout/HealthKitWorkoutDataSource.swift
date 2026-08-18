#if canImport(HealthKit)
import HealthKit

/// HealthKit adapter. Not compiled or tested on Windows (ADR 0009).
final class HealthKitWorkoutDataSource: WorkoutDataSource, @unchecked Sendable {
    func start() async throws {
        throw WorkoutSourceError.notAvailableOnThisPlatform
    }

    func stop() async throws {
        throw WorkoutSourceError.notAvailableOnThisPlatform
    }

    var snapshots: AsyncStream<WorkoutSnapshot> {
        AsyncStream { $0.finish() }
    }
}
#endif
