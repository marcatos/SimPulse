import XCTest
@testable import SimPulse

@MainActor
final class SessionDetailViewModelTests: XCTestCase {
    func testLoadUnknownIdSetsNotFoundError() async {
        let model = SessionDetailViewModel(
            sessionId: "missing",
            repository: MockSessionRepository()
        )

        await model.load()

        XCTAssertNil(model.detail)
        XCTAssertEqual(model.errorText, "Session not found.")
        XCTAssertFalse(model.isLoading)
    }

    func testLoadSuccessfulDetail() async {
        let model = SessionDetailViewModel(
            sessionId: "mock-1",
            repository: MockSessionRepository()
        )

        await model.load()

        XCTAssertNotNil(model.detail)
        XCTAssertEqual(model.detail?.id, "mock-1")
        XCTAssertNil(model.errorText)
        XCTAssertFalse(model.isLoading)
    }
}
