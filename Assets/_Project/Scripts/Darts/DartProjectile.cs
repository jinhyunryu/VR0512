using ArcadeRoom.Core;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

namespace ArcadeRoom.Darts
{
    [RequireComponent(typeof(Rigidbody))]
    [RequireComponent(typeof(XRGrabInteractable))]
    public class DartProjectile : MonoBehaviour, IArcadeResettable
    {
        [SerializeField] private XRGrabInteractable grabInteractable;
        [SerializeField] private Rigidbody dartRigidbody;
        [SerializeField] private float minReleaseSpeedForStick = 1.2f;

        public bool IsHeld { get; private set; }
        public bool IsArmedAfterRelease { get; private set; }
        public Vector3 CurrentVelocity => dartRigidbody != null ? dartRigidbody.linearVelocity : Vector3.zero;

        private void Awake()
        {
            ResolveReferences();
            RegisterCallbacks();
        }

        private void OnDestroy()
        {
            UnregisterCallbacks();
        }

        public bool IsReleaseFastEnough()
        {
            return IsArmedAfterRelease && CurrentVelocity.sqrMagnitude >= minReleaseSpeedForStick * minReleaseSpeedForStick;
        }

        public void MarkStuck()
        {
            IsArmedAfterRelease = false;
        }

        public void ResetState()
        {
            IsHeld = false;
            IsArmedAfterRelease = false;
        }

        private void OnSelectEntered(SelectEnterEventArgs args)
        {
            ResolveReferences();

            IsHeld = true;
            IsArmedAfterRelease = false;

            if (transform.parent != null)
            {
                transform.SetParent(null, true);
            }

            if (dartRigidbody == null)
            {
                return;
            }

            dartRigidbody.isKinematic = false;
            dartRigidbody.useGravity = true;
        }

        private void OnSelectExited(SelectExitEventArgs args)
        {
            IsHeld = false;
            IsArmedAfterRelease = true;
        }

        private void ResolveReferences()
        {
            if (grabInteractable == null)
            {
                grabInteractable = GetComponent<XRGrabInteractable>();
            }

            if (dartRigidbody == null)
            {
                dartRigidbody = GetComponent<Rigidbody>();
            }
        }

        private void RegisterCallbacks()
        {
            if (grabInteractable == null)
            {
                return;
            }

            grabInteractable.selectEntered.RemoveListener(OnSelectEntered);
            grabInteractable.selectExited.RemoveListener(OnSelectExited);
            grabInteractable.selectEntered.AddListener(OnSelectEntered);
            grabInteractable.selectExited.AddListener(OnSelectExited);
        }

        private void UnregisterCallbacks()
        {
            if (grabInteractable == null)
            {
                return;
            }

            grabInteractable.selectEntered.RemoveListener(OnSelectEntered);
            grabInteractable.selectExited.RemoveListener(OnSelectExited);
        }
    }
}
