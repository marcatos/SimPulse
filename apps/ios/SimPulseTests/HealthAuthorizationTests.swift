import XCTest
@testable import SimPulse

@MainActor
final class HealthAuthorizationTests: XCTestCase {
    func testLoadCallsAuthorizationPortOnEveryLoad() async {
        let auth = MockHealthAuthorization(hasPrompted: false)
        let model = SessionListViewModel(
            repository: MockSessionRepository(sessions: []),
            authorization: auth
        )

        await model.load()
        await model.load()

        XCTAssertEqual(auth.requestCallCount, 2)
        XCTAssertTrue(auth.hasPrompted)
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

    func testLoadContinuesAfterAuthThrowAndSetsNeedsHealthAccess() async {
        let auth = MockHealthAuthorization(hasPrompted: false)
        auth.throwOnRequest = NSError(domain: "test", code: 1)
        let model = SessionListViewModel(
            repository: MockSessionRepository(sessions: []),
            authorization: auth
        )

        await model.load()

        XCTAssertFalse(auth.hasPrompted)
        XCTAssertEqual(model.emptyReason, .needsHealthAccess)
        XCTAssertTrue(model.sessions.isEmpty)
    }
}
