import Foundation

struct SessionListRowPresentation: Equatable {
    var titleText: String
    var durationText: String
    var averageHeartRateText: String
    var maximumHeartRateText: String
    var caloriesText: String

    static func from(_ summary: SessionSummary) -> SessionListRowPresentation {
        SessionListRowPresentation(
            titleText: Self.formatStart(summary.startedAt),
            durationText: Self.formatDuration(summary.duration),
            averageHeartRateText: Self.formatBpm(summary.averageHeartRateBpm),
            maximumHeartRateText: Self.formatBpm(summary.maximumHeartRateBpm),
            caloriesText: Self.formatCalories(summary.activeKilocalories)
        )
    }

    private static func formatStart(_ date: Date) -> String {
        date.formatted(date: .abbreviated, time: .shortened)
    }

    private static func formatDuration(_ duration: TimeInterval) -> String {
        let total = max(0, Int(duration))
        let hours = total / 3600
        let minutes = (total % 3600) / 60
        let seconds = total % 60
        return String(format: "%02d:%02d:%02d", hours, minutes, seconds)
    }

    private static func formatBpm(_ value: Int?) -> String {
        value.map(String.init) ?? "--"
    }

    private static func formatCalories(_ value: Double?) -> String {
        guard let value else { return "--" }
        return String(format: "%.0f", value)
    }
}
