import SwiftUI

@main
struct SimPulseWatchApp: App {
    @StateObject private var model = WorkoutPreviewLaunch.makeModel()

    var body: some Scene {
        WindowGroup {
            Group {
                if WorkoutPreviewLaunch.forcesLuminanceReduced {
                    WorkoutView(model: model)
                        .environment(\.isLuminanceReduced, true)
                } else {
                    WorkoutView(model: model)
                }
            }
        }
    }
}
