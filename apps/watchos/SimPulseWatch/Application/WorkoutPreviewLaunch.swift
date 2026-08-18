import Foundation
import os

@MainActor
enum WorkoutPreviewLaunch {
    private static let log = Logger(subsystem: "com.marcatos.SimPulse", category: "watch-ui")

    static var forcesLuminanceReduced: Bool {
        #if DEBUG
        ProcessInfo.processInfo.arguments.contains("--simpulse-preview-always-on")
        #else
        false
        #endif
    }

    static func makeModel() -> WorkoutViewModel {
        #if DEBUG
        let arguments = ProcessInfo.processInfo.arguments
        if arguments.contains("--simpulse-preview-recording")
            || arguments.contains("--simpulse-preview-always-on")
        {
            log.info("preview launch mode=recording")
            return WorkoutViewModel(controller: WorkoutSessionController(source: recordingSource()))
        }
        if arguments.contains("--simpulse-preview-idle") {
            log.info("preview launch mode=idle")
            return WorkoutViewModel(controller: WorkoutSessionController(source: MockWorkoutDataSource()))
        }
        #endif
        return WorkoutViewModel.live()
    }

    #if DEBUG
    private static func recordingSource() -> MockWorkoutDataSource {
        MockWorkoutDataSource(
            samples: [
                WorkoutSnapshot(
                    elapsed: 95,
                    currentHeartRateBpm: 148,
                    averageHeartRateBpm: 132,
                    maximumHeartRateBpm: 161,
                    activeKilocalories: 22,
                    isRunning: true
                )
            ]
        )
    }
    #endif
}
