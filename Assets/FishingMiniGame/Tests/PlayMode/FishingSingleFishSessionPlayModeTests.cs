using System;
using System.Collections;
using FishingMiniGame.Core;
using FishingMiniGame.Runtime;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace FishingMiniGame.Tests
{
    public sealed class FishingSingleFishSessionPlayModeTests
    {
        [UnityTest]
        public IEnumerator Catch_CompletesSessionAndDoesNotSelectNextFish()
        {
            SessionFixture fixture = new SessionFixture();
            yield return LoadSingleSession(fixture);
            FishingGameController controller = fixture.Controller;
            FishingMiniGameFacade facade = fixture.Facade;
            string fishId = controller.Snapshot.FishId;
            MockFishingInputSource input = new MockFishingInputSource();
            controller.SetInputSource(input);
            int completionCount = 0;
            facade.SessionCompleted += _ => completionCount++;
            facade.BeginRound();

            yield return CatchCurrentFish(controller, input, 25f);
            Assert.That(controller.SessionSnapshot.State, Is.EqualTo(FishingSessionState.Completed));
            Assert.That(controller.LastSessionResult.Outcome, Is.EqualTo(FishingSessionOutcome.Caught));
            Assert.That(completionCount, Is.EqualTo(1));
            Assert.That(controller.Snapshot.State, Is.EqualTo(FishingPlayerState.Caught));

            yield return new WaitForSecondsRealtime(2.6f);
            Assert.That(controller.Snapshot.State, Is.EqualTo(FishingPlayerState.Caught));
            Assert.That(controller.Snapshot.FishId, Is.EqualTo(fishId));
            Assert.That(completionCount, Is.EqualTo(1));
        }

        [UnityTest]
        public IEnumerator PostHookEscape_CompletesSessionAndDoesNotSelectNextFish()
        {
            SessionFixture fixture = new SessionFixture();
            yield return LoadSingleSession(fixture);
            FishingGameController controller = fixture.Controller;
            FishingMiniGameFacade facade = fixture.Facade;
            string fishId = controller.Snapshot.FishId;
            MockFishingInputSource input = new MockFishingInputSource();
            controller.SetInputSource(input);
            facade.BeginRound();

            yield return EnterFighting(controller, input);
            float deadline = Time.realtimeSinceStartup + 8f;
            while (controller.Snapshot.State == FishingPlayerState.Fighting && Time.realtimeSinceStartup < deadline)
            {
                input.SetNextFrame(Frame(tension: 1f));
                yield return null;
            }

            Assert.That(controller.Snapshot.State, Is.EqualTo(FishingPlayerState.Escaped));
            Assert.That(controller.SessionSnapshot.State, Is.EqualTo(FishingSessionState.Completed));
            Assert.That(controller.LastSessionResult.Outcome, Is.EqualTo(FishingSessionOutcome.Escaped));
            Assert.That(controller.LastSessionResult.CycleResult.EscapeReason, Is.EqualTo(FishingEscapeReason.LineBroken));

            yield return new WaitForSecondsRealtime(2.6f);
            Assert.That(controller.Snapshot.State, Is.EqualTo(FishingPlayerState.Escaped));
            Assert.That(controller.Snapshot.FishId, Is.EqualTo(fishId));
        }

        [UnityTest]
        public IEnumerator MissedBite_IsRetryWithSameFishAndSessionStillPlaying()
        {
            SessionFixture fixture = new SessionFixture();
            yield return LoadSingleSession(fixture);
            FishingGameController controller = fixture.Controller;
            FishingMiniGameFacade facade = fixture.Facade;
            string fishId = controller.Snapshot.FishId;
            MockFishingInputSource input = new MockFishingInputSource();
            controller.SetInputSource(input);
            facade.BeginRound();

            yield return CastAndWaitForBite(controller, input);
            yield return WaitUntilOrFail(
                () => controller.Snapshot.State == FishingPlayerState.Idle,
                6f,
                "A missed bite did not return to Idle for a retry.");

            Assert.That(controller.SessionSnapshot.State, Is.EqualTo(FishingSessionState.Playing));
            Assert.That(controller.LastSessionResult, Is.Null);
            Assert.That(controller.SessionSnapshot.PreHookFailureCount, Is.EqualTo(1));
            Assert.That(controller.Snapshot.FishId, Is.EqualTo(fishId));

            input.SetNextFrame(Frame(castPressed: true));
            yield return null;
            Assert.That(controller.Snapshot.State, Is.EqualTo(FishingPlayerState.Casting));
            Assert.That(controller.Snapshot.FishId, Is.EqualTo(fishId));
        }

        [UnityTest]
        public IEnumerator LegacyRoundDuration_DoesNotEndSingleFishSession()
        {
            SessionFixture fixture = new SessionFixture();
            yield return LoadSingleSession(fixture, 0.1f);
            FishingGameController controller = fixture.Controller;
            FishingMiniGameFacade facade = fixture.Facade;
            facade.BeginRound();

            yield return new WaitForSecondsRealtime(0.5f);

            Assert.That(controller.SessionSnapshot.State, Is.EqualTo(FishingSessionState.Playing));
            Assert.That(controller.LastSessionResult, Is.Null);
            Assert.That(controller.RoundSnapshot.State, Is.EqualTo(FishingRoundState.Ready));
        }

        private static IEnumerator LoadSingleSession(
            SessionFixture fixture,
            float legacyRoundDuration = 180f)
        {
            yield return SceneManager.LoadSceneAsync("FishingStandalone", LoadSceneMode.Single);
            yield return null;

            FishingGameController controller = UnityEngine.Object.FindAnyObjectByType<FishingGameController>();
            FishingMiniGameFacade facade = UnityEngine.Object.FindAnyObjectByType<FishingMiniGameFacade>();
            Assert.That(controller, Is.Not.Null);
            Assert.That(facade, Is.Not.Null);
            fixture.Controller = controller;
            fixture.Facade = facade;
            FishProfile fish = controller.Config.BuildSessionFishProfile();
            facade.ConfigureFlowMode(FishingGameMode.SingleFishSession, fish);
            facade.Initialize(new FishingLaunchContext
            {
                RoundId = "single-fish-playmode",
                LocalParticipantId = "tester",
                RoundDurationSeconds = legacyRoundDuration,
                CountdownSeconds = 0f,
                Seed = 31
            });
            Assert.That(controller.Mode, Is.EqualTo(FishingGameMode.SingleFishSession));
            Assert.That(controller.SessionSnapshot.FishId, Is.EqualTo(fish.FishId));
        }

        private sealed class SessionFixture
        {
            public FishingGameController Controller;
            public FishingMiniGameFacade Facade;
        }

        private static IEnumerator CatchCurrentFish(
            FishingGameController controller,
            MockFishingInputSource input,
            float fightTimeout)
        {
            yield return EnterFighting(controller, input);

            float deadline = Time.realtimeSinceStartup + fightTimeout;
            float rawTension = 0.5f;
            while (controller.Snapshot.State == FishingPlayerState.Fighting && Time.realtimeSinceStartup < deadline)
            {
                rawTension = Mathf.Clamp01(rawTension + (0.52f - controller.Snapshot.TensionNormalized) * 0.85f);
                bool running = controller.Snapshot.Feedback.State == FishingFeedbackState.Run;
                float counterYaw = running ? -controller.Snapshot.FightDirection : 0f;
                float reel = running ? 0.35f : 1f;
                input.SetNextFrame(Frame(tension: rawTension, reel: reel, rodYaw: counterYaw));
                yield return null;
            }

            Assert.That(controller.Snapshot.State, Is.EqualTo(FishingPlayerState.Caught),
                "The configured single fish was not caught with adaptive mock input.");
        }

        private static IEnumerator EnterFighting(
            FishingGameController controller,
            MockFishingInputSource input)
        {
            yield return CastAndWaitForBite(controller, input);
            input.SetNextFrame(Frame(hookPressed: true));
            yield return null;
            Assert.That(controller.Snapshot.State, Is.EqualTo(FishingPlayerState.Hooked));
            yield return WaitUntilOrFail(
                () => controller.Snapshot.State == FishingPlayerState.Fighting,
                2f,
                "The hooked fish did not enter Fighting.");
        }

        private static IEnumerator CastAndWaitForBite(
            FishingGameController controller,
            MockFishingInputSource input)
        {
            yield return WaitUntilOrFail(
                () => controller.SessionSnapshot.State == FishingSessionState.Playing &&
                      controller.Snapshot.State == FishingPlayerState.Idle,
                2f,
                "The single-fish session did not become playable.");
            input.SetNextFrame(Frame(castPressed: true));
            yield return null;
            input.SetNextFrame(Frame(castReleased: true));
            yield return null;
            Assert.That(controller.Snapshot.State, Is.EqualTo(FishingPlayerState.Waiting));
            yield return WaitUntilOrFail(
                () => controller.Snapshot.State == FishingPlayerState.BiteWindow,
                6f,
                "The configured fish did not bite.");
        }

        private static FishingInputFrame Frame(
            bool castPressed = false,
            bool castReleased = false,
            bool hookPressed = false,
            float tension = 0.5f,
            float reel = 0f,
            float rodYaw = 0f)
        {
            return new FishingInputFrame
            {
                CastPressed = castPressed,
                CastReleased = castReleased,
                HookPressed = hookPressed,
                TensionNormalized = tension,
                ReelDelta = reel,
                RodYaw = rodYaw,
                IsDeviceConnected = true
            };
        }

        private static IEnumerator WaitUntilOrFail(Func<bool> condition, float timeoutSeconds, string message)
        {
            float deadline = Time.realtimeSinceStartup + timeoutSeconds;
            while (!condition() && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.That(condition(), Is.True, message);
        }
    }
}
