import SwiftUI
import UIKit

struct SessionListView: View {
    @ObservedObject var model: SessionListViewModel
    @Environment(\.openURL) private var openURL

    var body: some View {
        NavigationStack {
            Group {
                if model.isLoading && model.sessions.isEmpty {
                    ProgressView("Loading sessions…")
                } else if model.sessions.isEmpty {
                    ContentUnavailableView {
                        Label(emptyTitle, systemImage: "flag.checkered")
                    } description: {
                        Text(emptyDescription)
                    } actions: {
                        if model.emptyReason == .needsHealthAccess {
                            Button("Open Settings") {
                                if let url = URL(string: UIApplication.openSettingsURLString) {
                                    openURL(url)
                                }
                            }
                        }
                    }
                } else {
                    List(model.sessions) { session in
                        NavigationLink(value: session.id) {
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
            }
            .navigationDestination(for: String.self) { id in
                SessionDetailView(sessionId: id, repository: model.sessionsRepository)
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

    private var emptyTitle: String {
        switch model.emptyReason {
        case .healthUnavailable:
            "Health unavailable"
        default:
            "No sessions yet"
        }
    }

    private var emptyDescription: String {
        switch model.emptyReason {
        case .needsHealthAccess:
            "Allow SimPulse in Settings → Health, or start a Sim Racing workout on Apple Watch."
        case .healthUnavailable:
            "Health data is not available on this device."
        case nil:
            model.errorText ?? "Workouts from Apple Watch will appear here."
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
    SessionListView(model: SessionListViewModel(
        repository: MockSessionRepository(sessions: []),
        authorization: MockHealthAuthorization(hasPrompted: true)
    ))
}

#Preview("Mock sessions") {
    SessionListView(model: SessionListViewModel(
        repository: MockSessionRepository(),
        authorization: MockHealthAuthorization(hasPrompted: true)
    ))
}
