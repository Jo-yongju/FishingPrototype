using System;
using System.Collections.Generic;
using FishingMiniGame.Core;
using UnityEngine;

namespace FishingMiniGame.Runtime
{
    [DisallowMultipleComponent]
    public sealed class FishingGameController : MonoBehaviour
    {
        [SerializeField] private FishingGameConfigAsset config;
        [SerializeField] private bool autoStartRound;
        [SerializeField] private bool logStateChanges = true;

        private IFishingInputSource _inputSource;
        private IFishingFeedbackOutput _feedbackOutput;
        private LocalFishingAuthority _authority;
        private FishingRoundTracker _roundTracker;
        private FishingLaunchContext _launchContext;
        private FishingRules _baseRules;
        private FishProfile[] _fishCatalog;
        private System.Random _fishRandom;
        private FishingPlayerState _lastLoggedState;
        private int _nextFishIndex;
        private bool _prepareNextFish;
        private bool _paused;
        private bool _initialized;

        public FishingSnapshot Snapshot => _authority?.Current;
        public FishingCycleResult LastResult => _authority?.LastResult;
        public FishingRoundSnapshot RoundSnapshot => _roundTracker?.Current;
        public FishingRoundResult LastRoundResult => _roundTracker?.Result;
        public IReadOnlyList<FishingCatchRecord> CatchHistory => _roundTracker?.Catches;
        public FishingFeedbackFrame LastFeedback => _feedbackOutput is MockFishingFeedbackOutput mock
            ? mock.LastFrame
            : Snapshot?.Feedback ?? default;
        public FishingGameConfigAsset Config => config;
        public bool IsPaused => _paused;
        public event Action<FishingCycleResult> CycleFinished;
        public event Action<FishingRoundResult> RoundFinished;

        private void Awake()
        {
            InitializeRuntime();
        }

        private void OnEnable()
        {
            InitializeRuntime();
        }

        private void Start()
        {
            if (autoStartRound) BeginRound();
        }

        private void Update()
        {
            if (!HasRuntimeDependencies()) InitializeRuntime();
            if (!HasRuntimeDependencies() || _paused) return;

            float deltaTime = Time.unscaledDeltaTime;
            _roundTracker.Tick(deltaTime);
            if (_roundTracker.Current.State != FishingRoundState.Playing)
            {
                if (_roundTracker.Current.State == FishingRoundState.Completed ||
                    _roundTracker.Current.State == FishingRoundState.Aborted)
                {
                    _feedbackOutput.StopFeedback();
                }
                return;
            }

            FishingInputFrame input = _inputSource.ReadFrame();
            _authority.Tick(input, deltaTime);
            _feedbackOutput.ApplyFeedback(_authority.Current.Feedback);
            PrepareNextFishIfReady();
            LogStateTransitionIfNeeded();
        }

        private void OnDisable()
        {
            _feedbackOutput?.StopFeedback();
        }

        private void OnDestroy()
        {
            if (_authority != null) _authority.CycleFinished -= OnCycleFinished;
            if (_roundTracker != null) _roundTracker.Completed -= OnRoundFinished;
            _feedbackOutput?.StopFeedback();
        }

        public void Configure(FishingGameConfigAsset gameConfig, bool shouldAutoStart)
        {
            config = gameConfig;
            autoStartRound = shouldAutoStart;
        }

        public void InitializeRuntime()
        {
            if (HasRuntimeDependencies()) return;

            if (_authority != null) _authority.CycleFinished -= OnCycleFinished;
            if (_roundTracker != null) _roundTracker.Completed -= OnRoundFinished;
            _feedbackOutput?.StopFeedback();

            _inputSource = new KeyboardFishingInputSource("local-player");
            _feedbackOutput = new MockFishingFeedbackOutput();
            _authority = new LocalFishingAuthority();
            _authority.CycleFinished += OnCycleFinished;
            _roundTracker = new FishingRoundTracker();
            _roundTracker.Completed += OnRoundFinished;
            _initialized = true;
            RebuildRound(config != null ? config.BuildLaunchContext() : new FishingLaunchContext());
        }

        public void ConfigureLaunchContext(FishingLaunchContext context)
        {
            InitializeRuntime();
            RebuildRound(context ?? (config != null ? config.BuildLaunchContext() : new FishingLaunchContext()));
        }

        public void BeginRound()
        {
            InitializeRuntime();
            if (_roundTracker.Current.State == FishingRoundState.Completed ||
                _roundTracker.Current.State == FishingRoundState.Aborted)
            {
                RebuildRound(_launchContext);
            }
            _paused = false;
            _roundTracker.Begin();
        }

        public void StopRound()
        {
            if (!HasRuntimeDependencies()) return;
            _roundTracker.Stop();
        }

        public void AbortRound()
        {
            if (!HasRuntimeDependencies()) return;
            _roundTracker.Abort();
        }

        public void SetPaused(bool paused)
        {
            _paused = paused;
            if (paused) _feedbackOutput?.StopFeedback();
        }

        public void SetInputSource(IFishingInputSource inputSource)
        {
            _inputSource = inputSource ?? throw new ArgumentNullException(nameof(inputSource));
            _inputSource.ResetState();
        }

        public void SetFeedbackOutput(IFishingFeedbackOutput feedbackOutput)
        {
            _feedbackOutput?.StopFeedback();
            _feedbackOutput = feedbackOutput ?? throw new ArgumentNullException(nameof(feedbackOutput));
        }

        public void ResetCycle()
        {
            if (_roundTracker?.Current.State != FishingRoundState.Playing) return;
            _inputSource?.ResetState();
            _authority?.ResetCycle();
            _feedbackOutput?.StopFeedback();
            _prepareNextFish = false;
            if (_authority?.Current != null) _lastLoggedState = _authority.Current.State;
        }

        private bool HasRuntimeDependencies()
        {
            return _initialized && _inputSource != null && _feedbackOutput != null &&
                _authority != null && _roundTracker != null && _fishCatalog != null;
        }

        private void RebuildRound(FishingLaunchContext context)
        {
            _launchContext = (context ?? new FishingLaunchContext()).Copy();
            _launchContext.Sanitize();
            _baseRules = config != null ? config.BuildRules() : new FishingRules();
            _baseRules.Sanitize();
            _fishCatalog = config != null ? config.BuildFishProfiles() : new[] { new FishProfile() };
            if (_fishCatalog.Length == 0) _fishCatalog = new[] { new FishProfile() };
            _fishRandom = new System.Random(_launchContext.Seed);
            _nextFishIndex = 0;
            _prepareNextFish = false;
            _paused = false;

            FishProfile firstFish = SelectNextFish();
            _authority.Initialize(new FishingRoundContext
            {
                ParticipantId = _launchContext.LocalParticipantId,
                Seed = _launchContext.Seed,
                Rules = _baseRules,
                Fish = firstFish
            });
            _roundTracker.Initialize(_launchContext);
            _inputSource.ResetState();
            _feedbackOutput.StopFeedback();
            _lastLoggedState = _authority.Current.State;
        }

        private FishProfile SelectNextFish()
        {
            if (config == null || config.CycleFishInCatalogOrder)
            {
                FishProfile selected = _fishCatalog[_nextFishIndex % _fishCatalog.Length];
                _nextFishIndex++;
                return selected;
            }

            float totalWeight = 0f;
            for (int i = 0; i < _fishCatalog.Length; i++) totalWeight += _fishCatalog[i].RarityWeight;
            double roll = _fishRandom.NextDouble() * totalWeight;
            for (int i = 0; i < _fishCatalog.Length; i++)
            {
                roll -= _fishCatalog[i].RarityWeight;
                if (roll <= 0d) return _fishCatalog[i];
            }
            return _fishCatalog[_fishCatalog.Length - 1];
        }

        private void PrepareNextFishIfReady()
        {
            if (!_prepareNextFish || _authority.Current.State != FishingPlayerState.Idle) return;
            _authority.PrepareCycle(SelectNextFish(), _baseRules);
            _prepareNextFish = false;
            _lastLoggedState = _authority.Current.State;
        }

        private void OnCycleFinished(FishingCycleResult result)
        {
            _roundTracker.RecordCycle(result);
            _prepareNextFish = true;
            CycleFinished?.Invoke(result);
            if (logStateChanges)
            {
                Debug.Log(result.WasCaught
                    ? $"[Fishing] Cycle {result.CycleNumber} caught {result.FishId}, score +{result.AwardedScore}."
                    : $"[Fishing] Cycle {result.CycleNumber} escaped: {result.EscapeReason}.", this);
            }
        }

        private void OnRoundFinished(FishingRoundResult result)
        {
            _feedbackOutput?.StopFeedback();
            RoundFinished?.Invoke(result);
            if (logStateChanges)
            {
                Debug.Log($"[Fishing] Round finished: {result.EndReason}, score {result.TotalScore}, caught {result.CaughtCount}.", this);
            }
        }

        private void LogStateTransitionIfNeeded()
        {
            if (!logStateChanges || _authority.Current.State == _lastLoggedState) return;
            _lastLoggedState = _authority.Current.State;
            Debug.Log($"[Fishing] State -> {_lastLoggedState}", this);
        }
    }
}
