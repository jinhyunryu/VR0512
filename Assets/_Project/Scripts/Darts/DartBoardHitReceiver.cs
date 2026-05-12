using UnityEngine;

namespace ArcadeRoom.Darts
{
    public class DartBoardHitReceiver : MonoBehaviour
    {
        [SerializeField] private Transform stickRoot;
        [SerializeField] private float embedDepth = 0.04f;

        public Transform StickRoot => stickRoot != null ? stickRoot : transform;
        public float EmbedDepth => embedDepth;

        public void Configure(Transform customStickRoot, float customEmbedDepth)
        {
            stickRoot = customStickRoot;
            embedDepth = customEmbedDepth;
        }
    }
}
