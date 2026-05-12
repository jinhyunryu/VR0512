using System;
using ArcadeRoom.Core;
using UnityEngine;
using UnityEngine.Events;

namespace ArcadeRoom.Pinball
{
    [DisallowMultipleComponent]
    public sealed class PinballSpinnerRotationEvent : MonoBehaviour, IArcadeResettable
    {
        [SerializeField] private Rigidbody spinnerBody;
        [SerializeField] private Transform axisSource;
        [SerializeField] private Vector3 localAxis = Vector3.right;
        [SerializeField, Min(1f)] private float degreesPerStep = 90f;
        [SerializeField, Min(0f)] private float minAngularSpeedDegrees = 20f;
        [SerializeField] private UnityEvent onStepRotated = new UnityEvent();
        [SerializeField] private UnityEvent onFullTurnRotated = new UnityEvent();

        private float _stepAccumulator;
        private float _turnAccumulator;

        public event Action StepRotated;
        public event Action FullTurnRotated;

        private void Awake()
        {
            ResolveReferences();
        }

        private void OnValidate()
        {
            ResolveReferences();
        }

        private void FixedUpdate()
        {
            if (spinnerBody == null || spinnerBody.isKinematic)
            {
                return;
            }

            var axis = GetWorldAxis();
            var angularSpeedDegrees = Mathf.Abs(Vector3.Dot(spinnerBody.angularVelocity, axis) * Mathf.Rad2Deg);
            if (angularSpeedDegrees < minAngularSpeedDegrees)
            {
                return;
            }

            var deltaDegrees = angularSpeedDegrees * Time.fixedDeltaTime;
            _stepAccumulator += deltaDegrees;
            _turnAccumulator += deltaDegrees;

            while (_stepAccumulator >= degreesPerStep)
            {
                _stepAccumulator -= degreesPerStep;
                onStepRotated?.Invoke();
                StepRotated?.Invoke();
            }

            while (_turnAccumulator >= 360f)
            {
                _turnAccumulator -= 360f;
                onFullTurnRotated?.Invoke();
                FullTurnRotated?.Invoke();
            }
        }

        public void ResetState()
        {
            _stepAccumulator = 0f;
            _turnAccumulator = 0f;

            if (spinnerBody == null)
            {
                return;
            }

            spinnerBody.linearVelocity = Vector3.zero;
            spinnerBody.angularVelocity = Vector3.zero;
        }

        private void ResolveReferences()
        {
            if (spinnerBody == null)
            {
                spinnerBody = GetComponent<Rigidbody>();
            }

            if (axisSource == null)
            {
                axisSource = transform;
            }
        }

        private Vector3 GetWorldAxis()
        {
            var source = axisSource != null ? axisSource : transform;
            var axis = localAxis.sqrMagnitude > 0.0001f ? localAxis.normalized : Vector3.right;
            return source.TransformDirection(axis).normalized;
        }
    }
}
