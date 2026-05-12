using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

namespace ArcadeRoom.Curling
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Rigidbody))]
    public class CurlingStoneCollisionHaptics : MonoBehaviour
    {
        [SerializeField] private XRGrabInteractable grabInteractable;
        [SerializeField] private string otherStoneNameKeyword = "Curling";
        [SerializeField, Min(0f)] private float minImpactSpeed = 0.25f;
        [SerializeField, Min(0f)] private float maxImpactSpeed = 2.5f;
        [SerializeField, Range(0f, 1f)] private float minAmplitude = 0.12f;
        [SerializeField, Range(0f, 1f)] private float maxAmplitude = 0.45f;
        [SerializeField, Min(0f)] private float duration = 0.06f;
        [SerializeField, Min(0f)] private float cooldown = 0.08f;
        [SerializeField, Min(0f)] private float afterReleaseHapticWindow = 1.25f;

        private readonly List<XRBaseInputInteractor> _selectingInputInteractors = new();
        private readonly List<RecentInteractor> _recentInputInteractors = new();
        private float _lastHapticTime = float.NegativeInfinity;

        private struct RecentInteractor
        {
            public XRBaseInputInteractor interactor;
            public float expiresAt;
        }

        private void Awake()
        {
            ResolveReferences();
        }

        private void OnEnable()
        {
            ResolveReferences();

            if (grabInteractable == null)
            {
                return;
            }

            grabInteractable.selectEntered.AddListener(HandleSelectEntered);
            grabInteractable.selectExited.AddListener(HandleSelectExited);
        }

        private void OnDisable()
        {
            if (grabInteractable != null)
            {
                grabInteractable.selectEntered.RemoveListener(HandleSelectEntered);
                grabInteractable.selectExited.RemoveListener(HandleSelectExited);
            }

            _selectingInputInteractors.Clear();
            _recentInputInteractors.Clear();
        }

        private void OnValidate()
        {
            ResolveReferences();
            maxImpactSpeed = Mathf.Max(maxImpactSpeed, minImpactSpeed);
            maxAmplitude = Mathf.Max(maxAmplitude, minAmplitude);
        }

        private void OnCollisionEnter(Collision collision)
        {
            RemoveExpiredRecentInteractors();

            if (_selectingInputInteractors.Count == 0 && _recentInputInteractors.Count == 0)
            {
                return;
            }

            if (Time.time - _lastHapticTime < cooldown)
            {
                return;
            }

            var impactSpeed = collision.relativeVelocity.magnitude;
            if (impactSpeed < minImpactSpeed || !IsOtherCurlingStone(collision))
            {
                return;
            }

            PlayHaptics(impactSpeed);
            _lastHapticTime = Time.time;
        }

        private void HandleSelectEntered(SelectEnterEventArgs args)
        {
            var inputInteractor = ResolveInputInteractor(args.interactorObject);
            if (inputInteractor != null && !_selectingInputInteractors.Contains(inputInteractor))
            {
                RemoveRecentInteractor(inputInteractor);
                _selectingInputInteractors.Add(inputInteractor);
            }
        }

        private void HandleSelectExited(SelectExitEventArgs args)
        {
            var inputInteractor = ResolveInputInteractor(args.interactorObject);
            if (inputInteractor != null)
            {
                _selectingInputInteractors.Remove(inputInteractor);

                if (afterReleaseHapticWindow > 0f)
                {
                    AddRecentInteractor(inputInteractor);
                }
            }
        }

        private void PlayHaptics(float impactSpeed)
        {
            var speedT = maxImpactSpeed > minImpactSpeed
                ? Mathf.InverseLerp(minImpactSpeed, maxImpactSpeed, impactSpeed)
                : 1f;
            var amplitude = Mathf.Lerp(minAmplitude, maxAmplitude, speedT);

            for (var i = _selectingInputInteractors.Count - 1; i >= 0; i--)
            {
                var interactor = _selectingInputInteractors[i];
                if (interactor == null)
                {
                    _selectingInputInteractors.RemoveAt(i);
                    continue;
                }

                interactor.SendHapticImpulse(amplitude, duration);
            }

            for (var i = _recentInputInteractors.Count - 1; i >= 0; i--)
            {
                var recent = _recentInputInteractors[i];
                if (recent.interactor == null || recent.expiresAt < Time.time)
                {
                    _recentInputInteractors.RemoveAt(i);
                    continue;
                }

                if (!_selectingInputInteractors.Contains(recent.interactor))
                {
                    recent.interactor.SendHapticImpulse(amplitude, duration);
                }
            }
        }

        private bool IsOtherCurlingStone(Collision collision)
        {
            var target = collision.rigidbody != null
                ? collision.rigidbody.gameObject
                : collision.collider.gameObject;

            if (target.GetComponentInParent<CurlingStoneCollisionHaptics>() != null)
            {
                return true;
            }

            return !string.IsNullOrEmpty(otherStoneNameKeyword) && target.name.Contains(otherStoneNameKeyword);
        }

        private static XRBaseInputInteractor ResolveInputInteractor(IXRSelectInteractor interactorObject)
        {
            if (interactorObject is XRBaseInputInteractor inputInteractor)
            {
                return inputInteractor;
            }

            return interactorObject is Component component
                ? component.GetComponentInParent<XRBaseInputInteractor>()
                : null;
        }

        private void AddRecentInteractor(XRBaseInputInteractor inputInteractor)
        {
            RemoveRecentInteractor(inputInteractor);
            _recentInputInteractors.Add(new RecentInteractor
            {
                interactor = inputInteractor,
                expiresAt = Time.time + afterReleaseHapticWindow,
            });
        }

        private void RemoveRecentInteractor(XRBaseInputInteractor inputInteractor)
        {
            for (var i = _recentInputInteractors.Count - 1; i >= 0; i--)
            {
                if (_recentInputInteractors[i].interactor == inputInteractor)
                {
                    _recentInputInteractors.RemoveAt(i);
                }
            }
        }

        private void RemoveExpiredRecentInteractors()
        {
            for (var i = _recentInputInteractors.Count - 1; i >= 0; i--)
            {
                var recent = _recentInputInteractors[i];
                if (recent.interactor == null || recent.expiresAt < Time.time)
                {
                    _recentInputInteractors.RemoveAt(i);
                }
            }
        }

        private void ResolveReferences()
        {
            if (grabInteractable == null)
            {
                grabInteractable = GetComponent<XRGrabInteractable>();
            }
        }
    }
}
