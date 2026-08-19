import XCTest
@testable import SimPulse

final class WatchWorkoutSummarySyncTests: XCTestCase {
    private func sampleMessage(sessionId: String = "ABC-123-UUID") -> WatchWorkoutSummaryMessage {
        WatchWorkoutSummaryMessage(
            schemaVersion: WatchWorkoutSummaryWire.schemaVersion,
            sessionId: sessionId,
            startedAt: Date(timeIntervalSince1970: 1_700_000_000),
            endedAt: Date(timeIntervalSince1970: 1_700_001_800),
            durationSeconds: 1_800,
            averageHeartRateBpm: 132,
            maximumHeartRateBpm: 161,
            activeKilocalories: 22.4
        )
    }

    func testMessageRoundTripPreservesFields() throws {
        let original = sampleMessage()

        let userInfo = try original.makeUserInfo()
        let decoded = try XCTUnwrap(WatchWorkoutSummaryMessage.fromUserInfo(userInfo))

        XCTAssertEqual(decoded, original)
    }

    func testMessageFromFactoryMatchesExplicitInit() {
        let explicit = sampleMessage(sessionId: "factory-session")
        let fromFactory = WatchWorkoutSummaryMessage.from(
            sessionId: explicit.sessionId,
            startedAt: explicit.startedAt,
            endedAt: explicit.endedAt,
            durationSeconds: explicit.durationSeconds,
            averageHeartRateBpm: explicit.averageHeartRateBpm,
            maximumHeartRateBpm: explicit.maximumHeartRateBpm,
            activeKilocalories: explicit.activeKilocalories
        )

        XCTAssertEqual(fromFactory, explicit)
    }

    func testUnknownSchemaVersionReturnsNil() throws {
        var future = sampleMessage()
        future.schemaVersion = 99

        let userInfo = try future.makeUserInfo()
        let decoded = try WatchWorkoutSummaryMessage.fromUserInfo(userInfo)

        XCTAssertNil(decoded)
    }

    func testOutboxEnqueueIsIdempotentBySessionId() throws {
        let tempDir = FileManager.default.temporaryDirectory
            .appendingPathComponent(UUID().uuidString, isDirectory: true)
        try FileManager.default.createDirectory(at: tempDir, withIntermediateDirectories: true)
        addTeardownBlock {
            try? FileManager.default.removeItem(at: tempDir)
        }

        let outbox = FileWorkoutSummaryOutbox(outboxDirectory: tempDir)
        let first = sampleMessage()
        var updated = first
        updated.durationSeconds = 2_400

        try outbox.enqueue(first)
        try outbox.enqueue(updated)

        XCTAssertEqual(outbox.pendingCount, 1)
        let pending = try outbox.pendingMessages()
        XCTAssertEqual(pending.count, 1)
        XCTAssertEqual(pending[0].durationSeconds, 2_400)
    }

    func testOutboxQuarantinesUndecodableFile() throws {
        let tempDir = FileManager.default.temporaryDirectory
            .appendingPathComponent(UUID().uuidString, isDirectory: true)
        try FileManager.default.createDirectory(at: tempDir, withIntermediateDirectories: true)
        addTeardownBlock {
            try? FileManager.default.removeItem(at: tempDir)
        }

        let outbox = FileWorkoutSummaryOutbox(outboxDirectory: tempDir)
        let valid = sampleMessage(sessionId: "valid-session")
        try outbox.enqueue(valid)

        let poisonURL = tempDir.appendingPathComponent("poison.json")
        try Data("{ not-json".utf8).write(to: poisonURL)

        XCTAssertEqual(outbox.pendingCount, 2)
        let pending = try outbox.pendingMessages()
        XCTAssertEqual(pending.count, 1)
        XCTAssertEqual(pending[0].sessionId, "valid-session")
        XCTAssertEqual(outbox.pendingCount, 1)

        let quarantineURL = tempDir.appendingPathComponent("quarantine/poison.json")
        XCTAssertTrue(FileManager.default.fileExists(atPath: quarantineURL.path))
    }

    func testIngestDuplicateReturnsFalse() throws {
        let suiteName = "WatchWorkoutSummarySyncTests.\(UUID().uuidString)"
        let defaults = try XCTUnwrap(UserDefaults(suiteName: suiteName))
        addTeardownBlock { UserDefaults.standard.removePersistentDomain(forName: suiteName) }

        let ingest = UserDefaultsWorkoutSummaryIngest(defaults: defaults)
        let message = sampleMessage(sessionId: "dup-session")

        XCTAssertTrue(try ingest.merge(message))
        XCTAssertFalse(try ingest.merge(message))
    }

    func testIngestNewPostsNotification() throws {
        let suiteName = "WatchWorkoutSummarySyncTests.\(UUID().uuidString)"
        let defaults = try XCTUnwrap(UserDefaults(suiteName: suiteName))
        addTeardownBlock { UserDefaults.standard.removePersistentDomain(forName: suiteName) }

        let ingest = UserDefaultsWorkoutSummaryIngest(defaults: defaults)
        let message = sampleMessage(sessionId: "notify-session")
        let expectation = expectation(forNotification: .simpulseWorkoutSummaryMerged, object: nil)

        XCTAssertTrue(try ingest.merge(message))

        wait(for: [expectation], timeout: 1.0)
    }
}
