using ArcadeRoom.Audio;
using UnityEngine;

namespace ArcadeRoom.Pinball
{
    /// <summary>
    /// Plays a switch SFX when a pinball passes through a trigger or hits a switch collider.
    /// Attach this to a rollover switch trigger/collider.
    /// This script only handles SFX.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class PinballSwitchSfx : MonoBehaviour
    {
        [Header("Ball Filter")]
        [Tooltip("Only colliders on these layers can trigger the switch SFX. Set this to PinballBall.")]
        [SerializeField] private LayerMask ballLayerMask;

        [Tooltip("Optional component check. If enabled, the object must also have PinballBall in its parent or Rigidbody object.")]
        [SerializeField] private bool requirePinballBallComponent = true;

        [Tooltip("Minimum ball speed required to play the SFX.")]
        [SerializeField, Min(0f)] private float minBallSpeed = 0.05f;

        [Tooltip("Prevents repeated SFX if the ball remains inside the trigger or rapidly re-enters.")]
        [SerializeField, Min(0f)] private float cooldown = 0.05f;

        [Tooltip("Ignore trigger/collision events briefly after Play starts.")]
        [SerializeField, Min(0f)] private float startupIgnoreTime = 0.15f;

        [Header("SFX")]
        [SerializeField] private AudioClip switchClip;

        [SerializeField, Range(0f, 1f)] private float volume = 0.7f;

        [SerializeField] private Vector2 pitchRange = new(0.96f, 1.04f);

        [SerializeField, Range(0f, 1f)] private float spatialBlend = 1f;

        [SerializeField, Min(0.1f)] private float maxDistance = 7f;

        [Tooltip("Name used for the temporary AudioSource object created by ArcadeSfxPlayer.")]
        [SerializeField] private string audioObjectName = "Pinball_SFX_Switch";

        private float _enabledTime;
        private float _lastPlayTime = float.NegativeInfinity;

        private void OnEnable()
        {
            _enabledTime = Time.time;
        }

        private void OnValidate()
        {
            pitchRange = ArcadeSfxPlayer.NormalizePitchRange(pitchRange);
            maxDistance = Mathf.Max(0.1f, maxDistance);
            cooldown = Mathf.Max(0f, cooldown);
            startupIgnoreTime = Mathf.Max(0f, startupIgnoreTime);
            minBallSpeed = Mathf.Max(0f, minBallSpeed);
        }

        private void OnTriggerEnter(Collider other)
        {
            TryPlay(other, other.transform.position, GetBallSpeed(other));
        }

        private void OnCollisionEnter(Collision collision)
        {
            var hitPosition = collision.contactCount > 0
                ? collision.GetContact(0).point
                : collision.transform.position;

            TryPlay(collision.collider, hitPosition, collision.relativeVelocity.magnitude);
        }

        private void TryPlay(Collider other, Vector3 position, float speed)
        {
            if (switchClip == null)
            {
                return;
            }

            if (Time.time - _enabledTime < startupIgnoreTime)
            {
                return;
            }

            if (Time.time - _lastPlayTime < cooldown)
            {
                return;
            }

            if (!IsAllowedBall(other))
            {
                return;
            }

            if (speed < minBallSpeed)
            {
                return;
            }

            _lastPlayTime = Time.time;

            ArcadeSfxPlayer.PlayOneShot(
                switchClip,
                position,
                volume,
                ArcadeSfxPlayer.RandomPitch(pitchRange),
                spatialBlend,
                maxDistance,
                audioObjectName);
        }

        private bool IsAllowedBall(Collider other)
        {
            if (other == null)
            {
                return false;
            }

            if (!IsLayerAllowed(other))
            {
                return false;
            }

            if (!requirePinballBallComponent)
            {
                return true;
            }

            if (other.GetComponentInParent<PinballBall>() != null)
            {
                return true;
            }

            var attachedRigidbody = other.attachedRigidbody;
            return attachedRigidbody != null && attachedRigidbody.GetComponent<PinballBall>() != null;
        }

        private bool IsLayerAllowed(Collider other)
        {
            var colliderLayerAllowed = (ballLayerMask.value & (1 << other.gameObject.layer)) != 0;
            if (colliderLayerAllowed)
            {
                return true;
            }

            var attachedRigidbody = other.attachedRigidbody;
            if (attachedRigidbody == null)
            {
                return false;
            }

            return (ballLayerMask.value & (1 << attachedRigidbody.gameObject.layer)) != 0;
        }

        private static float GetBallSpeed(Collider other)
        {
            var attachedRigidbody = other != null ? other.attachedRigidbody : null;
            return attachedRigidbody != null ? attachedRigidbody.linearVelocity.magnitude : float.MaxValue;
        }
    }
}
