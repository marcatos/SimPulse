import Foundation

struct SessionDetailPresentation: Equatable {
    var titleText: String
    var durationText: String
    var averageHeartRateText: String
    var maximumHeartRateText: String
    var caloriesText: String
    var hasHeartRateChart: Bool

    static func from(_ detail: SessionDetail) -> SessionDetailPresentation {
        SessionDetailPresentation(
            titleText: SessionFormatting.formatStart(detail.startedAt),
            durationText: SessionFormatting.formatDuration(detail.duration),
            averageHeartRateText: SessionFormatting.formatBpm(detail.averageHeartRateBpm),
            maximumHeartRateText: SessionFormatting.formatBpm(detail.maximumHeartRateBpm),
            caloriesText: SessionFormatting.formatCalories(detail.activeKilocalories),
            hasHeartRateChart: !detail.heartRatePoints.isEmpty
        )
    }
}
