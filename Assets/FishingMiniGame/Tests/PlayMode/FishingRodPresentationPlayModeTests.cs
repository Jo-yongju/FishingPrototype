using System.Collections;
using FishingMiniGame.Core;
using FishingMiniGame.Runtime;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace FishingMiniGame.Tests
{
    public sealed class FishingRodPresentationPlayModeTests
    {
        [UnityTest]
        public IEnumerator RodFocusedView_UsesControllerInputAndKeepsLineAttached()
        {
            yield return SceneManager.LoadSceneAsync("FishingStandalone", LoadSceneMode.Single);
            yield return null;

            FishingGameController controller = Object.FindAnyObjectByType<FishingGameController>();
            FishingMiniGameFacade facade = Object.FindAnyObjectByType<FishingMiniGameFacade>();
            Assert.That(controller, Is.Not.Null);
            Assert.That(facade, Is.Not.Null);

            facade.ConfigureFlowMode(FishingGameMode.SingleFishSession, controller.Config.BuildSessionFishProfile());
            facade.Initialize(new FishingLaunchContext
            {
                RoundId = "rod-presentation-test",
                CountdownSeconds = 0f,
                Seed = 59
            });
            MockFishingInputSource input = new MockFishingInputSource();
            controller.SetInputSource(input);
            facade.BeginRound();
            yield return null;

            Camera camera = Camera.main;
            Transform rodPose = FindTransform("RodPosePivot");
            Transform rodGrip = rodPose != null ? rodPose.parent : null;
            Transform rodTip = FindTransform("Rod Tip");
            Transform angler = FindTransform("Coastal Angler");
            LineRenderer line = FindLine("Fishing Line");
            Assert.That(camera, Is.Not.Null);
            Assert.That(rodGrip, Is.Not.Null);
            Assert.That(rodPose, Is.Not.Null);
            Assert.That(rodTip, Is.Not.Null);
            Assert.That(angler, Is.Not.Null);
            Assert.That(line, Is.Not.Null);

            Quaternion cameraRotation = camera.transform.rotation;
            Vector3 neutralTipPosition = rodTip.position;
            input.SetNextFrame(new FishingInputFrame
            {
                RodPitch = 0.8f,
                RodYaw = -0.65f,
                MotionStrength = 0.8f,
                TensionNormalized = 0.5f,
                IsDeviceConnected = true
            });
            yield return new WaitForSecondsRealtime(0.35f);

            Assert.That(controller.LastInputFrame.RodPitch, Is.EqualTo(0.8f).Within(0.001f));
            Assert.That(controller.LastInputFrame.RodYaw, Is.EqualTo(-0.65f).Within(0.001f));
            Assert.That(Quaternion.Angle(Quaternion.identity, rodPose.localRotation), Is.GreaterThan(8f));
            Assert.That(Vector3.Distance(neutralTipPosition, rodTip.position), Is.GreaterThan(0.35f));
            Assert.That(Quaternion.Angle(cameraRotation, camera.transform.rotation), Is.LessThan(0.01f));
            Assert.That(camera.fieldOfView, Is.EqualTo(60f).Within(0.01f));
            Assert.That(Vector3.Distance(line.GetPosition(0), rodTip.position), Is.LessThan(0.002f));

            Vector3 gripViewport = camera.WorldToViewportPoint(rodGrip.position);
            Vector3 tipViewport = camera.WorldToViewportPoint(rodTip.position);
            AssertViewportVisible(gripViewport, "Rod grip");
            AssertViewportVisible(tipViewport, "Rod tip");

            Renderer[] bodyRenderers = angler.GetComponentsInChildren<Renderer>(true);
            for (int i = 0; i < bodyRenderers.Length; i++)
            {
                Assert.That(bodyRenderers[i].enabled, Is.False,
                    $"Full-body renderer '{bodyRenderers[i].name}' should be hidden in SingleFishSession view.");
            }
        }

        private static Transform FindTransform(string objectName)
        {
            Transform[] transforms = Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            for (int i = 0; i < transforms.Length; i++)
            {
                if (transforms[i].name == objectName) return transforms[i];
            }
            return null;
        }

        private static LineRenderer FindLine(string objectName)
        {
            LineRenderer[] lines = Object.FindObjectsByType<LineRenderer>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            for (int i = 0; i < lines.Length; i++)
            {
                if (lines[i].name == objectName) return lines[i];
            }
            return null;
        }

        private static void AssertViewportVisible(Vector3 point, string label)
        {
            Assert.That(point.z, Is.GreaterThan(0f), $"{label} is behind the camera.");
            Assert.That(point.x, Is.InRange(0f, 1f), $"{label} is outside the horizontal Game View.");
            Assert.That(point.y, Is.InRange(0f, 1f), $"{label} is outside the vertical Game View.");
        }
    }
}
