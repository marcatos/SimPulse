import Foundation

struct SessionListRowPresentation: Equatable {
    var titleText: String
    var durationText: String
    var averageHeartRateText: String
    var maximumHeartRateText: String
    var caloriesText: String

    static func from(_ summary: SessionSummary) -> SessionListRowPresentation {
        SessionListRowPresentation(
            titleText: SessionFormatting.formatStart(summary.startedAt),
            durationText: SessionFormatting.formatDuration(summary.duration),
            averageHeartRateText: SessionFormatting.formatBpm(summary.averageHeartRateBpm),
            maximumHeartRateText: SessionFormatting.formatBpm(summary.maximumHeartRateBpm),
            caloriesText: SessionFormatting.formatCalories(summary.activeKilocalories)
        )
    }
}
