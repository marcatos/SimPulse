#if os(iOS)
import Foundation
import os
import WatchConnectivity

/// Receives queued workout summaries from the paired Watch via `transferUserInfo`.
final class WatchConnectivitySummaryReceiver: NSObject, WCSessionDelegate, @unchecked Sendable {
    private let ingest: WorkoutSummaryIngest
    private let session: WCSession
    private let log = Logger(subsystem: "com.marcatos.SimPulse", category: "wc-receiver")

    init(ingest: WorkoutSummaryIngest, session: WCSession = .default) {
        self.ingest = ingest
        self.session = session
        super.init()
    }

    static func live() -> WatchConnectivitySummaryReceiver {
        WatchConnectivitySummaryReceiver(ingest: UserDefaultsWorkoutSummaryIngest())
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
    }

    func sessionDidBecomeInactive(_ session: WCSession) {
        log.info("session became inactive")
    }

    func sessionDidDeactivate(_ session: WCSession) {
        log.info("session deactivated; reactivating")
        session.activate()
    }

    func session(_ session: WCSession, didReceiveUserInfo userInfo: [String: Any] = [:]) {
        guard userInfo[WatchWorkoutSummaryWire.userInfoKey] != nil else {
            return
        }

        guard let data = userInfo[WatchWorkoutSummaryWire.userInfoKey] as? Data else {
            log.error("workout summary payload is not Data")
            return
        }

        do {
            let message = try Self.decoder.decode(WatchWorkoutSummaryMessage.self, from: data)
            guard message.schemaVersion == WatchWorkoutSummaryWire.schemaVersion else {
                log.warning(
                    "unknown workout summary schema version=\(message.schemaVersion, privacy: .public)"
                )
                return
            }
            let merged = try ingest.merge(message)
            log.info(
                "workout summary merged sessionId=\(message.sessionId, privacy: .public) new=\(merged, privacy: .public)"
            )
        } catch {
            log.error("workout summary decode failed: \(error.localizedDescription, privacy: .public)")
        }
    }

    private static let decoder: JSONDecoder = {
        let decoder = JSONDecoder()
        decoder.dateDecodingStrategy = .secondsSince1970
        return decoder
    }()
}
#endif
