using System;
using System.Collections;
using System.Collections.Generic;
using ArcadeRoom.Audio;
using ArcadeRoom.Reset;
using ArcadeRoom.Stations;
using MikeNspired.XRIStarterKit;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace ArcadeRoom.YoTyan
{
    [DisallowMultipleComponent]
    public class YoTyanStationController : ArcadeStationRoot
    {
        private const string DefaultSlotGoalClipPath = "Assets/_Project/Audio/SFX/Yotyan/Yotyan_Ball_slot_goal.wav";
        private const string DefaultGameFinishClipPath = "Assets/_Project/Audio/SFX/Game_finish.wav";
        private const string ResetRequestMessage = "ArcadeRoom.YoTyan.ResetRequest";
        private const string ScoreRequestMessage = "ArcadeRoom.YoTyan.ScoreRequest";
        private const string StateMessage = "ArcadeRoom.YoTyan.State";

        [Header("Scene References")]
        [SerializeField] private Transform blackBallTray;
        [SerializeField] private Transform redBallTray;
        [SerializeField] private Transform blackCreateZone;
        [SerializeField] private Transform redCreateZone;
        [SerializeField] private Transform ballSpawnRoot;
        [SerializeField] private Canvas scoreboardCanvas;
        [SerializeField] private YoTyanScoreboardUI scoreboardUI;
        [SerializeField] private List<YoTyanSlotTrigger> slotTriggers = new();

        [Header("Ball Setup")]
        [SerializeField] private GameObject blackBallPrefab;
        [SerializeField] private GameObject redBallPrefab;
        [SerializeField, Min(0)] private int blackBallCount = 12;
        [SerializeField, Min(0)] private int redBallCount = 12;
        [SerializeField] private bool spawnOnStart;

        [Header("Game State")]
        [SerializeField] private YoTyanBallTeam startingTurn = YoTyanBallTeam.Black;
        [SerializeField] private YoTyanBallTeam currentTurn = YoTyanBallTeam.Black;
        [SerializeField, Min(0)] private int blackScore;
        [SerializeField, Min(0)] private int redScore;
        [SerializeField] private bool gameOver;

        [Header("Multiplayer")]
        [SerializeField] private bool enableMultiplayerSync = true;

        [Header("Reset Stability")]
        [SerializeField, Min(0f)] private float resetCooldown = 0.35f;
        [SerializeField, Min(0f)] private float resetRespawnDelay = 0.5f;

        [Header("Editor Helpers")]
        [SerializeField] private bool assignHomeSpawnsOnAwake = true;
        [SerializeField] private bool assignHomeSpawnsInEditor = true;
        [SerializeField] private bool buildScoreboardInEditor = true;
        [SerializeField] private bool hideScoreboardUntilFirstReset = true;
        [SerializeField] private bool drawSpawnPointGizmos = true;
        [SerializeField] private Color blackSpawnPointColor = new(0.15f, 0.15f, 0.15f, 1f);
        [SerializeField] private Color redSpawnPointColor = new(0.75f, 0.15f, 0.15f, 1f);
        [SerializeField] private float spawnPointGizmoRadius = 0.025f;
        [SerializeField] private List<Transform> blackSpawnPoints = new();
        [SerializeField] private List<Transform> redSpawnPoints = new();

        [Header("SFX")]
        [SerializeField] private AudioClip slotGoalClip;
        [SerializeField, Range(0f, 1f)] private float slotGoalVolume = 0.85f;
        [SerializeField] private AudioClip gameFinishClip;
        [SerializeField, Range(0f, 1f)] private float gameFinishVolume = 0.9f;
        [SerializeField, Range(0f, 1f)] private float sfxSpatialBlend = 1f;
        [SerializeField, Min(0.1f)] private float sfxMaxDistance = 8f;

        private readonly List<YoTyanBall> _spawnedBalls = new();
        private GameObject _resolvedBlackTemplate;
        private GameObject _resolvedRedTemplate;
        private bool _scoreboardHasBeenShown;
        private bool _finishSfxPlayed;
        private bool _networkPrefabsRegistered;
        private bool _networkHandlersRegistered;
        private NetworkManager _registeredNetworkManager;
        private readonly Dictionary<YoTyanBall, float> _lastReturnedAt = new();
        private Coroutine _resetRespawnRoutine;
        private float _lastResetStartedAt = float.NegativeInfinity;

        public IReadOnlyList<Transform> BlackSpawnPoints => blackSpawnPoints;
        public IReadOnlyList<Transform> RedSpawnPoints => redSpawnPoints;
        public int BlackScore => blackScore;
        public int RedScore => redScore;
        public YoTyanBallTeam CurrentTurn => currentTurn;
        public bool GameOver => gameOver;

        protected override void Awake()
        {
            base.Awake();
            ResolveReferences();
            ResolveDefaultSfx();

            if (assignHomeSpawnsOnAwake)
            {
                AssignHomeSpawnPoints();
            }
        }

        protected override void OnValidate()
        {
            base.OnValidate();
            ResolveReferences();
            sfxMaxDistance = Mathf.Max(0.1f, sfxMaxDistance);
            ResolveDefaultSfx();

            if (!Application.isPlaying)
            {
                ConfigureSlotTriggers();
                ConfigureReturnZones();

                if (buildScoreboardInEditor)
                {
                    EnsureScoreboardSetup();
                }

                if (assignHomeSpawnsInEditor)
                {
                    AssignHomeSpawnPoints();
                }

                UpdateScoreboard();
            }
        }

        private void Start()
        {
            EnsureSetupComplete();
            EnsureNetworkSetup();

            if (spawnOnStart)
            {
                ResetState();
            }
            else if (hideScoreboardUntilFirstReset)
            {
                SetScoreboardVisible(false);
            }
            else
            {
                UpdateScoreboard();
            }
        }

        private void Update()
        {
            EnsureNetworkSetup();
        }

        public override void ResetState()
        {
            if (!TryBeginResetCooldown())
            {
                return;
            }

            if (TryRequestNetworkResetFromClient())
            {
                return;
            }

            PerformResetState();
        }

        public void AssignHomeSpawnPoints()
        {
            ResolveReferences();
            AssignHomeSpawnPointsForTeam(_spawnedBalls, YoTyanBallTeam.Black, blackSpawnPoints);
            AssignHomeSpawnPointsForTeam(_spawnedBalls, YoTyanBallTeam.Red, redSpawnPoints);
        }

        public void SpawnBallsNow()
        {
            PerformResetState();
        }

        private void PerformResetState()
        {
            EnsureSetupComplete();
            CancelPendingResetRespawn();
            ResetMatchState();
            ClearSpawnedBalls();
            RefreshResettables();
            SetScoreboardVisible(true);
            UpdateScoreboard();
            QueueResetRespawn();
        }

        public void ResetAllBallsToHome()
        {
            for (var i = 0; i < _spawnedBalls.Count; i++)
            {
                var ball = _spawnedBalls[i];
                if (ball == null)
                {
                    continue;
                }

                ball.ResetToHomeSpawnNetworked();
            }

            UpdateScoreboard();
        }

        public void RefreshSpawnPointLists()
        {
            ResolveReferences();
            ConfigureSlotTriggers();
            UpdateScoreboard();
        }

        public void BuildScoreboardNow()
        {
            ResolveReferences();
            EnsureScoreboardSetup();
            SetScoreboardVisible(true);
            UpdateScoreboard();
        }

        public void TryScoreBall(YoTyanBall ball, int scoreValue)
        {
            if (TryRequestNetworkScoreFromClient(ball, scoreValue))
            {
                return;
            }

            PerformScoreBall(ball, scoreValue);
        }

        private void PerformScoreBall(YoTyanBall ball, int scoreValue)
        {
            if (ball == null || ball.IsScored || ball.IsGrabbed || gameOver)
            {
                return;
            }

            EnsureServerOwnsNetworkBall(ball);
            var extraTurnAwarded = currentTurn == ball.Team;
            ball.MarkScoredNetworked();
            PlaySlotGoalSfx(ball.transform.position);

            if (ball.TryGetComponent<Rigidbody>(out var body))
            {
                body.linearVelocity = Vector3.zero;
                body.angularVelocity = Vector3.zero;
                body.Sleep();
            }

            if (ball.Team == YoTyanBallTeam.Black)
            {
                blackScore += scoreValue;
            }
            else
            {
                redScore += scoreValue;
            }

            if (currentTurn != ball.Team)
            {
                currentTurn = ball.Team;
            }

            CheckForGameOver();
            UpdateScoreboard();

            if (extraTurnAwarded && !gameOver && scoreboardUI != null)
            {
                scoreboardUI.PlayExtraTurnBlink(currentTurn);
            }
        }

        public bool TryReturnBallToHome(YoTyanBall ball)
        {
            if (ball == null || ball.IsScored || ball.IsReturnBlocked || gameOver || !ball.CanLocalReturnZoneControl)
            {
                return false;
            }

            PerformReturnBallToHome(ball);
            return true;
        }

        public void NotifyBallReturned(YoTyanBall ball)
        {
            if (ball == null || ball.IsScored || gameOver)
            {
                return;
            }

            if (currentTurn == ball.Team)
            {
                currentTurn = GetOppositeTeam(currentTurn);
                UpdateScoreboard();
            }
        }

        private void PerformReturnBallToHome(YoTyanBall ball)
        {
            if (ball == null || ball.IsScored || ball.IsReturnBlocked || gameOver || !ball.CanLocalReturnZoneControl || !CanProcessReturn(ball))
            {
                return;
            }

            EnsureServerOwnsNetworkBall(ball);
            NotifyBallReturned(ball);
            ball.ResetToHomeSpawnNetworked();
        }

        private void EnsureSetupComplete()
        {
            ResolveReferences();
            RegisterNetworkPrefabsIfPossible();
            BuildResetButtonPrototype();
            ConfigureSlotTriggers();
            ConfigureReturnZones();
            EnsureScoreboardSetup();
            HideTemplateBall(_resolvedBlackTemplate);
            HideTemplateBall(_resolvedRedTemplate);
            HideScenePreviewBall("YotyanBall_Black");
            HideScenePreviewBall("YotyanBall_Red");
        }

        private void ResolveReferences()
        {
#if UNITY_EDITOR
            TryAssignDefaultPrefabs();
#endif
            blackBallTray = blackBallTray != null ? blackBallTray : FindSceneTransform("BallTray_B");
            redBallTray = redBallTray != null ? redBallTray : FindSceneTransform("BallTray_R");
            blackCreateZone = blackCreateZone != null ? blackCreateZone : FindChildByName(blackBallTray, "B_BallCreateZone");
            redCreateZone = redCreateZone != null ? redCreateZone : FindChildByName(redBallTray, "R_BallCreateZone");
            ballSpawnRoot = ballSpawnRoot != null ? ballSpawnRoot : (SpawnRoot != null ? SpawnRoot : (GameplayRoot != null ? GameplayRoot : transform));
            scoreboardCanvas = scoreboardCanvas != null ? scoreboardCanvas : ResolveScoreboardCanvas();
            scoreboardUI = scoreboardUI != null ? scoreboardUI : (scoreboardCanvas != null ? scoreboardCanvas.GetComponent<YoTyanScoreboardUI>() : null);

            blackSpawnPoints = GetOrderedSpawnPoints(blackCreateZone);
            redSpawnPoints = GetOrderedSpawnPoints(redCreateZone);
            _resolvedBlackTemplate = ResolveBallTemplate(blackBallPrefab, "YotyanBall_Black");
            _resolvedRedTemplate = ResolveBallTemplate(redBallPrefab, "YotyanBall_Red");

            CollectSpawnedBalls();
        }

        private void BuildResetButtonPrototype()
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

        private void ConfigureSlotTriggers()
        {
            var configuredTriggers = new List<YoTyanSlotTrigger>();
            var transforms = FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None);

            for (var i = 0; i < transforms.Length; i++)
            {
                var candidate = transforms[i];
                if (candidate == null || candidate.gameObject.scene != gameObject.scene)
                {
                    continue;
                }

                if (!TryGetScoreValueFromName(candidate.name, out var scoreValue))
                {
                    continue;
                }

                var trigger = GetOrAddComponent<YoTyanSlotTrigger>(candidate.gameObject);
                trigger.Configure(this, scoreValue);
                configuredTriggers.Add(trigger);
            }

            slotTriggers = configuredTriggers;
        }

        private void ConfigureReturnZones()
        {
            var transforms = FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None);

            for (var i = 0; i < transforms.Length; i++)
            {
                var candidate = transforms[i];
                if (candidate == null || candidate.gameObject.scene != gameObject.scene)
                {
                    continue;
                }

                if (!IsReturnZoneName(candidate.name))
                {
                    continue;
                }

                var zone = GetOrAddComponent<YoTyanReturnZone>(candidate.gameObject);
                zone.Configure(this);
            }
        }

        private void EnsureScoreboardSetup()
        {
            if (scoreboardCanvas == null)
            {
                return;
            }

            scoreboardUI = scoreboardUI != null ? scoreboardUI : GetOrAddComponent<YoTyanScoreboardUI>(scoreboardCanvas.gameObject);
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

        private void SpawnAllBalls()
        {
            SpawnTeamBalls(_resolvedBlackTemplate, YoTyanBallTeam.Black, blackSpawnPoints, blackBallCount, "YotyanBall_Black");
            SpawnTeamBalls(_resolvedRedTemplate, YoTyanBallTeam.Red, redSpawnPoints, redBallCount, "YotyanBall_Red");
        }

        private void SpawnTeamBalls(GameObject template, YoTyanBallTeam team, List<Transform> spawnPoints, int count, string baseName)
        {
            if (template == null || spawnPoints.Count == 0 || count <= 0)
            {
                return;
            }

            var parent = ballSpawnRoot != null ? ballSpawnRoot : transform;
            var spawnCount = Mathf.Min(count, spawnPoints.Count);
            var networkSpawn = IsNetworkServerActive() && template.TryGetComponent<NetworkObject>(out _);

            for (var i = 0; i < spawnCount; i++)
            {
                var spawnPoint = spawnPoints[i];
                if (spawnPoint == null)
                {
                    continue;
                }

                var instance = networkSpawn
                    ? Instantiate(template, spawnPoint.position, spawnPoint.rotation)
                    : Instantiate(template, spawnPoint.position, spawnPoint.rotation, parent);
                instance.name = $"{baseName}_{i + 1:00}";
                instance.SetActive(true);

                var ball = GetOrAddComponent<YoTyanBall>(instance);
                ball.ClearScoredNetworked();
                ball.AssignHomeSpawnPoint(spawnPoint);

                if (instance.TryGetComponent<Rigidbody>(out var body))
                {
                    SetBodyPoseAndStopSafely(body, spawnPoint.position, spawnPoint.rotation);
                }

                _spawnedBalls.Add(ball);

                if (networkSpawn
                    && instance.TryGetComponent<NetworkObject>(out var networkObject)
                    && !networkObject.IsSpawned)
                {
                    networkObject.Spawn(true);
                }
            }
        }

        private void AssignHomeSpawnPointsForTeam(List<YoTyanBall> balls, YoTyanBallTeam team, List<Transform> spawnPoints)
        {
            if (spawnPoints.Count == 0)
            {
                return;
            }

            var assignedCount = 0;
            for (var i = 0; i < balls.Count; i++)
            {
                var ball = balls[i];
                if (ball == null || ball.Team != team)
                {
                    continue;
                }

                if (assignedCount >= spawnPoints.Count)
                {
                    break;
                }

                ball.AssignHomeSpawnPoint(spawnPoints[assignedCount]);
                assignedCount++;
            }
        }

        private void ClearSpawnedBalls()
        {
            CollectSpawnedBalls();

            var ballsToClear = new List<YoTyanBall>(_spawnedBalls);
            CollectNetworkSpawnedBalls(ballsToClear);

            if (IsNetworkServerActive())
            {
                SuspendNetworkSpawnedBalls(ballsToClear);
                return;
            }

            for (var i = 0; i < ballsToClear.Count; i++)
            {
                var ball = ballsToClear[i];
                if (ball == null)
                {
                    continue;
                }

                DestroyObject(ball.gameObject);
            }

            _spawnedBalls.Clear();
        }

        private void SuspendNetworkSpawnedBalls(List<YoTyanBall> ballsToSuspend)
        {
            _spawnedBalls.Clear();

            for (var i = 0; i < ballsToSuspend.Count; i++)
            {
                var ball = ballsToSuspend[i];
                if (ball == null)
                {
                    continue;
                }

                if (ball.TryGetComponent<NetworkObject>(out var networkObject) && networkObject.IsSpawned)
                {
                    EnsureServerOwnsNetworkBall(ball);
                    ball.ClearScoredNetworked();
                    ball.SetResetSuspendedNetworked(true);

                    if (!_spawnedBalls.Contains(ball))
                    {
                        _spawnedBalls.Add(ball);
                    }

                    continue;
                }

                DestroyObject(ball.gameObject);
            }
        }

        private void QueueResetRespawn()
        {
            CancelPendingResetRespawn();

            if (!Application.isPlaying || resetRespawnDelay <= 0f)
            {
                RespawnBallsAfterReset();
                return;
            }

            _resetRespawnRoutine = StartCoroutine(RespawnBallsAfterDelay());
        }

        private IEnumerator RespawnBallsAfterDelay()
        {
            yield return new WaitForSeconds(resetRespawnDelay);
            RespawnBallsAfterReset();
            _resetRespawnRoutine = null;
        }

        private void RespawnBallsAfterReset()
        {
            if (IsNetworkServerActive() && _spawnedBalls.Count > 0)
            {
                RespawnSuspendedNetworkBalls();
            }
            else
            {
                SpawnAllBalls();
            }

            AssignHomeSpawnPoints();
            UpdateScoreboard();
        }

        private void RespawnSuspendedNetworkBalls()
        {
            CollectSpawnedBalls();
            RestoreSuspendedNetworkBallsForTeam(YoTyanBallTeam.Black, blackSpawnPoints, blackBallCount);
            RestoreSuspendedNetworkBallsForTeam(YoTyanBallTeam.Red, redSpawnPoints, redBallCount);
        }

        private void RestoreSuspendedNetworkBallsForTeam(YoTyanBallTeam team, List<Transform> spawnPoints, int count)
        {
            if (spawnPoints.Count == 0 || count <= 0)
            {
                return;
            }

            var assignedCount = 0;
            var maxCount = Mathf.Min(count, spawnPoints.Count);
            for (var i = 0; i < _spawnedBalls.Count; i++)
            {
                var ball = _spawnedBalls[i];
                if (ball == null || ball.Team != team)
                {
                    continue;
                }

                if (assignedCount >= maxCount)
                {
                    ball.MarkScoredNetworked();
                    ball.SetResetSuspendedNetworked(true);
                    continue;
                }

                var spawnPoint = spawnPoints[assignedCount];
                if (spawnPoint == null)
                {
                    continue;
                }

                EnsureServerOwnsNetworkBall(ball);
                ball.AssignHomeSpawnPoint(spawnPoint);
                ball.ClearScoredNetworked();
                ball.ResetToHomeSpawnNetworked();
                assignedCount++;
            }
        }

        private void CancelPendingResetRespawn()
        {
            if (_resetRespawnRoutine == null)
            {
                return;
            }

            StopCoroutine(_resetRespawnRoutine);
            _resetRespawnRoutine = null;
        }

        private static void SetBodyPoseAndStopSafely(Rigidbody body, Vector3 position, Quaternion rotation)
        {
            if (body == null)
            {
                return;
            }

            SetBodyVelocitySafely(body, Vector3.zero, Vector3.zero);
            body.position = position;
            body.rotation = rotation;
            body.Sleep();
        }

        private static void SetBodyVelocitySafely(Rigidbody body, Vector3 linearVelocity, Vector3 angularVelocity)
        {
            if (body == null)
            {
                return;
            }

            if (body.isKinematic)
            {
                return;
            }

            body.linearVelocity = linearVelocity;
            body.angularVelocity = angularVelocity;
        }

        private void CollectSpawnedBalls()
        {
            _spawnedBalls.RemoveAll(ball => ball == null);

            var discoveredBalls = FindObjectsByType<YoTyanBall>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            for (var i = 0; i < discoveredBalls.Length; i++)
            {
                var ball = discoveredBalls[i];
                if (!IsRuntimeSpawnedBall(ball))
                {
                    continue;
                }

                if (_spawnedBalls.Contains(ball))
                {
                    continue;
                }

                _spawnedBalls.Add(ball);
            }
        }

        private void CollectNetworkSpawnedBalls(List<YoTyanBall> target)
        {
            if (!IsNetworkServerActive() || NetworkManager.Singleton == null)
            {
                return;
            }

            foreach (var pair in NetworkManager.Singleton.SpawnManager.SpawnedObjects)
            {
                var networkObject = pair.Value;
                if (networkObject == null || !networkObject.TryGetComponent<YoTyanBall>(out var ball))
                {
                    continue;
                }

                if (!IsRuntimeSpawnedBall(ball) || target.Contains(ball))
                {
                    continue;
                }

                target.Add(ball);
            }
        }

        private bool IsRuntimeSpawnedBall(YoTyanBall ball)
        {
            if (ball == null || ball.gameObject.scene != gameObject.scene)
            {
                return false;
            }

            if (ReferenceEquals(ball.gameObject, _resolvedBlackTemplate)
                || ReferenceEquals(ball.gameObject, _resolvedRedTemplate))
            {
                return false;
            }

            if (ball.TryGetComponent<NetworkObject>(out var networkObject) && networkObject.IsSpawned)
            {
                return true;
            }

            return ball.name.StartsWith("YotyanBall_Black_", StringComparison.Ordinal)
                || ball.name.StartsWith("YotyanBall_Red_", StringComparison.Ordinal);
        }

        private void ResetMatchState()
        {
            blackScore = 0;
            redScore = 0;
            gameOver = false;
            _finishSfxPlayed = false;
            _lastReturnedAt.Clear();
            currentTurn = startingTurn;
        }

        private void CheckForGameOver()
        {
            var wasGameOver = gameOver;
            gameOver = GetRemainingBallCount(YoTyanBallTeam.Black) <= 0
                && GetRemainingBallCount(YoTyanBallTeam.Red) <= 0;

            if (gameOver && !wasGameOver)
            {
                PlayGameFinishSfx();
            }
        }

        private void UpdateScoreboard()
        {
            EnsureScoreboardSetup();

            if (scoreboardUI != null)
            {
                scoreboardUI.Refresh(
                    blackScore,
                    redScore,
                    GetRemainingBallCount(YoTyanBallTeam.Black),
                    GetRemainingBallCount(YoTyanBallTeam.Red),
                    currentTurn,
                    gameOver);
            }

            BroadcastNetworkStateIfServer();
        }

        private void ApplyNetworkState(
            int newBlackScore,
            int newRedScore,
            int blackBallsRemaining,
            int redBallsRemaining,
            YoTyanBallTeam newCurrentTurn,
            bool newGameOver)
        {
            blackScore = newBlackScore;
            redScore = newRedScore;
            currentTurn = newCurrentTurn;
            gameOver = newGameOver;

            SetScoreboardVisible(true);
            EnsureScoreboardSetup();
            scoreboardUI?.Refresh(
                blackScore,
                redScore,
                blackBallsRemaining,
                redBallsRemaining,
                currentTurn,
                gameOver);
        }

        private void EnsureNetworkSetup()
        {
            if (!enableMultiplayerSync)
            {
                return;
            }

            RegisterNetworkPrefabsIfPossible();

            var networkManager = NetworkManager.Singleton;
            if (networkManager == null || !networkManager.IsListening)
            {
                return;
            }

            if (_registeredNetworkManager != null && _registeredNetworkManager != networkManager)
            {
                UnregisterNetworkHandlers(_registeredNetworkManager);
            }

            if (_networkHandlersRegistered && _registeredNetworkManager == networkManager)
            {
                return;
            }

            _registeredNetworkManager = networkManager;
            networkManager.OnClientConnectedCallback += HandleNetworkClientConnected;

            var messagingManager = networkManager.CustomMessagingManager;
            messagingManager.RegisterNamedMessageHandler(ResetRequestMessage, HandleResetRequestMessage);
            messagingManager.RegisterNamedMessageHandler(ScoreRequestMessage, HandleScoreRequestMessage);
            messagingManager.RegisterNamedMessageHandler(StateMessage, HandleStateMessage);
            _networkHandlersRegistered = true;
        }

        private void OnDestroy()
        {
            CancelPendingResetRespawn();

            if (_registeredNetworkManager != null)
            {
                UnregisterNetworkHandlers(_registeredNetworkManager);
                _registeredNetworkManager = null;
            }
        }

        private void UnregisterNetworkHandlers(NetworkManager networkManager)
        {
            if (!_networkHandlersRegistered)
            {
                return;
            }

            networkManager.OnClientConnectedCallback -= HandleNetworkClientConnected;

            var messagingManager = networkManager.CustomMessagingManager;
            if (messagingManager == null)
            {
                _networkHandlersRegistered = false;
                return;
            }

            messagingManager.UnregisterNamedMessageHandler(ResetRequestMessage);
            messagingManager.UnregisterNamedMessageHandler(ScoreRequestMessage);
            messagingManager.UnregisterNamedMessageHandler(StateMessage);
            _networkHandlersRegistered = false;
        }

        private void RegisterNetworkPrefabsIfPossible()
        {
            if (!enableMultiplayerSync || _networkPrefabsRegistered)
            {
                return;
            }

            var networkManager = NetworkManager.Singleton;
            if (networkManager == null || networkManager.IsListening)
            {
                return;
            }

            var registeredBlack = TryRegisterNetworkPrefab(networkManager, _resolvedBlackTemplate);
            var registeredRed = TryRegisterNetworkPrefab(networkManager, _resolvedRedTemplate);
            _networkPrefabsRegistered = registeredBlack || registeredRed;
        }

        private static bool TryRegisterNetworkPrefab(NetworkManager networkManager, GameObject prefab)
        {
            if (networkManager == null || prefab == null || !prefab.TryGetComponent<NetworkObject>(out _))
            {
                return false;
            }

            var prefabs = networkManager.NetworkConfig.Prefabs.Prefabs;
            for (var i = 0; i < prefabs.Count; i++)
            {
                if (prefabs[i] != null && prefabs[i].Prefab == prefab)
                {
                    return true;
                }
            }

            return networkManager.NetworkConfig.Prefabs.Add(new NetworkPrefab
            {
                Prefab = prefab
            });
        }

        private bool TryRequestNetworkResetFromClient()
        {
            if (!IsNetworkClientOnlyActive())
            {
                return false;
            }

            EnsureNetworkSetup();
            using var writer = new FastBufferWriter(128, Allocator.Temp);
            writer.WriteValueSafe(StationId);
            NetworkManager.Singleton.CustomMessagingManager.SendNamedMessage(
                ResetRequestMessage,
                NetworkManager.ServerClientId,
                writer);
            return true;
        }

        private bool TryRequestNetworkScoreFromClient(YoTyanBall ball, int scoreValue)
        {
            if (ball == null || !IsNetworkClientOnlyActive())
            {
                return false;
            }

            if (!ball.TryGetComponent<NetworkObject>(out var networkObject) || !networkObject.IsSpawned)
            {
                return false;
            }

            EnsureNetworkSetup();
            using var writer = new FastBufferWriter(160, Allocator.Temp);
            writer.WriteValueSafe(StationId);
            writer.WriteValueSafe(networkObject.NetworkObjectId);
            writer.WriteValueSafe(Mathf.Clamp(scoreValue, 1, 3));
            NetworkManager.Singleton.CustomMessagingManager.SendNamedMessage(
                ScoreRequestMessage,
                NetworkManager.ServerClientId,
                writer);
            return true;
        }

        private void HandleResetRequestMessage(ulong senderClientId, FastBufferReader reader)
        {
            reader.ReadValueSafe(out string stationId);
            if (!IsNetworkServerActive() || !IsMatchingStationId(stationId))
            {
                return;
            }

            if (!TryBeginResetCooldown())
            {
                return;
            }

            PerformResetState();
        }

        private void HandleScoreRequestMessage(ulong senderClientId, FastBufferReader reader)
        {
            reader.ReadValueSafe(out string stationId);
            reader.ReadValueSafe(out ulong networkObjectId);
            reader.ReadValueSafe(out int scoreValue);

            if (!IsNetworkServerActive() || !IsMatchingStationId(stationId))
            {
                return;
            }

            if (!TryResolveNetworkBall(networkObjectId, out var ball))
            {
                return;
            }

            PerformScoreBall(ball, Mathf.Clamp(scoreValue, 1, 3));
        }

        private void HandleStateMessage(ulong senderClientId, FastBufferReader reader)
        {
            reader.ReadValueSafe(out string stationId);
            reader.ReadValueSafe(out int newBlackScore);
            reader.ReadValueSafe(out int newRedScore);
            reader.ReadValueSafe(out int blackBallsRemaining);
            reader.ReadValueSafe(out int redBallsRemaining);
            reader.ReadValueSafe(out int newCurrentTurn);
            reader.ReadValueSafe(out bool newGameOver);

            if (IsNetworkServerActive() || !IsMatchingStationId(stationId))
            {
                return;
            }

            ApplyNetworkState(
                newBlackScore,
                newRedScore,
                blackBallsRemaining,
                redBallsRemaining,
                (YoTyanBallTeam)newCurrentTurn,
                newGameOver);
        }

        private void HandleNetworkClientConnected(ulong clientId)
        {
            if (!IsNetworkServerActive() || !_scoreboardHasBeenShown)
            {
                return;
            }

            SendNetworkStateToClient(clientId);
        }

        private bool TryResolveNetworkBall(ulong networkObjectId, out YoTyanBall ball)
        {
            ball = null;
            if (NetworkManager.Singleton == null
                || !NetworkManager.Singleton.SpawnManager.SpawnedObjects.TryGetValue(networkObjectId, out var networkObject)
                || networkObject == null)
            {
                return false;
            }

            return networkObject.TryGetComponent(out ball) && ball != null;
        }

        private void BroadcastNetworkStateIfServer()
        {
            if (!IsNetworkServerActive())
            {
                return;
            }

            using var writer = CreateStateWriter();
            NetworkManager.Singleton.CustomMessagingManager.SendNamedMessageToAll(StateMessage, writer);
        }

        private void SendNetworkStateToClient(ulong clientId)
        {
            using var writer = CreateStateWriter();
            NetworkManager.Singleton.CustomMessagingManager.SendNamedMessage(StateMessage, clientId, writer);
        }

        private FastBufferWriter CreateStateWriter()
        {
            var writer = new FastBufferWriter(256, Allocator.Temp);
            writer.WriteValueSafe(StationId);
            writer.WriteValueSafe(blackScore);
            writer.WriteValueSafe(redScore);
            writer.WriteValueSafe(GetRemainingBallCount(YoTyanBallTeam.Black));
            writer.WriteValueSafe(GetRemainingBallCount(YoTyanBallTeam.Red));
            writer.WriteValueSafe((int)currentTurn);
            writer.WriteValueSafe(gameOver);
            return writer;
        }

        private void EnsureServerOwnsNetworkBall(YoTyanBall ball)
        {
            if (!IsNetworkServerActive()
                || ball == null
                || !ball.TryGetComponent<NetworkObject>(out var networkObject)
                || !networkObject.IsSpawned
                || networkObject.OwnerClientId == NetworkManager.ServerClientId)
            {
                return;
            }

            EnsureTransferableOwnership(networkObject);
            networkObject.ChangeOwnership(NetworkManager.ServerClientId);
        }

        private static void EnsureTransferableOwnership(NetworkObject networkObject)
        {
            if (networkObject == null || networkObject.IsOwnershipTransferable)
            {
                return;
            }

            networkObject.SetOwnershipStatus(
                NetworkObject.OwnershipStatus.Transferable,
                false,
                NetworkObject.OwnershipLockActions.SetAndUnlock);
        }

        private bool CanProcessReturn(YoTyanBall ball)
        {
            if (ball == null)
            {
                return false;
            }

            if (_lastReturnedAt.TryGetValue(ball, out var lastReturnedAt)
                && Time.time - lastReturnedAt < 0.5f)
            {
                return false;
            }

            _lastReturnedAt[ball] = Time.time;
            return true;
        }

        private bool TryBeginResetCooldown()
        {
            if (!Application.isPlaying || resetCooldown <= 0f)
            {
                return true;
            }

            var now = Time.unscaledTime;
            if (now - _lastResetStartedAt < resetCooldown)
            {
                return false;
            }

            _lastResetStartedAt = now;
            return true;
        }

        private bool IsNetworkClientOnlyActive()
        {
            var networkManager = NetworkManager.Singleton;
            return enableMultiplayerSync
                && networkManager != null
                && networkManager.IsListening
                && networkManager.IsClient
                && !networkManager.IsServer;
        }

        private bool IsNetworkServerActive()
        {
            var networkManager = NetworkManager.Singleton;
            return enableMultiplayerSync
                && networkManager != null
                && networkManager.IsListening
                && networkManager.IsServer;
        }

        private bool IsMatchingStationId(string stationId)
        {
            return string.Equals(stationId, StationId, StringComparison.Ordinal);
        }

        private void ResolveDefaultSfx()
        {
            if (slotGoalClip == null)
            {
                slotGoalClip = ArcadeSfxPlayer.LoadEditorClip(DefaultSlotGoalClipPath);
            }

            if (gameFinishClip == null)
            {
                gameFinishClip = ArcadeSfxPlayer.LoadEditorClip(DefaultGameFinishClipPath);
            }
        }

        private void PlaySlotGoalSfx(Vector3 worldPosition)
        {
            ArcadeSfxPlayer.PlayOneShot(
                slotGoalClip,
                worldPosition,
                slotGoalVolume,
                1f,
                sfxSpatialBlend,
                sfxMaxDistance,
                "YoTyan_SFX_SlotGoal");
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
                sfxSpatialBlend,
                sfxMaxDistance,
                "YoTyan_SFX_GameFinish");
        }

        private int GetRemainingBallCount(YoTyanBallTeam team)
        {
            var count = 0;
            for (var i = 0; i < _spawnedBalls.Count; i++)
            {
                var ball = _spawnedBalls[i];
                if (ball == null || ball.Team != team || ball.IsScored)
                {
                    continue;
                }

                count++;
            }

            return count;
        }

        private static YoTyanBallTeam GetOppositeTeam(YoTyanBallTeam team)
        {
            return team == YoTyanBallTeam.Black ? YoTyanBallTeam.Red : YoTyanBallTeam.Black;
        }

        private static bool TryGetScoreValueFromName(string objectName, out int scoreValue)
        {
            if (objectName.StartsWith("Slot3PointTrigger", StringComparison.Ordinal))
            {
                scoreValue = 3;
                return true;
            }

            if (objectName.StartsWith("Slot2PointTrigger", StringComparison.Ordinal))
            {
                scoreValue = 2;
                return true;
            }

            if (objectName.StartsWith("Slot1PointTrigger", StringComparison.Ordinal))
            {
                scoreValue = 1;
                return true;
            }

            scoreValue = 0;
            return false;
        }

        private static bool IsReturnZoneName(string objectName)
        {
            return string.Equals(objectName, "ReturnZone", StringComparison.Ordinal)
                || string.Equals(objectName, "FloorReturnZone", StringComparison.Ordinal);
        }

        private static void HideTemplateBall(GameObject template)
        {
            if (template == null || !template.scene.IsValid())
            {
                return;
            }

            template.SetActive(false);
        }

        private static void HideScenePreviewBall(string objectName)
        {
            var preview = FindSceneTransform(objectName);
            if (preview != null)
            {
                preview.gameObject.SetActive(false);
            }
        }

        private GameObject ResolveBallTemplate(GameObject assignedTemplate, string fallbackName)
        {
            if (assignedTemplate != null)
            {
                return assignedTemplate;
            }

            var transformCandidate = FindSceneTransform(fallbackName);
            return transformCandidate != null ? transformCandidate.gameObject : null;
        }

        private static Transform FindSceneTransform(string objectName)
        {
            var sceneObject = GameObject.Find(objectName);
            return sceneObject != null ? sceneObject.transform : null;
        }

        private static Transform FindChildByName(Transform parent, string childName)
        {
            return parent == null ? null : parent.Find(childName);
        }

        private Canvas FindWorldSpaceCanvasInScene()
        {
            var canvases = FindObjectsByType<Canvas>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            for (var i = 0; i < canvases.Length; i++)
            {
                var canvas = canvases[i];
                if (canvas == null || canvas.gameObject.scene != gameObject.scene)
                {
                    continue;
                }

                if (canvas.renderMode == RenderMode.WorldSpace)
                {
                    return canvas;
                }
            }

            return null;
        }

        private Canvas ResolveScoreboardCanvas()
        {
            var namedCanvas = FindSceneTransform("YoTyanCanvas");
            if (namedCanvas != null && namedCanvas.TryGetComponent<Canvas>(out var canvas))
            {
                return canvas;
            }

            return FindWorldSpaceCanvasInScene();
        }

        private static List<Transform> GetOrderedSpawnPoints(Transform createZone)
        {
            var points = new List<Transform>();
            if (createZone == null)
            {
                return points;
            }

            for (var i = 0; i < createZone.childCount; i++)
            {
                var child = createZone.GetChild(i);
                if (child.name.StartsWith("SpawnPoint", StringComparison.Ordinal))
                {
                    points.Add(child);
                }
            }

            points.Sort((left, right) => ExtractIndex(left.name).CompareTo(ExtractIndex(right.name)));
            return points;
        }

        private static int ExtractIndex(string value)
        {
            var number = 0;
            var started = false;

            for (var i = 0; i < value.Length; i++)
            {
                if (!char.IsDigit(value[i]))
                {
                    continue;
                }

                started = true;
                number = (number * 10) + (value[i] - '0');
            }

            return started ? number : int.MaxValue;
        }

        private static T GetOrAddComponent<T>(GameObject target) where T : Component
        {
            if (!target.TryGetComponent<T>(out var component))
            {
                component = target.AddComponent<T>();
            }

            return component;
        }

        private void OnDrawGizmosSelected()
        {
            if (!drawSpawnPointGizmos)
            {
                return;
            }

            DrawSpawnPointGizmos(blackSpawnPoints, blackSpawnPointColor);
            DrawSpawnPointGizmos(redSpawnPoints, redSpawnPointColor);
        }

        private void DrawSpawnPointGizmos(List<Transform> spawnPoints, Color color)
        {
            Gizmos.color = color;

            for (var i = 0; i < spawnPoints.Count; i++)
            {
                var spawnPoint = spawnPoints[i];
                if (spawnPoint == null)
                {
                    continue;
                }

                Gizmos.DrawSphere(spawnPoint.position, spawnPointGizmoRadius);
            }
        }

        private static void DestroyObject(GameObject target)
        {
            if (target == null)
            {
                return;
            }

#if UNITY_EDITOR
            if (!Application.isPlaying)
            {
                DestroyImmediate(target);
                return;
            }
#endif

            Destroy(target);
        }

#if UNITY_EDITOR
        private void TryAssignDefaultPrefabs()
        {
            if (blackBallPrefab == null)
            {
                blackBallPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Prefabs/YoTyan/YotyanBall_Black.prefab");
            }

            if (redBallPrefab == null)
            {
                redBallPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Prefabs/YoTyan/YotyanBall_Red.prefab");
            }
        }
#endif
    }
}
