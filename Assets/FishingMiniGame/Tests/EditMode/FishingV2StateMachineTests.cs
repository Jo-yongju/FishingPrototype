using FishingMiniGame.Core;
using NUnit.Framework;

namespace FishingMiniGame.Tests
{
    public sealed class FishingV2StateMachineTests
    {
        [Test]
        public void HookedToFighting_InitializesMetricsWithoutPreFightRisk()
        {
            FishingStateMachine machine = CreateMachine(new FishingV2FightTuning
            {
                InitialDistanceMeters = 12f,
                InitialVirtualTensionNormalized = 1f,
                BreakStressPerSecond = 10f
            });

            DriveToHooked(machine, 0f);
            Assert.That(machine.Current.State, Is.EqualTo(FishingPlayerState.Hooked));
            Assert.That(machine.Current.BreakStressNormalized, Is.Zero);
            Assert.That(machine.Current.HookLooseRiskNormalized, Is.Zero);

            machine.Tick(Frame(), 0.01f);
            Assert.That(machine.Current.State, Is.EqualTo(FishingPlayerState.Fighting));
            Assert.That(machine.Current.FishDistanceMeters, Is.EqualTo(12f));
            Assert.That(machine.Current.FishStaminaNormalized, Is.EqualTo(1f));
            Assert.That(machine.Current.BreakStressNormalized, Is.Zero);
        }

        [Test]
        public void CanCatch_TransitionsStateMachineToCaught()
        {
            FishingStateMachine machine = CreateMachine(new FishingV2FightTuning
            {
                InitialDistanceMeters = 1f,
                CatchDistanceMeters = 1f,
                CatchStaminaNormalized = 1f
            });
            DriveToFighting(machine, 0.5f);

            machine.Tick(Frame(reel: 0f, pitch: 0f), 0.1f);

            Assert.That(machine.Current.State, Is.EqualTo(FishingPlayerState.Caught));
            Assert.That(machine.LastResult.WasCaught, Is.True);
        }

        [Test]
        public void LineBrokenCondition_TransitionsStateMachineToMatchingEscape()
        {
            FishingStateMachine machine = CreateMachine(new FishingV2FightTuning
            {
                InitialVirtualTensionNormalized = 1f,
                TensionFallPerSecond = 0.01f,
                BreakStressPerSecond = 4f,
                SevereBreakStressBonusPerSecond = 4f
            });
            DriveToFighting(machine, 0f);

            machine.Tick(Frame(reel: 1f, yaw: -1f), 0.25f);

            Assert.That(machine.Current.State, Is.EqualTo(FishingPlayerState.Escaped));
            Assert.That(machine.Current.EscapeReason, Is.EqualTo(FishingEscapeReason.LineBroken));
        }

        [Test]
        public void SlackLineCondition_TransitionsStateMachineToMatchingEscape()
        {
            FishingStateMachine machine = CreateMachine(new FishingV2FightTuning
            {
                InitialVirtualTensionNormalized = 0f,
                TensionRisePerSecond = 0.01f,
                LooseRiskPerSecond = 4f
            });
            DriveToFighting(machine, 1f);

            machine.Tick(Frame(pitch: -1f), 0.25f);

            Assert.That(machine.Current.State, Is.EqualTo(FishingPlayerState.Escaped));
            Assert.That(machine.Current.EscapeReason, Is.EqualTo(FishingEscapeReason.SlackLine));
        }

        [Test]
        public void InputTension_IsNotCopiedIntoSingleFishVirtualTension()
        {
            FishingStateMachine lowInput = CreateMachine(new FishingV2FightTuning());
            FishingStateMachine highInput = CreateMachine(new FishingV2FightTuning());
            DriveToFighting(lowInput, 0f);
            DriveToFighting(highInput, 1f);

            lowInput.Tick(Frame(tension: 0f), 0.1f);
            highInput.Tick(Frame(tension: 1f), 0.1f);

            Assert.That(lowInput.Current.RawTensionNormalized, Is.EqualTo(0f));
            Assert.That(highInput.Current.RawTensionNormalized, Is.EqualTo(1f));
            Assert.That(lowInput.Current.VirtualLineTensionNormalized,
                Is.EqualTo(highInput.Current.VirtualLineTensionNormalized).Within(0.0001f));
            Assert.That(lowInput.Current.TensionNormalized,
                Is.EqualTo(lowInput.Current.VirtualLineTensionNormalized).Within(0.0001f));
        }

        private static FishingStateMachine CreateMachine(FishingV2FightTuning tuning)
        {
            FishProfile fish = new FishProfile
            {
                UseSpeciesRuleOverrides = true,
                MinBiteDelaySeconds = 0.05f,
                MaxBiteDelaySeconds = 0.05f,
                HookWindowSeconds = 1f,
                RunChance = 0f,
                RestChance = 1f,
                MinBehaviorPhaseSeconds = 10f,
                MaxBehaviorPhaseSeconds = 10f
            };
            FishingRules rules = new FishingRules
            {
                MinBiteDelaySeconds = 0.05f,
                MaxBiteDelaySeconds = 0.05f,
                HookWindowSeconds = 1f,
                HookSettleSeconds = 0f
            };
            FishingStateMachine machine = new FishingStateMachine();
            machine.Initialize(new FishingRoundContext
            {
                Seed = 73,
                Rules = rules,
                Fish = fish,
                UseV2FightModel = true,
                V2FightTuning = tuning
            });
            return machine;
        }

        private static void DriveToHooked(FishingStateMachine machine, float tension)
        {
            machine.Tick(Frame(castPressed: true, tension: tension), 0.1f);
            machine.Tick(Frame(castReleased: true, tension: tension), 0.1f);
            machine.Tick(Frame(tension: tension), 0.1f);
            Assert.That(machine.Current.State, Is.EqualTo(FishingPlayerState.BiteWindow));
            machine.Tick(Frame(hookPressed: true, tension: tension), 0.01f);
        }

        private static void DriveToFighting(FishingStateMachine machine, float tension)
        {
            DriveToHooked(machine, tension);
            machine.Tick(Frame(tension: tension), 0.01f);
            Assert.That(machine.Current.State, Is.EqualTo(FishingPlayerState.Fighting));
        }

        private static FishingInputFrame Frame(
            bool castPressed = false,
            bool castReleased = false,
            bool hookPressed = false,
            float reel = 0f,
            float tension = 0.5f,
            float pitch = 0f,
            float yaw = 0f)
        {
            return new FishingInputFrame
            {
                CastPressed = castPressed,
                CastReleased = castReleased,
                HookPressed = hookPressed,
                ReelDelta = reel,
                TensionNormalized = tension,
                RodPitch = pitch,
                RodYaw = yaw,
                IsDeviceConnected = true
            };
        }
    }
}
