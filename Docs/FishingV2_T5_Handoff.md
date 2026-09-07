# Fishing V2 T6 Presentation Handoff

T5 establishes a device-agnostic SingleFish V2 pre-fight flow before the
existing T4 fight pipeline:

```text
CastPressed / CastReleased
    -> Waiting attempt
    -> Nibble event + observed active duration
    -> observed post-nibble gap
    -> Bite event / BiteWindow
    -> HookPressed
    -> Hooked
    -> Fighting
```

`FishingStateMachine` consumes semantic `FishingInputFrame` values only. It
does not know whether they came from keyboard, mock input, IMU, serial, or a
future network adapter.

## Pre-fight snapshot contract

- Nibble active state: `IsNibbling`, `NibbleRemainingSeconds`,
  `NibbleIntensityNormalized`.
- Nibble one-shot signal: `NibbleEventSequence` increments exactly once when a
  new nibble begins.
- Bite one-shot signal: `BiteEventSequence` increments exactly once on entry to
  `BiteWindow`.
- Hook timing: `HookWindowRemainingSeconds`.
- Same-fish retry counters: `EarlyHookCount`, `MissedBiteRetryCount`.

Consumers should detect a new one-shot event by comparing the current sequence
with their last consumed sequence. A reset to zero means a new fish/cycle; it is
not an event.

Every V2 bite is preceded by one observed nibble. The bite gate is based on the
actual observed nibble end, so a coarse frame cannot collapse nibble start,
nibble duration, post-nibble gap, and bite into one update. Early hooks and
missed bites start a new schedule for the same fish without `CycleFinished`.

## Existing fight pipeline

T4 remains the single authoritative fight pipeline:

```text
FishingV2FishAI
    -> FishingV2FishAIOutput
    -> FishingV2BehaviorSample
    -> FishingV2FightModel
```

`FishingV2FishAI` owns Fight / Run / Rest decisions and event signals. It does
not calculate distance, stamina, virtual tension, risks, catch, or escape.
`FishingV2FightModel` owns those T3 metrics. `FishingStateMachine` is the only
integration layer allowed to turn metric outcomes into Caught / Escaped.

## Public signals for future consumers

- Run telegraph: `IsRunTelegraphing`, `RunTelegraphDirectionNormalized`,
  `RunTelegraphRemainingSeconds`.
- Head shake: `HeadShakeActive`, `HeadShakeIntensityNormalized`,
  `HeadShakeEventSequence`.
- Final run: `FinalRunDecisionMade`, `FinalRunPending`, `IsFinalRun`,
  `FinalRunUsed`.

All signals are mirrored onto `FishingSnapshot`; presentation and hardware
adapters must consume the snapshot/output rather than reading AI internals.

For pulse-style consumers, a Head Shake is new only when
`HeadShakeActive && HeadShakeEventSequence > LastConsumedSequence`. Set
`LastConsumedSequence` to the observed sequence after consuming it and reset
the consumer value to `0` when a new fish fight starts. A reset from a prior
sequence to `0` is not an event.

T6 presentation must consume these snapshot signals without changing Core
state, retry schedules, AI behavior, or fight metrics. Visual rod bend, line,
splash, bite reaction, head-shake animation, and camera reaction remain T6
work. Hardware feedback and device adapters remain later boundaries.
