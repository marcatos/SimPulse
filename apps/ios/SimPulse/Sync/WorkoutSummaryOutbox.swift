import Foundation
import os

protocol WorkoutSummaryOutbox: Sendable {
    func enqueue(_ message: WatchWorkoutSummaryMessage) throws
    var pendingCount: Int { get }
    func pendingMessages() throws -> [WatchWorkoutSummaryMessage]
    func remove(sessionId: String) throws
}

final class FileWorkoutSummaryOutbox: WorkoutSummaryOutbox, @unchecked Sendable {
    private let fileManager: FileManager
    private let outboxDirectory: URL
    private let log = Logger(subsystem: "com.marcatos.SimPulse", category: "outbox")

    init(fileManager: FileManager = .default, outboxDirectory: URL? = nil) {
        self.fileManager = fileManager
        if let outboxDirectory {
            self.outboxDirectory = outboxDirectory
        } else {
            let appSupport = fileManager.urls(for: .applicationSupportDirectory, in: .userDomainMask).first
                ?? fileManager.temporaryDirectory
            self.outboxDirectory = appSupport
                .appendingPathComponent("SimPulse/outbox", isDirectory: true)
        }
    }

    func enqueue(_ message: WatchWorkoutSummaryMessage) throws {
        try ensureDirectoryExists()
        let data = try Self.encoder.encode(message)
        try data.write(to: fileURL(for: message.sessionId), options: .atomic)
    }

    var pendingCount: Int {
        (try? jsonFileURLs().count) ?? 0
    }

    func pendingMessages() throws -> [WatchWorkoutSummaryMessage] {
        try ensureDirectoryExists()
        return try jsonFileURLs().compactMap { url in
            do {
                let data = try Data(contentsOf: url)
                return try Self.decoder.decode(WatchWorkoutSummaryMessage.self, from: data)
            } catch {
                log.error(
                    "undecodable outbox file=\(url.lastPathComponent, privacy: .public) error=\(error.localizedDescription, privacy: .public)"
                )
                quarantine(fileAt: url)
                return nil
            }
        }
    }

    func remove(sessionId: String) throws {
        let url = fileURL(for: sessionId)
        guard fileManager.fileExists(atPath: url.path) else { return }
        try fileManager.removeItem(at: url)
    }

    private func fileURL(for sessionId: String) -> URL {
        outboxDirectory.appendingPathComponent("\(sessionId).json")
    }

    private func jsonFileURLs() throws -> [URL] {
        try ensureDirectoryExists()
        let urls = try fileManager.contentsOfDirectory(
            at: outboxDirectory,
            includingPropertiesForKeys: [.isRegularFileKey]
        )
        return urls.filter { url in
            url.pathExtension == "json" && !url.hasDirectoryPath
        }
    }

    private func quarantine(fileAt url: URL) {
        do {
            let quarantineDirectory = outboxDirectory.appendingPathComponent("quarantine", isDirectory: true)
            if !fileManager.fileExists(atPath: quarantineDirectory.path) {
                try fileManager.createDirectory(at: quarantineDirectory, withIntermediateDirectories: true)
            }
            let destination = quarantineDirectory.appendingPathComponent(url.lastPathComponent)
            if fileManager.fileExists(atPath: destination.path) {
                try fileManager.removeItem(at: destination)
            }
            try fileManager.moveItem(at: url, to: destination)
        } catch {
            log.error(
                "quarantine failed file=\(url.lastPathComponent, privacy: .public); deleting error=\(error.localizedDescription, privacy: .public)"
            )
            try? fileManager.removeItem(at: url)
        }
    }

    private func ensureDirectoryExists() throws {
        if !fileManager.fileExists(atPath: outboxDirectory.path) {
            try fileManager.createDirectory(at: outboxDirectory, withIntermediateDirectories: true)
        }
    }

    private static let encoder: JSONEncoder = {
        let encoder = JSONEncoder()
        encoder.dateEncodingStrategy = .secondsSince1970
        return encoder
    }()

    private static let decoder: JSONDecoder = {
        let decoder = JSONDecoder()
        decoder.dateDecodingStrategy = .secondsSince1970
        return decoder
    }()
}
