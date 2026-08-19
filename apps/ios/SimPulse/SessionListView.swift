import SwiftUI

struct SessionListView: View {
    @ObservedObject var model: SessionListViewModel

    var body: some View {
        NavigationStack {
            Group {
                if model.isLoading && model.sessions.isEmpty {
                    ProgressView("Loading sessions…")
                } else if model.sessions.isEmpty {
                    ContentUnavailableView(
                        "No sessions yet",
                        systemImage: "flag.checkered",
                        description: Text(
                            model.errorText
                                ?? "Workouts from Apple Watch will appear here."
                        )
                    )
                } else {
                    List(model.sessions) { session in
                        let row = SessionListRowPresentation.from(session)
                        VStack(alignment: .leading, spacing: 4) {
                            Text(row.titleText)
                                .font(.headline)
                            Text(row.durationText)
                                .font(.body.monospacedDigit())
                            HStack {
                                labeled("AVG", row.averageHeartRateText)
                                labeled("MAX", row.maximumHeartRateText)
                                labeled("KCAL", row.caloriesText)
                            }
                            .font(.caption.monospacedDigit())
                            .foregroundStyle(.secondary)
                        }
                        .padding(.vertical, 2)
                    }
                }
            }
            .navigationTitle("Sessions")
            .task {
                await model.load()
            }
            .refreshable {
                await model.load()
            }
        }
    }

    private func labeled(_ title: String, _ value: String) -> some View {
        VStack(alignment: .leading, spacing: 0) {
            Text(title)
            Text(value)
                .bold()
        }
        .frame(maxWidth: .infinity, alignment: .leading)
    }
}

#Preview("Empty") {
    SessionListView(model: SessionListViewModel(repository: MockSessionRepository(sessions: [])))
}

#Preview("Mock sessions") {
    SessionListView(model: SessionListViewModel(repository: MockSessionRepository()))
}
