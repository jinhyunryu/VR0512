using UnityEngine;
using Unity.Netcode;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

namespace ArcadeRoom.Curling
{
    public enum CurlingTeam
    {
        Red,
        Blue,
    }

    [DisallowMultipleComponent]
    public sealed class CurlingStone : NetworkBehaviour
    {
        [SerializeField] private CurlingTeam team;
        [SerializeField] private Transform scoreProbe;
        [SerializeField] private XRGrabInteractable grabInteractable;
        [SerializeField] private bool requestCollisionOwnership = true;
        [SerializeField, Min(0f)] private float minCollisionOwnershipSpeed = 0.08f;

        private CurlingStationController _station;
        private NetworkObject _networkObject;
        private Rigidbody _body;
        private bool _hasBeenUsed;

        public CurlingTeam Team => team;
        public Transform ScoreProbe => scoreProbe != null ? scoreProbe : transform;
        public bool HasBeenUsed => _hasBeenUsed;

        private void Awake()
        {
            ResolveReferences();
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

        private void OnDisable()
        {
            if (grabInteractable != null)
            {
                grabInteractable.selectEntered.RemoveListener(HandleSelectEntered);
                grabInteractable.selectExited.RemoveListener(HandleSelectExited);
            }
        }

        private void OnValidate()
        {
            ResolveReferences();
        }

        public void Initialize(CurlingTeam stoneTeam, CurlingStationController station)
        {
            team = stoneTeam;
            _station = station;
            _hasBeenUsed = false;
            ResolveReferences();
        }

        public void ResetUsage(CurlingStationController station)
        {
            _station = station;
            _hasBeenUsed = false;
        }

        public bool MarkUsed()
        {
            if (_hasBeenUsed)
            {
                return false;
            }

            _hasBeenUsed = true;
            return true;
        }

        private void HandleSelectEntered(SelectEnterEventArgs args)
        {
            if (!IsNetworkSpawned())
            {
                return;
            }

            if (!IsOwner)
            {
                RequestOwnershipServerRpc(NetworkManager.Singleton.LocalClientId);
            }
        }

        private void HandleSelectExited(SelectExitEventArgs args)
        {
            if (!MarkUsed())
            {
                return;
            }

            if (TryHandleNetworkUsed())
            {
                return;
            }

            if (_station == null)
            {
                _station = FindFirstObjectByType<CurlingStationController>();
            }

            _station?.NotifyStoneUsed(this);
        }

        private void ResolveReferences()
        {
            if (grabInteractable == null)
            {
                grabInteractable = GetComponent<XRGrabInteractable>();
            }

            if (_networkObject == null)
            {
                _networkObject = GetComponent<NetworkObject>();
            }

            if (_body == null)
            {
                _body = GetComponent<Rigidbody>();
            }

            if (scoreProbe == null)
            {
                var child = transform.Find("ScoreProbe");
                if (child != null)
                {
                    scoreProbe = child;
                }
            }
        }

        private void OnCollisionEnter(Collision collision)
        {
            if (!requestCollisionOwnership || !IsNetworkSpawned() || !IsOwner)
            {
                return;
            }

            if (_body != null && _body.linearVelocity.magnitude < minCollisionOwnershipSpeed)
            {
                return;
            }

            var otherStone = collision.collider.GetComponentInParent<CurlingStone>();
            if (otherStone == null
                || otherStone == this
                || !otherStone.TryGetComponent<NetworkObject>(out var otherNetworkObject)
                || !otherNetworkObject.IsSpawned
                || otherNetworkObject.OwnerClientId == NetworkManager.Singleton.LocalClientId)
            {
                return;
            }

            RequestOtherStoneOwnershipServerRpc(
                otherNetworkObject.NetworkObjectId,
                NetworkManager.Singleton.LocalClientId);
        }

        private bool TryHandleNetworkUsed()
        {
            if (!IsNetworkSpawned())
            {
                return false;
            }

            if (IsServer)
            {
                if (_station == null)
                {
                    _station = FindFirstObjectByType<CurlingStationController>();
                }

                _station?.NotifyStoneUsed(this);
                return true;
            }

            if (_station == null)
            {
                _station = FindFirstObjectByType<CurlingStationController>();
            }

            if (_station != null)
            {
                _station.RequestNetworkStoneUsed(this);
            }
            else
            {
                NotifyUsedServerRpc();
            }

            return true;
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
        private void NotifyUsedServerRpc()
        {
            if (!MarkUsed())
            {
                return;
            }

            if (_station == null)
            {
                _station = FindFirstObjectByType<CurlingStationController>();
            }

            _station?.NotifyStoneUsed(this);
        }

        [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
        private void RequestOtherStoneOwnershipServerRpc(ulong targetNetworkObjectId, ulong requestedOwnerClientId)
        {
            if (NetworkManager.Singleton == null
                || !NetworkManager.Singleton.SpawnManager.SpawnedObjects.TryGetValue(targetNetworkObjectId, out var target)
                || target == null
                || !target.TryGetComponent<CurlingStone>(out _)
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
                NetworkObject.OwnershipStatus.Distributable | NetworkObject.OwnershipStatus.Transferable,
                false,
                NetworkObject.OwnershipLockActions.SetAndUnlock);
        }
    }
}
