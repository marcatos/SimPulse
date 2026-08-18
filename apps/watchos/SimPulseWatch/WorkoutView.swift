import SwiftUI

struct WorkoutView: View {
    var elapsed: String = "00:00:00"
    var heartRate: String = "--"
    var averageHeartRate: String = "--"
    var maximumHeartRate: String = "--"
    var calories: String = "--"

    var body: some View {
        VStack(spacing: 8) {
            Text(elapsed)
                .font(.title.monospacedDigit())
            Text(heartRate)
                .font(.largeTitle.bold().monospacedDigit())
            Text("BPM")
                .font(.caption)
            HStack {
                labeled("AVG", averageHeartRate)
                labeled("MAX", maximumHeartRate)
                labeled("KCAL", calories)
            }
            .font(.caption.monospacedDigit())
        }
        .padding()
    }

    private func labeled(_ title: String, _ value: String) -> some View {
        VStack {
            Text(title)
            Text(value)
                .bold()
        }
        .frame(maxWidth: .infinity)
    }
}
