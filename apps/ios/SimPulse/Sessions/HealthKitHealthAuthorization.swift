import Foundation
import os

#if canImport(HealthKit)
import HealthKit
#endif

final class HealthKitHealthAuthorization: HealthAuthorization, @unchecked Sendable {
    static let promptedDefaultsKey = "com.marcatos.SimPulse.healthAuthPrompted"
    private let defaults: UserDefaults
    private let log = Logger(subsystem: "com.marcatos.SimPulse", category: "health-auth")

    #if canImport(HealthKit)
    private let store: HKHealthStore

    init(defaults: UserDefaults = .standard, store: HKHealthStore = HKHealthStore()) {
        self.defaults = defaults
        self.store = store
    }
    #else
    init(defaults: UserDefaults = .standard) {
        self.defaults = defaults
    }
    #endif

    var hasPrompted: Bool {
        defaults.bool(forKey: Self.promptedDefaultsKey)
    }

    var isAvailable: Bool {
        #if canImport(HealthKit)
        HKHealthStore.isHealthDataAvailable()
        #else
        false
        #endif
    }

    func requestAccessIfNeeded() async throws {
        if hasPrompted { return }
        guard isAvailable else {
            defaults.set(true, forKey: Self.promptedDefaultsKey)
            log.info("healthkit unavailable; skipping authorization sheet")
            return
        }
        #if canImport(HealthKit)
        let share: Set<HKSampleType> = [
            HKQuantityType(.activeEnergyBurned),
            HKObjectType.workoutType(),
        ]
        let read: Set<HKObjectType> = [
            HKQuantityType(.heartRate),
            HKQuantityType(.activeEnergyBurned),
            HKObjectType.workoutType(),
        ]
        do {
            try await store.requestAuthorization(toShare: share, read: read)
            defaults.set(true, forKey: Self.promptedDefaultsKey)
            log.info("healthkit authorization prompt completed")
        } catch {
            log.info("healthkit authorization failed: \(error.localizedDescription, privacy: .public)")
            throw error
        }
        #else
        defaults.set(true, forKey: Self.promptedDefaultsKey)
        #endif
    }
}
