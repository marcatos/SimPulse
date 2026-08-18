import SwiftUI

@main
struct SimPulseWatchApp: App {
    @StateObject private var model = WorkoutViewModel.live()

    var body: some Scene {
        WindowGroup {
            WorkoutView(model: model)
        }
    }
}
