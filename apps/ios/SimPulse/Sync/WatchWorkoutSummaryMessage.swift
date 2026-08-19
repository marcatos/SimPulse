import Foundation

enum WatchWorkoutSummaryWire {
    static let schemaVersion = 1
    static let userInfoKey = "com.marcatos.SimPulse.workoutSummary"
}

struct WatchWorkoutSummaryMessage: Codable, Equatable, Sendable {
    var schemaVersion: Int
    var sessionId: String
    var startedAt: Date
    var endedAt: Date
    var durationSeconds: TimeInterval
    var averageHeartRateBpm: Int?
    var maximumHeartRateBpm: Int?
    var activeKilocalories: Double?
}

extension WatchWorkoutSummaryMessage {
    static func from(
        sessionId: String,
        startedAt: Date,
        endedAt: Date,
        durationSeconds: TimeInterval,
        averageHeartRateBpm: Int?,
        maximumHeartRateBpm: Int?,
        activeKilocalories: Double?
    ) -> WatchWorkoutSummaryMessage {
        WatchWorkoutSummaryMessage(
            schemaVersion: WatchWorkoutSummaryWire.schemaVersion,
            sessionId: sessionId,
            startedAt: startedAt,
            endedAt: endedAt,
            durationSeconds: durationSeconds,
            averageHeartRateBpm: averageHeartRateBpm,
            maximumHeartRateBpm: maximumHeartRateBpm,
            activeKilocalories: activeKilocalories
        )
    }

    func makeUserInfo() throws -> [String: Any] {
        let data = try Self.encoder.encode(self)
        return [WatchWorkoutSummaryWire.userInfoKey: data]
    }

    static func fromUserInfo(_ userInfo: [String: Any]) throws -> WatchWorkoutSummaryMessage? {
        guard let data = userInfo[WatchWorkoutSummaryWire.userInfoKey] as? Data else {
            return nil
        }
        let message = try decoder.decode(WatchWorkoutSummaryMessage.self, from: data)
        guard message.schemaVersion == WatchWorkoutSummaryWire.schemaVersion else {
            return nil
        }
        return message
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
