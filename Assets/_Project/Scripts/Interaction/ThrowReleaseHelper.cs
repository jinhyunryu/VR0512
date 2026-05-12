using UnityEngine;

namespace ArcadeRoom.Interaction
{
    public class ThrowReleaseHelper : MonoBehaviour
    {
        [SerializeField] private float linearVelocityMultiplier = 1f;
        [SerializeField] private float angularVelocityMultiplier = 1f;

        public Vector3 AdjustLinearVelocity(Vector3 sourceVelocity)
        {
            return sourceVelocity * linearVelocityMultiplier;
        }

        public Vector3 AdjustAngularVelocity(Vector3 sourceAngularVelocity)
        {
            return sourceAngularVelocity * angularVelocityMultiplier;
        }
    }
}
