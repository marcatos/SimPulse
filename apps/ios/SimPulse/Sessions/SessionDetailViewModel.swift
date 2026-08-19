import Foundation
import os

@MainActor
final class SessionDetailViewModel: ObservableObject {
    @Published private(set) var detail: SessionDetail?
    @Published private(set) var isLoading = false
    @Published private(set) var errorText: String?

    private let sessionId: String
    private let repository: SessionRepository
    private let log = Logger(subsystem: "com.marcatos.SimPulse", category: "session-detail")

    init(sessionId: String, repository: SessionRepository) {
        self.sessionId = sessionId
        self.repository = repository
    }

    func load() async {
        isLoading = true
        errorText = nil
        let started = ContinuousClock.now

        do {
            detail = try await repository.sessionDetail(id: sessionId)
            if detail == nil {
                errorText = "Session not found."
            }
            let elapsed = ContinuousClock.now - started
            let ms = elapsed.components.seconds * 1000
                + elapsed.components.attoseconds / 1_000_000_000_000_000
            log.info(
                "session detail loaded id=\(self.sessionId, privacy: .public) found=\(self.detail != nil, privacy: .public) in \(ms, privacy: .public) ms"
            )
        } catch {
            detail = nil
            errorText = "Could not load session."
            log.error("session detail failed: \(error.localizedDescription, privacy: .public)")
        }
        isLoading = false
    }
}
