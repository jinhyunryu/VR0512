#if PHOTON_UNITY_NETWORKING || PUN_2_OR_NEWER
using Photon.Pun;
using Photon.Realtime;
using UnityEngine;

namespace ArcadeRoom.Multiplayer
{
    [DisallowMultipleComponent]
    public sealed class PhotonPunArcadeRoomLauncher : MonoBehaviourPunCallbacks
    {
        [Header("Session")]
        [SerializeField] private ArcadeMultiplayerSessionConfig sessionConfig;
        [SerializeField] private bool connectOnStart = true;
        [SerializeField] private bool automaticallySyncScene = true;

        [Header("Player Spawn")]
        [SerializeField] private GameObject networkPlayerPrefab;
        [SerializeField] private Transform spawnRoot;

        private bool _spawnedLocalPlayer;

        private void Start()
        {
            if (connectOnStart)
            {
                Connect();
            }
        }

        public void Connect()
        {
            if (PhotonNetwork.IsConnected)
            {
                JoinDefaultRoom();
                return;
            }

            PhotonNetwork.AutomaticallySyncScene = automaticallySyncScene;
            PhotonNetwork.GameVersion = sessionConfig != null
                ? sessionConfig.GameVersion
                : "arcade-room-prototype-v1";
            PhotonNetwork.ConnectUsingSettings();
        }

        public override void OnConnectedToMaster()
        {
            JoinDefaultRoom();
        }

        public override void OnJoinedRoom()
        {
            SpawnLocalNetworkPlayer();
        }

        public override void OnPlayerEnteredRoom(Player newPlayer)
        {
            if (PhotonNetwork.IsMasterClient && automaticallySyncScene && sessionConfig != null)
            {
                PhotonNetwork.LoadLevel(sessionConfig.MainSceneName);
            }
        }

        private void JoinDefaultRoom()
        {
            var roomName = sessionConfig != null
                ? sessionConfig.DefaultRoomName
                : "ArcadeRoom_Main";
            var maxPlayers = sessionConfig != null ? sessionConfig.MaxPlayers : (byte)2;

            var options = new RoomOptions
            {
                MaxPlayers = maxPlayers,
                IsOpen = true,
                IsVisible = true
            };

            PhotonNetwork.JoinOrCreateRoom(roomName, options, TypedLobby.Default);
        }

        private void SpawnLocalNetworkPlayer()
        {
            if (_spawnedLocalPlayer || networkPlayerPrefab == null)
            {
                return;
            }

            var spawn = ResolveSpawnPoint();
            var position = spawn != null ? spawn.position : transform.position;
            var rotation = spawn != null ? spawn.rotation : transform.rotation;
            var parent = spawnRoot != null ? spawnRoot : null;

            PhotonNetwork.Instantiate(networkPlayerPrefab.name, position, rotation, 0);

            if (parent != null)
            {
                Debug.Log("PhotonNetwork.Instantiate requires a Resources prefab, so runtime parent is not applied automatically.", this);
            }

            _spawnedLocalPlayer = true;
        }

        private Transform ResolveSpawnPoint()
        {
            var desiredSlot = PhotonNetwork.LocalPlayer != null && PhotonNetwork.LocalPlayer.ActorNumber == 2
                ? ArcadePlayerSlot.Player2
                : ArcadePlayerSlot.Player1;

            var points = FindObjectsByType<ArcadeMultiplayerSpawnPoint>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            for (var i = 0; i < points.Length; i++)
            {
                if (points[i] != null && points[i].PlayerSlot == desiredSlot)
                {
                    return points[i].transform;
                }
            }

            return points.Length > 0 && points[0] != null ? points[0].transform : null;
        }
    }
}
#endif
