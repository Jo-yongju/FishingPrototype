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

T7 does not implement the hardware paths below. It does not define UART packets, COM ports, a transport rate or timeout, motor current, torque, motor direction, encoders, worker threads, or a transport scheduler. In particular, Protocol V1 details such as `R,000–100`, 50 Hz, or a 300 ms timeout remain open for final comparison during T8/T9 integration.

Future physical resistance output path:

```text
Unity / PC
    -> ResistanceNormalized 0.0–1.0
    -> ESP32-S3
    -> UART
    -> STM32G431
    -> Safety / rate limiter
    -> safe Resistance -> Iq_ref mapping
    -> Sensored FOC
    -> BLDC
    -> physical reel crank resistance
```

Future hardware input paths:

```text
Rod IMU / controls
    -> ESP32-S3
    -> Unity

STM32G431 encoder telemetry
    -> ESP32-S3
    -> Unity ReelDelta
```

`ResistanceNormalized == 0.0` continues to mean an active-control request for no resistance, not STOP. `Stop()` remains the separate safety action used for pause, disconnect, abort, session end, and equivalent unsafe lifecycle states; it bypasses slew/ramp and requests immediate torque-off.

Unity's base slew shapes the game feel of REST, FIGHT, RUN, and Final Run. The Head Shake signal remains a fast pulse overlay applied after that base slew. A separate STM32G431 rate limiter will be defined during real hardware integration as a safety limiter and tuned on the physical system so that it does not excessively flatten the Head Shake overlay.

Actual `Iq` in amperes, torque in N·m, and `IQ_MAX_SAFE` are not fixed by T7. They must be measured and selected after STM32G431 hardware bring-up.
