using UnityEngine;

namespace ArcadeRoom.Interaction
{
    public class PullLaunchInteractor : MonoBehaviour
    {
        [SerializeField] private Transform pullOrigin;
        [SerializeField] private float maxPullDistance = 0.35f;
        [SerializeField] private float maxLaunchSpeed = 6f;

        public Vector3 CalculateLaunchVelocity(Vector3 pulledWorldPosition)
        {
            var origin = pullOrigin != null ? pullOrigin.position : transform.position;
            var pullVector = origin - pulledWorldPosition;

            if (pullVector.sqrMagnitude <= Mathf.Epsilon)
            {
                return Vector3.zero;
            }

            var clampedDistance = Mathf.Min(pullVector.magnitude, Mathf.Max(0.01f, maxPullDistance));
            var normalizedStrength = clampedDistance / Mathf.Max(0.01f, maxPullDistance);
            return pullVector.normalized * (normalizedStrength * maxLaunchSpeed);
        }
    }
}
