import SwiftUI

struct WorkoutView: View {
    @ObservedObject var model: WorkoutViewModel
    @Environment(\.isLuminanceReduced) private var isLuminanceReduced

    var body: some View {
        let glance = WorkoutGlancePresentation.from(
            snapshot: model.snapshot,
            errorText: model.errorText,
            isLuminanceReduced: isLuminanceReduced
        )

        VStack(spacing: isLuminanceReduced ? 4 : 6) {
            Text(glance.stateText)
                .font(.caption.weight(.semibold))
                .textCase(.uppercase)
            Text(glance.heartRateText)
                .font(.system(size: isLuminanceReduced ? 40 : 48, weight: .bold, design: .rounded))
                .monospacedDigit()
                .minimumScaleFactor(0.5)
                .lineLimit(1)
            Text("BPM")
                .font(.caption2)
            Text(glance.elapsedText)
                .font(.title3.monospacedDigit())
            if !isLuminanceReduced {
                HStack {
                    labeled("AVG", glance.averageHeartRateText)
                    labeled("MAX", glance.maximumHeartRateText)
                    labeled("KCAL", glance.caloriesText)
                }
                .font(.caption.monospacedDigit())
            }
            if glance.showsControls {
                Button(model.snapshot.isRunning ? "End" : "Start") {
                    model.toggle()
                }
            }
            if glance.showsError, let errorText = glance.errorText {
                Text(errorText)
                    .font(.caption2)
                    .foregroundStyle(.red)
                    .lineLimit(3)
            }
        }
        .padding(.horizontal, 4)
        .frame(maxWidth: .infinity, maxHeight: .infinity)
    }

    private func labeled(_ title: String, _ value: String) -> some View {
        VStack(spacing: 0) {
            Text(title)
            Text(value)
                .bold()
        }
        .frame(maxWidth: .infinity)
    }
}

#Preview("Idle") {
    WorkoutView(model: WorkoutViewModel(controller: WorkoutSessionController(source: MockWorkoutDataSource())))
}

#Preview("Recording") {
    let source = MockWorkoutDataSource(
        samples: [
            WorkoutSnapshot(
                elapsed: 95,
                currentHeartRateBpm: 148,
                averageHeartRateBpm: 132,
                maximumHeartRateBpm: 161,
                activeKilocalories: 22,
                isRunning: true
            )
        ]
    )
    WorkoutView(model: WorkoutViewModel(controller: WorkoutSessionController(source: source)))
}
