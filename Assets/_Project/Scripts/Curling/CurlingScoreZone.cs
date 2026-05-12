using UnityEngine;

namespace ArcadeRoom.Curling
{
    [DisallowMultipleComponent]
    public sealed class CurlingScoreZone : MonoBehaviour
    {
        [SerializeField] private SphereCollider sphereCollider;
        [SerializeField] private Transform centerOverride;
        [SerializeField] private int pointValue = 1;
        [SerializeField, Min(0f)] private float radiusOverride;

        public int PointValue => pointValue;

        private void Awake()
        {
            ResolveReferences();
        }

        private void OnValidate()
        {
            ResolveReferences();
        }

        public bool ContainsProbe(Transform probe)
        {
            if (probe == null)
            {
                return false;
            }

            var radius = GetRadius();
            if (radius <= 0f)
            {
                return false;
            }

            var offset = probe.position - GetCenter();
            offset.y = 0f;
            return offset.sqrMagnitude <= radius * radius;
        }

        private Vector3 GetCenter()
        {
            if (centerOverride != null)
            {
                return centerOverride.position;
            }

            return sphereCollider != null
                ? sphereCollider.transform.TransformPoint(sphereCollider.center)
                : transform.position;
        }

        private float GetRadius()
        {
            if (radiusOverride > 0f)
            {
                return radiusOverride;
            }

            if (sphereCollider == null)
            {
                return 0f;
            }

            var scale = sphereCollider.transform.lossyScale;
            return sphereCollider.radius * Mathf.Max(Mathf.Abs(scale.x), Mathf.Abs(scale.z));
        }

        private void ResolveReferences()
        {
            if (sphereCollider == null)
            {
                sphereCollider = GetComponent<SphereCollider>();
            }
        }

        private void OnDrawGizmosSelected()
        {
            ResolveReferences();

            var radius = GetRadius();
            if (radius <= 0f)
            {
                return;
            }

            Gizmos.color = pointValue >= 2 ? Color.red : Color.cyan;
            Gizmos.DrawWireSphere(GetCenter(), radius);
        }
    }
}
