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
        #if canImport(HealthKit)
        guard HKHealthStore.isHealthDataAvailable() else {
            log.info("healthkit unavailable; session detail unavailable")
            return nil
        }
        guard let uuid = UUID(uuidString: id) else {
            return nil
        }

        let started = ContinuousClock.now
        do {
            guard let workout = try await fetchWorkout(uuid: uuid) else {
                return nil
            }
            guard Self.isSimRacing(workout) else {
                return nil
            }

            let samples: [HKQuantitySample]
            do {
                samples = try await fetchHeartRateSamples(from: workout.startDate, to: workout.endDate)
            } catch {
                log.error("healthkit hr query failed: \(error.localizedDescription, privacy: .public)")
                return nil
            }

            let points = Self.mapHeartRateSamples(samples)
            log.info("listed \(points.count, privacy: .public) hr samples for session")

            let energy = workout.totalEnergyBurned?.doubleValue(for: .kilocalorie())
            let detail = SessionDetail(
                id: workout.uuid.uuidString,
                startedAt: workout.startDate,
                duration: workout.duration,
                averageHeartRateBpm: HeartRateMetricsCalculator.averageBpm(points),
                maximumHeartRateBpm: HeartRateMetricsCalculator.maximumBpm(points),
                activeKilocalories: energy,
                heartRatePoints: points,
                source: .healthKit
            )

            let elapsed = ContinuousClock.now - started
            let elapsedMs = elapsed.components.seconds * 1000
                + elapsed.components.attoseconds / 1_000_000_000_000_000
            log.info(
                "loaded session detail in \(elapsedMs, privacy: .public) ms"
            )
            return detail
        } catch {
            log.error("healthkit session detail failed: \(error.localizedDescription, privacy: .public)")
            return nil
        }
        #else
        log.info("healthkit not imported; session detail unavailable")
        return nil
        #endif
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
    private func fetchWorkout(uuid: UUID) async throws -> HKWorkout? {
        try await withCheckedThrowingContinuation { continuation in
            let predicate = HKQuery.predicateForObject(with: uuid)
            let query = HKSampleQuery(
                sampleType: HKObjectType.workoutType(),
                predicate: predicate,
                limit: 1,
                sortDescriptors: nil
            ) { _, samples, error in
                if let error {
                    continuation.resume(throwing: error)
                    return
                }
                continuation.resume(returning: (samples as? [HKWorkout])?.first)
            }
            store.execute(query)
        }
    }

    private func fetchHeartRateSamples(from start: Date, to end: Date) async throws -> [HKQuantitySample] {
        try await withCheckedThrowingContinuation { continuation in
            let heartRateType = HKQuantityType(.heartRate)
            let predicate = HKQuery.predicateForSamples(
                withStart: start,
                end: end,
                options: [.strictStartDate, .strictEndDate]
            )
            let sort = NSSortDescriptor(key: HKSampleSortIdentifierStartDate, ascending: true)
            let query = HKSampleQuery(
                sampleType: heartRateType,
                predicate: predicate,
                limit: HKObjectQueryNoLimit,
                sortDescriptors: [sort]
            ) { _, samples, error in
                if let error {
                    continuation.resume(throwing: error)
                    return
                }
                continuation.resume(returning: (samples as? [HKQuantitySample]) ?? [])
            }
            store.execute(query)
        }
    }

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

    private static func isSimRacing(_ workout: HKWorkout) -> Bool {
        let metadata = workout.metadata ?? [:]
        return metadata[activityMetadataKey] as? String == activityMetadataValue
    }

    static func mapHeartRateSamples(_ samples: [HKQuantitySample]) -> [HeartRatePoint] {
        let unit = HKUnit.count().unitDivided(by: .minute())
        return samples
            .map { sample in
                HeartRatePoint(
                    timestamp: sample.startDate,
                    beatsPerMinute: sample.quantity.doubleValue(for: unit)
                )
            }
            .sorted { $0.timestamp < $1.timestamp }
    }

    private static func mapWorkout(_ workout: HKWorkout) -> SessionSummary? {
        guard isSimRacing(workout) else {
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
