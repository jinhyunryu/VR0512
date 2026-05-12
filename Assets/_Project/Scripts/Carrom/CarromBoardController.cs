using System.Collections.Generic;
using ArcadeRoom.Audio;
using ArcadeRoom.Core;
using ArcadeRoom.Interaction;
using ArcadeRoom.Reset;
using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

namespace ArcadeRoom.Carrom
{
    [DisallowMultipleComponent]
    public class CarromBoardController : MonoBehaviour, IArcadeResettable
    {
        private const string DefaultGameFinishClipPath = "Assets/_Project/Audio/SFX/Game_finish.wav";

        [Header("Scene References")]
        [SerializeField] private Transform boardSurface;
        [SerializeField, FormerlySerializedAs("boardBoundsCollider")] private Collider boardFloorCollider;
        [SerializeField] private Transform wallTop;
        [SerializeField] private Transform wallBottom;
        [SerializeField] private Transform wallLeft;
        [SerializeField] private Transform wallRight;
        [SerializeField] private Transform pocketTopLeft;
        [SerializeField] private Transform pocketTopRight;
        [SerializeField] private Transform pocketBottomLeft;
        [SerializeField] private Transform pocketBottomRight;
        [SerializeField] private Transform coinCreateArea;
        [SerializeField] private Transform playerCoinCreate;
        [SerializeField] private Transform playerCoinRoot;
        [SerializeField] private Transform blackCoinRoot;
        [SerializeField] private Transform whiteCoinRoot;
        [SerializeField] private Transform coinsRoot;

        [Header("Spawn Sources")]
        [SerializeField] private GameObject playerCoinPrefab;
        [SerializeField] private GameObject blackCoinPrefab;
        [SerializeField] private GameObject whiteCoinPrefab;
        [SerializeField] private int blackCoinCount = 1;
        [SerializeField] private int whiteCoinCount = 1;

        [Header("Fallback Layout")]
        [SerializeField] private bool allowRuntimeReferenceAutofill;
        [SerializeField] private bool autoManageBoardCollider = true;
        [SerializeField] private bool autoManageWallColliders = true;
        [SerializeField] private bool autoManagePocketColliders = true;
        [SerializeField] private bool usePocketDistanceCapture = true;
        [SerializeField, Min(0f)] private float pocketCaptureRadius = 0.09f;
        [SerializeField] private bool useTargetCoinPocketMagnet = true;
        [SerializeField, Min(0f)] private float pocketMagnetRadius = 0.1f;
        [SerializeField, Min(0f)] private float pocketMagnetAcceleration = 1.6f;
        [SerializeField, Min(0f)] private float pocketMagnetMaxSpeed = 0.75f;
        [SerializeField] private Vector2 boardSize = new Vector2(0.74f, 0.74f);
        [SerializeField] private float boardThickness = 0.02f;
        [SerializeField] private float wallHeight = 0.045f;
        [SerializeField] private float wallThickness = 0.03f;
        [SerializeField] private float pocketRadius = 0.055f;
        [SerializeField] private Vector2 coinCreateSize = new Vector2(0.26f, 0.18f);
        [SerializeField] private Vector2 playerCoinCreateSize = new Vector2(0.22f, 0.14f);
        [SerializeField] private float createAreaEdgePadding = 0.006f;

        [Header("Piece Tuning")]
        [SerializeField] private float pieceThickness = 0.012f;
        [SerializeField] private float playerCoinRadius = 0.033f;
        [SerializeField] private float targetCoinRadius = 0.024f;
        [SerializeField] private bool overrideSpawnScale;
        [SerializeField] private float playerCoinSpawnScale = 1f;
        [SerializeField] private float targetCoinSpawnScale = 1f;
        [SerializeField] private float playerCoinMass = 0.085f;
        [SerializeField] private float targetCoinMass = 0.032f;
        [SerializeField] private float pieceLinearDamping = 0.9f;
        [SerializeField] private float pieceAngularDamping = 1.2f;
        [SerializeField] private float settleVelocityThreshold = 0.05f;
        [SerializeField] private bool preserveExistingRigidbodyTuning = true;

        [Header("Audio")]
        [SerializeField] private CarromSfxController sfxController;
        [SerializeField] private AudioClip gameFinishClip;
        [SerializeField, Range(0f, 1f)] private float gameFinishVolume = 0.9f;
        [SerializeField, Range(0f, 1f)] private float gameFinishSpatialBlend = 1f;
        [SerializeField, Min(0.1f)] private float gameFinishMaxDistance = 7f;

        private readonly List<CarromPiece> _pieces = new();
        private readonly List<CarromPiece> _targetPieces = new();
        private readonly List<Transform> _spawnedRoots = new();
        private CarromPiece _playerCoinPiece;
        private Collider _playerCoinCreateCollider;
        private Collider _coinCreateCollider;
        private PhysicsMaterial _runtimePhysicsMaterial;
        private GameObject _playerCoinSource;
        private GameObject _blackCoinSource;
        private GameObject _whiteCoinSource;
        private bool _runtimeBuilt;
        private bool _templatesPrepared;
        private bool _gameFinished;
        private bool _finishSfxPlayed;

        public Transform BoardSurface => transform;
        public Transform StrikerStartLine => playerCoinCreate;
        public float PieceCenterHeight => pieceThickness * 0.5f;
        public float CoinRadius => targetCoinRadius;
        public float StrikerRadius => playerCoinRadius;
        public Quaternion PieceRotation => transform.rotation;
        public bool GameFinished => _gameFinished;
        public Vector2 PlayableHalfExtents
        {
            get
            {
                var bounds = GetBoardBounds();
                return new Vector2(bounds.extents.x, bounds.extents.z);
            }
        }

        private void Awake()
        {
            ResolveReferences();
            ResolveDefaultSfx();
        }

        private void OnValidate()
        {
            ResolveReferences();
            ResolveDefaultSfx();
            blackCoinCount = Mathf.Max(0, blackCoinCount);
            whiteCoinCount = Mathf.Max(0, whiteCoinCount);
            playerCoinSpawnScale = Mathf.Max(0.1f, playerCoinSpawnScale);
            targetCoinSpawnScale = Mathf.Max(0.1f, targetCoinSpawnScale);
            pocketCaptureRadius = Mathf.Max(0f, pocketCaptureRadius);
            pocketMagnetRadius = Mathf.Max(pocketCaptureRadius, pocketMagnetRadius);
            pocketMagnetAcceleration = Mathf.Max(0f, pocketMagnetAcceleration);
            pocketMagnetMaxSpeed = Mathf.Max(0f, pocketMagnetMaxSpeed);
            gameFinishMaxDistance = Mathf.Max(0.1f, gameFinishMaxDistance);
        }

        private void FixedUpdate()
        {
            TryCaptureTargetPiecesNearPockets();
        }

        public void EnsureRuntimeSetup()
        {
            if (_runtimeBuilt)
            {
                return;
            }

            ForceReadyState();
        }

        public void ResetState()
        {
            ForceReadyState();
        }

        public void ForceReadyState()
        {
            ResolveReferences();
            EnsureBoardCollider();
            EnsureWall(wallTop, true);
            EnsureWall(wallBottom, true);
            EnsureWall(wallLeft, false);
            EnsureWall(wallRight, false);
            EnsurePocket(pocketTopLeft, "RuntimePocketTL");
            EnsurePocket(pocketTopRight, "RuntimePocketTR");
            EnsurePocket(pocketBottomLeft, "RuntimePocketBL");
            EnsurePocket(pocketBottomRight, "RuntimePocketBR");
            EnsureCoinCreateArea();
            EnsurePlayerCoinCreateArea();
            PrepareTemplateSources();
            RebuildInitialSetup();
            _runtimeBuilt = true;
        }

        public void TryPocketPiece(CarromPiece piece, Vector3 pocketWorldPosition)
        {
            if (piece == null || piece.IsPocketed)
            {
                return;
            }

            if (piece.PieceType == CarromPieceType.PlayerCoin)
            {
                return;
            }

            piece.Pocket(pocketWorldPosition);
            ResolveSfxController();
            sfxController?.PlayPocket(pocketWorldPosition);
        }

        public void NotifyPieceCollision(CarromPiece piece, Collision collision)
        {
            if (piece == null || collision == null || piece.IsPocketed)
            {
                return;
            }

            if (collision.rigidbody != null &&
                collision.rigidbody.TryGetComponent<CarromPiece>(out var otherPiece) &&
                otherPiece != null &&
                otherPiece.GetInstanceID() < piece.GetInstanceID())
            {
                return;
            }

            ResolveSfxController();
            sfxController?.PlayPieceCollision(collision);
        }

        private void TryCaptureTargetPiecesNearPockets()
        {
            if ((!usePocketDistanceCapture && !useTargetCoinPocketMagnet) || _targetPieces.Count <= 0)
            {
                return;
            }

            var captureRadius = Mathf.Max(0f, pocketCaptureRadius);
            var scanRadius = useTargetCoinPocketMagnet
                ? Mathf.Max(captureRadius, pocketMagnetRadius)
                : captureRadius;

            if (scanRadius <= 0f)
            {
                return;
            }

            var scanRadiusSqr = scanRadius * scanRadius;
            var captureRadiusSqr = captureRadius * captureRadius;
            var boardNormal = transform.up;

            for (var index = 0; index < _targetPieces.Count; index++)
            {
                var piece = _targetPieces[index];
                if (piece == null || piece.IsPocketed || !piece.gameObject.activeInHierarchy)
                {
                    continue;
                }

                if (!TryGetClosestPocketPosition(piece.transform.position, scanRadiusSqr, boardNormal, out var pocketPosition, out var sqrDistance))
                {
                    continue;
                }

                if (usePocketDistanceCapture && captureRadius > 0f && sqrDistance <= captureRadiusSqr)
                {
                    TryPocketPiece(piece, pocketPosition);
                    continue;
                }

                if (useTargetCoinPocketMagnet)
                {
                    ApplyTargetCoinPocketMagnet(piece, pocketPosition, boardNormal);
                }
            }
        }

        private bool TryGetClosestPocketPosition(
            Vector3 pieceWorldPosition,
            float radiusSqr,
            Vector3 boardNormal,
            out Vector3 pocketPosition,
            out float sqrDistance)
        {
            pocketPosition = default;
            var closestSqr = radiusSqr;
            var found = false;

            CheckPocketCapture(pocketTopLeft, pieceWorldPosition, boardNormal, ref closestSqr, ref pocketPosition, ref found);
            CheckPocketCapture(pocketTopRight, pieceWorldPosition, boardNormal, ref closestSqr, ref pocketPosition, ref found);
            CheckPocketCapture(pocketBottomLeft, pieceWorldPosition, boardNormal, ref closestSqr, ref pocketPosition, ref found);
            CheckPocketCapture(pocketBottomRight, pieceWorldPosition, boardNormal, ref closestSqr, ref pocketPosition, ref found);

            sqrDistance = closestSqr;
            return found;
        }

        private void ApplyTargetCoinPocketMagnet(CarromPiece piece, Vector3 pocketWorldPosition, Vector3 boardNormal)
        {
            if (piece == null || piece.PieceType != CarromPieceType.TargetCoin || piece.IsPocketed)
            {
                return;
            }

            var body = piece.Body;
            if (body == null || body.isKinematic)
            {
                return;
            }

            var planarToPocket = Vector3.ProjectOnPlane(pocketWorldPosition - piece.transform.position, boardNormal);
            if (planarToPocket.sqrMagnitude <= 0.000001f)
            {
                return;
            }

            body.WakeUp();
            body.AddForce(planarToPocket.normalized * pocketMagnetAcceleration, ForceMode.Acceleration);

            if (pocketMagnetMaxSpeed <= 0f)
            {
                return;
            }

            var currentVelocity = body.linearVelocity;
            var planarVelocity = Vector3.ProjectOnPlane(currentVelocity, boardNormal);
            var maxSpeedSqr = pocketMagnetMaxSpeed * pocketMagnetMaxSpeed;
            if (planarVelocity.sqrMagnitude <= maxSpeedSqr)
            {
                return;
            }

            var cappedPlanarVelocity = planarVelocity.normalized * pocketMagnetMaxSpeed;
            body.linearVelocity = currentVelocity - planarVelocity + cappedPlanarVelocity;
        }

        private static void CheckPocketCapture(
            Transform pocket,
            Vector3 pieceWorldPosition,
            Vector3 boardNormal,
            ref float closestSqr,
            ref Vector3 pocketPosition,
            ref bool found)
        {
            if (pocket == null)
            {
                return;
            }

            var planarDelta = Vector3.ProjectOnPlane(pieceWorldPosition - pocket.position, boardNormal);
            var sqrMagnitude = planarDelta.sqrMagnitude;
            if (sqrMagnitude > closestSqr)
            {
                return;
            }

            closestSqr = sqrMagnitude;
            pocketPosition = pocket.position;
            found = true;
        }

        public void NotifyPiecePocketed(CarromPiece piece)
        {
            if (piece == null)
            {
                return;
            }

            if (piece.PieceType == CarromPieceType.PlayerCoin)
            {
                return;
            }

            if (piece.PieceType != CarromPieceType.TargetCoin)
            {
                return;
            }

            if (GetRemainingTargetCoinCount() <= 0)
            {
                _gameFinished = true;
                PlayGameFinishSfx(piece.transform.position);
            }
        }

        private void ResolveReferences()
        {
            if (Application.isPlaying && !allowRuntimeReferenceAutofill)
            {
                return;
            }

            boardSurface = ResolveReference(boardSurface, "BoardSurface");
            boardFloorCollider = ResolveColliderReference(boardFloorCollider, "BoardCollider", "BoardCollider (1)");
            wallTop = ResolveReference(wallTop, "Wall_Top");
            wallBottom = ResolveReference(wallBottom, "Wall_Bottom");
            wallLeft = ResolveReference(wallLeft, "Wall_Left", "Wall_Lift");
            wallRight = ResolveReference(wallRight, "Wall_Right");
            pocketTopLeft = ResolveReference(pocketTopLeft, "Pocket_TL");
            pocketTopRight = ResolveReference(pocketTopRight, "Pocket_TR");
            pocketBottomLeft = ResolveReference(pocketBottomLeft, "Pocket_BL");
            pocketBottomRight = ResolveReference(pocketBottomRight, "Pocket_BR");
            coinCreateArea = ResolveReference(coinCreateArea, "Coin_Create");
            playerCoinCreate = ResolveReference(playerCoinCreate, "PlayerCoin_Create");
            playerCoinRoot = ResolveReference(playerCoinRoot, "PlayerCoin", "Player_Coin", "Striker");
            blackCoinRoot = ResolveReference(blackCoinRoot, "B_Coin", "B_CoinP");
            whiteCoinRoot = ResolveReference(whiteCoinRoot, "W_Coin", "W_CoinP");
            coinsRoot = ResolveReference(coinsRoot, "Coins");
            ResolveSfxController();
        }

        private void ResolveSfxController()
        {
            if (sfxController == null)
            {
                sfxController = GetComponentInChildren<CarromSfxController>(true);
            }

            if (sfxController == null && transform.parent != null)
            {
                sfxController = transform.parent.GetComponentInChildren<CarromSfxController>(true);
            }
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
                "Carrom_SFX_GameFinish");
        }

        private void EnsureBoardCollider()
        {
            if (!autoManageBoardCollider)
            {
                return;
            }

            var collider = GetComponent<Collider>();
            if (collider == null)
            {
                var boxCollider = gameObject.AddComponent<BoxCollider>();
                boxCollider.center = new Vector3(0f, -boardThickness * 0.5f, 0f);
                boxCollider.size = new Vector3(boardSize.x, boardThickness, boardSize.y);
                collider = boxCollider;
            }

            collider.isTrigger = false;
            AssignPhysicsMaterial(collider);
            boardFloorCollider = collider;
        }

        private void EnsureWall(Transform wallAnchor, bool horizontalWall)
        {
            if (wallAnchor == null)
            {
                return;
            }

            var collider = wallAnchor.GetComponent<Collider>();
            if (collider == null)
            {
                if (!autoManageWallColliders)
                {
                    return;
                }

                var boxCollider = wallAnchor.gameObject.AddComponent<BoxCollider>();
                boxCollider.size = horizontalWall
                    ? new Vector3(boardSize.x, wallHeight, wallThickness)
                    : new Vector3(wallThickness, wallHeight, boardSize.y);
                collider = boxCollider;
            }

            if (autoManageWallColliders)
            {
                collider.isTrigger = false;
                AssignPhysicsMaterial(collider);
            }
        }

        private void EnsurePocket(Transform pocketAnchor, string runtimeName)
        {
            if (pocketAnchor == null)
            {
                return;
            }

            var collider = pocketAnchor.GetComponent<Collider>();
            if (collider == null)
            {
                if (!autoManagePocketColliders)
                {
                    return;
                }

                var sphereCollider = pocketAnchor.gameObject.AddComponent<SphereCollider>();
                sphereCollider.radius = pocketRadius;
                collider = sphereCollider;
            }

            if (autoManagePocketColliders)
            {
                collider.isTrigger = true;
            }

            var trigger = GetOrAddComponent<CarromPocketTrigger>(pocketAnchor.gameObject);
            trigger.Initialize(this);

            if (autoManagePocketColliders && !HasVisibleRenderer(pocketAnchor))
            {
                CreatePrimitiveVisual(
                    runtimeName,
                    PrimitiveType.Cylinder,
                    pocketAnchor,
                    new Vector3(0f, -PieceCenterHeight, 0f),
                    new Vector3(pocketRadius * 1.8f, 0.002f, pocketRadius * 1.8f),
                    new Color(0.06f, 0.06f, 0.06f),
                    true);
            }
        }

        private void EnsureCoinCreateArea()
        {
            if (coinCreateArea == null)
            {
                var instance = new GameObject("Coin_Create");
                instance.transform.SetParent(GetBoardGroupParent(), false);
                instance.transform.localPosition = new Vector3(0f, PieceCenterHeight, 0f);
                coinCreateArea = instance.transform;
            }

            _coinCreateCollider = coinCreateArea.GetComponent<Collider>();
            if (_coinCreateCollider == null)
            {
                var boxCollider = coinCreateArea.gameObject.AddComponent<BoxCollider>();
                boxCollider.size = new Vector3(coinCreateSize.x, pieceThickness, coinCreateSize.y);
                _coinCreateCollider = boxCollider;
            }

            _coinCreateCollider.isTrigger = true;
        }

        private void EnsurePlayerCoinCreateArea()
        {
            if (playerCoinCreate == null)
            {
                var instance = new GameObject("PlayerCoin_Create");
                instance.transform.SetParent(GetBoardGroupParent(), false);
                instance.transform.localPosition = new Vector3(0f, PieceCenterHeight, -(boardSize.y * 0.5f) + 0.13f);
                playerCoinCreate = instance.transform;
            }

            _playerCoinCreateCollider = playerCoinCreate.GetComponent<Collider>();
            if (_playerCoinCreateCollider == null)
            {
                var boxCollider = playerCoinCreate.gameObject.AddComponent<BoxCollider>();
                boxCollider.size = new Vector3(playerCoinCreateSize.x, pieceThickness, playerCoinCreateSize.y);
                _playerCoinCreateCollider = boxCollider;
            }

            _playerCoinCreateCollider.isTrigger = true;
        }

        private void PrepareTemplateSources()
        {
            if (_templatesPrepared)
            {
                return;
            }

            _playerCoinSource = playerCoinPrefab != null ? playerCoinPrefab : playerCoinRoot != null ? playerCoinRoot.gameObject : null;
            _blackCoinSource = blackCoinPrefab != null ? blackCoinPrefab : blackCoinRoot != null ? blackCoinRoot.gameObject : null;
            _whiteCoinSource = whiteCoinPrefab != null ? whiteCoinPrefab : whiteCoinRoot != null ? whiteCoinRoot.gameObject : null;

            DeactivateSceneTemplate(playerCoinRoot);
            DeactivateSceneTemplate(blackCoinRoot);
            DeactivateSceneTemplate(whiteCoinRoot);

            playerCoinRoot = null;
            blackCoinRoot = null;
            whiteCoinRoot = null;

            _templatesPrepared = true;
        }

        private void RebuildInitialSetup()
        {
            ClearSpawnedPieces();

            _gameFinished = false;
            _finishSfxPlayed = false;

            SpawnPlayerCoin();
            SpawnTargetCoins();
            RefreshPieceRegistry();
        }

        private void SpawnPlayerCoin()
        {
            var instance = SpawnPieceInstance(_playerCoinSource, "Player_Coin", GetCreateAreaCenterWorldPosition(), GetRuntimePiecesParent());
            if (instance == null)
            {
                instance = CreateRuntimePiece("Player_Coin", GetCreateAreaCenterWorldPosition(), GetRuntimePiecesParent());
            }

            playerCoinRoot = instance;
            ApplySpawnScale(playerCoinRoot, playerCoinSpawnScale);
            playerCoinRoot.SetPositionAndRotation(GetCreateAreaCenterWorldPosition(), PieceRotation);

            var body = GetOrAddComponent(playerCoinRoot.gameObject, out var createdPlayerBody);
            ConfigurePieceBody(body, playerCoinMass, createdPlayerBody);

            var collider = EnsurePieceCollider(
                playerCoinRoot,
                playerCoinRadius,
                new Color(0.89f, 0.63f, 0.22f),
                "RuntimePlayerCoin");

            AssignPhysicsMaterial(collider);
            AlignPieceToBoard(playerCoinRoot, collider);

            var interactable = playerCoinRoot.GetComponent<XRGrabInteractable>();
            if (interactable == null)
            {
                interactable = playerCoinRoot.gameObject.AddComponent<SimpleGrabInteractable>();
                interactable.movementType = XRBaseInteractable.MovementType.VelocityTracking;
                interactable.throwOnDetach = false;
                interactable.useDynamicAttach = false;
                interactable.trackPosition = true;
                interactable.trackRotation = false;
                interactable.matchAttachPosition = false;
                interactable.matchAttachRotation = false;
            }

            var piece = GetOrAddComponent<CarromPiece>(playerCoinRoot.gameObject);
            piece.Initialize(this, CarromPieceType.PlayerCoin);

            var resettable = GetOrAddComponent<ResettableRigidbody>(playerCoinRoot.gameObject);
            resettable.CaptureInitialState();

            piece.ClearPocketedState();
            _playerCoinPiece = piece;
            RegisterSpawnedRoot(playerCoinRoot);
        }

        private void SpawnTargetCoins()
        {
            _targetPieces.Clear();

            var totalCount = Mathf.Max(0, blackCoinCount) + Mathf.Max(0, whiteCoinCount);
            if (totalCount <= 0)
            {
                return;
            }

            var positions = GenerateTargetCoinPositions(totalCount);
            var spawnIndex = 0;
            var blackRemaining = Mathf.Max(0, blackCoinCount);
            var whiteRemaining = Mathf.Max(0, whiteCoinCount);

            while (blackRemaining > 0 || whiteRemaining > 0)
            {
                if (blackRemaining > 0)
                {
                    var piece = SpawnTargetCoinInstance(
                        _blackCoinSource,
                        $"B_Coin_{spawnIndex + 1:00}",
                        positions[spawnIndex],
                        new Color(0.12f, 0.12f, 0.14f));

                    if (blackCoinRoot == null)
                    {
                        blackCoinRoot = piece != null ? piece.transform : null;
                    }

                    spawnIndex++;
                    blackRemaining--;
                }

                if (whiteRemaining > 0)
                {
                    var piece = SpawnTargetCoinInstance(
                        _whiteCoinSource,
                        $"W_Coin_{spawnIndex + 1:00}",
                        positions[spawnIndex],
                        new Color(0.92f, 0.9f, 0.8f));

                    if (whiteCoinRoot == null)
                    {
                        whiteCoinRoot = piece != null ? piece.transform : null;
                    }

                    spawnIndex++;
                    whiteRemaining--;
                }
            }
        }

        private CarromPiece SpawnTargetCoinInstance(GameObject source, string runtimeName, Vector3 worldPosition, Color runtimeColor)
        {
            var instance = SpawnPieceInstance(source, runtimeName, worldPosition, GetTargetCoinParent());
            if (instance == null)
            {
                instance = CreateRuntimePiece(runtimeName, worldPosition, GetTargetCoinParent());
            }

            instance.SetPositionAndRotation(worldPosition, PieceRotation);
            ApplySpawnScale(instance, targetCoinSpawnScale);

            var body = GetOrAddComponent(instance.gameObject, out var createdTargetBody);
            ConfigurePieceBody(body, targetCoinMass, createdTargetBody);

            var collider = EnsurePieceCollider(instance, targetCoinRadius, runtimeColor, $"{runtimeName}_Runtime");
            AssignPhysicsMaterial(collider);
            AlignPieceToBoard(instance, collider);

            var piece = GetOrAddComponent<CarromPiece>(instance.gameObject);
            piece.Initialize(this, CarromPieceType.TargetCoin);

            var resettable = GetOrAddComponent<ResettableRigidbody>(instance.gameObject);
            resettable.CaptureInitialState();

            piece.ClearPocketedState();
            _targetPieces.Add(piece);
            RegisterSpawnedRoot(instance);
            return piece;
        }

        private bool AreAllPiecesSettled()
        {
            RefreshPieceRegistry();
            var velocityThresholdSqr = settleVelocityThreshold * settleVelocityThreshold;

            for (var index = 0; index < _pieces.Count; index++)
            {
                var piece = _pieces[index];
                if (piece == null || piece.IsPocketed || !piece.gameObject.activeInHierarchy)
                {
                    continue;
                }

                var body = piece.Body;
                if (body == null || body.isKinematic)
                {
                    continue;
                }

                if (body.linearVelocity.sqrMagnitude > velocityThresholdSqr)
                {
                    return false;
                }
            }

            return true;
        }

        private void RefreshPieceRegistry()
        {
            _pieces.Clear();

            if (playerCoinRoot != null)
            {
                _playerCoinPiece = playerCoinRoot.GetComponent<CarromPiece>();
            }

            if (_playerCoinPiece != null)
            {
                _pieces.Add(_playerCoinPiece);
            }

            for (var index = _targetPieces.Count - 1; index >= 0; index--)
            {
                var piece = _targetPieces[index];
                if (piece == null)
                {
                    _targetPieces.RemoveAt(index);
                    continue;
                }

                if (!_pieces.Contains(piece))
                {
                    _pieces.Add(piece);
                }
            }
        }
        private int GetRemainingTargetCoinCount()
        {
            var remaining = 0;
            for (var index = 0; index < _targetPieces.Count; index++)
            {
                var piece = _targetPieces[index];
                if (piece != null && !piece.IsPocketed)
                {
                    remaining++;
                }
            }

            return remaining;
        }

        private Bounds GetBoardBounds()
        {
            var collider = boardFloorCollider;
            if (collider == null)
            {
                collider = ResolveColliderReference(null, "BoardCollider", "BoardCollider (1)");
                if (!Application.isPlaying || allowRuntimeReferenceAutofill)
                {
                    boardFloorCollider = collider;
                }
            }

            if (collider == null)
            {
                collider = GetComponent<Collider>();
            }

            if (collider != null)
            {
                return collider.bounds;
            }

            return new Bounds(transform.position, new Vector3(boardSize.x, boardThickness, boardSize.y));
        }

        private Bounds GetCoinCreateBounds()
        {
            if (_coinCreateCollider == null && coinCreateArea != null)
            {
                _coinCreateCollider = coinCreateArea.GetComponent<Collider>();
            }

            if (_coinCreateCollider != null)
            {
                return _coinCreateCollider.bounds;
            }

            return new Bounds(transform.position, new Vector3(coinCreateSize.x, pieceThickness, coinCreateSize.y));
        }

        private Bounds GetCreateAreaBounds()
        {
            if (_playerCoinCreateCollider == null && playerCoinCreate != null)
            {
                _playerCoinCreateCollider = playerCoinCreate.GetComponent<Collider>();
            }

            if (_playerCoinCreateCollider != null)
            {
                return _playerCoinCreateCollider.bounds;
            }

            var center = playerCoinCreate != null
                ? playerCoinCreate.position
                : transform.position + (transform.forward * (-(boardSize.y * 0.5f) + 0.13f));

            return new Bounds(center, new Vector3(playerCoinCreateSize.x, pieceThickness, playerCoinCreateSize.y));
        }

        private Vector3 GetCreateAreaCenterWorldPosition()
        {
            var bounds = GetCreateAreaBounds();
            return new Vector3(bounds.center.x, GetPieceWorldY(), bounds.center.z);
        }

        private float GetPieceWorldY()
        {
            return GetBoardBounds().max.y + PieceCenterHeight;
        }

        private void ApplySpawnScale(Transform pieceRoot, float scaleMultiplier)
        {
            if (pieceRoot == null || !overrideSpawnScale)
            {
                return;
            }

            var safeScale = Mathf.Max(0.1f, scaleMultiplier);
            pieceRoot.localScale = pieceRoot.localScale * safeScale;
        }

        private void AlignPieceToBoard(Transform pieceRoot, Collider pieceCollider)
        {
            if (pieceRoot == null)
            {
                return;
            }

            var position = pieceRoot.position;
            if (pieceCollider == null)
            {
                position.y = GetPieceWorldY();
                pieceRoot.position = position;
                return;
            }

            var boardTopY = GetBoardBounds().max.y;
            var bottomOffset = pieceCollider.bounds.min.y - pieceRoot.position.y;
            position.y = boardTopY - bottomOffset + 0.0005f;
            pieceRoot.position = position;
        }

        private float GetPlayerCoinRadius()
        {
            if (playerCoinRoot == null)
            {
                return playerCoinRadius;
            }

            var collider = playerCoinRoot.GetComponentInChildren<Collider>(true);
            if (collider == null)
            {
                return playerCoinRadius;
            }

            return Mathf.Max(0.001f, Mathf.Min(collider.bounds.extents.x, collider.bounds.extents.z));
        }

        private List<Vector3> GenerateTargetCoinPositions(int count)
        {
            var positions = new List<Vector3>(count);
            if (TryGeneratePositionsInsideCoinCreateCollider(count, positions))
            {
                return positions;
            }

            var bounds = GetCoinCreateBounds();
            var padding = targetCoinRadius + createAreaEdgePadding;
            var minX = bounds.min.x + padding;
            var maxX = bounds.max.x - padding;
            var minZ = bounds.min.z + padding;
            var maxZ = bounds.max.z - padding;

            if (minX > maxX)
            {
                minX = bounds.center.x;
                maxX = bounds.center.x;
            }

            if (minZ > maxZ)
            {
                minZ = bounds.center.z;
                maxZ = bounds.center.z;
            }

            var columns = Mathf.Max(1, Mathf.CeilToInt(Mathf.Sqrt(count)));
            var rows = Mathf.Max(1, Mathf.CeilToInt((float)count / columns));
            var worldY = GetPieceWorldY();

            for (var index = 0; index < count; index++)
            {
                var row = index / columns;
                var column = index % columns;
                var xT = columns == 1 ? 0.5f : (float)column / (columns - 1);
                var zT = rows == 1 ? 0.5f : (float)row / (rows - 1);
                positions.Add(new Vector3(
                    Mathf.Lerp(minX, maxX, xT),
                    worldY,
                    Mathf.Lerp(minZ, maxZ, zT)));
            }

            return positions;
        }

        private bool TryGeneratePositionsInsideCoinCreateCollider(int count, List<Vector3> positions)
        {
            if (count <= 0)
            {
                return true;
            }

            if (_coinCreateCollider == null && coinCreateArea != null)
            {
                _coinCreateCollider = coinCreateArea.GetComponent<Collider>();
            }

            if (_coinCreateCollider is not BoxCollider boxCollider)
            {
                return false;
            }

            var padding = targetCoinRadius + createAreaEdgePadding;
            var localCenter = boxCollider.center;
            var localExtents = boxCollider.size * 0.5f;
            var minLocalX = localCenter.x - localExtents.x + padding;
            var maxLocalX = localCenter.x + localExtents.x - padding;
            var minLocalZ = localCenter.z - localExtents.z + padding;
            var maxLocalZ = localCenter.z + localExtents.z - padding;

            if (minLocalX > maxLocalX)
            {
                minLocalX = localCenter.x;
                maxLocalX = localCenter.x;
            }

            if (minLocalZ > maxLocalZ)
            {
                minLocalZ = localCenter.z;
                maxLocalZ = localCenter.z;
            }

            var columns = Mathf.Max(1, Mathf.CeilToInt(Mathf.Sqrt(count)));
            var rows = Mathf.Max(1, Mathf.CeilToInt((float)count / columns));
            var worldY = GetPieceWorldY();

            for (var index = 0; index < count; index++)
            {
                var row = index / columns;
                var column = index % columns;
                var xT = columns == 1 ? 0.5f : (float)column / (columns - 1);
                var zT = rows == 1 ? 0.5f : (float)row / (rows - 1);
                var localPosition = new Vector3(
                    Mathf.Lerp(minLocalX, maxLocalX, xT),
                    localCenter.y,
                    Mathf.Lerp(minLocalZ, maxLocalZ, zT));
                var worldPosition = boxCollider.transform.TransformPoint(localPosition);
                worldPosition.y = worldY;
                positions.Add(worldPosition);
            }

            return true;
        }

        private void ClearSpawnedPieces()
        {
            for (var index = 0; index < _spawnedRoots.Count; index++)
            {
                var root = _spawnedRoots[index];
                if (root == null)
                {
                    continue;
                }

                root.gameObject.SetActive(false);
                Object.Destroy(root.gameObject);
            }

            _spawnedRoots.Clear();
            _targetPieces.Clear();
            _pieces.Clear();
            _playerCoinPiece = null;
            playerCoinRoot = null;
            blackCoinRoot = null;
            whiteCoinRoot = null;
        }

        private Transform SpawnPieceInstance(GameObject source, string fallbackName, Vector3 worldPosition, Transform parent)
        {
            if (source == null)
            {
                return null;
            }

            var instance = Object.Instantiate(source, worldPosition, PieceRotation, parent);
            instance.name = fallbackName;
            instance.SetActive(true);
            return instance.transform;
        }

        private Transform CreateRuntimePiece(string name, Vector3 worldPosition, Transform parent)
        {
            var instance = new GameObject(name);
            instance.transform.SetParent(parent, true);
            instance.transform.SetPositionAndRotation(worldPosition, PieceRotation);
            return instance.transform;
        }

        private Transform GetBoardGroupParent()
        {
            return transform.parent != null ? transform.parent : transform;
        }

        private Transform GetRuntimePiecesParent()
        {
            return GetBoardGroupParent();
        }

        private Transform GetTargetCoinParent()
        {
            return coinsRoot != null ? coinsRoot : GetRuntimePiecesParent();
        }

        private Collider EnsurePieceCollider(Transform pieceRoot, float radius, Color color, string runtimeName)
        {
            var existingCollider = FindPrimaryPieceCollider(pieceRoot);
            if (existingCollider != null)
            {
                return existingCollider;
            }

            var showVisual = !HasVisibleRenderer(pieceRoot);
            var instance = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            instance.name = runtimeName;
            instance.transform.SetParent(pieceRoot, false);
            instance.transform.localPosition = Vector3.zero;
            instance.transform.localRotation = Quaternion.identity;
            instance.transform.localScale = new Vector3(radius * 2f, pieceThickness * 0.5f, radius * 2f);

            var primitiveCollider = instance.GetComponent<Collider>();
            if (primitiveCollider != null)
            {
                primitiveCollider.enabled = false;
            }

            var meshFilter = instance.GetComponent<MeshFilter>();
            var meshCollider = instance.AddComponent<MeshCollider>();
            meshCollider.sharedMesh = meshFilter != null ? meshFilter.sharedMesh : null;
            meshCollider.convex = true;

            var renderer = instance.GetComponent<Renderer>();
            if (renderer != null)
            {
                renderer.enabled = showVisual;
                if (showVisual)
                {
                    renderer.material.color = color;
                }
            }

            return meshCollider;
        }

        private static Collider FindPrimaryPieceCollider(Transform pieceRoot)
        {
            if (pieceRoot == null)
            {
                return null;
            }

            var colliders = pieceRoot.GetComponentsInChildren<Collider>(true);
            for (var index = 0; index < colliders.Length; index++)
            {
                if (!colliders[index].isTrigger)
                {
                    return colliders[index];
                }
            }

            return null;
        }

        private void ConfigurePieceBody(Rigidbody body, float mass, bool createdBody)
        {
            if (createdBody || !preserveExistingRigidbodyTuning)
            {
                body.useGravity = false;
                body.interpolation = body.interpolation == RigidbodyInterpolation.None
                    ? RigidbodyInterpolation.Interpolate
                    : body.interpolation;
                body.collisionDetectionMode = body.collisionDetectionMode == CollisionDetectionMode.Discrete
                    ? CollisionDetectionMode.ContinuousDynamic
                    : body.collisionDetectionMode;
                body.constraints |= RigidbodyConstraints.FreezePositionY
                    | RigidbodyConstraints.FreezeRotationX
                    | RigidbodyConstraints.FreezeRotationZ;
                body.mass = mass;
                body.linearDamping = pieceLinearDamping;
                body.angularDamping = pieceAngularDamping;
            }
        }

        private void AssignPhysicsMaterial(Collider collider)
        {
            if (collider == null)
            {
                return;
            }

            if (collider.sharedMaterial != null)
            {
                return;
            }

            collider.material = GetRuntimePhysicsMaterial();
        }

        private PhysicsMaterial GetRuntimePhysicsMaterial()
        {
            if (_runtimePhysicsMaterial != null)
            {
                return _runtimePhysicsMaterial;
            }

            _runtimePhysicsMaterial = new PhysicsMaterial("CarromRuntimePhysics")
            {
                dynamicFriction = 0.05f,
                staticFriction = 0.05f,
                bounciness = 0.16f,
                frictionCombine = PhysicsMaterialCombine.Minimum,
                bounceCombine = PhysicsMaterialCombine.Maximum
            };

            return _runtimePhysicsMaterial;
        }

        private Transform ResolveReference(Transform existing, params string[] candidateNames)
        {
            if (existing != null)
            {
                return existing;
            }

            for (var index = 0; index < candidateNames.Length; index++)
            {
                var found = FindRelativeTransform(candidateNames[index]);
                if (found != null)
                {
                    return found;
                }
            }

            return null;
        }

        private Collider ResolveColliderReference(Collider existing, params string[] candidateNames)
        {
            if (existing != null)
            {
                return existing;
            }

            for (var index = 0; index < candidateNames.Length; index++)
            {
                var found = FindRelativeTransform(candidateNames[index]);
                if (found != null && found.TryGetComponent<Collider>(out var namedCollider))
                {
                    return namedCollider;
                }
            }

            if (boardSurface != null && boardSurface.TryGetComponent<Collider>(out var boardSurfaceCollider))
            {
                return boardSurfaceCollider;
            }

            if (TryGetComponent<Collider>(out var rootCollider))
            {
                return rootCollider;
            }

            return null;
        }

        private Transform FindRelativeTransform(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                return null;
            }

            if (transform.name == name)
            {
                return transform;
            }

            var directChild = transform.Find(name);
            if (directChild != null)
            {
                return directChild;
            }

            var parent = transform.parent;
            if (parent != null)
            {
                if (parent.name == name)
                {
                    return parent;
                }

                var sibling = parent.Find(name);
                if (sibling != null)
                {
                    return sibling;
                }

                var nestedInParent = FindInChildrenRecursive(parent, name);
                if (nestedInParent != null)
                {
                    return nestedInParent;
                }
            }

            return FindInChildrenRecursive(transform, name);
        }

        private static Transform FindInChildrenRecursive(Transform root, string name)
        {
            if (root == null)
            {
                return null;
            }

            for (var index = 0; index < root.childCount; index++)
            {
                var child = root.GetChild(index);
                if (child.name == name)
                {
                    return child;
                }

                var nested = FindInChildrenRecursive(child, name);
                if (nested != null)
                {
                    return nested;
                }
            }

            return null;
        }

        private static bool HasVisibleRenderer(Transform root)
        {
            var renderers = root.GetComponentsInChildren<Renderer>(true);
            for (var index = 0; index < renderers.Length; index++)
            {
                if (renderers[index].enabled)
                {
                    return true;
                }
            }

            return false;
        }

        private static GameObject CreatePrimitiveVisual(
            string name,
            PrimitiveType primitiveType,
            Transform parent,
            Vector3 localPosition,
            Vector3 localScale,
            Color color,
            bool removeCollider)
        {
            var instance = GameObject.CreatePrimitive(primitiveType);
            instance.name = name;
            instance.transform.SetParent(parent, false);
            instance.transform.localPosition = localPosition;
            instance.transform.localRotation = Quaternion.identity;
            instance.transform.localScale = localScale;

            if (removeCollider)
            {
                var collider = instance.GetComponent<Collider>();
                if (collider != null)
                {
                    Object.Destroy(collider);
                }
            }

            var renderer = instance.GetComponent<Renderer>();
            if (renderer != null)
            {
                renderer.material.color = color;
            }

            return instance;
        }

        private void RegisterSpawnedRoot(Transform root)
        {
            if (root != null && !_spawnedRoots.Contains(root))
            {
                _spawnedRoots.Add(root);
            }
        }

        private static void DeactivateSceneTemplate(Transform template)
        {
            if (template == null)
            {
                return;
            }

            if (template.gameObject.scene.IsValid())
            {
                template.gameObject.SetActive(false);
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

        private static Rigidbody GetOrAddComponent(GameObject target, out bool created)
        {
            if (!target.TryGetComponent<Rigidbody>(out var body))
            {
                body = target.AddComponent<Rigidbody>();
                created = true;
                return body;
            }

            created = false;
            return body;
        }
    }
}
