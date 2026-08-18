import Foundation

/// Maps a workout snapshot to glanceable Watch copy. Pure so Always On and metric
/// formatting can be tested without HealthKit or SwiftUI.
struct WorkoutGlancePresentation: Equatable {
    var elapsedText: String
    var heartRateText: String
    var averageHeartRateText: String
    var maximumHeartRateText: String
    var caloriesText: String
    var stateText: String
    var errorText: String?
    var showsControls: Bool
    var showsError: Bool

    static func from(
        snapshot: WorkoutSnapshot,
        errorText: String?,
        isLuminanceReduced: Bool
    ) -> WorkoutGlancePresentation {
        let hasError = !(errorText ?? "").isEmpty
        return WorkoutGlancePresentation(
            elapsedText: formatElapsed(snapshot.elapsed),
            heartRateText: formatBpm(snapshot.currentHeartRateBpm),
            averageHeartRateText: formatBpm(snapshot.averageHeartRateBpm),
            maximumHeartRateText: formatBpm(snapshot.maximumHeartRateBpm),
            caloriesText: formatCalories(snapshot.activeKilocalories),
            stateText: snapshot.isRunning ? "Recording" : "Idle",
            errorText: errorText,
            showsControls: !isLuminanceReduced,
            showsError: hasError && !isLuminanceReduced
        )
    }

    private static func formatElapsed(_ elapsed: TimeInterval) -> String {
        let total = max(0, Int(elapsed))
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
