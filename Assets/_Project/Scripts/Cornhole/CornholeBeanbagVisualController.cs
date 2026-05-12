using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

namespace ArcadeRoom.Cornhole
{
    [DisallowMultipleComponent]
    public class CornholeBeanbagVisualController : MonoBehaviour
    {
        public enum VisualState
        {
            Held = 0,
            Airborne = 1,
            LandedFlat = 2,
            RimHang = 3,
            InHole = 4
        }

        [Header("References")]
        [SerializeField] private XRGrabInteractable grabInteractable;
        [SerializeField] private Rigidbody body;
        [SerializeField] private Transform visualRoot;
        [SerializeField] private Transform heldPose;
        [SerializeField] private Transform airbornePose;
        [SerializeField] private Transform landedFlatPose;
        [SerializeField] private Transform rimHangPose;
        [SerializeField] private Transform inHolePose;

        [Header("Timing")]
        [SerializeField, Min(0.01f)] private float stateBlendSpeed = 14f;
        [SerializeField, Min(0f)] private float groundedContactGrace = 0.12f;
        [SerializeField, Range(0f, 1f)] private float minGroundNormalDot = 0.15f;
        [SerializeField, Min(0f)] private float minReleaseTimeForGroundedPose = 0.05f;

        [Header("Debug")]
        [SerializeField] private bool useForcedState;
        [SerializeField] private VisualState forcedState = VisualState.Airborne;

        private bool _rimHangActive;
        private bool _inHoleActive;
        private float _lastGroundedTime = float.NegativeInfinity;
        private float _lastReleasedTime = float.NegativeInfinity;
        private VisualState _currentState = VisualState.Airborne;

        private void Reset()
        {
            CacheReferences();
        }

        private void OnValidate()
        {
            CacheReferences();
        }

        private void Awake()
        {
            CacheReferences();
        }

        private void OnEnable()
        {
            if (grabInteractable != null)
            {
                grabInteractable.selectEntered.AddListener(OnSelectEntered);
                grabInteractable.selectExited.AddListener(OnSelectExited);
            }
        }

        private void OnDisable()
        {
            if (grabInteractable != null)
            {
                grabInteractable.selectEntered.RemoveListener(OnSelectEntered);
                grabInteractable.selectExited.RemoveListener(OnSelectExited);
            }
        }

        private void Update()
        {
            UpdateState();
            ApplyVisualState(Time.deltaTime);
        }

        private void OnCollisionEnter(Collision collision)
        {
            RegisterGroundContact(collision);
        }

        private void OnCollisionStay(Collision collision)
        {
            RegisterGroundContact(collision);
        }

        public void SetRimHangState(bool active)
        {
            _rimHangActive = active;
            if (active)
            {
                _inHoleActive = false;
            }

            if (active)
            {
                _lastGroundedTime = Time.time;
            }
        }

        public void SetInHoleState(bool active)
        {
            _inHoleActive = active;
            if (active)
            {
                _rimHangActive = false;
            }
        }

        public bool IsInHole => _inHoleActive;

        private void OnSelectEntered(SelectEnterEventArgs _)
        {
            _rimHangActive = false;
            _inHoleActive = false;
        }

        private void OnSelectExited(SelectExitEventArgs _)
        {
            _lastReleasedTime = Time.time;
        }

        private void CacheReferences()
        {
            if (grabInteractable == null)
            {
                grabInteractable = GetComponent<XRGrabInteractable>();
            }

            if (body == null)
            {
                body = GetComponent<Rigidbody>();
            }
        }

        private void UpdateState()
        {
            if (visualRoot == null)
            {
                return;
            }

            if (useForcedState)
            {
                _currentState = forcedState;
                return;
            }

            if (grabInteractable != null && grabInteractable.isSelected)
            {
                _currentState = VisualState.Held;
                return;
            }

            if (_inHoleActive)
            {
                _currentState = VisualState.InHole;
                return;
            }

            if (_rimHangActive)
            {
                _currentState = VisualState.RimHang;
                return;
            }

            if (HasRecentGroundContact() && Time.time - _lastReleasedTime >= minReleaseTimeForGroundedPose)
            {
                _currentState = VisualState.LandedFlat;
                return;
            }

            _currentState = VisualState.Airborne;
        }

        private void ApplyVisualState(float deltaTime)
        {
            if (visualRoot == null)
            {
                return;
            }

            var target = GetTargetPose();
            if (target == null)
            {
                return;
            }

            var t = 1f - Mathf.Exp(-stateBlendSpeed * deltaTime);
            visualRoot.localPosition = Vector3.Lerp(visualRoot.localPosition, target.localPosition, t);
            visualRoot.localRotation = Quaternion.Slerp(visualRoot.localRotation, target.localRotation, t);
            visualRoot.localScale = Vector3.Lerp(visualRoot.localScale, target.localScale, t);
        }

        private Transform GetTargetPose()
        {
            return _currentState switch
            {
                VisualState.Held => heldPose != null ? heldPose : airbornePose,
                VisualState.LandedFlat => landedFlatPose != null ? landedFlatPose : airbornePose,
                VisualState.RimHang => rimHangPose != null ? rimHangPose : landedFlatPose,
                VisualState.InHole => inHolePose != null ? inHolePose : rimHangPose,
                _ => airbornePose,
            };
        }

        private bool HasRecentGroundContact()
        {
            return Time.time - _lastGroundedTime <= groundedContactGrace;
        }

        private void RegisterGroundContact(Collision collision)
        {
            if (collision == null || collision.contactCount <= 0)
            {
                return;
            }

            for (var i = 0; i < collision.contactCount; i++)
            {
                if (collision.GetContact(i).normal.y >= minGroundNormalDot)
                {
                    _lastGroundedTime = Time.time;
                    return;
                }
            }
        }
    }
}
