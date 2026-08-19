import Foundation

/// Stub adapter — Task 3 adds HealthKit requestAuthorization and UserDefaults persistence.
final class HealthKitHealthAuthorization: HealthAuthorization, @unchecked Sendable {
    var hasPrompted: Bool { false }
    var isAvailable: Bool { true }

    func requestAccessIfNeeded() async throws {
        // Task 3 implements HealthKit authorization.
    }
}
