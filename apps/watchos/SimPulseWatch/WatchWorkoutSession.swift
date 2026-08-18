import Foundation

/// Watch-side port. Recording must continue if the iPhone is unreachable.
protocol WatchWorkoutSession: Sendable {
    func startSimRacing() async throws
    func endSimRacing() async throws
    func companionReachabilityDidChange(_ reachable: Bool)
}

extension WorkoutSessionController: WatchWorkoutSession {}
