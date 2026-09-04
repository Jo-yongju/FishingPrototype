using FishingMiniGame.Core;
using UnityEngine;

namespace FishingMiniGame.Runtime
{
    public sealed class KeyboardFishingInputSource : IFishingInputSource
    {
        private const float TensionAdjustPerSecond = 0.48f;
        private readonly string _participantId;
        private long _sequence;
        private float _mockTension = 0.5f;

        public KeyboardFishingInputSource(string participantId)
        {
            _participantId = string.IsNullOrWhiteSpace(participantId) ? "local-player" : participantId;
        }

        public bool IsConnected => true;
        public float MockTension => _mockTension;

        public FishingInputFrame ReadFrame()
        {
            float tensionDirection = 0f;
            if (Input.GetKey(KeyCode.E)) tensionDirection += 1f;
            if (Input.GetKey(KeyCode.Q)) tensionDirection -= 1f;
            _mockTension = Mathf.Clamp01(_mockTension + (tensionDirection * TensionAdjustPerSecond * Time.unscaledDeltaTime));

            float rodPitch = 0f;
            if (Input.GetKey(KeyCode.W)) rodPitch += 1f;
            if (Input.GetKey(KeyCode.S)) rodPitch -= 1f;

            float rodYaw = 0f;
            if (Input.GetKey(KeyCode.D)) rodYaw += 1f;
            if (Input.GetKey(KeyCode.A)) rodYaw -= 1f;

            bool spaceDown = Input.GetKeyDown(KeyCode.Space);
            return new FishingInputFrame
            {
                ParticipantId = _participantId,
                Sequence = ++_sequence,
                TimestampSeconds = Time.unscaledTimeAsDouble,
                CastPressed = spaceDown,
                CastReleased = Input.GetKeyUp(KeyCode.Space),
                HookPressed = spaceDown,
                ReelDelta = Input.GetKey(KeyCode.R) || Input.GetMouseButton(0) ? 1f : 0f,
                TensionNormalized = _mockTension,
                RodPitch = rodPitch,
                RodYaw = rodYaw,
                MotionStrength = Mathf.Max(Mathf.Abs(rodPitch), Mathf.Abs(rodYaw)),
                IsDeviceConnected = true
            };
        }

        public void ResetState()
        {
            _sequence = 0;
            _mockTension = 0.5f;
        }
    }
}
