import Foundation

enum SessionFormatting {
    static func formatStart(_ date: Date) -> String {
        date.formatted(date: .abbreviated, time: .shortened)
    }

    static func formatDuration(_ duration: TimeInterval) -> String {
        let total = max(0, Int(duration))
        let hours = total / 3600
        let minutes = (total % 3600) / 60
        let seconds = total % 60
        return String(format: "%02d:%02d:%02d", hours, minutes, seconds)
    }

    static func formatBpm(_ value: Int?) -> String {
        value.map(String.init) ?? "--"
    }

    static func formatCalories(_ value: Double?) -> String {
        guard let value else { return "--" }
        return String(format: "%.0f", value)
    }
}
