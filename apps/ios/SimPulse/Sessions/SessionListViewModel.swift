import Foundation
import os

@MainActor
final class SessionListViewModel: ObservableObject {
    @Published private(set) var sessions: [SessionSummary] = []
    @Published private(set) var isLoading = false
    @Published private(set) var errorText: String?
    @Published private(set) var emptyReason: SessionListEmptyReason?

    private let repository: SessionRepository
    private let authorization: HealthAuthorization
    private let log = Logger(subsystem: "com.marcatos.SimPulse", category: "session-list")
    private var mergeObserver: NSObjectProtocol?

    var sessionsRepository: SessionRepository { repository }

    init(repository: SessionRepository, authorization: HealthAuthorization) {
        self.repository = repository
        self.authorization = authorization
        mergeObserver = NotificationCenter.default.addObserver(
            forName: .simpulseWorkoutSummaryMerged,
            object: nil,
            queue: .main
        ) { [weak self] _ in
            Task { await self?.load() }
        }
    }

    deinit {
        if let mergeObserver {
            NotificationCenter.default.removeObserver(mergeObserver)
        }
    }

    static func live() -> SessionListViewModel {
        #if DEBUG
        if ProcessInfo.processInfo.arguments.contains("--simpulse-preview-sessions") {
            return SessionListViewModel(
                repository: MockSessionRepository(),
                authorization: MockHealthAuthorization(hasPrompted: true)
            )
        }
        #endif
        return SessionListViewModel(
            repository: HealthKitSessionRepository(),
            authorization: HealthKitHealthAuthorization()
        )
    }

    func load() async {
        isLoading = true
        errorText = nil
        emptyReason = nil
        let started = ContinuousClock.now

        do {
            try await authorization.requestAccessIfNeeded()
        } catch {
            log.error("health authorization failed: \(error.localizedDescription, privacy: .public)")
        }

        do {
            sessions = try await repository.listSessions()
            if sessions.isEmpty {
                if !authorization.isAvailable {
                    emptyReason = .healthUnavailable
                } else {
                    emptyReason = .needsHealthAccess
                }
            } else {
                emptyReason = nil
            }
            let elapsed = ContinuousClock.now - started
            let ms = elapsed.components.seconds * 1000
                + elapsed.components.attoseconds / 1_000_000_000_000_000
            log.info(
                "session list loaded count=\(self.sessions.count, privacy: .public) in \(ms, privacy: .public) ms"
            )
        } catch {
            sessions = []
            errorText = "Could not load sessions."
            log.error("session list failed: \(error.localizedDescription, privacy: .public)")
        }
        isLoading = false
    }
}
