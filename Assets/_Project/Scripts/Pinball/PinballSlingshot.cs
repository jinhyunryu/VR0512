using System;
using ArcadeRoom.Audio;
using UnityEngine;
using UnityEngine.Events;

namespace ArcadeRoom.Pinball
{
    [DisallowMultipleComponent]
    public sealed class PinballSlingshot : MonoBehaviour
    {
        private const string DefaultSlingshotClipPath = "Assets/_Project/Audio/SFX/Pinball/PinBall Bumper Hit.mp3";

        [SerializeField] private Transform directionSource;
        [SerializeField] private Vector3 localLaunchDirection = Vector3.forward;
        [Header("Activation")]
        [SerializeField] private bool launchOnCollision = true;
        [SerializeField] private bool launchOnTrigger = true;
        [SerializeField, Min(0f)] private float impulse = 3.5f;
        [SerializeField, Min(0f)] private float upwardImpulse = 0.02f;
        [SerializeField, Min(0f)] private float minIncomingSpeed = 0.08f;
        [SerializeField, Min(0f)] private float cooldown = 0.05f;
        [SerializeField] private bool removeVelocityAgainstLaunchDirection = true;
        [Header("Rubber Bounce")]
        [SerializeField] private bool useRubberBounce = true;
        [SerializeField] private bool preferCollisionNormalDirection = true;
        [SerializeField, Range(0f, 1.5f)] private float rubberRestitution = 0.55f;
        [SerializeField, Min(0f)] private float minRubberImpulse = 0.08f;
        [SerializeField, Min(0f)] private float maxRubberImpulse = 1.15f;
        [SerializeField, Min(0f)] private float maxOutgoingSpeed = 2.4f;
        [SerializeField, Range(0f, 1f)] private float preserveTangentialVelocity = 0.85f;
        [SerializeField] private UnityEvent onLaunched = new UnityEvent();
        [SerializeField] private PinballBallUnityEvent onBallLaunched = new PinballBallUnityEvent();
        [Header("SFX")]
        [SerializeField] private AudioClip slingshotClip;
        [SerializeField, Range(0f, 1f)] private float slingshotVolume = 0.65f;
        [SerializeField] private Vector2 pitchRange = new(0.98f, 1.08f);
        [SerializeField, Range(0f, 1f)] private float spatialBlend = 1f;
        [SerializeField, Min(0.1f)] private float maxDistance = 7f;

        private float _lastLaunchTime = float.NegativeInfinity;

        public event Action<PinballBall> BallLaunched;

        private void Awake()
        {
            ResolveDefaultSfx();
        }

        private void OnValidate()
        {
            pitchRange = ArcadeSfxPlayer.NormalizePitchRange(pitchRange);
            maxDistance = Mathf.Max(0.1f, maxDistance);
            maxRubberImpulse = Mathf.Max(minRubberImpulse, maxRubberImpulse);
            maxOutgoingSpeed = Mathf.Max(0f, maxOutgoingSpeed);
            ResolveDefaultSfx();
        }

        private void OnCollisionEnter(Collision collision)
        {
            if (!launchOnCollision)
            {
                return;
            }

            var ball = collision.rigidbody != null
                ? collision.rigidbody.GetComponent<PinballBall>()
                : collision.collider.GetComponentInParent<PinballBall>();

            if (ball == null || collision.relativeVelocity.magnitude < minIncomingSpeed)
            {
                return;
            }

            var hasContact = collision.contactCount > 0;
            var contactNormal = hasContact ? collision.GetContact(0).normal : Vector3.zero;
            Launch(ball, contactNormal, hasContact);
        }

        private void OnTriggerEnter(Collider other)
        {
            TryLaunchFromTrigger(other);
        }

        public void TryLaunchFromTrigger(Collider other)
        {
            var ball = other.GetComponentInParent<PinballBall>();
            if (!launchOnTrigger || ball == null || ball.Body == null || ball.Body.linearVelocity.magnitude < minIncomingSpeed)
            {
                return;
            }

            Launch(ball, Vector3.zero, false);
        }

        public void Launch(PinballBall ball)
        {
            Launch(ball, Vector3.zero, false);
        }

        private void Launch(PinballBall ball, Vector3 contactNormal, bool hasContactNormal)
        {
            if (ball == null || ball.Body == null || Time.time - _lastLaunchTime < cooldown)
            {
                return;
            }

            var launchDirection = GetLaunchDirection(contactNormal, hasContactNormal, ball.transform.position);
            var body = ball.Body;

            if (useRubberBounce)
            {
                ApplyRubberBounce(body, launchDirection);
            }
            else
            {
                ApplyLegacyImpulse(body, launchDirection);
            }

            _lastLaunchTime = Time.time;
            ArcadeSfxPlayer.PlayOneShot(
                slingshotClip,
                ball.transform.position,
                slingshotVolume,
                ArcadeSfxPlayer.RandomPitch(pitchRange),
                spatialBlend,
                maxDistance,
                "Pinball_SFX_Slingshot");
            onLaunched?.Invoke();
            onBallLaunched?.Invoke(ball);
            BallLaunched?.Invoke(ball);
        }

        private void ApplyLegacyImpulse(Rigidbody body, Vector3 launchDirection)
        {
            if (removeVelocityAgainstLaunchDirection)
            {
                var velocityAgainstLaunch = Vector3.Dot(body.linearVelocity, -launchDirection);
                if (velocityAgainstLaunch > 0f)
                {
                    body.linearVelocity += launchDirection * velocityAgainstLaunch;
                }
            }

            body.WakeUp();
            body.AddForce(launchDirection * impulse + Vector3.up * upwardImpulse, ForceMode.Impulse);
        }

        private void ApplyRubberBounce(Rigidbody body, Vector3 launchDirection)
        {
            var currentVelocity = body.linearVelocity;
            var incomingAgainstLaunch = Mathf.Max(0f, Vector3.Dot(currentVelocity, -launchDirection));
            var outgoingImpulse = Mathf.Clamp(
                impulse + (incomingAgainstLaunch * rubberRestitution),
                minRubberImpulse,
                maxRubberImpulse);

            var tangentialVelocity = Vector3.ProjectOnPlane(currentVelocity, launchDirection) * preserveTangentialVelocity;
            body.WakeUp();
            body.linearVelocity = tangentialVelocity;
            body.AddForce(launchDirection * outgoingImpulse + Vector3.up * upwardImpulse, ForceMode.Impulse);
            ClampOutgoingSpeed(body);
        }

        private void ClampOutgoingSpeed(Rigidbody body)
        {
            if (maxOutgoingSpeed <= 0f || body.linearVelocity.sqrMagnitude <= maxOutgoingSpeed * maxOutgoingSpeed)
            {
                return;
            }

            body.linearVelocity = body.linearVelocity.normalized * maxOutgoingSpeed;
        }

        private Vector3 GetLaunchDirection(Vector3 contactNormal, bool hasContactNormal, Vector3 ballWorldPosition)
        {
            if (preferCollisionNormalDirection && hasContactNormal && contactNormal.sqrMagnitude > 0.0001f)
            {
                var orientedNormal = contactNormal.normalized;
                var awayFromSlingshot = ballWorldPosition - transform.position;
                if (Vector3.Dot(orientedNormal, awayFromSlingshot) < 0f)
                {
                    orientedNormal = -orientedNormal;
                }

                return orientedNormal;
            }

            var source = directionSource != null ? directionSource : transform;
            var direction = localLaunchDirection.sqrMagnitude > 0.0001f
                ? localLaunchDirection.normalized
                : Vector3.forward;

            return source.TransformDirection(direction).normalized;
        }

        private void ResolveDefaultSfx()
        {
            if (slingshotClip == null)
            {
                slingshotClip = ArcadeSfxPlayer.LoadEditorClip(DefaultSlingshotClipPath);
            }
        }
    }
}
