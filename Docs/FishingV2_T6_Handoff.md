# Fishing V2 T6 Presentation Handoff

T6 adds presentation-only interpretation of the public `FishingSnapshot` contract. It does not change Core gameplay, fish AI, fight metrics, retry schedules, catch/escape rules, IoT, hardware feedback, or networking.

## Presentation architecture

```text
FishingSnapshot
    -> FishingV2PresentationInput
    -> FishingV2PresentationFeedback
    -> FishingV2VisualFrame
    -> FishingWorldView / FishVisualAdapter / AnglerVisualAdapter
```

`FishingV2PresentationFeedback` owns visual sequence consumption and pause-sensitive phases. Its deterministic outputs drive rod load, lateral pull, bobber dip, line sag/jitter, surface disturbance, fish motion, and bounded camera impulses. `FishingV2PresentationTuning` contains only visual values.

## Signal-to-visual mapping

| Visual channel | Public input signals |
| --- | --- |
| Nibble | `IsNibbling`, `NibbleIntensityNormalized`, `NibbleEventSequence` |
| Bite | `BiteEventSequence`, `State == BiteWindow` |
| Hook | `State == Hooked`, `StateElapsedSeconds` |
| Fight / Run / Rest | `V2BehaviorState`, `V2FishForceNormalized`, `V2FishDirectionNormalized` |
| Run telegraph | `IsRunTelegraphing`, `RunTelegraphDirectionNormalized` |
| Head shake | `HeadShakeActive`, `HeadShakeIntensityNormalized`, `HeadShakeEventSequence` |
| Final Run | `FinalRunPending`, `IsFinalRun` |
| Fighting line and rod load | `VirtualLineTensionNormalized`, `VirtualTensionZone` |

Pre-fight line shape follows presentation policy rather than interpreting `VirtualLineTensionNormalized`: Waiting is visibly slack, Nibble is somewhat tauter, Bite is sharply taut, and Hooked remains taut for the Fighting handoff. During Fighting, lower virtual tension produces more midpoint sag and higher virtual tension produces a nearly straight line. Line endpoints remain Rod Tip and Ocean Bobber.

Fight is the medium-load baseline. Rest reduces rod bend, lateral pull, surface disturbance, and fish motion. Run raises all four and uses `V2FishDirectionNormalized`. Run telegraph adds a weaker directional pre-cue using `RunTelegraphDirectionNormalized`. Final Run is a capped multiplier on normal Run rather than a new gameplay or presentation state. Head shake overlays a short, fast rod/line/surface/fish accent.

## One-shot consumption and pause

Nibble, Bite, and Head Shake consumers follow this exact rule:

```text
current == last: no event
current > last: trigger once and set last = current
current < last: cycle/fish reset; set last = current without triggering
```

A reset to zero is never an event. Visual pulse timers, head-shake phase, line jitter, and camera impulse phase advance only while `FishingGameController.IsPaused` is false. Ambient ocean/scenery animation retains its existing policy.

Camera feedback stores the SingleFish base transform and evaluates every frame as base position plus transient offset and base rotation multiplied by transient rotation. Bite, Head Shake, and Final Run start can create small capped impulses; ordinary `RodPitch` / `RodYaw` input never changes the camera. Every impulse returns exactly to the base transform without accumulation.

## T7 public signals

The existing public signals available to future hardware feedback work are:

- `V2BehaviorState`
- `V2FishForceNormalized`
- `V2FishDirectionNormalized`
- `VirtualLineTensionNormalized`
- `VirtualTensionZone`
- `HeadShakeActive`
- `HeadShakeIntensityNormalized`
- `HeadShakeEventSequence`
- `IsRunTelegraphing`
- `RunTelegraphDirectionNormalized`
- `FinalRunPending`
- `IsFinalRun`

T6 deliberately does not define a `ResistanceCommand`, UART packet, `Iq_ref`, motor direction, or physical-device mapping. T7 must consume the existing public signals without reading `FishingV2FishAI` internals or coupling Core to a transport.
