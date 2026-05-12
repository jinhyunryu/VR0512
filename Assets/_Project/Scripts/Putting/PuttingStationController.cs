using System.Collections.Generic;
using ArcadeRoom.Audio;
using ArcadeRoom.Reset;
using ArcadeRoom.Stations;
using MikeNspired.XRIStarterKit;
using UnityEngine;

namespace ArcadeRoom.Putting
{
    [DisallowMultipleComponent]
    public class PuttingStationController : ArcadeStationRoot
    {
        private const string DefaultHoleInClipPath = "Assets/_Project/Audio/SFX/Putting/Putting_Hole_In.wav";
        private const string DefaultGameFinishClipPath = "Assets/_Project/Audio/SFX/Game_finish.wav";

        [Header("Ball Spawn")]
        [SerializeField] private GameObject golfBallPrefab;
        [SerializeField] private Collider ballCreateZone;
        [SerializeField] private Transform ballSpawnRoot;
        [SerializeField] private bool spawnOnStart;
        [SerializeField, Min(0)] private int ballCount = 1;
        [SerializeField, Min(0f)] private float edgePadding = 0.03f;
        [SerializeField, Min(0f)] private float spacingPadding = 0.03f;
        [SerializeField, Min(0f)] private float spawnSurfaceOffset = 0.01f;
        [SerializeField] private bool useSpawnYOverride;
        [SerializeField] private float spawnYOverride = 0.084f;

        [Header("Stroke Tracking")]
        [SerializeField] private Canvas scoreboardCanvas;
        [SerializeField] private PuttingScoreboardUI scoreboardUI;
        [SerializeField] private List<PuttingHoleTrigger> holeTriggers = new();
        [SerializeField] private string golfBallNamePrefix = "GolfBall";
        [SerializeField, Min(0f)] private float strokeCooldown = 0.15f;
        [SerializeField, Min(0)] private int strokeCount;
        [SerializeField] private bool allBallsHoled;

        [Header("Scoreboard Defaults")]
        [SerializeField] private bool autoCreateScoreboardCanvas = true;
        [SerializeField] private Vector3 scoreboardLocalPosition = new(0f, 1.15f, -0.75f);
        [SerializeField] private Vector3 scoreboardLocalEulerAngles = new(55f, 0f, 0f);
        [SerializeField] private Vector2 scoreboardSize = new(780f, 280f);
        [SerializeField, Min(0.0001f)] private float scoreboardWorldScale = 0.0015f;
        [SerializeField] private bool hideScoreboardUntilFirstReset = true;

        [Header("SFX")]
        [SerializeField] private AudioClip holeInClip;
        [SerializeField, Range(0f, 1f)] private float holeInVolume = 0.85f;
        [SerializeField] private AudioClip gameFinishClip;
        [SerializeField, Range(0f, 1f)] private float gameFinishVolume = 0.9f;
        [SerializeField, Range(0f, 1f)] private float sfxSpatialBlend = 1f;
        [SerializeField, Min(0.1f)] private float sfxMaxDistance = 8f;

        private readonly List<GameObject> _spawnedBalls = new();
        private readonly HashSet<int> _holedBallIds = new();
        private GameObject _resolvedBallTemplate;
        private float _lastStrokeTime = float.NegativeInfinity;
        private bool _scoreboardHasBeenShown;
        private bool _finishSfxPlayed;

        protected override void Awake()
        {
            base.Awake();
            ResolveReferences();
            ResolveDefaultSfx();
        }

        protected override void OnValidate()
        {
            base.OnValidate();
            ResolveReferences();
            sfxMaxDistance = Mathf.Max(0.1f, sfxMaxDistance);
            ResolveDefaultSfx();

            if (!Application.isPlaying)
            {
                UpdateScoreboard();
            }
        }

        private void Start()
        {
            EnsureSetupComplete(false);

            if (spawnOnStart)
            {
                ResetState();
                return;
            }

            if (hideScoreboardUntilFirstReset)
            {
                SetScoreboardVisible(false);
            }
            else
            {
                UpdateScoreboard();
            }
        }

        public override void ResetState()
        {
            base.ResetState();
            SpawnBallsNow();
        }

        public void SpawnBallsNow()
        {
            EnsureSetupComplete(true);
            ResetMatchState();
            ClearSpawnedBalls();
            SpawnBalls();
            RefreshResettables();
            SetScoreboardVisible(true);
            UpdateScoreboard();
        }

        public void BuildScoreboardNow()
        {
            ResolveReferences();
            EnsureScoreboardSetup(true);
            SetScoreboardVisible(true);
            UpdateScoreboard();
        }

        public void ConfigureHoleTriggersNow()
        {
            ResolveReferences();
            ConfigureHoleTriggers();
        }

        public void NotifyBallHit(GameObject ball)
        {
            if (!IsTrackedGolfBall(ball) || IsBallHoled(ball))
            {
                return;
            }

            if (Time.time - _lastStrokeTime < strokeCooldown)
            {
                return;
            }

            strokeCount++;
            _lastStrokeTime = Time.time;
            UpdateScoreboard();
        }

        public void NotifyBallHoled(GameObject ball)
        {
            if (!IsTrackedGolfBall(ball))
            {
                return;
            }

            if (!_holedBallIds.Add(ball.GetInstanceID()))
            {
                return;
            }

            var wasAllBallsHoled = allBallsHoled;
            PlayHoleInSfx(ball.transform.position);

            if (ball.TryGetComponent<Rigidbody>(out var body))
            {
                body.linearVelocity = Vector3.zero;
                body.angularVelocity = Vector3.zero;
                body.Sleep();
            }

            allBallsHoled = GetTrackedBallCount() > 0 && _holedBallIds.Count >= GetTrackedBallCount();
            if (allBallsHoled && !wasAllBallsHoled)
            {
                PlayGameFinishSfx(ball.transform.position);
            }

            UpdateScoreboard();
        }

        private void EnsureSetupComplete(bool createScoreboard)
        {
            ResolveReferences();
            BuildResetButtonBinding();
            ConfigureHoleTriggers();
            EnsureScoreboardSetup(createScoreboard);
            HideTemplateBall(_resolvedBallTemplate);
        }

        private void ResolveReferences()
        {
            if (ballSpawnRoot == null)
            {
                ballSpawnRoot = SpawnRoot ?? GameplayRoot ?? transform;
            }

            if (ballCreateZone == null)
            {
                ballCreateZone =
                    ResolveNamedTransform("Ball_CreateZone")?.GetComponent<Collider>() ??
                    ResolveNamedTransform("BallSpawn")?.GetComponent<Collider>();
            }

            _resolvedBallTemplate = ResolveBallTemplate(golfBallPrefab, "GolfBall");

            if (scoreboardCanvas == null)
            {
                scoreboardCanvas = ResolveScoreboardCanvas();
            }

            if (scoreboardUI == null && scoreboardCanvas != null)
            {
                scoreboardUI = scoreboardCanvas.GetComponent<PuttingScoreboardUI>();
            }
        }

        private void BuildResetButtonBinding()
        {
            if (ResetButton == null)
            {
                return;
            }

            var existingTrigger = ResetButton.GetComponentInChildren<ResetButtonTrigger>(true);
            if (existingTrigger != null)
            {
                existingTrigger.Initialize(null, this, false, true, false);
                return;
            }

            var target = ResolveResetButtonTarget();
            if (target == null)
            {
                return;
            }

            var trigger = GetOrAddComponent<ResetButtonTrigger>(target);
            trigger.Initialize(null, this, false, true, false);
        }

        private GameObject ResolveResetButtonTarget()
        {
            if (ResetButton == null)
            {
                return null;
            }

            var pushButton = ResetButton.GetComponentInChildren<XRPushButton>(true);
            if (pushButton != null)
            {
                return pushButton.gameObject;
            }

            for (var i = 0; i < ResetButton.childCount; i++)
            {
                var child = ResetButton.GetChild(i);
                if (child != null)
                {
                    return child.gameObject;
                }
            }

            return null;
        }

        private void SpawnBalls()
        {
            if (_resolvedBallTemplate == null || ballCount <= 0)
            {
                return;
            }

            var parent = ballSpawnRoot != null ? ballSpawnRoot : (GameplayRoot != null ? GameplayRoot : transform);
            var positions = BuildSpawnPositions(_resolvedBallTemplate, ballCount);
            var rotation = _resolvedBallTemplate.transform.rotation;

            for (var i = 0; i < positions.Count; i++)
            {
                var instance = Instantiate(_resolvedBallTemplate, positions[i], rotation, parent);
                instance.name = $"GolfBall_{i + 1:00}";
                instance.SetActive(true);

                if (instance.TryGetComponent<Rigidbody>(out var body))
                {
                    body.linearVelocity = Vector3.zero;
                    body.angularVelocity = Vector3.zero;
                }

                _spawnedBalls.Add(instance);
            }
        }

        private void ResetMatchState()
        {
            strokeCount = 0;
            allBallsHoled = false;
            _finishSfxPlayed = false;
            _lastStrokeTime = float.NegativeInfinity;
            _holedBallIds.Clear();
        }

        private void ConfigureHoleTriggers()
        {
            var configuredTriggers = new List<PuttingHoleTrigger>();

            for (var i = 0; i < holeTriggers.Count; i++)
            {
                var trigger = holeTriggers[i];
                if (trigger == null)
                {
                    continue;
                }

                trigger.Configure(this, golfBallNamePrefix);
                configuredTriggers.Add(trigger);
            }

            var colliders = FindObjectsByType<Collider>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            for (var i = 0; i < colliders.Length; i++)
            {
                var candidate = colliders[i];
                if (!IsHoleColliderCandidate(candidate))
                {
                    continue;
                }

                var trigger = GetOrAddComponent<PuttingHoleTrigger>(candidate.gameObject);
                trigger.Configure(this, golfBallNamePrefix);

                if (!configuredTriggers.Contains(trigger))
                {
                    configuredTriggers.Add(trigger);
                }
            }

            holeTriggers = configuredTriggers;
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
                : GetOrAddComponent<PuttingScoreboardUI>(scoreboardCanvas.gameObject);
            scoreboardUI.EnsureLayout();
        }

        private void SetScoreboardVisible(bool visible)
        {
            if (visible)
            {
                _scoreboardHasBeenShown = true;
            }
            else if (!hideScoreboardUntilFirstReset || _scoreboardHasBeenShown)
            {
                return;
            }

            if (scoreboardCanvas != null && scoreboardCanvas.gameObject.activeSelf != visible)
            {
                scoreboardCanvas.gameObject.SetActive(visible);
            }
        }

        private Canvas CreateScoreboardCanvas()
        {
            var canvasObject = new GameObject(
                "PuttingCanvas",
                typeof(RectTransform),
                typeof(Canvas),
                typeof(UnityEngine.UI.CanvasScaler),
                typeof(UnityEngine.UI.GraphicRaycaster));

            var parent = StationRootTransform != null ? StationRootTransform : transform;
            canvasObject.transform.SetParent(parent, false);
            canvasObject.transform.localPosition = scoreboardLocalPosition;
            canvasObject.transform.localEulerAngles = scoreboardLocalEulerAngles;
            canvasObject.transform.localScale = Vector3.one * scoreboardWorldScale;

            var rectTransform = canvasObject.GetComponent<RectTransform>();
            rectTransform.sizeDelta = scoreboardSize;

            var canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            return canvas;
        }

        private Canvas ResolveScoreboardCanvas()
        {
            var namedCanvas = ResolveNamedTransform("PuttingCanvas");
            if (namedCanvas != null && namedCanvas.TryGetComponent<Canvas>(out var canvas))
            {
                return canvas;
            }

            return null;
        }

        private void UpdateScoreboard()
        {
            if (scoreboardUI == null && scoreboardCanvas != null)
            {
                scoreboardUI = scoreboardCanvas.GetComponent<PuttingScoreboardUI>();
            }

            if (scoreboardUI == null)
            {
                return;
            }

            scoreboardUI.Refresh(strokeCount, allBallsHoled);
        }

        private void ResolveDefaultSfx()
        {
            if (holeInClip == null)
            {
                holeInClip = ArcadeSfxPlayer.LoadEditorClip(DefaultHoleInClipPath);
            }

            if (gameFinishClip == null)
            {
                gameFinishClip = ArcadeSfxPlayer.LoadEditorClip(DefaultGameFinishClipPath);
            }
        }

        private void PlayHoleInSfx(Vector3 worldPosition)
        {
            ArcadeSfxPlayer.PlayOneShot(
                holeInClip,
                worldPosition,
                holeInVolume,
                1f,
                sfxSpatialBlend,
                sfxMaxDistance,
                "Putting_SFX_HoleIn");
        }

        private void PlayGameFinishSfx(Vector3 worldPosition)
        {
            if (_finishSfxPlayed)
            {
                return;
            }

            _finishSfxPlayed = true;
            ArcadeSfxPlayer.PlayOneShot(
                gameFinishClip,
                worldPosition,
                gameFinishVolume,
                1f,
                sfxSpatialBlend,
                sfxMaxDistance,
                "Putting_SFX_GameFinish");
        }

        private bool IsTrackedGolfBall(GameObject ball)
        {
            if (ball == null || !ball.name.StartsWith(golfBallNamePrefix))
            {
                return false;
            }

            for (var i = 0; i < _spawnedBalls.Count; i++)
            {
                if (_spawnedBalls[i] == ball)
                {
                    return true;
                }
            }

            return _spawnedBalls.Count == 0;
        }

        private bool IsBallHoled(GameObject ball)
        {
            return ball != null && _holedBallIds.Contains(ball.GetInstanceID());
        }

        private int GetTrackedBallCount()
        {
            var count = 0;
            for (var i = 0; i < _spawnedBalls.Count; i++)
            {
                if (_spawnedBalls[i] != null)
                {
                    count++;
                }
            }

            return count;
        }

        private bool IsHoleColliderCandidate(Collider candidate)
        {
            if (candidate == null
                || !candidate.enabled
                || !candidate.isTrigger
                || candidate == ballCreateZone
                || candidate.gameObject.scene != gameObject.scene)
            {
                return false;
            }

            if (HasNonHoleName(candidate.transform))
            {
                return false;
            }

            return HasHoleName(candidate.transform)
                || IsUnderNamedRoot(candidate.transform, "GolfBoard")
                || IsUnderNamedRoot(candidate.transform, "PuttingBoard");
        }

        private static bool HasHoleName(Transform target)
        {
            while (target != null)
            {
                var name = target.name;
                if (name.Contains("Hole")
                    || name.Contains("Cup")
                    || name.Contains("Pocket"))
                {
                    return true;
                }

                target = target.parent;
            }

            return false;
        }

        private static bool HasNonHoleName(Transform target)
        {
            while (target != null)
            {
                var name = target.name;
                if (name.Contains("CreateZone")
                    || name.Contains("Spawn")
                    || name.Contains("Return")
                    || name.Contains("Reset")
                    || name.Contains("Button"))
                {
                    return true;
                }

                target = target.parent;
            }

            return false;
        }

        private static bool IsUnderNamedRoot(Transform target, string rootName)
        {
            while (target != null)
            {
                if (target.name.StartsWith(rootName))
                {
                    return true;
                }

                target = target.parent;
            }

            return false;
        }

        private List<Vector3> BuildSpawnPositions(GameObject template, int count)
        {
            var positions = new List<Vector3>(count);

            if (ballCreateZone == null)
            {
                var origin = ballSpawnRoot != null ? ballSpawnRoot.position : transform.position;
                for (var i = 0; i < count; i++)
                {
                    positions.Add(origin + Vector3.right * (GetBallFootprint(template) + spacingPadding) * i);
                }

                return positions;
            }

            var bounds = ballCreateZone.bounds;
            var ballDiameter = GetBallFootprint(template);
            var ballHeight = GetBallHeight(template);
            var cellSize = ballDiameter + spacingPadding;
            var usableWidth = Mathf.Max(ballDiameter, bounds.size.x - edgePadding * 2f);
            var usableDepth = Mathf.Max(ballDiameter, bounds.size.z - edgePadding * 2f);
            var columns = Mathf.Max(1, Mathf.FloorToInt((usableWidth + spacingPadding) / Mathf.Max(0.01f, cellSize)));
            var maxRows = Mathf.Max(1, Mathf.FloorToInt((usableDepth + spacingPadding) / Mathf.Max(0.01f, cellSize)));

            columns = Mathf.Min(columns, count);
            var rows = Mathf.CeilToInt(count / (float)columns);
            if (rows > maxRows)
            {
                rows = maxRows;
                columns = Mathf.CeilToInt(count / (float)rows);
            }

            var stepX = columns <= 1 ? 0f : Mathf.Min(cellSize, usableWidth / (columns - 1));
            var stepZ = rows <= 1 ? 0f : Mathf.Min(cellSize, usableDepth / (rows - 1));
            var startX = bounds.center.x - stepX * (columns - 1) * 0.5f;
            var startZ = bounds.center.z - stepZ * (rows - 1) * 0.5f;
            var y = useSpawnYOverride
                ? spawnYOverride
                : bounds.min.y + ballHeight * 0.5f + spawnSurfaceOffset;

            for (var index = 0; index < count; index++)
            {
                var row = index / columns;
                var column = index % columns;

                positions.Add(new Vector3(
                    startX + column * stepX,
                    y,
                    startZ + row * stepZ));
            }

            return positions;
        }

        private void ClearSpawnedBalls()
        {
            for (var i = 0; i < _spawnedBalls.Count; i++)
            {
                var ball = _spawnedBalls[i];
                if (ball == null)
                {
                    continue;
                }

                Destroy(ball);
            }

            _spawnedBalls.Clear();
        }

        private static float GetBallFootprint(GameObject template)
        {
            if (template == null)
            {
                return 0.05f;
            }

            if (template.TryGetComponent<SphereCollider>(out var sphere))
            {
                return sphere.radius * 2f * Mathf.Max(
                    Mathf.Abs(template.transform.localScale.x),
                    Mathf.Abs(template.transform.localScale.z));
            }

            if (template.TryGetComponent<CapsuleCollider>(out var capsule))
            {
                return capsule.radius * 2f * Mathf.Max(
                    Mathf.Abs(template.transform.localScale.x),
                    Mathf.Abs(template.transform.localScale.z));
            }

            if (template.TryGetComponent<BoxCollider>(out var box))
            {
                return Mathf.Max(
                    box.size.x * Mathf.Abs(template.transform.localScale.x),
                    box.size.z * Mathf.Abs(template.transform.localScale.z));
            }

            return 0.05f;
        }

        private static float GetBallHeight(GameObject template)
        {
            if (template == null)
            {
                return 0.05f;
            }

            if (template.TryGetComponent<SphereCollider>(out var sphere))
            {
                return sphere.radius * 2f * Mathf.Abs(template.transform.localScale.y);
            }

            if (template.TryGetComponent<CapsuleCollider>(out var capsule))
            {
                return capsule.height * Mathf.Abs(template.transform.localScale.y);
            }

            if (template.TryGetComponent<BoxCollider>(out var box))
            {
                return box.size.y * Mathf.Abs(template.transform.localScale.y);
            }

            return 0.05f;
        }

        private void HideTemplateBall(GameObject template)
        {
            if (template == null || !template.scene.IsValid())
            {
                return;
            }

            template.SetActive(false);
        }

        private GameObject ResolveBallTemplate(GameObject assignedTemplate, params string[] fallbackNames)
        {
            if (assignedTemplate != null)
            {
                return assignedTemplate;
            }

            for (var i = 0; i < fallbackNames.Length; i++)
            {
                var transformCandidate = ResolveNamedTransform(fallbackNames[i]);
                if (transformCandidate != null)
                {
                    return transformCandidate.gameObject;
                }
            }

            return null;
        }

        private Transform ResolveNamedTransform(string name)
        {
            var transforms = GetComponentsInChildren<Transform>(true);
            for (var i = 0; i < transforms.Length; i++)
            {
                var candidate = transforms[i];
                if (candidate != null && candidate.name == name)
                {
                    return candidate;
                }
            }

            var sceneTransforms = FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            for (var i = 0; i < sceneTransforms.Length; i++)
            {
                var candidate = sceneTransforms[i];
                if (candidate != null && candidate.name == name)
                {
                    return candidate;
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
