import Charts
import SwiftUI

struct SessionDetailView: View {
    @StateObject private var model: SessionDetailViewModel

    init(sessionId: String, repository: SessionRepository) {
        _model = StateObject(
            wrappedValue: SessionDetailViewModel(sessionId: sessionId, repository: repository)
        )
    }

    var body: some View {
        Group {
            if model.isLoading && model.detail == nil {
                ProgressView("Loading session…")
            } else if let detail = model.detail {
                let presentation = SessionDetailPresentation.from(detail)
                ScrollView {
                    VStack(alignment: .leading, spacing: 16) {
                        Text(presentation.titleText)
                            .font(.headline)
                        Text(presentation.durationText)
                            .font(.title2.monospacedDigit())
                        HStack {
                            metricLabel("AVG", presentation.averageHeartRateText)
                            metricLabel("MAX", presentation.maximumHeartRateText)
                            metricLabel("KCAL", presentation.caloriesText)
                        }
                        .font(.caption.monospacedDigit())
                        .foregroundStyle(.secondary)

                        if presentation.hasHeartRateChart {
                            Chart(detail.heartRatePoints) { point in
                                LineMark(
                                    x: .value("Time", point.timestamp),
                                    y: .value("BPM", point.beatsPerMinute)
                                )
                            }
                            .frame(height: 220)
                            .accessibilityLabel("Heart rate over time")
                        } else {
                            ContentUnavailableView(
                                "No heart rate samples",
                                systemImage: "heart.slash",
                                description: Text("This session has no heart rate data in Health.")
                            )
                        }
                    }
                    .frame(maxWidth: .infinity, alignment: .leading)
                    .padding()
                }
            } else {
                ContentUnavailableView(
                    "Session unavailable",
                    systemImage: "flag.checkered",
                    description: Text(model.errorText ?? "Session not found.")
                )
            }
        }
        .navigationTitle("Session")
        .navigationBarTitleDisplayMode(.inline)
        .task { await model.load() }
    }

    private func metricLabel(_ title: String, _ value: String) -> some View {
        VStack(alignment: .leading, spacing: 0) {
            Text(title)
            Text(value)
                .bold()
        }
        .frame(maxWidth: .infinity, alignment: .leading)
    }
}

#Preview("Mock detail") {
    NavigationStack {
        SessionDetailView(sessionId: "mock-1", repository: MockSessionRepository())
    }
}

#Preview("Empty heart rate") {
    let summary = SessionSummary(
        id: "empty-hr",
        startedAt: Date(timeIntervalSince1970: 1_700_100_000),
        duration: 600,
        averageHeartRateBpm: nil,
        maximumHeartRateBpm: nil,
        activeKilocalories: 50,
        source: .mock
    )
    let emptyDetail = SessionDetail(
        id: "empty-hr",
        startedAt: summary.startedAt,
        duration: summary.duration,
        averageHeartRateBpm: nil,
        maximumHeartRateBpm: nil,
        activeKilocalories: summary.activeKilocalories,
        heartRatePoints: [],
        source: .mock
    )
    let repo = MockSessionRepository(
        sessions: [summary],
        detailsById: ["empty-hr": emptyDetail]
    )
    return NavigationStack {
        SessionDetailView(sessionId: "empty-hr", repository: repo)
    }
}
