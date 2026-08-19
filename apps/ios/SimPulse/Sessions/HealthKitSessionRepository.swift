import Foundation
import os

#if canImport(HealthKit)
import HealthKit
#endif

/// Lists Sim Racing workouts from HealthKit. Empty when unavailable, denied, or none found.
final class HealthKitSessionRepository: SessionRepository, @unchecked Sendable {
    private static let activityMetadataKey = "com.marcatos.SimPulse.activity"
    private static let activityMetadataValue = "Sim Racing"
    private let log = Logger(subsystem: "com.marcatos.SimPulse", category: "session-store")

    #if canImport(HealthKit)
    private let store: HKHealthStore

    init(store: HKHealthStore = HKHealthStore()) {
        self.store = store
    }
    #else
    init() {}
    #endif

    func sessionDetail(id: String) async throws -> SessionDetail? {
        nil
    }

    func listSessions() async throws -> [SessionSummary] {
        #if canImport(HealthKit)
        guard HKHealthStore.isHealthDataAvailable() else {
            log.info("healthkit unavailable; returning empty session list")
            return []
        }

        let started = ContinuousClock.now
        do {
            let samples = try await fetchWorkouts()
            let summaries = samples.compactMap(Self.mapWorkout).sorted { $0.startedAt > $1.startedAt }
            let elapsed = ContinuousClock.now - started
            let elapsedMs = elapsed.components.seconds * 1000
                + elapsed.components.attoseconds / 1_000_000_000_000_000
            log.info(
                "listed \(summaries.count, privacy: .public) healthkit sessions in \(elapsedMs, privacy: .public) ms"
            )
            return summaries
        } catch {
            log.error("healthkit session query failed: \(error.localizedDescription, privacy: .public)")
            return []
        }
        #else
        log.info("healthkit not imported; returning empty session list")
        return []
        #endif
    }

    #if canImport(HealthKit)
    private func fetchWorkouts() async throws -> [HKWorkout] {
        try await withCheckedThrowingContinuation { continuation in
            let sort = NSSortDescriptor(key: HKSampleSortIdentifierStartDate, ascending: false)
            let query = HKSampleQuery(
                sampleType: HKObjectType.workoutType(),
                predicate: nil,
                limit: HKObjectQueryNoLimit,
                sortDescriptors: [sort]
            ) { _, samples, error in
                if let error {
                    continuation.resume(throwing: error)
                    return
                }
                continuation.resume(returning: (samples as? [HKWorkout]) ?? [])
            }
            store.execute(query)
        }
    }

    private static func mapWorkout(_ workout: HKWorkout) -> SessionSummary? {
        let metadata = workout.metadata ?? [:]
        guard metadata[Self.activityMetadataKey] as? String == Self.activityMetadataValue else {
            return nil
        }

        let energy = workout.totalEnergyBurned?.doubleValue(for: .kilocalorie())
        return SessionSummary(
            id: workout.uuid.uuidString,
            startedAt: workout.startDate,
            duration: workout.duration,
            averageHeartRateBpm: nil,
            maximumHeartRateBpm: nil,
            activeKilocalories: energy,
            source: .healthKit
        )
    }
    #endif
}
