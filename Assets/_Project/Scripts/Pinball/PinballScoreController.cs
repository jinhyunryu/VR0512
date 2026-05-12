using System.Collections.Generic;
using ArcadeRoom.Audio;
using MikeNspired.XRIStarterKit;
using UnityEngine;
using UnityEngine.UI;

namespace ArcadeRoom.Pinball
{
    [ExecuteAlways]
    [DisallowMultipleComponent]
    public sealed class PinballScoreController : MonoBehaviour
    {
        private const string DefaultGameFinishClipPath = "Assets/_Project/Audio/SFX/Game_finish.wav";

        [Header("Timer")]
        [SerializeField, Min(1f)] private float matchDurationSeconds = 90f;
        [SerializeField] private bool hideScoreboardUntilFirstStart;
        [SerializeField] private bool showScoreboardPreviewInEditor = true;

        [Header("Score Values")]
        [SerializeField] private int switchScore = 10;
        [SerializeField] private int slingshotScore = 25;
        [SerializeField] private int spinnerStepScore = 5;
        [SerializeField] private int spinnerFullTurnScore = 20;
        [SerializeField] private int dropTargetScore = 100;

        [Header("Reset")]
        [SerializeField] private bool resetDropTargetsOnStart = true;

        [Header("References")]
        [SerializeField] private Canvas scoreboardCanvas;
        [SerializeField] private PinballScoreboardUI scoreboardUI;
        [SerializeField] private Transform scoreboardAnchor;
        [SerializeField] private Transform startButtonRoot;
        [SerializeField] private XRPushButton startButton;
        [SerializeField] private PinballPlunger plunger;
        [Header("SFX")]
        [SerializeField] private AudioClip gameFinishClip;
        [SerializeField, Range(0f, 1f)] private float gameFinishVolume = 0.9f;
        [SerializeField, Range(0f, 1f)] private float gameFinishSpatialBlend = 0f;
        [SerializeField, Min(0.1f)] private float gameFinishMaxDistance = 10f;

        [Header("Scoreboard Defaults")]
        [SerializeField] private bool autoCreateScoreboardCanvas = true;
        [SerializeField] private Vector3 scoreboardLocalPosition = new(0f, 0f, 0.002f);
        [SerializeField] private Vector3 scoreboardLocalEulerAngles = Vector3.zero;
        [SerializeField] private Vector2 scoreboardSize = new(720f, 300f);
        [SerializeField, Min(0.0001f)] private float scoreboardWorldScale = 0.0008f;

        private int _score;
        private float _elapsedSeconds;
        private bool _gameStarted;
        private bool _timerRunning;
        private bool _timeExpired;
        private bool _finishSfxPlayed;
        private readonly List<PinballSwitchPass> _registeredSwitches = new();
        private readonly List<PinballSlingshot> _registeredSlingshots = new();
        private readonly List<PinballDropTarget> _registeredDropTargets = new();
        private readonly List<PinballSpinnerRotationEvent> _registeredSpinners = new();

        private void Awake()
        {
            EnsureSetupComplete(false);
            ResolveDefaultSfx();
            RefreshScoreboard();
        }

        private void OnEnable()
        {
            EnsureSetupComplete(!Application.isPlaying && showScoreboardPreviewInEditor);

            if (!Application.isPlaying)
            {
                SetScoreboardVisible(showScoreboardPreviewInEditor);
                RefreshScoreboard();
                return;
            }

            RegisterEvents();
        }

        private void Start()
        {
            if (!Application.isPlaying)
            {
                return;
            }

            EnsureSetupComplete(true);
            RefreshScoreboard();

            if (hideScoreboardUntilFirstStart && !_gameStarted)
            {
                SetScoreboardVisible(false);
            }
        }

        private void Update()
        {
            if (!Application.isPlaying)
            {
                return;
            }

            if (!_timerRunning || _timeExpired)
            {
                return;
            }

            _elapsedSeconds += Time.deltaTime;
            if (_elapsedSeconds >= matchDurationSeconds)
            {
                _elapsedSeconds = matchDurationSeconds;
                EndGame();
                return;
            }

            RefreshScoreboard();
        }

        private void OnDisable()
        {
            if (Application.isPlaying)
            {
                UnregisterEvents();
            }
        }

        private void OnValidate()
        {
            matchDurationSeconds = Mathf.Max(1f, matchDurationSeconds);
            scoreboardWorldScale = Mathf.Max(0.0001f, scoreboardWorldScale);
            ResolveReferences();
            ResolveDefaultSfx();

            if (!Application.isPlaying)
            {
                if (showScoreboardPreviewInEditor)
                {
                    EnsureScoreboardSetup(true);
                    SetScoreboardVisible(true);
                }

                RefreshScoreboard();
            }
        }

        public void StartNewGame()
        {
            _score = 0;
            _elapsedSeconds = 0f;
            _gameStarted = true;
            _timerRunning = false;
            _timeExpired = false;
            _finishSfxPlayed = false;

            EnsureSetupComplete(true);
            if (resetDropTargetsOnStart)
            {
                ResetDropTargets();
            }

            ResetGameOverZones();
            SetScoreboardVisible(true);
            RefreshScoreboard();
        }

        [ContextMenu("Build Scoreboard Now")]
        public void BuildScoreboardNow()
        {
            EnsureSetupComplete(true);
            SetScoreboardVisible(true);
            RefreshScoreboard();
        }

        public void StartTimer()
        {
            if (!_gameStarted)
            {
                StartNewGame();
            }

            if (_timeExpired)
            {
                return;
            }

            _timerRunning = true;
            SetScoreboardVisible(true);
            RefreshScoreboard();
        }

        public void StopTimer()
        {
            _timerRunning = false;
            RefreshScoreboard();
        }

        public void EndGame()
        {
            if (!_gameStarted)
            {
                return;
            }

            _timerRunning = false;
            _timeExpired = true;
            PlayGameFinishSfx();
            RefreshScoreboard();
        }

        public void AddScore(int amount)
        {
            if (!_gameStarted || _timeExpired)
            {
                return;
            }

            _score = Mathf.Max(0, _score + amount);
            RefreshScoreboard();
        }

        public void AddSwitchScore()
        {
            AddScore(switchScore);
        }

        public void AddSlingshotScore()
        {
            AddScore(slingshotScore);
        }

        public void AddSpinnerStepScore()
        {
            AddScore(spinnerStepScore);
        }

        public void AddSpinnerFullTurnScore()
        {
            AddScore(spinnerFullTurnScore);
        }

        public void AddDropTargetScore()
        {
            AddScore(dropTargetScore);
        }

        private void HandleBallLaunched(PinballBall ball)
        {
            StartTimer();
        }

        private void HandleSwitchPassed(PinballBall ball)
        {
            AddSwitchScore();
        }

        private void HandleSlingshotLaunched(PinballBall ball)
        {
            AddSlingshotScore();
        }

        private void HandleDropTargetHit(PinballBall ball)
        {
            AddDropTargetScore();
        }

        private void EnsureSetupComplete(bool createScoreboard)
        {
            ResolveReferences();
            EnsureScoreboardSetup(createScoreboard);
        }

        private void ResolveReferences()
        {
            if (scoreboardCanvas == null)
            {
                scoreboardCanvas = ResolveNamedTransform("PinballCanvas")?.GetComponent<Canvas>()
                    ?? ResolveNamedTransform("ScoreBoardCanvas")?.GetComponent<Canvas>();
            }

            if (scoreboardUI == null && scoreboardCanvas != null)
            {
                scoreboardUI = scoreboardCanvas.GetComponent<PinballScoreboardUI>();
            }

            if (scoreboardAnchor == null)
            {
                scoreboardAnchor = ResolveNamedTransform("score board")
                    ?? ResolveNamedTransform("ScoreBoard")
                    ?? ResolveNamedTransform("scoreboard");
            }

            if (startButtonRoot == null)
            {
                startButtonRoot = ResolveNamedTransform("StartButton");
            }

            if (startButton == null && startButtonRoot != null)
            {
                startButton = startButtonRoot.GetComponentInChildren<XRPushButton>(true);
            }

            if (plunger == null)
            {
                plunger = GetComponentInChildren<PinballPlunger>(true);
            }
        }

        private void EnsureScoreboardSetup(bool createIfMissing)
        {
            if (scoreboardCanvas == null && autoCreateScoreboardCanvas && createIfMissing)
            {
                scoreboardCanvas = CreateScoreboardCanvas();
            }

            if (scoreboardCanvas == null)
            {
                return;
            }

            scoreboardUI = scoreboardUI != null
                ? scoreboardUI
                : GetOrAddComponent<PinballScoreboardUI>(scoreboardCanvas.gameObject);
            scoreboardUI.EnsureLayout();
        }

        private Canvas CreateScoreboardCanvas()
        {
            var canvasObject = new GameObject(
                "PinballCanvas",
                typeof(RectTransform),
                typeof(Canvas),
                typeof(CanvasScaler),
                typeof(GraphicRaycaster));

            var parent = scoreboardAnchor != null ? scoreboardAnchor : transform;
            canvasObject.transform.SetParent(parent, false);
            canvasObject.transform.localPosition = scoreboardLocalPosition;
            canvasObject.transform.localEulerAngles = scoreboardLocalEulerAngles;
            canvasObject.transform.localScale = Vector3.one * scoreboardWorldScale;

            var rectTransform = canvasObject.GetComponent<RectTransform>();
            rectTransform.sizeDelta = scoreboardSize;

            var canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;

            var scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.dynamicPixelsPerUnit = 10f;
            return canvas;
        }

        private void SetScoreboardVisible(bool visible)
        {
            if (scoreboardCanvas != null && scoreboardCanvas.gameObject.activeSelf != visible)
            {
                scoreboardCanvas.gameObject.SetActive(visible);
            }
        }

        private void RefreshScoreboard()
        {
            if (scoreboardUI == null && scoreboardCanvas != null)
            {
                scoreboardUI = scoreboardCanvas.GetComponent<PinballScoreboardUI>();
            }

            if (scoreboardUI == null)
            {
                return;
            }

            scoreboardUI.Refresh(_elapsedSeconds, _score);
        }

        private void RegisterEvents()
        {
            if (startButton != null)
            {
                startButton.OnPress.RemoveListener(StartNewGame);
                startButton.OnPress.AddListener(StartNewGame);
            }

            if (plunger != null)
            {
                plunger.BallLaunched -= HandleBallLaunched;
                plunger.BallLaunched += HandleBallLaunched;
            }

            RegisterPlayfieldEvents();
        }

        private void UnregisterEvents()
        {
            if (startButton != null)
            {
                startButton.OnPress.RemoveListener(StartNewGame);
            }

            if (plunger != null)
            {
                plunger.BallLaunched -= HandleBallLaunched;
            }

            UnregisterPlayfieldEvents();
        }

        private void RegisterPlayfieldEvents()
        {
            UnregisterPlayfieldEvents();

            GetComponentsInChildren(true, _registeredSwitches);
            for (var i = 0; i < _registeredSwitches.Count; i++)
            {
                _registeredSwitches[i].BallPassed += HandleSwitchPassed;
            }

            GetComponentsInChildren(true, _registeredSlingshots);
            for (var i = 0; i < _registeredSlingshots.Count; i++)
            {
                _registeredSlingshots[i].BallLaunched += HandleSlingshotLaunched;
            }

            GetComponentsInChildren(true, _registeredDropTargets);
            for (var i = 0; i < _registeredDropTargets.Count; i++)
            {
                _registeredDropTargets[i].BallHit += HandleDropTargetHit;
            }

            GetComponentsInChildren(true, _registeredSpinners);
            for (var i = 0; i < _registeredSpinners.Count; i++)
            {
                _registeredSpinners[i].StepRotated += AddSpinnerStepScore;
                _registeredSpinners[i].FullTurnRotated += AddSpinnerFullTurnScore;
            }
        }

        private void UnregisterPlayfieldEvents()
        {
            for (var i = 0; i < _registeredSwitches.Count; i++)
            {
                if (_registeredSwitches[i] != null)
                {
                    _registeredSwitches[i].BallPassed -= HandleSwitchPassed;
                }
            }

            for (var i = 0; i < _registeredSlingshots.Count; i++)
            {
                if (_registeredSlingshots[i] != null)
                {
                    _registeredSlingshots[i].BallLaunched -= HandleSlingshotLaunched;
                }
            }

            for (var i = 0; i < _registeredDropTargets.Count; i++)
            {
                if (_registeredDropTargets[i] != null)
                {
                    _registeredDropTargets[i].BallHit -= HandleDropTargetHit;
                }
            }

            for (var i = 0; i < _registeredSpinners.Count; i++)
            {
                if (_registeredSpinners[i] != null)
                {
                    _registeredSpinners[i].StepRotated -= AddSpinnerStepScore;
                    _registeredSpinners[i].FullTurnRotated -= AddSpinnerFullTurnScore;
                }
            }

            _registeredSwitches.Clear();
            _registeredSlingshots.Clear();
            _registeredDropTargets.Clear();
            _registeredSpinners.Clear();
        }

        private void ResetGameOverZones()
        {
            var zones = FindObjectsByType<PinballGameOverZone>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            for (var i = 0; i < zones.Length; i++)
            {
                var zone = zones[i];
                if (zone != null && zone.gameObject.scene == gameObject.scene)
                {
                    zone.ResetZone();
                }
            }
        }

        private void ResetDropTargets()
        {
            var dropTargets = new List<PinballDropTarget>();
            GetComponentsInChildren(true, dropTargets);
            for (var i = 0; i < dropTargets.Count; i++)
            {
                if (dropTargets[i] != null)
                {
                    dropTargets[i].ResetState();
                }
            }
        }

        private void ResolveDefaultSfx()
        {
            if (gameFinishClip == null)
            {
                gameFinishClip = ArcadeSfxPlayer.LoadEditorClip(DefaultGameFinishClipPath);
            }
        }

        private void PlayGameFinishSfx()
        {
            if (_finishSfxPlayed)
            {
                return;
            }

            _finishSfxPlayed = true;
            var sourcePosition = scoreboardCanvas != null ? scoreboardCanvas.transform.position : transform.position;
            ArcadeSfxPlayer.PlayOneShot(
                gameFinishClip,
                sourcePosition,
                gameFinishVolume,
                1f,
                gameFinishSpatialBlend,
                gameFinishMaxDistance,
                "Pinball_SFX_GameFinish");
        }

        private Transform ResolveNamedTransform(string objectName)
        {
            var transforms = GetComponentsInChildren<Transform>(true);
            for (var i = 0; i < transforms.Length; i++)
            {
                var candidate = transforms[i];
                if (candidate != null && candidate.name == objectName)
                {
                    return candidate;
                }
            }

            if (!gameObject.scene.IsValid() || !gameObject.scene.isLoaded)
            {
                return null;
            }

            var roots = gameObject.scene.GetRootGameObjects();
            for (var i = 0; i < roots.Length; i++)
            {
                var root = roots[i];
                if (root == null)
                {
                    continue;
                }

                transforms = root.GetComponentsInChildren<Transform>(true);
                for (var j = 0; j < transforms.Length; j++)
                {
                    var candidate = transforms[j];
                    if (candidate != null && candidate.name == objectName)
                    {
                        return candidate;
                    }
                }
            }

            return null;
        }

        private static T GetOrAddComponent<T>(GameObject target) where T : Component
        {
            if (!target.TryGetComponent<T>(out var component))
            {
                component = target.AddComponent<T>();
            }

            return component;
        }
    }
}
