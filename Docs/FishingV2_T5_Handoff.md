# Fishing V2 T5 Handoff

T4 establishes one authoritative SingleFish V2 fight pipeline:

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

## T5 boundary

T5 may implement Cast -> Nibble -> Bite -> Hook V2 before this pipeline. It
must not redesign the fish AI or feed `FishingFeedbackState` back into it.
Future presentation can map telegraph/head-shake/final-run signals to visual
effects, and future force-feedback can map the head-shake sequence to a torque
pulse without changing the pure AI.
