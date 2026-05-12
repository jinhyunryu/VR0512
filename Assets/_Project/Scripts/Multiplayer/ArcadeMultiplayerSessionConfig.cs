using UnityEngine;

namespace ArcadeRoom.Multiplayer
{
    [CreateAssetMenu(
        fileName = "ArcadeMultiplayerSessionConfig",
        menuName = "Arcade Room/Multiplayer Session Config")]
    public sealed class ArcadeMultiplayerSessionConfig : ScriptableObject
    {
        [SerializeField] private string gameVersion = "arcade-room-prototype-v1";
        [SerializeField] private string defaultRoomName = "ArcadeRoom_Main";
        [SerializeField, Range(1, 2)] private byte maxPlayers = 2;
        [SerializeField] private string mainSceneName = "ArcadeRoom_Main";

        public string GameVersion => gameVersion;
        public string DefaultRoomName => defaultRoomName;
        public byte MaxPlayers => maxPlayers;
        public string MainSceneName => mainSceneName;
    }
}
