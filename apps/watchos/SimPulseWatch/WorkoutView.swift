import SwiftUI

struct WorkoutView: View {
    @ObservedObject var model: WorkoutViewModel

    var body: some View {
        VStack(spacing: 8) {
            Text(elapsedText)
                .font(.title.monospacedDigit())
            Text(heartRateText)
                .font(.largeTitle.bold().monospacedDigit())
            Text("BPM")
                .font(.caption)
            HStack {
                labeled("AVG", averageText)
                labeled("MAX", maximumText)
                labeled("KCAL", caloriesText)
            }
            .font(.caption.monospacedDigit())
            Button(model.snapshot.isRunning ? "End" : "Start") {
                model.toggle()
            }
            if let errorText = model.errorText {
                Text(errorText)
                    .font(.caption2)
                    .foregroundStyle(.red)
                    .lineLimit(3)
            }
        }
        .padding()
    }

    private var elapsedText: String {
        let total = Int(model.snapshot.elapsed)
        let hours = total / 3600
        let minutes = (total % 3600) / 60
        let seconds = total % 60
        return String(format: "%02d:%02d:%02d", hours, minutes, seconds)
    }

    private var heartRateText: String { model.snapshot.currentHeartRateBpm.map(String.init) ?? "--" }
    private var averageText: String { model.snapshot.averageHeartRateBpm.map(String.init) ?? "--" }
    private var maximumText: String { model.snapshot.maximumHeartRateBpm.map(String.init) ?? "--" }
    private var caloriesText: String {
        guard let kcal = model.snapshot.activeKilocalories else { return "--" }
        return String(format: "%.0f", kcal)
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

#Preview {
    WorkoutView(model: WorkoutViewModel(controller: WorkoutSessionController(source: MockWorkoutDataSource())))
}
