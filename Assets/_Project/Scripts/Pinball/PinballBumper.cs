using ArcadeRoom.Audio;
using UnityEngine;

namespace ArcadeRoom.Pinball
{
    [DisallowMultipleComponent]
    public sealed class PinballBumper : MonoBehaviour
    {
        private const string DefaultBumperHitClipPath = "Assets/_Project/Audio/SFX/Pinball/PinBall Bumper Hit.mp3";

        [SerializeField, Min(0f)] private float impulse = 4f;
        [SerializeField, Min(0f)] private float upwardImpulse = 0.05f;
        [SerializeField, Min(0f)] private float minIncomingSpeed = 0.15f;
        [SerializeField, Min(0f)] private float cooldown = 0.03f;
        [SerializeField] private bool bumpWhileStuck = true;
        [SerializeField, Min(0f)] private float stuckSpeedThreshold = 0.05f;
        [Header("SFX")]
        [SerializeField] private AudioClip bumperHitClip;
        [SerializeField, Range(0f, 1f)] private float bumperHitVolume = 0.7f;
        [SerializeField] private Vector2 hitPitchRange = new(0.96f, 1.05f);
        [SerializeField, Range(0f, 1f)] private float spatialBlend = 1f;
        [SerializeField, Min(0.1f)] private float maxDistance = 7f;

        private float _lastBumpTime = float.NegativeInfinity;

        private void Awake()
        {
            ResolveDefaultSfx();
        }

        private void OnValidate()
        {
            hitPitchRange = ArcadeSfxPlayer.NormalizePitchRange(hitPitchRange);
            maxDistance = Mathf.Max(0.1f, maxDistance);
            ResolveDefaultSfx();
        }

        private void OnCollisionEnter(Collision collision)
        {
            TryBump(collision, false);
        }

        private void OnCollisionStay(Collision collision)
        {
            if (bumpWhileStuck)
            {
                TryBump(collision, true);
            }
        }

        private void TryBump(Collision collision, bool allowStuckBump)
        {
            if (!CanBump(collision, allowStuckBump))
            {
                return;
            }

            var targetBody = collision.rigidbody;
            if (targetBody == null)
            {
                return;
            }

            var direction = targetBody.worldCenterOfMass - transform.position;
            if (direction.sqrMagnitude < 0.0001f && collision.contactCount > 0)
            {
                direction = collision.GetContact(0).normal;
            }

            direction.y = upwardImpulse > 0f ? Mathf.Abs(direction.y) : 0f;
            direction = direction.sqrMagnitude > 0.0001f ? direction.normalized : transform.forward;

            targetBody.WakeUp();
            targetBody.AddForce(direction * impulse + Vector3.up * upwardImpulse, ForceMode.Impulse);
            _lastBumpTime = Time.time;

            ArcadeSfxPlayer.PlayOneShot(
                bumperHitClip,
                GetCollisionPosition(collision),
                bumperHitVolume,
                ArcadeSfxPlayer.RandomPitch(hitPitchRange),
                spatialBlend,
                maxDistance,
                "Pinball_SFX_Bumper");
        }

        private bool CanBump(Collision collision, bool allowStuckBump)
        {
            if (Time.time - _lastBumpTime < cooldown)
            {
                return false;
            }

            if (collision.rigidbody == null || collision.rigidbody.GetComponent<PinballBall>() == null)
            {
                return false;
            }

            if (collision.relativeVelocity.magnitude >= minIncomingSpeed)
            {
                return true;
            }

            return allowStuckBump && collision.rigidbody.linearVelocity.magnitude <= stuckSpeedThreshold;
        }

        private void ResolveDefaultSfx()
        {
            if (bumperHitClip == null)
            {
                bumperHitClip = ArcadeSfxPlayer.LoadEditorClip(DefaultBumperHitClipPath);
            }
        }

        private static Vector3 GetCollisionPosition(Collision collision)
        {
            return collision.contactCount > 0
                ? collision.GetContact(0).point
                : collision.transform.position;
        }
    }
}
