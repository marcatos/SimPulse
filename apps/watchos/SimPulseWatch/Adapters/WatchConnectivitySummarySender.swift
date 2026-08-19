#if os(watchOS)
import Foundation
import os
import WatchConnectivity

/// Queues workout summaries on disk and hands them to the system via `transferUserInfo`.
/// Outbox entries are removed only after `session(_:didFinish:error:)` reports success.
final class WatchConnectivitySummarySender: NSObject, WCSessionDelegate, @unchecked Sendable {
    private let outbox: WorkoutSummaryOutbox
    private let session: WCSession
    private let log = Logger(subsystem: "com.marcatos.SimPulse", category: "wc-sender")
    private let lock = NSLock()
    private var inFlightSessionIds = Set<String>()

    init(outbox: WorkoutSummaryOutbox, session: WCSession = .default) {
        self.outbox = outbox
        self.session = session
        super.init()
    }

    func start() {
        guard WCSession.isSupported() else {
            log.error("WCSession not supported on this device")
            return
        }
        session.delegate = self
        session.activate()
        log.info("WCSession activation requested")
    }

    func enqueueAndTransfer(_ message: WatchWorkoutSummaryMessage) throws {
        try outbox.enqueue(message)
        log.info(
            "outbox enqueued sessionId=\(message.sessionId, privacy: .public) pending=\(self.outbox.pendingCount, privacy: .public)"
        )
        try transferIfPossible(message)
    }

    func flushIfPossible() {
        do {
            let pending = try outbox.pendingMessages()
            log.info("flush pending count=\(pending.count, privacy: .public)")
            for message in pending {
                try transferIfPossible(message)
            }
        } catch {
            log.error("flush failed: \(error.localizedDescription, privacy: .public)")
        }
    }

    private func transferIfPossible(_ message: WatchWorkoutSummaryMessage) throws {
        guard session.activationState == .activated else {
            log.info(
                "session not activated; keeping sessionId=\(message.sessionId, privacy: .public) in outbox"
            )
            return
        }

        lock.lock()
        if inFlightSessionIds.contains(message.sessionId) {
            lock.unlock()
            log.info("transfer already in flight sessionId=\(message.sessionId, privacy: .public)")
            return
        }
        lock.unlock()

        if outstandingSessionIds().contains(message.sessionId) {
            lock.lock()
            inFlightSessionIds.insert(message.sessionId)
            lock.unlock()
            log.info("transfer already outstanding sessionId=\(message.sessionId, privacy: .public)")
            return
        }

        let userInfo = try message.makeUserInfo()
        session.transferUserInfo(userInfo)

        lock.lock()
        inFlightSessionIds.insert(message.sessionId)
        lock.unlock()

        log.info("transferUserInfo queued sessionId=\(message.sessionId, privacy: .public)")
    }

    func session(
        _ session: WCSession,
        activationDidCompleteWith activationState: WCSessionActivationState,
        error: Error?
    ) {
        if let error {
            log.error("activation failed: \(error.localizedDescription, privacy: .public)")
            return
        }
        log.info("activation complete state=\(activationState.rawValue, privacy: .public)")
        flushIfPossible()
    }

    func sessionReachabilityDidChange(_ session: WCSession) {
        log.info("reachability changed reachable=\(session.isReachable, privacy: .public)")
        flushIfPossible()
    }

    func session(
        _ session: WCSession,
        didFinish userInfoTransfer: WCSessionUserInfoTransfer,
        error: Error?
    ) {
        let sessionId = (try? WatchWorkoutSummaryMessage.fromUserInfo(userInfoTransfer.userInfo))??.sessionId

        lock.lock()
        if let sessionId {
            inFlightSessionIds.remove(sessionId)
        }
        lock.unlock()

        guard let sessionId else {
            log.error("didFinish could not decode sessionId from transfer")
            return
        }

        if let error {
            log.error(
                "transferUserInfo failed sessionId=\(sessionId, privacy: .public): \(error.localizedDescription, privacy: .public)"
            )
            return
        }

        do {
            try outbox.remove(sessionId: sessionId)
            log.info("transferUserInfo confirmed sessionId=\(sessionId, privacy: .public)")
        } catch {
            log.error(
                "outbox remove after transfer failed sessionId=\(sessionId, privacy: .public): \(error.localizedDescription, privacy: .public)"
            )
        }
    }

    private func outstandingSessionIds() -> Set<String> {
        var ids = Set<String>()
        for transfer in session.outstandingUserInfoTransfers {
            if let message = try? WatchWorkoutSummaryMessage.fromUserInfo(transfer.userInfo), let message {
                ids.insert(message.sessionId)
            }
        }
        return ids
    }
}
#endif
