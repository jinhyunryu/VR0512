using ArcadeRoom.Core;
using UnityEngine;

namespace ArcadeRoom.Darts
{
    [RequireComponent(typeof(DartProjectile))]
    [RequireComponent(typeof(Rigidbody))]
    public class DartStickOnHit : MonoBehaviour, IArcadeResettable
    {
        [SerializeField] private DartProjectile dartProjectile;
        [SerializeField] private Rigidbody dartRigidbody;
        [SerializeField] private float minStickSpeed = 1.5f;

        private bool _isStuck;

        private void Awake()
        {
            ResolveReferences();
        }

        private void OnCollisionEnter(Collision collision)
        {
            if (_isStuck || dartProjectile == null || dartProjectile.IsHeld || !dartProjectile.IsReleaseFastEnough())
            {
                return;
            }

            if (dartRigidbody == null || dartRigidbody.linearVelocity.magnitude < minStickSpeed)
            {
                return;
            }

            var receiver = collision.collider.GetComponentInParent<DartBoardHitReceiver>();
            if (receiver == null)
            {
                return;
            }

            StickToBoard(receiver, collision);
        }

        public void ResetState()
        {
            _isStuck = false;

            if (dartRigidbody == null)
            {
                ResolveReferences();
            }

            if (dartRigidbody == null)
            {
                return;
            }

            dartRigidbody.useGravity = true;
            dartRigidbody.isKinematic = false;
        }

        private void StickToBoard(DartBoardHitReceiver receiver, Collision collision)
        {
            var contact = collision.GetContact(0);
            var forward = dartRigidbody.linearVelocity.sqrMagnitude > 0.0001f ? dartRigidbody.linearVelocity.normalized : transform.forward;
            var up = Mathf.Abs(Vector3.Dot(forward, Vector3.up)) > 0.95f ? Vector3.right : Vector3.up;
            var targetPosition = contact.point + (forward * receiver.EmbedDepth);
            var targetRotation = Quaternion.LookRotation(forward, up);

            transform.SetPositionAndRotation(targetPosition, targetRotation);
            transform.SetParent(receiver.StickRoot, true);

            dartRigidbody.linearVelocity = Vector3.zero;
            dartRigidbody.angularVelocity = Vector3.zero;
            dartRigidbody.useGravity = false;
            dartRigidbody.isKinematic = true;

            dartProjectile.MarkStuck();
            _isStuck = true;
        }

        private void ResolveReferences()
        {
            if (dartProjectile == null)
            {
                dartProjectile = GetComponent<DartProjectile>();
            }

            if (dartRigidbody == null)
            {
                dartRigidbody = GetComponent<Rigidbody>();
            }
        }
    }
}
