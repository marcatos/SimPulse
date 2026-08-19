import SwiftUI

@main
struct SimPulseApp: App {
    private let summaryReceiver: WatchConnectivitySummaryReceiver
    @StateObject private var sessionList = SessionListViewModel.live()

    init() {
        let receiver = WatchConnectivitySummaryReceiver.live()
        summaryReceiver = receiver
        receiver.start()
    }

    var body: some Scene {
        WindowGroup {
            SessionListView(model: sessionList)
        }
    }
}
