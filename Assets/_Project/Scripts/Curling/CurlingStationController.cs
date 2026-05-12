using System.Collections.Generic;
using ArcadeRoom.Audio;
using ArcadeRoom.Reset;
using ArcadeRoom.Stations;
using MikeNspired.XRIStarterKit;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;

namespace ArcadeRoom.Curling
{
    [DisallowMultipleComponent]
    public class CurlingStationController : ArcadeStationRoot
    {
        private const string DefaultGameFinishClipPath = "Assets/_Project/Audio/SFX/Game_finish.wav";
        private const string ResetRequestMessage = "ArcadeRoom.Curling.ResetRequest";
        private const string StoneUsedRequestMessage = "ArcadeRoom.Curling.StoneUsedRequest";
        private const string StateMessage = "ArcadeRoom.Curling.State";

        [Header("Stone Spawn")]
        [SerializeField] private GameObject redStonePrefab;
        [SerializeField] private GameObject blueStonePrefab;
        [SerializeField] private Collider redCreateZone;
        [SerializeField] private Collider blueCreateZone;
        [SerializeField] private Transform stoneSpawnRoot;
        [SerializeField] private bool spawnOnStart;
        [SerializeField, Min(0)] private int redStoneCount = 5;
        [SerializeField, Min(0)] private int blueStoneCount = 5;
        [SerializeField, Min(0f)] private float edgePadding = 0.08f;
        [SerializeField, Min(0f)] private float spacingPadding = 0.08f;
        [SerializeField, Min(0f)] private float spawnSurfaceOffset = 0.02f;
        [SerializeField] private bool useSpawnYOverride;
        [SerializeField] private float spawnYOverride = 0.49f;

        [Header("Gameplay")]
        [SerializeField] private CurlingTeam startingTurn = CurlingTeam.Red;
        [SerializeField] private bool enableMultiplayerSync = true;

        [Header("Scoreboard")]
        [SerializeField] private CurlingScoreboardUI scoreboardUI;
        [SerializeField] private bool findScoreZonesInScene = true;
        [SerializeField] private bool hideScoreboardUntilFirstReset = true;
        [SerializeField] private List<CurlingScoreZone> scoreZones = new();

        [Header("SFX")]
        [SerializeField] private AudioClip gameFinishClip;
        [SerializeField, Range(0f, 1f)] private float gameFinishVolume = 0.9f;
        [SerializeField, Range(0f, 1f)] private float gameFinishSpatialBlend = 1f;
        [SerializeField, Min(0.1f)] private float gameFinishMaxDistance = 8f;

        private readonly List<GameObject> _spawnedStones = new();
        private GameObject _resolvedRedTemplate;
        private GameObject _resolvedBlueTemplate;
        private CurlingTeam _currentTurn;
        private int _redScore;
        private int _blueScore;
        private int _redBallsRemaining;
        private int _blueBallsRemaining;
        private bool _gameOver;
        private bool _scoreboardHasBeenShown;
        private bool _finishSfxPlayed;
        private bool _networkPrefabsRegistered;
        private bool _networkHandlersRegistered;
        private NetworkManager _registeredNetworkManager;

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
            gameFinishMaxDistance = Mathf.Max(0.1f, gameFinishMaxDistance);
            ResolveDefaultSfx();
        }

        private void Start()
        {
            EnsureSetupComplete();
            EnsureNetworkSetup();
            ResetGameplayState();

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
                RefreshScoreboard();
            }
        }

        private void FixedUpdate()
        {
            EnsureNetworkSetup();

            if (_spawnedStones.Count == 0)
            {
                RefreshSpawnedStoneCache();
                if (_spawnedStones.Count == 0)
                {
                    return;
                }
            }

            if (RecalculateScores())
            {
                RefreshScoreboard();
            }
        }

        public override void ResetState()
        {
            if (TryRequestNetworkResetFromClient())
            {
                return;
            }

            PerformResetState();
        }

        public void RequestNetworkStoneUsed(CurlingStone stone)
        {
            if (stone == null || !IsNetworkClientOnlyActive())
            {
                return;
            }

            if (!stone.TryGetComponent<NetworkObject>(out var networkObject) || !networkObject.IsSpawned)
            {
                return;
            }

            EnsureNetworkSetup();
            using var writer = new FastBufferWriter(128, Allocator.Temp);
            writer.WriteValueSafe(StationId);
            writer.WriteValueSafe(networkObject.NetworkObjectId);
            NetworkManager.Singleton.CustomMessagingManager.SendNamedMessage(
                StoneUsedRequestMessage,
                NetworkManager.ServerClientId,
                writer);
        }

        private void PerformResetState()
        {
            EnsureSetupComplete();
            base.ResetState();

            ClearSpawnedStones();
            ResetGameplayState();
            SpawnAllStones();
            RecalculateScores();
            SetScoreboardVisible(true);
            RefreshScoreboard();
            RefreshResettables();
        }

        public void NotifyStoneUsed(CurlingStone stone)
        {
            if (stone == null || _gameOver)
            {
                return;
            }

            if (stone.Team == CurlingTeam.Red)
            {
                _redBallsRemaining = Mathf.Max(0, _redBallsRemaining - 1);
            }
            else
            {
                _blueBallsRemaining = Mathf.Max(0, _blueBallsRemaining - 1);
            }

            var wasGameOver = _gameOver;
            _gameOver = _redBallsRemaining <= 0 && _blueBallsRemaining <= 0;
            if (_gameOver && !wasGameOver)
            {
                PlayGameFinishSfx(stone.transform.position);
            }

            AdvanceTurn();
            RefreshScoreboard();
        }

        private void EnsureSetupComplete()
        {
            ResolveReferences();
            RegisterNetworkPrefabsIfPossible();
            ResolveScoreboard();
            RebuildScoreZoneCache();
            BuildResetButtonPrototype();
            HideTemplateStone(_resolvedRedTemplate);
            HideTemplateStone(_resolvedBlueTemplate);
        }

        private void ResolveReferences()
        {
            if (stoneSpawnRoot == null)
            {
                stoneSpawnRoot = ResolveNamedTransform("StoneSpawnRoot") ?? SpawnRoot ?? GameplayRoot;
            }

            if (redCreateZone == null)
            {
                redCreateZone = ResolveNamedTransform("Red_CreateZone")?.GetComponent<Collider>();
            }

            if (blueCreateZone == null)
            {
                blueCreateZone = ResolveNamedTransform("Blue_CreateZone")?.GetComponent<Collider>();
            }

            _resolvedRedTemplate = ResolveStoneTemplate(redStonePrefab, "Red_CurlingStone", "Red_CurlingBall");
            _resolvedBlueTemplate = ResolveStoneTemplate(blueStonePrefab, "Blue_CurlingStone", "Blue_CurlingBall");
        }

        private void ResetGameplayState()
        {
            _currentTurn = startingTurn;
            _redScore = 0;
            _blueScore = 0;
            _redBallsRemaining = redStoneCount;
            _blueBallsRemaining = blueStoneCount;
            _gameOver = false;
            _finishSfxPlayed = false;
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

        private void SpawnAllStones()
        {
            SpawnTeamStones(_resolvedRedTemplate, redCreateZone, redStoneCount, "Red_CurlingStone", CurlingTeam.Red);
            SpawnTeamStones(_resolvedBlueTemplate, blueCreateZone, blueStoneCount, "Blue_CurlingStone", CurlingTeam.Blue);
        }

        private void SpawnTeamStones(GameObject template, Collider zone, int count, string baseName, CurlingTeam team)
        {
            if (template == null || zone == null || count <= 0)
            {
                return;
            }

            var parent = stoneSpawnRoot != null ? stoneSpawnRoot : (GameplayRoot != null ? GameplayRoot : transform);
            var positions = BuildSpawnPositions(zone, template, count);
            var rotation = template.transform.rotation;
            var networkSpawn = IsNetworkServerActive() && template.TryGetComponent<NetworkObject>(out _);

            for (var i = 0; i < positions.Count; i++)
            {
                var instance = networkSpawn
                    ? Instantiate(template, positions[i], rotation)
                    : Instantiate(template, positions[i], rotation, parent);

                instance.name = $"{baseName}_{i + 1:00}";
                ConfigureSpawnedStone(instance, team);
                instance.SetActive(true);

                if (instance.TryGetComponent<Rigidbody>(out var body))
                {
                    body.linearVelocity = Vector3.zero;
                    body.angularVelocity = Vector3.zero;
                }

                _spawnedStones.Add(instance);

                if (networkSpawn && instance.TryGetComponent<NetworkObject>(out var networkObject) && !networkObject.IsSpawned)
                {
                    networkObject.Spawn(true);
                }
            }
        }

        private List<Vector3> BuildSpawnPositions(Collider zone, GameObject template, int count)
        {
            var positions = new List<Vector3>(count);
            var bounds = zone.bounds;
            var footprint = GetStoneFootprint(template);
            var stoneHeight = GetStoneHeight(template);
            var cellWidth = footprint.x + spacingPadding;
            var cellDepth = footprint.y + spacingPadding;
            var usableWidth = Mathf.Max(footprint.x, bounds.size.x - edgePadding * 2f);
            var usableDepth = Mathf.Max(footprint.y, bounds.size.z - edgePadding * 2f);
            var columns = Mathf.Max(1, Mathf.FloorToInt((usableWidth + spacingPadding) / Mathf.Max(0.01f, cellWidth)));
            var maxRows = Mathf.Max(1, Mathf.FloorToInt((usableDepth + spacingPadding) / Mathf.Max(0.01f, cellDepth)));

            columns = Mathf.Min(columns, count);
            var rows = Mathf.CeilToInt(count / (float)columns);
            if (rows > maxRows)
            {
                rows = maxRows;
                columns = Mathf.CeilToInt(count / (float)rows);
            }

            var stepX = columns <= 1 ? 0f : Mathf.Min(cellWidth, usableWidth / (columns - 1));
            var stepZ = rows <= 1 ? 0f : Mathf.Min(cellDepth, usableDepth / (rows - 1));
            var startX = bounds.center.x - stepX * (columns - 1) * 0.5f;
            var startZ = bounds.center.z - stepZ * (rows - 1) * 0.5f;
            var y = useSpawnYOverride
                ? spawnYOverride
                : bounds.min.y + stoneHeight * 0.5f + spawnSurfaceOffset;

            for (var index = 0; index < count; index++)
            {
                var row = index / columns;
                var column = index % columns;
                var position = new Vector3(
                    startX + column * stepX,
                    y,
                    startZ + row * stepZ);

                positions.Add(position);
            }

            return positions;
        }

        private void ConfigureSpawnedStone(GameObject instance, CurlingTeam team)
        {
            if (instance == null)
            {
                return;
            }

            if (!instance.TryGetComponent<CurlingStone>(out var stone))
            {
                stone = instance.AddComponent<CurlingStone>();
            }

            stone.Initialize(team, this);
        }

        private void ClearSpawnedStones()
        {
            RefreshSpawnedStoneCache();

            var stonesToClear = new List<GameObject>(_spawnedStones);
            CollectNetworkSpawnedStones(stonesToClear);

            for (var i = 0; i < stonesToClear.Count; i++)
            {
                var stone = stonesToClear[i];
                if (stone == null)
                {
                    continue;
                }

                if (IsNetworkServerActive()
                    && stone.TryGetComponent<NetworkObject>(out var networkObject)
                    && networkObject.IsSpawned)
                {
                    EnsureServerOwnsNetworkObject(networkObject);
                    networkObject.Despawn(true);
                    continue;
                }

                Destroy(stone);
            }

            _spawnedStones.Clear();
        }

        private static Vector2 GetStoneFootprint(GameObject template)
        {
            if (template == null)
            {
                return new Vector2(0.4f, 0.4f);
            }

            if (template.TryGetComponent<CapsuleCollider>(out var capsule))
            {
                var radiusX = capsule.radius * Mathf.Abs(template.transform.localScale.x);
                var radiusZ = capsule.radius * Mathf.Abs(template.transform.localScale.z);
                return new Vector2(radiusX * 2f, radiusZ * 2f);
            }

            if (template.TryGetComponent<SphereCollider>(out var sphere))
            {
                var radius = sphere.radius * Mathf.Max(
                    Mathf.Abs(template.transform.localScale.x),
                    Mathf.Abs(template.transform.localScale.z));
                return new Vector2(radius * 2f, radius * 2f);
            }

            if (template.TryGetComponent<BoxCollider>(out var box))
            {
                var scale = template.transform.localScale;
                return new Vector2(
                    box.size.x * Mathf.Abs(scale.x),
                    box.size.z * Mathf.Abs(scale.z));
            }

            return new Vector2(0.4f, 0.4f);
        }

        private static float GetStoneHeight(GameObject template)
        {
            if (template == null)
            {
                return 0.5f;
            }

            if (template.TryGetComponent<CapsuleCollider>(out var capsule))
            {
                return capsule.height * Mathf.Abs(template.transform.localScale.y);
            }

            if (template.TryGetComponent<SphereCollider>(out var sphere))
            {
                return sphere.radius * 2f * Mathf.Abs(template.transform.localScale.y);
            }

            if (template.TryGetComponent<BoxCollider>(out var box))
            {
                return box.size.y * Mathf.Abs(template.transform.localScale.y);
            }

            return 0.5f;
        }

        private void HideTemplateStone(GameObject template)
        {
            if (template == null || !template.scene.IsValid())
            {
                return;
            }

            template.SetActive(false);
        }

        private GameObject ResolveStoneTemplate(GameObject assignedTemplate, params string[] fallbackNames)
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

            return null;
        }

        private bool RecalculateScores()
        {
            var newRedScore = 0;
            var newBlueScore = 0;

            if (scoreZones.Count == 0)
            {
                RebuildScoreZoneCache();
            }

            for (var i = 0; i < _spawnedStones.Count; i++)
            {
                var stoneObject = _spawnedStones[i];
                if (stoneObject == null || !stoneObject.activeInHierarchy)
                {
                    continue;
                }

                var stone = stoneObject.GetComponent<CurlingStone>();
                if (stone == null)
                {
                    continue;
                }

                var score = CalculateStoneScore(stone);
                if (stone.Team == CurlingTeam.Red)
                {
                    newRedScore += score;
                }
                else
                {
                    newBlueScore += score;
                }
            }

            if (newRedScore == _redScore && newBlueScore == _blueScore)
            {
                return false;
            }

            _redScore = newRedScore;
            _blueScore = newBlueScore;
            return true;
        }

        private int CalculateStoneScore(CurlingStone stone)
        {
            var total = 0;
            for (var i = 0; i < scoreZones.Count; i++)
            {
                var zone = scoreZones[i];
                if (zone != null && zone.ContainsProbe(stone.ScoreProbe))
                {
                    total += zone.PointValue;
                }
            }

            return total;
        }

        private void AdvanceTurn()
        {
            if (_gameOver)
            {
                return;
            }

            var nextTurn = _currentTurn == CurlingTeam.Red ? CurlingTeam.Blue : CurlingTeam.Red;
            if (nextTurn == CurlingTeam.Red && _redBallsRemaining <= 0 && _blueBallsRemaining > 0)
            {
                nextTurn = CurlingTeam.Blue;
            }
            else if (nextTurn == CurlingTeam.Blue && _blueBallsRemaining <= 0 && _redBallsRemaining > 0)
            {
                nextTurn = CurlingTeam.Red;
            }

            _currentTurn = nextTurn;
        }

        private void ResolveScoreboard()
        {
            if (scoreboardUI == null)
            {
                scoreboardUI = GetComponentInChildren<CurlingScoreboardUI>(true);
            }

            if (scoreboardUI == null)
            {
                var scoreboards = FindObjectsByType<CurlingScoreboardUI>(FindObjectsInactive.Include, FindObjectsSortMode.None);
                if (scoreboards.Length > 0)
                {
                    scoreboardUI = scoreboards[0];
                }
            }

            scoreboardUI?.EnsureLayout();
        }

        private void RebuildScoreZoneCache()
        {
            scoreZones.RemoveAll(zone => zone == null);
            if (scoreZones.Count > 0)
            {
                return;
            }

            var childZones = GetComponentsInChildren<CurlingScoreZone>(true);
            for (var i = 0; i < childZones.Length; i++)
            {
                scoreZones.Add(childZones[i]);
            }

            if (scoreZones.Count > 0 || !findScoreZonesInScene)
            {
                return;
            }

            var sceneZones = FindObjectsByType<CurlingScoreZone>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            for (var i = 0; i < sceneZones.Length; i++)
            {
                if (sceneZones[i] != null && !scoreZones.Contains(sceneZones[i]))
                {
                    scoreZones.Add(sceneZones[i]);
                }
            }
        }

        private void RefreshScoreboard()
        {
            ResolveScoreboard();
            scoreboardUI?.Refresh(
                _redScore,
                _blueScore,
                _redBallsRemaining,
                _blueBallsRemaining,
                _currentTurn,
                _gameOver);

            BroadcastNetworkStateIfServer();
        }

        private void ApplyNetworkState(
            int redScore,
            int blueScore,
            int redBallsRemaining,
            int blueBallsRemaining,
            CurlingTeam currentTurn,
            bool gameOver)
        {
            _redScore = redScore;
            _blueScore = blueScore;
            _redBallsRemaining = redBallsRemaining;
            _blueBallsRemaining = blueBallsRemaining;
            _currentTurn = currentTurn;
            _gameOver = gameOver;

            SetScoreboardVisible(true);
            ResolveScoreboard();
            scoreboardUI?.Refresh(
                _redScore,
                _blueScore,
                _redBallsRemaining,
                _blueBallsRemaining,
                _currentTurn,
                _gameOver);
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
            messagingManager.RegisterNamedMessageHandler(StoneUsedRequestMessage, HandleStoneUsedRequestMessage);
            messagingManager.RegisterNamedMessageHandler(StateMessage, HandleStateMessage);
            _networkHandlersRegistered = true;
        }

        private void OnDestroy()
        {
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
            messagingManager.UnregisterNamedMessageHandler(StoneUsedRequestMessage);
            messagingManager.UnregisterNamedMessageHandler(StateMessage);
            _networkHandlersRegistered = false;
        }

        private void RefreshSpawnedStoneCache()
        {
            _spawnedStones.RemoveAll(stone => stone == null);

            var stones = FindObjectsByType<CurlingStone>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            for (var i = 0; i < stones.Length; i++)
            {
                var stone = stones[i];
                if (!IsRuntimeSpawnedStone(stone) || _spawnedStones.Contains(stone.gameObject))
                {
                    continue;
                }

                if (stone.TryGetComponent<NetworkObject>(out var networkObject)
                    && NetworkManager.Singleton != null
                    && NetworkManager.Singleton.IsListening
                    && !networkObject.IsSpawned)
                {
                    continue;
                }

                _spawnedStones.Add(stone.gameObject);
            }
        }

        private void CollectNetworkSpawnedStones(List<GameObject> target)
        {
            if (!IsNetworkServerActive() || NetworkManager.Singleton == null)
            {
                return;
            }

            foreach (var pair in NetworkManager.Singleton.SpawnManager.SpawnedObjects)
            {
                var networkObject = pair.Value;
                if (networkObject == null || !networkObject.TryGetComponent<CurlingStone>(out var stone))
                {
                    continue;
                }

                if (!IsRuntimeSpawnedStone(stone) || target.Contains(stone.gameObject))
                {
                    continue;
                }

                target.Add(stone.gameObject);
            }
        }

        private bool IsRuntimeSpawnedStone(CurlingStone stone)
        {
            if (stone == null || !stone.gameObject.activeInHierarchy || stone.gameObject.scene != gameObject.scene)
            {
                return false;
            }

            if (ReferenceEquals(stone.gameObject, _resolvedRedTemplate)
                || ReferenceEquals(stone.gameObject, _resolvedBlueTemplate))
            {
                return false;
            }

            if (stone.TryGetComponent<NetworkObject>(out var networkObject) && networkObject.IsSpawned)
            {
                return true;
            }

            return stone.name.StartsWith("Red_CurlingStone_", System.StringComparison.Ordinal)
                || stone.name.StartsWith("Blue_CurlingStone_", System.StringComparison.Ordinal);
        }

        private void EnsureServerOwnsNetworkObject(NetworkObject networkObject)
        {
            if (!IsNetworkServerActive()
                || networkObject == null
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
                NetworkObject.OwnershipStatus.Distributable | NetworkObject.OwnershipStatus.Transferable,
                false,
                NetworkObject.OwnershipLockActions.SetAndUnlock);
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

            var registeredRed = TryRegisterNetworkPrefab(networkManager, _resolvedRedTemplate);
            var registeredBlue = TryRegisterNetworkPrefab(networkManager, _resolvedBlueTemplate);
            _networkPrefabsRegistered = registeredRed || registeredBlue;
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

        private void HandleResetRequestMessage(ulong senderClientId, FastBufferReader reader)
        {
            reader.ReadValueSafe(out string stationId);
            if (!IsNetworkServerActive() || !IsMatchingStationId(stationId))
            {
                return;
            }

            PerformResetState();
        }

        private void HandleStoneUsedRequestMessage(ulong senderClientId, FastBufferReader reader)
        {
            reader.ReadValueSafe(out string stationId);
            reader.ReadValueSafe(out ulong networkObjectId);

            if (!IsNetworkServerActive() || !IsMatchingStationId(stationId))
            {
                return;
            }

            if (!NetworkManager.Singleton.SpawnManager.SpawnedObjects.TryGetValue(networkObjectId, out var networkObject)
                || networkObject == null
                || !networkObject.TryGetComponent<CurlingStone>(out var stone)
                || !stone.MarkUsed())
            {
                return;
            }

            NotifyStoneUsed(stone);
        }

        private void HandleStateMessage(ulong senderClientId, FastBufferReader reader)
        {
            reader.ReadValueSafe(out string stationId);
            reader.ReadValueSafe(out int redScore);
            reader.ReadValueSafe(out int blueScore);
            reader.ReadValueSafe(out int redBallsRemaining);
            reader.ReadValueSafe(out int blueBallsRemaining);
            reader.ReadValueSafe(out int currentTurn);
            reader.ReadValueSafe(out bool gameOver);

            if (IsNetworkServerActive() || !IsMatchingStationId(stationId))
            {
                return;
            }

            ApplyNetworkState(
                redScore,
                blueScore,
                redBallsRemaining,
                blueBallsRemaining,
                (CurlingTeam)currentTurn,
                gameOver);
        }

        private void HandleNetworkClientConnected(ulong clientId)
        {
            if (!IsNetworkServerActive() || !_scoreboardHasBeenShown)
            {
                return;
            }

            SendNetworkStateToClient(clientId);
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
            writer.WriteValueSafe(_redScore);
            writer.WriteValueSafe(_blueScore);
            writer.WriteValueSafe(_redBallsRemaining);
            writer.WriteValueSafe(_blueBallsRemaining);
            writer.WriteValueSafe((int)_currentTurn);
            writer.WriteValueSafe(_gameOver);
            return writer;
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
            return string.Equals(stationId, StationId, System.StringComparison.Ordinal);
        }

        private void ResolveDefaultSfx()
        {
            if (gameFinishClip == null)
            {
                gameFinishClip = ArcadeSfxPlayer.LoadEditorClip(DefaultGameFinishClipPath);
            }
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
                gameFinishSpatialBlend,
                gameFinishMaxDistance,
                "Curling_SFX_GameFinish");
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

            ResolveScoreboard();

            if (scoreboardUI != null && scoreboardUI.gameObject.activeSelf != visible)
            {
                scoreboardUI.gameObject.SetActive(visible);
            }
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
