import Foundation
import os

@MainActor
final class SessionListViewModel: ObservableObject {
    @Published private(set) var sessions: [SessionSummary] = []
    @Published private(set) var isLoading = false
    @Published private(set) var errorText: String?

    private let repository: SessionRepository
    private let log = Logger(subsystem: "com.marcatos.SimPulse", category: "session-list")

    init(repository: SessionRepository) {
        self.repository = repository
    }

    static func live() -> SessionListViewModel {
        #if DEBUG
        if ProcessInfo.processInfo.arguments.contains("--simpulse-preview-sessions") {
            return SessionListViewModel(repository: MockSessionRepository())
        }
        #endif
        return SessionListViewModel(repository: HealthKitSessionRepository())
    }

    func load() async {
        isLoading = true
        errorText = nil
        let started = ContinuousClock.now
        do {
            sessions = try await repository.listSessions()
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
