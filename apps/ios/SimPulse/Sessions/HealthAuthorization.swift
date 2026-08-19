import Foundation

enum SessionListEmptyReason: Equatable {
    case needsHealthAccess
    case healthUnavailable
}

/// Port for requesting HealthKit access without coupling UI to HealthKit.
protocol HealthAuthorization: Sendable {
    var hasPrompted: Bool { get }
    var isAvailable: Bool { get }
    func requestAccessIfNeeded() async throws
}

final class MockHealthAuthorization: HealthAuthorization, @unchecked Sendable {
    private(set) var requestCallCount = 0
    private var prompted: Bool
    var isAvailable: Bool
    var throwOnRequest: Error?
    var countOnlyUnpromptedRequests = false

    init(hasPrompted: Bool = false, isAvailable: Bool = true) {
        self.prompted = hasPrompted
        self.isAvailable = isAvailable
    }

    var hasPrompted: Bool { prompted }

    func requestAccessIfNeeded() async throws {
        let shouldCount = !countOnlyUnpromptedRequests || !prompted
        if shouldCount {
            requestCallCount += 1
        }
        guard isAvailable else {
            prompted = true
            return
        }
        if let throwOnRequest {
            throw throwOnRequest
        }
        prompted = true
    }
}
