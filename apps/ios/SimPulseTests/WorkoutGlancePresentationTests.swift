import XCTest
@testable import SimPulse

final class WorkoutGlancePresentationTests: XCTestCase {
    func testIdleSnapshotUsesPlaceholdersAndIdleState() {
        let snapshot = WorkoutSnapshot(
            elapsed: 0,
            currentHeartRateBpm: nil,
            averageHeartRateBpm: nil,
            maximumHeartRateBpm: nil,
            activeKilocalories: nil,
            isRunning: false
        )

        let glance = WorkoutGlancePresentation.from(
            snapshot: snapshot,
            errorText: nil,
            isLuminanceReduced: false
        )

        XCTAssertEqual(glance.elapsedText, "00:00:00")
        XCTAssertEqual(glance.heartRateText, "--")
        XCTAssertEqual(glance.averageHeartRateText, "--")
        XCTAssertEqual(glance.maximumHeartRateText, "--")
        XCTAssertEqual(glance.caloriesText, "--")
        XCTAssertEqual(glance.stateText, "Idle")
        XCTAssertTrue(glance.showsControls)
        XCTAssertFalse(glance.showsError)
    }

    func testRunningSnapshotFormatsMetricsAndRecordingState() {
        let snapshot = WorkoutSnapshot(
            elapsed: 3661,
            currentHeartRateBpm: 142,
            averageHeartRateBpm: 128,
            maximumHeartRateBpm: 161,
            activeKilocalories: 87.4,
            isRunning: true
        )

        let glance = WorkoutGlancePresentation.from(
            snapshot: snapshot,
            errorText: nil,
            isLuminanceReduced: false
        )

        XCTAssertEqual(glance.elapsedText, "01:01:01")
        XCTAssertEqual(glance.heartRateText, "142")
        XCTAssertEqual(glance.averageHeartRateText, "128")
        XCTAssertEqual(glance.maximumHeartRateText, "161")
        XCTAssertEqual(glance.caloriesText, "87")
        XCTAssertEqual(glance.stateText, "Recording")
        XCTAssertTrue(glance.showsControls)
    }

    func testAlwaysOnHidesControlsAndErrors() {
        let snapshot = WorkoutSnapshot(
            elapsed: 12,
            currentHeartRateBpm: 90,
            averageHeartRateBpm: 88,
            maximumHeartRateBpm: 91,
            activeKilocalories: 1,
            isRunning: true
        )

        let glance = WorkoutGlancePresentation.from(
            snapshot: snapshot,
            errorText: "HealthKit denied",
            isLuminanceReduced: true
        )

        XCTAssertEqual(glance.stateText, "Recording")
        XCTAssertEqual(glance.heartRateText, "90")
        XCTAssertFalse(glance.showsControls)
        XCTAssertFalse(glance.showsError)
    }

    func testInteractiveModeShowsErrorWhenPresent() {
        let snapshot = WorkoutSnapshot(
            elapsed: 0,
            currentHeartRateBpm: nil,
            averageHeartRateBpm: nil,
            maximumHeartRateBpm: nil,
            activeKilocalories: nil,
            isRunning: false
        )

        let glance = WorkoutGlancePresentation.from(
            snapshot: snapshot,
            errorText: "HealthKit denied",
            isLuminanceReduced: false
        )

        XCTAssertTrue(glance.showsError)
        XCTAssertEqual(glance.errorText, "HealthKit denied")
        XCTAssertTrue(glance.showsControls)
    }
}
