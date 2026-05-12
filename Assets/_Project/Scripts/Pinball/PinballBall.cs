using UnityEngine;

namespace ArcadeRoom.Pinball
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Rigidbody))]
    public sealed class PinballBall : MonoBehaviour
    {
        [SerializeField] private Rigidbody body;

        public Rigidbody Body
        {
            get
            {
                ResolveReferences();
                return body;
            }
        }

        private void Awake()
        {
            ResolveReferences();
        }

        private void OnValidate()
        {
            ResolveReferences();
        }

        public void ResetTo(Transform spawnPoint)
        {
            if (spawnPoint == null)
            {
                return;
            }

            ResetTo(spawnPoint.position, spawnPoint.rotation);
        }

        public void ResetTo(Vector3 position, Quaternion rotation)
        {
            ResolveReferences();

            if (body != null)
            {
                body.Sleep();
                body.linearVelocity = Vector3.zero;
                body.angularVelocity = Vector3.zero;
            }

            transform.SetPositionAndRotation(position, rotation);
            Physics.SyncTransforms();

            if (body == null)
            {
                return;
            }

            body.linearVelocity = Vector3.zero;
            body.angularVelocity = Vector3.zero;
            body.Sleep();
        }

        private void ResolveReferences()
        {
            if (body == null)
            {
                body = GetComponent<Rigidbody>();
            }
        }
    }
}
