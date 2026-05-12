using UnityEngine;

namespace ArcadeRoom.Multiplayer
{
    public enum ArcadePlayerSlot
    {
        Player1 = 0,
        Player2 = 1
    }

    [DisallowMultipleComponent]
    public sealed class ArcadeMultiplayerSpawnPoint : MonoBehaviour
    {
        [SerializeField] private ArcadePlayerSlot playerSlot;
        [SerializeField] private Color gizmoColor = new(0.1f, 0.85f, 1f, 0.75f);
        [SerializeField, Min(0.05f)] private float gizmoRadius = 0.25f;

        public ArcadePlayerSlot PlayerSlot => playerSlot;

        private void OnDrawGizmos()
        {
            Gizmos.color = gizmoColor;
            Gizmos.DrawWireSphere(transform.position, gizmoRadius);
            Gizmos.DrawRay(transform.position, transform.forward * gizmoRadius * 1.8f);
        }
    }
}
