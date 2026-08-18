import Foundation
import os

enum WorkoutSessionError: Error, Equatable {
    case alreadyRunning
    case notRunning
}

/// Application use case: start/end a Sim Racing workout without coupling to HealthKit.
/// iPhone reachability must not stop recording (WATCH-001).
final class WorkoutSessionController: @unchecked Sendable {
    private let source: WorkoutDataSource
    private let lock = NSLock()
    private var isRunning = false
    private let log = Logger(subsystem: "com.marcatos.SimPulse", category: "workout")

    var snapshots: AsyncStream<WorkoutSnapshot> { source.snapshots }

    init(source: WorkoutDataSource) {
        self.source = source
    }

    func startSimRacing() async throws {
        let startedAt = Date()
        lock.lock()
        if isRunning {
            lock.unlock()
            throw WorkoutSessionError.alreadyRunning
        }
        isRunning = true
        lock.unlock()

        log.info("workout start")
        do {
            try await source.start()
        } catch {
            lock.lock()
            isRunning = false
            lock.unlock()
            log.error("workout start failed: \(error.localizedDescription, privacy: .public)")
            throw error
        }

        let elapsedMs = Int(Date().timeIntervalSince(startedAt) * 1000)
        log.info("workout start completed in \(elapsedMs, privacy: .public) ms")
    }

    func endSimRacing() async throws {
        let startedAt = Date()
        lock.lock()
        if !isRunning {
            lock.unlock()
            throw WorkoutSessionError.notRunning
        }
        lock.unlock()

        log.info("workout end")
        do {
            try await source.stop()
        } catch {
            log.error("workout end failed: \(error.localizedDescription, privacy: .public)")
            throw error
        }

        lock.lock()
        isRunning = false
        lock.unlock()
        let elapsedMs = Int(Date().timeIntervalSince(startedAt) * 1000)
        log.info("workout end completed in \(elapsedMs, privacy: .public) ms")
    }

    func companionReachabilityDidChange(_ reachable: Bool) {
        log.info("companion reachable=\(reachable, privacy: .public); session unchanged")
    }
}
