using ArcadeRoom.Audio;
using ArcadeRoom.Core;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

namespace ArcadeRoom.YoTyan
{
    public enum YoTyanBallTeam
    {
        Black = 0,
        Red = 1
    }

    [DisallowMultipleComponent]
    public sealed class YoTyanBall : NetworkBehaviour, IArcadeResettable
    {
        private const string DefaultRollingClipPath = "Assets/_Project/Audio/SFX/Yotyan/Yotyan_Ball_Rolling.wav";
        private const string DefaultPlateHitClipPath = "Assets/_Project/Audio/SFX/Yotyan/Yotyan Plate Hit.mp3";

        [SerializeField] private YoTyanBallTeam team;
        [SerializeField] private Transform homeSpawnPoint;
        [SerializeField] private bool isScored;
        [Header("Network Ownership")]
        [SerializeField] private XRGrabInteractable grabInteractable;
        [SerializeField] private bool requestCollisionOwnership = true;
        [SerializeField, Min(0f)] private float minCollisionOwnershipSpeed = 0.08f;
        [SerializeField, Min(0f)] private float returnBlockOnGrabSeconds = 0.75f;
        [SerializeField, Min(0f)] private float returnBlockOnReleaseSeconds = 0.25f;
        [Header("SFX")]
        [SerializeField] private AudioSource rollingSource;
        [SerializeField] private AudioClip rollingClip;
        [SerializeField] private AudioClip plateHitClip;
        [SerializeField, Range(0f, 1f)] private float rollingVolume = 0.35f;
        [SerializeField, Min(0f)] private float minRollingSpeed = 0.04f;
        [SerializeField, Min(0.01f)] private float fullRollingSpeed = 1.1f;
        [SerializeField, Min(0f)] private float rollingFadeSpeed = 5f;
        [SerializeField] private Vector2 rollingPitchRange = new(0.92f, 1.08f);
        [SerializeField, Range(0f, 1f)] private float plateHitVolume = 0.5f;
        [SerializeField, Min(0f)] private float minPlateHitSpeed = 0.25f;
        [SerializeField, Min(0.01f)] private float fullPlateHitSpeed = 1.8f;
        [SerializeField, Min(0f)] private float plateHitCooldown = 0.08f;
        [SerializeField] private Vector2 plateHitPitchRange = new(0.94f, 1.06f);
        [SerializeField, Range(0f, 1f)] private float spatialBlend = 1f;
        [SerializeField, Min(0.1f)] private float maxDistance = 7f;

        private Rigidbody _cachedBody;
        private NetworkObject _networkObject;
        private Collider[] _cachedColliders;
        private Renderer[] _cachedRenderers;
        private bool _networkGrabbed;
        private bool _defaultUseGravity;
        private bool _defaultDetectCollisions = true;
        private bool _physicsDefaultsCaptured;
        private float _lastPlateHitTime = float.NegativeInfinity;
        private float _returnBlockedUntil = float.NegativeInfinity;

        public YoTyanBallTeam Team => team;
        public Transform HomeSpawnPoint => homeSpawnPoint;
        public bool IsScored => isScored;
        public bool IsGrabbed => (grabInteractable != null && grabInteractable.isSelected) || _networkGrabbed;
        public bool IsReturnBlocked => IsGrabbed || Time.time < _returnBlockedUntil;
        public bool CanLocalReturnZoneControl => !IsNetworkSpawned() || IsServer;

        private void Awake()
        {
            ResolveReferences();
            ResolveSfxReferences();
            CapturePhysicsDefaults();
        }

        private void OnEnable()
        {
            ResolveReferences();

            if (grabInteractable != null)
            {
                grabInteractable.selectEntered.AddListener(HandleSelectEntered);
                grabInteractable.selectExited.AddListener(HandleSelectExited);
            }
        }

        private void OnValidate()
        {
            if (gameObject.name.Contains("Red"))
            {
                team = YoTyanBallTeam.Red;
            }
            else if (gameObject.name.Contains("Black"))
            {
                team = YoTyanBallTeam.Black;
            }

            fullRollingSpeed = Mathf.Max(0.01f, fullRollingSpeed);
            fullPlateHitSpeed = Mathf.Max(0.01f, fullPlateHitSpeed);
            maxDistance = Mathf.Max(0.1f, maxDistance);
            rollingPitchRange = ArcadeSfxPlayer.NormalizePitchRange(rollingPitchRange);
            plateHitPitchRange = ArcadeSfxPlayer.NormalizePitchRange(plateHitPitchRange);
            ResolveReferences();
            ResolveSfxReferences();
        }

        private void Update()
        {
            UpdateRollingSfx();
        }

        public override void OnNetworkSpawn()
        {
            base.OnNetworkSpawn();
        }

        private void OnDisable()
        {
            if (grabInteractable != null)
            {
                grabInteractable.selectEntered.RemoveListener(HandleSelectEntered);
                grabInteractable.selectExited.RemoveListener(HandleSelectExited);
            }

            if (rollingSource != null)
            {
                rollingSource.Stop();
            }
        }

        private void OnCollisionEnter(Collision collision)
        {
            HandleCollisionOwnership(collision);

            if (isScored
                || plateHitClip == null
                || collision == null
                || Time.time - _lastPlateHitTime < plateHitCooldown)
            {
                return;
            }

            var otherBall = collision.rigidbody != null
                ? collision.rigidbody.GetComponent<YoTyanBall>()
                : collision.collider.GetComponentInParent<YoTyanBall>();
            if (otherBall != null)
            {
                return;
            }

            var speed = collision.relativeVelocity.magnitude;
            if (speed < minPlateHitSpeed)
            {
                return;
            }

            _lastPlateHitTime = Time.time;
            var volume = Mathf.InverseLerp(minPlateHitSpeed, fullPlateHitSpeed, speed) * plateHitVolume;
            ArcadeSfxPlayer.PlayOneShot(
                plateHitClip,
                GetCollisionPosition(collision),
                volume,
                ArcadeSfxPlayer.RandomPitch(plateHitPitchRange),
                spatialBlend,
                maxDistance,
                "YoTyan_SFX_PlateHit");
        }

        public void AssignHomeSpawnPoint(Transform spawnPoint)
        {
            homeSpawnPoint = spawnPoint;
        }

        public void MarkScored()
        {
            isScored = true;
        }

        public void MarkScoredNetworked()
        {
            ApplyScoredState(true, true);
            BroadcastScoredStateIfServer(true);
        }

        public void ClearScored()
        {
            isScored = false;
        }

        public void ClearScoredNetworked()
        {
            ApplyScoredState(false, false);
            BroadcastScoredStateIfServer(false);
        }

        public void ResetToHomeSpawn()
        {
            if (homeSpawnPoint == null)
            {
                return;
            }

            isScored = false;

            if (_cachedBody == null)
            {
                _cachedBody = GetComponent<Rigidbody>();
            }

            SetBodyPose(homeSpawnPoint.position, homeSpawnPoint.rotation);
        }

        public void ResetToHomeSpawnNetworked()
        {
            if (homeSpawnPoint == null)
            {
                return;
            }

            isScored = false;
            if (IsServer && IsSpawned)
            {
                SetNetworkGrabbedLocal(false);
                SetNetworkGrabbedClientRpc(false);
            }

            SetBodyPose(homeSpawnPoint.position, homeSpawnPoint.rotation);
            ApplyResetSuspended(false);

            if (IsServer && IsSpawned)
            {
                ApplyResetClientRpc(homeSpawnPoint.position, homeSpawnPoint.rotation);
            }
        }

        public void SetResetSuspendedNetworked(bool suspended)
        {
            if (IsServer && IsSpawned)
            {
                SetNetworkGrabbedLocal(false);
                SetNetworkGrabbedClientRpc(false);
            }

            ApplyResetSuspended(suspended);

            if (IsServer && IsSpawned)
            {
                ApplyResetSuspendedClientRpc(suspended);
            }
        }

        private void SetBodyPose(Vector3 position, Quaternion rotation)
        {
            if (_cachedBody == null)
            {
                _cachedBody = GetComponent<Rigidbody>();
            }

            if (_cachedBody != null)
            {
                SetBodyVelocitySafely(_cachedBody, Vector3.zero, Vector3.zero);
                _cachedBody.position = position;
                _cachedBody.rotation = rotation;
                _cachedBody.Sleep();
                return;
            }

            transform.SetPositionAndRotation(position, rotation);
        }

        private void UpdateRollingSfx()
        {
            if (rollingSource == null || rollingClip == null || _cachedBody == null)
            {
                return;
            }

            var speed = isScored ? 0f : _cachedBody.linearVelocity.magnitude;
            var speedT = Mathf.InverseLerp(minRollingSpeed, fullRollingSpeed, speed);
            var targetVolume = speed >= minRollingSpeed ? speedT * rollingVolume : 0f;
            rollingSource.volume = Mathf.MoveTowards(rollingSource.volume, targetVolume, rollingFadeSpeed * Time.deltaTime);
            rollingSource.pitch = Mathf.Lerp(rollingPitchRange.x, rollingPitchRange.y, speedT);

            if (rollingSource.volume > 0.001f)
            {
                if (!rollingSource.isPlaying)
                {
                    rollingSource.Play();
                }

                return;
            }

            if (rollingSource.isPlaying)
            {
                rollingSource.Stop();
            }
        }

        private void ResolveSfxReferences()
        {
            ResolveReferences();

            if (rollingSource == null)
            {
                rollingSource = GetComponent<AudioSource>();
            }

            if (rollingClip == null)
            {
                rollingClip = ArcadeSfxPlayer.LoadEditorClip(DefaultRollingClipPath);
            }

            if (plateHitClip == null)
            {
                plateHitClip = ArcadeSfxPlayer.LoadEditorClip(DefaultPlateHitClipPath);
            }

            ArcadeSfxPlayer.ConfigureLoopSource(rollingSource, rollingClip, spatialBlend, maxDistance);
        }

        private void ResolveReferences()
        {
            if (_cachedBody == null)
            {
                _cachedBody = GetComponent<Rigidbody>();
            }

            if (_networkObject == null)
            {
                _networkObject = GetComponent<NetworkObject>();
            }

            if (grabInteractable == null)
            {
                grabInteractable = GetComponent<XRGrabInteractable>();
            }

            if (_cachedColliders == null || _cachedColliders.Length == 0)
            {
                _cachedColliders = GetComponentsInChildren<Collider>(true);
            }

            if (_cachedRenderers == null || _cachedRenderers.Length == 0)
            {
                _cachedRenderers = GetComponentsInChildren<Renderer>(true);
            }
        }

        private void CapturePhysicsDefaults()
        {
            if (_physicsDefaultsCaptured)
            {
                return;
            }

            ResolveReferences();

            if (_cachedBody != null)
            {
                _defaultUseGravity = _cachedBody.useGravity;
                _defaultDetectCollisions = _cachedBody.detectCollisions;
            }

            _physicsDefaultsCaptured = true;
        }

        private void ApplyResetSuspended(bool suspended)
        {
            ResolveReferences();
            CapturePhysicsDefaults();

            if (_cachedBody != null)
            {
                SetBodyVelocitySafely(_cachedBody, Vector3.zero, Vector3.zero);
                _cachedBody.useGravity = suspended ? false : _defaultUseGravity;
                _cachedBody.detectCollisions = suspended ? false : _defaultDetectCollisions;

                if (suspended)
                {
                    _cachedBody.Sleep();
                }
                else
                {
                    _cachedBody.WakeUp();
                }
            }

            if (_cachedColliders != null)
            {
                for (var i = 0; i < _cachedColliders.Length; i++)
                {
                    if (_cachedColliders[i] != null)
                    {
                        _cachedColliders[i].enabled = !suspended;
                    }
                }
            }

            if (_cachedRenderers != null)
            {
                for (var i = 0; i < _cachedRenderers.Length; i++)
                {
                    if (_cachedRenderers[i] != null)
                    {
                        _cachedRenderers[i].enabled = !suspended;
                    }
                }
            }

            if (grabInteractable != null)
            {
                grabInteractable.enabled = !suspended;
            }

            if (suspended && rollingSource != null)
            {
                rollingSource.Stop();
            }
        }

        private void HandleSelectEntered(SelectEnterEventArgs args)
        {
            SetNetworkGrabbed(true);
            RequestOwnershipForLocalClient();
        }

        private void HandleSelectExited(SelectExitEventArgs args)
        {
            SetNetworkGrabbed(false);
        }

        private void SetNetworkGrabbed(bool grabbed)
        {
            SetNetworkGrabbedLocal(grabbed);

            if (!IsNetworkSpawned())
            {
                return;
            }

            if (IsServer)
            {
                SetNetworkGrabbedClientRpc(grabbed);
                return;
            }

            SetNetworkGrabbedServerRpc(grabbed);
        }

        private void SetNetworkGrabbedLocal(bool grabbed)
        {
            _networkGrabbed = grabbed;
            BlockReturnFor(grabbed ? returnBlockOnGrabSeconds : returnBlockOnReleaseSeconds);
        }

        private void BlockReturnFor(float seconds)
        {
            if (seconds <= 0f)
            {
                return;
            }

            _returnBlockedUntil = Mathf.Max(_returnBlockedUntil, Time.time + seconds);
        }

        private void HandleCollisionOwnership(Collision collision)
        {
            if (!requestCollisionOwnership || collision == null || !IsNetworkSpawned())
            {
                return;
            }

            if (collision.relativeVelocity.magnitude < minCollisionOwnershipSpeed)
            {
                return;
            }

            if (!IsOwner)
            {
                RequestOwnershipForLocalClient();
                return;
            }

            var otherBall = collision.collider.GetComponentInParent<YoTyanBall>();
            if (otherBall == null
                || otherBall == this
                || !otherBall.TryGetComponent<NetworkObject>(out var otherNetworkObject)
                || !otherNetworkObject.IsSpawned
                || otherNetworkObject.OwnerClientId == NetworkManager.Singleton.LocalClientId)
            {
                return;
            }

            RequestOtherBallOwnershipServerRpc(
                otherNetworkObject.NetworkObjectId,
                NetworkManager.Singleton.LocalClientId);
        }

        private void RequestOwnershipForLocalClient()
        {
            if (!IsNetworkSpawned()
                || NetworkManager.Singleton == null
                || _networkObject.OwnerClientId == NetworkManager.Singleton.LocalClientId)
            {
                return;
            }

            RequestOwnershipServerRpc(NetworkManager.Singleton.LocalClientId);
        }

        private void ApplyScoredState(bool scored, bool stopMotion)
        {
            isScored = scored;

            if (!stopMotion)
            {
                return;
            }

            if (_cachedBody == null)
            {
                _cachedBody = GetComponent<Rigidbody>();
            }

            if (_cachedBody == null)
            {
                return;
            }

            _cachedBody.linearVelocity = Vector3.zero;
            _cachedBody.angularVelocity = Vector3.zero;
            _cachedBody.Sleep();
        }

        private void BroadcastScoredStateIfServer(bool scored)
        {
            if (IsServer && IsSpawned)
            {
                ApplyScoredStateClientRpc(scored);
            }
        }

        private bool IsNetworkSpawned()
        {
            return NetworkManager.Singleton != null
                && NetworkManager.Singleton.IsListening
                && _networkObject != null
                && _networkObject.IsSpawned;
        }

        [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
        private void RequestOwnershipServerRpc(ulong requestedOwnerClientId)
        {
            if (_networkObject == null || !_networkObject.IsSpawned)
            {
                return;
            }

            if (_networkObject.OwnerClientId != requestedOwnerClientId)
            {
                EnsureTransferableOwnership(_networkObject);
                _networkObject.ChangeOwnership(requestedOwnerClientId);
            }
        }

        [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
        private void SetNetworkGrabbedServerRpc(bool grabbed)
        {
            SetNetworkGrabbedLocal(grabbed);
            SetNetworkGrabbedClientRpc(grabbed);
        }

        [Rpc(SendTo.Everyone)]
        private void SetNetworkGrabbedClientRpc(bool grabbed)
        {
            SetNetworkGrabbedLocal(grabbed);
        }

        [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
        private void RequestOtherBallOwnershipServerRpc(ulong targetNetworkObjectId, ulong requestedOwnerClientId)
        {
            if (NetworkManager.Singleton == null
                || !NetworkManager.Singleton.SpawnManager.SpawnedObjects.TryGetValue(targetNetworkObjectId, out var target)
                || target == null
                || !target.TryGetComponent<YoTyanBall>(out _)
                || target.OwnerClientId == requestedOwnerClientId)
            {
                return;
            }

            EnsureTransferableOwnership(target);
            target.ChangeOwnership(requestedOwnerClientId);
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

        [Rpc(SendTo.Everyone)]
        private void ApplyScoredStateClientRpc(bool scored)
        {
            ApplyScoredState(scored, scored);
        }

        [Rpc(SendTo.Everyone)]
        private void ApplyResetClientRpc(Vector3 position, Quaternion rotation)
        {
            isScored = false;
            SetBodyPose(position, rotation);
            ApplyResetSuspended(false);
        }

        [Rpc(SendTo.Everyone)]
        private void ApplyResetSuspendedClientRpc(bool suspended)
        {
            ApplyResetSuspended(suspended);
        }

        private static Vector3 GetCollisionPosition(Collision collision)
        {
            return collision.contactCount > 0
                ? collision.GetContact(0).point
                : collision.transform.position;
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

        public void ResetState()
        {
            ResetToHomeSpawn();
        }
    }
}
