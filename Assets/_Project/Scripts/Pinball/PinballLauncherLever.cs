using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

namespace ArcadeRoom.Pinball
{
    [DisallowMultipleComponent]
    public sealed class PinballLauncherLever : MonoBehaviour
    {
        [SerializeField] private PinballPlunger plunger;
        [SerializeField] private Transform movingHandle;
        [SerializeField] private XRBaseInteractable grabInteractable;
        [SerializeField] private Rigidbody handleBody;
        [SerializeField] private bool autoAssignGrabColliders = true;
        [Header("Pull Track")]
        [SerializeField] private Transform restPoint;
        [SerializeField] private Transform pulledLimitPoint;
        [SerializeField] private bool useCurrentPositionAsRest = true;
        [SerializeField] private Vector3 restLocalPosition;
        [SerializeField] private Vector3 localPullAxis = Vector3.back;
        [SerializeField, Min(0.001f)] private float maxPullDistance = 0.25f;
        [SerializeField] private bool invertInteractorPull;
        [SerializeField, Min(0f)] private float minLaunchPullRatio = 0.05f;
        [SerializeField, Min(0f)] private float returnSpeed = 0.75f;
        [Header("Runtime")]
        [SerializeField] private bool releaseOnDisable = true;
        [SerializeField] private bool zeroVelocityWhenConstrained = true;

        private Vector3 _runtimeRestLocalPosition;
        private IXRSelectInteractor _selectingInteractor;
        private float _grabStartInteractorPullDistance;
        private float _grabStartHandlePullDistance;
        private bool _grabbed;
        private bool _returning;

        private Vector3 PullAxis
        {
            get
            {
                if (localPullAxis.sqrMagnitude <= 0.0001f)
                {
                    return Vector3.back;
                }

                return localPullAxis.normalized;
            }
        }

        private void Awake()
        {
            ResolveReferences();
            CaptureRestPosition();
            ConfigureGrabInteractableForTrack();
        }

        private void OnEnable()
        {
            ResolveReferences();
            CaptureRestPosition();
            ConfigureGrabInteractableForTrack();
            RegisterGrabCallbacks();
        }

        private void OnDisable()
        {
            UnregisterGrabCallbacks();

            if (releaseOnDisable)
            {
                _grabbed = false;
                _returning = false;
                _selectingInteractor = null;
            }
        }

        private void LateUpdate()
        {
            if (movingHandle == null)
            {
                return;
            }

            if (_grabbed)
            {
                UpdateGrabbedHandlePosition();
                return;
            }

            if (!_returning)
            {
                return;
            }

            var nextPullDistance = Mathf.MoveTowards(GetPullDistance(), 0f, returnSpeed * Time.deltaTime);
            SetPullDistance(nextPullDistance);

            if (nextPullDistance <= 0.0001f)
            {
                _returning = false;
            }
        }

        private void OnValidate()
        {
            ResolveReferences();
            maxPullDistance = Mathf.Max(0.001f, maxPullDistance);
            minLaunchPullRatio = Mathf.Clamp01(minLaunchPullRatio);
            ConfigureGrabInteractableForTrack();
        }

        public void ReleaseLever()
        {
            ClampHandleToTrack();

            var pullRatio = GetPullRatio();

            _grabbed = false;
            _returning = true;
            _selectingInteractor = null;

            if (pullRatio >= minLaunchPullRatio && plunger != null)
            {
                plunger.LaunchByChargeRatio(pullRatio);
            }
        }

        public float GetPullRatio()
        {
            return Mathf.Clamp01(GetPullDistance() / maxPullDistance);
        }

        private void HandleSelectEntered(SelectEnterEventArgs args)
        {
            _selectingInteractor = args.interactorObject;
            _grabStartInteractorPullDistance = GetInteractorPullDistance(_selectingInteractor);
            _grabStartHandlePullDistance = GetPullDistance();
            _grabbed = true;
            _returning = false;
            ClampHandleToTrack();
        }

        private void HandleSelectExited(SelectExitEventArgs args)
        {
            ReleaseLever();
        }

        private void UpdateGrabbedHandlePosition()
        {
            if (_selectingInteractor == null)
            {
                ClampHandleToTrack();
                _grabbed = false;
                _returning = true;
                return;
            }

            var pullDelta = GetInteractorPullDistance(_selectingInteractor) - _grabStartInteractorPullDistance;
            if (invertInteractorPull)
            {
                pullDelta = -pullDelta;
            }

            SetPullDistance(_grabStartHandlePullDistance + pullDelta);
        }

        private void ClampHandleToTrack()
        {
            SetPullDistance(GetPullDistance());
        }

        private float GetPullDistance()
        {
            var delta = movingHandle.localPosition - _runtimeRestLocalPosition;
            return Mathf.Clamp(Vector3.Dot(delta, PullAxis), 0f, maxPullDistance);
        }

        private float GetInteractorPullDistance(IXRSelectInteractor interactor)
        {
            if (interactor == null)
            {
                return 0f;
            }

            var restWorldPosition = transform.TransformPoint(_runtimeRestLocalPosition);
            var worldPullAxis = transform.TransformDirection(PullAxis);
            return Vector3.Dot(interactor.transform.position - restWorldPosition, worldPullAxis);
        }

        private void SetPullDistance(float pullDistance)
        {
            movingHandle.localPosition = _runtimeRestLocalPosition + PullAxis * Mathf.Clamp(pullDistance, 0f, maxPullDistance);

            if (zeroVelocityWhenConstrained && handleBody != null)
            {
                ResetHandleVelocityIfDynamic();
            }
        }

        private void CaptureRestPosition()
        {
            if (movingHandle == null)
            {
                return;
            }

            var restPointMovesWithHandle = IsChildOfMovingHandle(restPoint);
            _runtimeRestLocalPosition = restPoint != null && !restPointMovesWithHandle
                ? transform.InverseTransformPoint(restPoint.position)
                : useCurrentPositionAsRest
                    ? movingHandle.localPosition
                    : restLocalPosition;

            if (pulledLimitPoint == null || IsChildOfMovingHandle(pulledLimitPoint))
            {
                return;
            }

            var pulledLocalPosition = transform.InverseTransformPoint(pulledLimitPoint.position);
            var pullOffset = pulledLocalPosition - _runtimeRestLocalPosition;
            if (pullOffset.sqrMagnitude <= 0.000001f)
            {
                return;
            }

            localPullAxis = pullOffset.normalized;
            maxPullDistance = pullOffset.magnitude;
        }

        private bool IsChildOfMovingHandle(Transform candidate)
        {
            return candidate != null && movingHandle != null && candidate.IsChildOf(movingHandle);
        }

        private void RegisterGrabCallbacks()
        {
            if (grabInteractable == null)
            {
                return;
            }

            grabInteractable.selectEntered.RemoveListener(HandleSelectEntered);
            grabInteractable.selectExited.RemoveListener(HandleSelectExited);
            grabInteractable.selectEntered.AddListener(HandleSelectEntered);
            grabInteractable.selectExited.AddListener(HandleSelectExited);
        }

        private void UnregisterGrabCallbacks()
        {
            if (grabInteractable == null)
            {
                return;
            }

            grabInteractable.selectEntered.RemoveListener(HandleSelectEntered);
            grabInteractable.selectExited.RemoveListener(HandleSelectExited);
        }

        private void ResolveReferences()
        {
            if (movingHandle == null)
            {
                movingHandle = transform;
            }

            if (grabInteractable == null)
            {
                grabInteractable = GetComponent<XRBaseInteractable>();
            }

            if (handleBody == null)
            {
                handleBody = GetComponent<Rigidbody>();
            }

            if (!autoAssignGrabColliders || grabInteractable == null || grabInteractable.colliders.Count > 0)
            {
                return;
            }

            var colliderRoot = movingHandle != null ? movingHandle : transform;
            var colliders = colliderRoot.GetComponentsInChildren<Collider>(true);
            foreach (var collider in colliders)
            {
                if (collider != null && !collider.isTrigger)
                {
                    grabInteractable.colliders.Add(collider);
                }
            }
        }

        private void ConfigureGrabInteractableForTrack()
        {
            if (grabInteractable is XRGrabInteractable grab)
            {
                grab.throwOnDetach = false;
            }
        }

        private void ResetHandleVelocityIfDynamic()
        {
            if (handleBody == null || handleBody.isKinematic)
            {
                return;
            }

            handleBody.linearVelocity = Vector3.zero;
            handleBody.angularVelocity = Vector3.zero;
        }
    }
}
