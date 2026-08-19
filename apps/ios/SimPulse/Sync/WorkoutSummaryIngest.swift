import Foundation

protocol WorkoutSummaryIngest: Sendable {
    /// Returns true if newly merged, false if duplicate.
    func merge(_ message: WatchWorkoutSummaryMessage) throws -> Bool
}

final class UserDefaultsWorkoutSummaryIngest: WorkoutSummaryIngest, @unchecked Sendable {
    static let seenKey = "com.marcatos.SimPulse.seenWorkoutSummaryIds"

    private let defaults: UserDefaults

    init(defaults: UserDefaults = .standard) {
        self.defaults = defaults
    }

    func merge(_ message: WatchWorkoutSummaryMessage) throws -> Bool {
        var seen = defaults.stringArray(forKey: Self.seenKey) ?? []
        if seen.contains(message.sessionId) {
            return false
        }
        seen.append(message.sessionId)
        defaults.set(seen, forKey: Self.seenKey)
        NotificationCenter.default.post(
            name: .simpulseWorkoutSummaryMerged,
            object: nil,
            userInfo: ["sessionId": message.sessionId]
        )
        return true
    }
}

extension Notification.Name {
    static let simpulseWorkoutSummaryMerged = Notification.Name("com.marcatos.SimPulse.workoutSummaryMerged")
}
