using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.InputSystem;

namespace ArcadeRoom.Pinball
{
    [DisallowMultipleComponent]
    public sealed class PinballPlunger : MonoBehaviour
    {
        [SerializeField] private InputActionReference launchAction;
        [SerializeField] private Transform launchPoint;
        [SerializeField] private Transform launchDirection;
        [SerializeField, Min(0f)] private float minImpulse = 2f;
        [SerializeField, Min(0f)] private float maxImpulse = 9f;
        [SerializeField, Min(0.01f)] private float fullChargeSeconds = 1.2f;
        [SerializeField, Min(0.01f)] private float chargeExponent = 1f;
        [SerializeField] private bool clearBallVelocityOnLaunch = true;
        [SerializeField] private bool snapBallToLaunchPointOnLaunch;
        [SerializeField] private bool preferFallbackSearch;
        [SerializeField, Min(0f)] private float fallbackLaunchRadius = 0.08f;
        [SerializeField] private UnityEvent onLaunched = new UnityEvent();
        [SerializeField] private PinballBallUnityEvent onBallLaunched = new PinballBallUnityEvent();

        private readonly List<PinballBall> _ballsInLaunchZone = new();
        private float _chargeStartTime;
        private bool _charging;

        public event Action<PinballBall> BallLaunched;

        private void OnEnable()
        {
            if (launchAction != null && launchAction.action != null)
            {
                launchAction.action.started += HandleChargeStarted;
                launchAction.action.canceled += HandleChargeCanceled;
                launchAction.action.Enable();
            }
        }

        private void OnDisable()
        {
            if (launchAction != null && launchAction.action != null)
            {
                launchAction.action.started -= HandleChargeStarted;
                launchAction.action.canceled -= HandleChargeCanceled;
            }
        }

        private void OnValidate()
        {
            maxImpulse = Mathf.Max(maxImpulse, minImpulse);
        }

        private void OnTriggerEnter(Collider other)
        {
            var ball = other.GetComponentInParent<PinballBall>();
            if (ball != null && !_ballsInLaunchZone.Contains(ball))
            {
                _ballsInLaunchZone.Add(ball);
            }
        }

        private void OnTriggerExit(Collider other)
        {
            var ball = other.GetComponentInParent<PinballBall>();
            if (ball != null)
            {
                _ballsInLaunchZone.Remove(ball);
            }
        }

        public void BeginCharge()
        {
            _charging = true;
            _chargeStartTime = Time.time;
        }

        public void Release()
        {
            if (!_charging)
            {
                BeginCharge();
            }

            var chargeTime = Mathf.Max(0f, Time.time - _chargeStartTime);
            var chargeRatio = Mathf.Clamp01(chargeTime / fullChargeSeconds);
            Launch(Mathf.Lerp(minImpulse, maxImpulse, chargeRatio));
            _charging = false;
        }

        public void Launch(float impulse)
        {
            var ball = GetLaunchBall();
            if (ball == null || ball.Body == null)
            {
                return;
            }

            var directionTransform = launchDirection != null ? launchDirection : transform;
            var direction = directionTransform.forward.sqrMagnitude > 0.0001f
                ? directionTransform.forward.normalized
                : Vector3.forward;

            if (clearBallVelocityOnLaunch)
            {
                ball.Body.linearVelocity = Vector3.zero;
                ball.Body.angularVelocity = Vector3.zero;
            }

            if (snapBallToLaunchPointOnLaunch && launchPoint != null)
            {
                ball.transform.position = launchPoint.position;
                Physics.SyncTransforms();
            }

            ball.Body.WakeUp();
            ball.Body.AddForce(direction * impulse, ForceMode.Impulse);

            onLaunched?.Invoke();
            onBallLaunched?.Invoke(ball);
            BallLaunched?.Invoke(ball);
        }

        public void LaunchByChargeRatio(float chargeRatio)
        {
            var shapedRatio = Mathf.Pow(Mathf.Clamp01(chargeRatio), chargeExponent);
            Launch(Mathf.Lerp(minImpulse, maxImpulse, shapedRatio));
        }

        private void HandleChargeStarted(InputAction.CallbackContext context)
        {
            BeginCharge();
        }

        private void HandleChargeCanceled(InputAction.CallbackContext context)
        {
            Release();
        }

        private PinballBall GetLaunchBall()
        {
            if (preferFallbackSearch)
            {
                var fallbackBall = GetFallbackLaunchBall();
                if (fallbackBall != null)
                {
                    return fallbackBall;
                }
            }

            for (var i = _ballsInLaunchZone.Count - 1; i >= 0; i--)
            {
                var ball = _ballsInLaunchZone[i];
                if (ball == null)
                {
                    _ballsInLaunchZone.RemoveAt(i);
                    continue;
                }

                return ball;
            }

            return preferFallbackSearch ? null : GetFallbackLaunchBall();
        }

        private PinballBall GetFallbackLaunchBall()
        {
            if (fallbackLaunchRadius <= 0f)
            {
                return null;
            }

            var colliders = Physics.OverlapSphere(transform.position, fallbackLaunchRadius, ~0, QueryTriggerInteraction.Ignore);
            PinballBall closestBall = null;
            var closestDistance = float.PositiveInfinity;

            foreach (var hit in colliders)
            {
                var ball = hit.GetComponentInParent<PinballBall>();
                if (ball == null)
                {
                    continue;
                }

                var distance = (ball.transform.position - transform.position).sqrMagnitude;
                if (distance < closestDistance)
                {
                    closestDistance = distance;
                    closestBall = ball;
                }
            }

            return closestBall;
        }
    }
}
