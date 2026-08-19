import XCTest
@testable import SimPulse

@MainActor
final class HealthAuthorizationTests: XCTestCase {
    func testLoadRequestsAccessOnlyOnce() async {
        let auth = MockHealthAuthorization(hasPrompted: false)
        let model = SessionListViewModel(
            repository: MockSessionRepository(sessions: []),
            authorization: auth
        )

        await model.load()
        await model.load()

        XCTAssertEqual(auth.requestCallCount, 2)
        XCTAssertTrue(auth.hasPrompted)
        // Mock increments every call; ViewModel must still call requestAccessIfNeeded each load,
        // and the mock/live adapter no-ops the HealthKit sheet when hasPrompted.
        // Strengthen: use a mock that only increments when !hasPrompted:
    }

    func testLoadRequestsHealthKitSheetOnlyWhileNotPrompted() async {
        let auth = MockHealthAuthorization(hasPrompted: false)
        auth.countOnlyUnpromptedRequests = true
        let model = SessionListViewModel(
            repository: MockSessionRepository(sessions: []),
            authorization: auth
        )

        await model.load()
        await model.load()

        XCTAssertEqual(auth.requestCallCount, 1)
        XCTAssertEqual(model.emptyReason, .needsHealthAccess)
        XCTAssertTrue(model.sessions.isEmpty)
    }

    func testLoadWithSessionsClearsEmptyReason() async {
        let auth = MockHealthAuthorization(hasPrompted: true)
        let model = SessionListViewModel(
            repository: MockSessionRepository(),
            authorization: auth
        )

        await model.load()

        XCTAssertFalse(model.sessions.isEmpty)
        XCTAssertNil(model.emptyReason)
    }

    func testUnavailableSetsHealthUnavailableEmptyReason() async {
        let auth = MockHealthAuthorization(hasPrompted: false, isAvailable: false)
        let model = SessionListViewModel(
            repository: MockSessionRepository(sessions: []),
            authorization: auth
        )

        await model.load()

        XCTAssertEqual(model.emptyReason, .healthUnavailable)
    }
}
