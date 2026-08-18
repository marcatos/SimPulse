# Product

## Vision

SimPulse answers questions a sim racer actually asks:

- How many active calories did I burn while sim racing?
- What were my average and maximum heart rate?
- How did heart rate evolve during the session?
- What happened in the simulator when HR peaked?
- Does performance deteriorate as physiological load increases?
- How does physiological response differ by car, track, and session type?
- How consistent am I during high-intensity portions of a race?
- How do sessions compare over time?

The differentiator is **synchronization of simulator telemetry and race events with physiological telemetry**.

## Non-goals (near term)

- Medical diagnosis, stress scoring, or recovery advice.
- Cloud accounts, social networks, or advertising.
- Supporting every simulator before iRacing works end-to-end.
- Monetization implementation before the vertical slice exists.

## Users

Solo sim racers using an Apple Watch and a Windows PC running iRacing.

## Tiers (architecture only; StoreKit not implemented)

Entitlement names are stable. Billing is not.

### Free

- Apple Watch workout
- Current / average / maximum HR
- Workout duration
- Active calories
- HealthKit integration
- Limited local history

### Premium (candidate: one-time ~€2.99)

- Unlimited history
- Advanced HR graphs
- Session comparisons and trends
- Manual simulator / car / track metadata
- CSV / data export
- Advanced statistics

### Pro (candidate: one-time ~€10–15)

- Windows Sim Racing Bridge
- Automatic simulator and session detection
- Simulator telemetry, laps, race events
- Biometric / telemetry synchronization
- Performance-under-load analytics
- Advanced Race Reports
- Shareable session cards

See [ADR 0008](adr/0008-entitlements.md).

## MVP vertical slice

1. Start a Sim Racing workout on Apple Watch.
2. Collect HR and active calories.
3. End workout.
4. Synchronize the session to iPhone.
5. Show history and session details.
6. Windows Bridge detects iRacing.
7. Bridge identifies basic session metadata.
8. iPhone receives Bridge session information.
9. SimPulse correlates simulator session and workout.
10. Report shows duration, HR avg/max, active calories, simulator, track, car, and basic laps.

Do not expand scope dramatically before this works.

## Race Report

A future report is a structured model, not a screenshot:

Simulator, track, car, session type, duration, laps, start/finish position, best lap, average/max HR, active calories, peak-HR timestamp, associated race event when known, HR timeline, lap timeline.

Missing fields are represented as unavailable — never invented.

Pipeline:

```text
Session Data → Analytics → RaceReport model → UI / share-card renderer
```

Analytics must not render UI. See [`ARCHITECTURE.md`](ARCHITECTURE.md).
