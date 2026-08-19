import SwiftUI

@main
struct SimPulseApp: App {
    @StateObject private var sessionList = SessionListViewModel.live()

    var body: some Scene {
        WindowGroup {
            SessionListView(model: sessionList)
        }
    }
}
