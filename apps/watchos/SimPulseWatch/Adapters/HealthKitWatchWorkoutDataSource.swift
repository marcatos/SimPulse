#if os(watchOS)
import Foundation
import HealthKit
import os

/// HealthKit adapter for watchOS. Activity type is `.other` named Sim Racing
/// (no honest HKWorkoutActivityType exists for sim racing).
final class HealthKitWatchWorkoutDataSource: NSObject, WorkoutDataSource, @unchecked Sendable {
    private let store = HKHealthStore()
    private let log = Logger(subsystem: "com.marcatos.SimPulse", category: "healthkit")
    private let lock = NSLock()
    private var session: HKWorkoutSession?
    private var builder: HKLiveWorkoutBuilder?
    private var startDate: Date?
    private var continuation: AsyncStream<WorkoutSnapshot>.Continuation?
    private var lastSnapshot = WorkoutSnapshot(
        elapsed: 0,
        currentHeartRateBpm: nil,
        averageHeartRateBpm: nil,
        maximumHeartRateBpm: nil,
        activeKilocalories: nil,
        isRunning: false
    )
    private let summarySender: WatchConnectivitySummarySender?

    let snapshots: AsyncStream<WorkoutSnapshot>

    init(summarySender: WatchConnectivitySummarySender? = nil) {
        let stream = AsyncStream<WorkoutSnapshot>.makeStream()
        snapshots = stream.stream
        continuation = stream.continuation
        self.summarySender = summarySender
        super.init()
    }

    func start() async throws {
        let startedAt = Date()
        guard HKHealthStore.isHealthDataAvailable() else {
            throw WorkoutSourceError.healthDataUnavailable
        }

        try await requestAuthorization()

        let configuration = HKWorkoutConfiguration()
        configuration.activityType = .other
        configuration.locationType = .indoor

        let workoutSession = try HKWorkoutSession(healthStore: store, configuration: configuration)
        let workoutBuilder = workoutSession.associatedWorkoutBuilder()
        workoutBuilder.dataSource = HKLiveWorkoutDataSource(healthStore: store, workoutConfiguration: configuration)
        workoutSession.delegate = self
        workoutBuilder.delegate = self

        lock.lock()
        session = workoutSession
        builder = workoutBuilder
        startDate = Date()
        lastSnapshot.isRunning = true
        let snapshot = lastSnapshot
        lock.unlock()

        continuation?.yield(snapshot)
        workoutSession.startActivity(with: Date())
        try await workoutBuilder.beginCollection(at: Date())
        try await workoutBuilder.addMetadata([
            HKMetadataKeyWorkoutBrandName: "SimPulse",
            "com.marcatos.SimPulse.activity": "Sim Racing"
        ] as [String: Any])

        let elapsedMs = Int(Date().timeIntervalSince(startedAt) * 1000)
        log.info("healthkit session started in \(elapsedMs, privacy: .public) ms activity=other name=SimRacing")
    }

    func stop() async throws {
        let startedAt = Date()
        lock.lock()
        let workoutSession = session
        let workoutBuilder = builder
        lock.unlock()

        guard let workoutSession, let workoutBuilder else {
            throw WorkoutSessionError.notRunning
        }

        workoutSession.end()
        try await workoutBuilder.endCollection(at: Date())
        let finished = try await workoutBuilder.finishWorkout()

        lock.lock()
        session = nil
        builder = nil
        startDate = nil
        lastSnapshot.isRunning = false
        let snapshot = lastSnapshot
        lock.unlock()
        continuation?.yield(snapshot)

        if let finished {
            if let summarySender {
                let message = Self.buildSummaryMessage(from: finished, snapshot: snapshot)
                do {
                    try summarySender.enqueueAndTransfer(message)
                } catch {
                    log.error("summary enqueue failed: \(error.localizedDescription, privacy: .public)")
                }
            }
        } else {
            log.error("healthkit finishWorkout returned nil; summary not enqueued")
        }

        let elapsedMs = Int(Date().timeIntervalSince(startedAt) * 1000)
        log.info("healthkit session ended in \(elapsedMs, privacy: .public) ms")
    }

    private static func buildSummaryMessage(
        from workout: HKWorkout,
        snapshot: WorkoutSnapshot
    ) -> WatchWorkoutSummaryMessage {
        let endedAt = workout.endDate ?? Date()
        let activeKilocalories =
            workout.totalEnergyBurned?.doubleValue(for: .kilocalorie()) ?? snapshot.activeKilocalories

        return WatchWorkoutSummaryMessage(
            schemaVersion: WatchWorkoutSummaryWire.schemaVersion,
            sessionId: workout.uuid.uuidString,
            startedAt: workout.startDate,
            endedAt: endedAt,
            durationSeconds: workout.duration,
            averageHeartRateBpm: snapshot.averageHeartRateBpm,
            maximumHeartRateBpm: snapshot.maximumHeartRateBpm,
            activeKilocalories: activeKilocalories
        )
    }

    private func requestAuthorization() async throws {
        let share: Set<HKSampleType> = [
            HKQuantityType(.activeEnergyBurned),
            HKObjectType.workoutType()
        ]
        let read: Set<HKObjectType> = [
            HKQuantityType(.heartRate),
            HKQuantityType(.activeEnergyBurned),
            HKObjectType.workoutType()
        ]
        try await store.requestAuthorization(toShare: share, read: read)
    }

    private func publish(_ mutate: (inout WorkoutSnapshot) -> Void) {
        lock.lock()
        mutate(&lastSnapshot)
        if let startDate {
            lastSnapshot.elapsed = Date().timeIntervalSince(startDate)
        }
        let snapshot = lastSnapshot
        lock.unlock()
        continuation?.yield(snapshot)
    }
}

extension HealthKitWatchWorkoutDataSource: HKWorkoutSessionDelegate {
    func workoutSession(
        _ workoutSession: HKWorkoutSession,
        didChangeTo toState: HKWorkoutSessionState,
        from fromState: HKWorkoutSessionState,
        date: Date
    ) {
        log.info("healthkit state \(fromState.rawValue, privacy: .public)->\(toState.rawValue, privacy: .public)")
    }

    func workoutSession(_ workoutSession: HKWorkoutSession, didFailWithError error: Error) {
        log.error("healthkit session failed: \(error.localizedDescription, privacy: .public)")
    }
}

extension HealthKitWatchWorkoutDataSource: HKLiveWorkoutBuilderDelegate {
    func workoutBuilderDidCollectEvent(_ workoutBuilder: HKLiveWorkoutBuilder) {}

    func workoutBuilder(_ workoutBuilder: HKLiveWorkoutBuilder, didCollectDataOf collectedTypes: Set<HKSampleType>) {
        publish { snapshot in
            snapshot.isRunning = true
            if collectedTypes.contains(HKQuantityType(.heartRate)) {
                let unit = HKUnit.count().unitDivided(by: .minute())
                if let quantity = workoutBuilder.statistics(for: HKQuantityType(.heartRate))?.mostRecentQuantity() {
                    snapshot.currentHeartRateBpm = Int(quantity.doubleValue(for: unit).rounded())
                }
                if let average = workoutBuilder.statistics(for: HKQuantityType(.heartRate))?.averageQuantity() {
                    snapshot.averageHeartRateBpm = Int(average.doubleValue(for: unit).rounded())
                }
                if let maximum = workoutBuilder.statistics(for: HKQuantityType(.heartRate))?.maximumQuantity() {
                    snapshot.maximumHeartRateBpm = Int(maximum.doubleValue(for: unit).rounded())
                }
            }
            if collectedTypes.contains(HKQuantityType(.activeEnergyBurned)) {
                let energy = workoutBuilder.statistics(for: HKQuantityType(.activeEnergyBurned))?.sumQuantity()
                snapshot.activeKilocalories = energy?.doubleValue(for: .kilocalorie())
            }
        }
    }
}
#endif
