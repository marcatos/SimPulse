import SwiftUI

struct SessionListView: View {
    var body: some View {
        NavigationStack {
            ContentUnavailableView(
                "No sessions yet",
                systemImage: "flag.checkered",
                description: Text("Workouts from Apple Watch will appear here.")
            )
            .navigationTitle("Sessions")
        }
    }
}
