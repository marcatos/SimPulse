import Foundation
import SwiftUI
import os

@MainActor
final class WorkoutViewModel: ObservableObject {
    @Published var snapshot = WorkoutSnapshot(
        elapsed: 0,
        currentHeartRateBpm: nil,
        averageHeartRateBpm: nil,
        maximumHeartRateBpm: nil,
        activeKilocalories: nil,
        isRunning: false
    )
    @Published var errorText: String?

    private let controller: WorkoutSessionController
    private let log = Logger(subsystem: "com.marcatos.SimPulse", category: "watch-ui")

    init(controller: WorkoutSessionController) {
        self.controller = controller
        Task { await consumeSnapshots() }
    }

    static func live() -> WorkoutViewModel {
        WorkoutViewModel(controller: WorkoutSessionController(source: HealthKitWatchWorkoutDataSource()))
    }

    func toggle() {
        Task {
            do {
                errorText = nil
                if snapshot.isRunning {
                    try await controller.endSimRacing()
                } else {
                    try await controller.startSimRacing()
                }
            } catch {
                errorText = error.localizedDescription
                log.error("workout toggle failed: \(error.localizedDescription, privacy: .public)")
            }
        }
    }

    func companionReachabilityDidChange(_ reachable: Bool) {
        controller.companionReachabilityDidChange(reachable)
    }

    private func consumeSnapshots() async {
        for await next in controller.snapshots {
            snapshot = next
        }
    }
}
