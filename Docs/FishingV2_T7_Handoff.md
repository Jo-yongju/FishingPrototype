# Fishing V2 T7 Resistance Feedback Handoff

T7 defines only the normalized game-side reel-resistance request. It consumes the existing public `FishingSnapshot`; it does not read fish-AI internals or change gameplay, presentation, input, networking, IoT, or hardware rules.

## Game-to-output architecture

```text
FishingSnapshot
    -> FishingV2ResistanceMapper
    -> FishingResistanceCommand
    -> IFishingResistanceOutput
    -> MockFishingResistanceOutput (T7)
```

`FishingGameController` owns runtime safety policy and `LastResistanceCommand`. It calls the mapper only while all of these conditions are true: SingleFishSession mode, session Playing, non-null snapshot, player state Fighting, not paused, and input device connected. Otherwise it synchronizes the mapper with `ResetToSafe(snapshot)` and calls the separate `Stop()` output path.

## Command meaning

- `ResistanceNormalized == 0`: a valid active-control command requesting no resistance. It is sent through `ApplyCommand()` and a normal decrease can use fall slew.
- `ResistanceNormalized == 1`: the maximum normalized game request.
- `Stop()`: a separate safety action that bypasses slew and immediately requests torque-off.

Neither the controller nor the mock infers STOP from the numeric command. Both expose `IsResistanceStopped` / `IsStopped` so that active zero and safety STOP remain distinguishable when the mock is replaced.

## Base resistance

The mapper uses behavior plus `V2FishForceNormalized`, not virtual line tension:

| Behavior | Base range |
| --- | --- |
| REST | 0.15–0.25 |
| FIGHT | 0.35–0.50 |
| RUN | 0.65–0.82 |

The ranges preserve `REST < FIGHT < RUN` at every force. Base resistance uses deterministic rise/fall slew of 1.8/sec and 2.8/sec, with delta time clamped to 0–0.1 seconds. `VirtualLineTensionNormalized` remains a gameplay line-risk/presentation signal and does not affect this physical resistance request.

## Final Run and Head Shake

Actual `IsFinalRun` strengthens RUN base resistance with a 1.12 multiplier capped at 0.92. `FinalRunPending` does not boost resistance.

`HeadShakeEventSequence` starts one 0.36-second, non-negative pulse overlay at up to 0.18. The overlay is applied after base slew so it remains a fast effect. Equal sequence values do not restart it; a lower/reset sequence synchronizes without triggering. Every safety reset clears pulse state and synchronizes to the current sequence, preventing stale replay after resume or reconnect.

## Safety and lifecycle

Pause, disconnect, session/round not Playing, non-Fighting state, reset, abort/end, component disable, component destroy, and every LegacyRound path use immediate `ResetToSafe + Stop`. `ResetToSafe(null)` is valid and clears all temporal state.

## Deferred hardware work

T7 does not implement UART, packets, COM ports, current, `Iq_ref`, torque in Nm, motor direction, encoders, worker threads, or a transport scheduler. A future T9 output can replace the mock while preserving the controller contract:

```text
ResistanceNormalized
    -> UART transport
    -> STM32G431
    -> safe Iq_ref mapping
    -> FOC
    -> physical reel resistance
```
