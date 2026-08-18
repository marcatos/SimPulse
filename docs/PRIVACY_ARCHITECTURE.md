# Privacy architecture

SimPulse treats biometric information as sensitive. Core product is **local-first**.

## Collected data

| Data | Source | Purpose |
| --- | --- | --- |
| Heart rate samples | HealthKit / Watch workout | Session metrics and correlation |
| Active energy | HealthKit / Watch workout | Calorie summary |
| Workout start/end | HealthKit | Session bounds |
| Simulator name, track, car, session type | iRacing via Bridge (or manual) | Context |
| Laps and race events | Simulator adapter | Correlation |
| Device pairing identity | Generated IDs | Trust the PC |

No advertising identifiers. No third-party analytics SDK by default.

## Storage location

- **Workouts and raw biometric samples:** HealthKit on the user's Apple devices. SimPulse reads what it is authorized to read.
- **SimPulse session metadata and derived reports:** planned SwiftData store in the iPhone app container.
- **Bridge:** session metadata in memory during a live session. Trusted device list on disk. **No heart-rate payloads on the PC disk** in the intended architecture.
- **No cloud store** in Phases 0–6.

## Retention

- HealthKit retention is controlled by the user and Apple.
- SimPulse local history: Free tier will cap count; Premium/Pro unlimited. Deletion must be possible from Settings (not implemented yet).
- Bridge trusted-device list: until the user revokes.

## Transmission

| Flow | Payload | Condition |
| --- | --- | --- |
| Watch → iPhone | Workout summary / samples via WatchConnectivity | Same Apple ID / paired Watch |
| Bridge → iPhone | Normalized simulator metadata, telemetry, events | After pairing, LAN only |
| iPhone → Bridge | Pairing, time-sync, session identifiers | After pairing |
| iPhone → Apple | HealthKit (on device) | User permission |
| Anywhere → SimPulse cloud | None | Not implemented |

Heart-rate samples are **not** sent to the Windows Bridge in the intended architecture. Correlation happens on iPhone.

If a future design needs biometrics on the PC, that is a new ADR and a privacy review, not a silent change.

## Deletion

Planned: in-app delete session, delete all SimPulse data, revoke Bridge pairing. HealthKit data is deleted through Apple Health, not silently by SimPulse.

Not implemented in Phase 0.

## HealthKit usage

Read/write workout sessions as `HKWorkoutActivityType.other` named Sim Racing (`com.marcatos.SimPulse.activity` metadata). There is no honest HealthKit sport type for sim racing. Request only HR, active energy, and workout types. Usage strings must be accurate. Never log raw HR or energy values.

## Windows Bridge communication

See [`SECURITY.md`](SECURITY.md). Simulator telemetry is not biometric data but is still user activity data. It stays on LAN.

## Future cloud implications

Possible later: backup, cross-device sync, team/season stats, web dashboard. That would require:

- Explicit opt-in
- Encryption in transit
- Documented retention
- Account deletion
- Privacy policy URL for App Store

Do not pre-build a backend. Do keep local IDs stable enough to map later.
