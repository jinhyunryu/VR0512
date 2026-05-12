using System;
using System.Collections;
using ArcadeRoom.Audio;
using ArcadeRoom.Core;
using UnityEngine;
using UnityEngine.Events;

namespace ArcadeRoom.Pinball
{
    [DisallowMultipleComponent]
    public sealed class PinballDropTarget : MonoBehaviour, IArcadeResettable
    {
        private const string DefaultDropTargetClipPath = "Assets/_Project/Audio/SFX/Pinball/Pinball_DropTarget_Hit.wav";

        [SerializeField] private Transform targetRoot;
        [SerializeField] private Collider[] targetColliders;
        [SerializeField, Min(0f)] private float minHitSpeed = 0.35f;
        [SerializeField] private bool oneShot = true;
        [SerializeField] private bool disableCollidersWhenDropped = true;
        [SerializeField] private Vector3 droppedLocalOffset = new(0f, -0.06f, 0f);
        [SerializeField] private Vector3 droppedLocalEulerOffset = Vector3.zero;
        [SerializeField, Min(0f)] private float dropDuration = 0.08f;
        [SerializeField] private UnityEvent onDropped = new UnityEvent();
        [SerializeField] private PinballBallUnityEvent onHitByBall = new PinballBallUnityEvent();
        [Header("SFX")]
        [SerializeField] private AudioClip dropTargetClip;
        [SerializeField, Range(0f, 1f)] private float dropTargetVolume = 0.75f;
        [SerializeField] private Vector2 pitchRange = new(0.95f, 1.04f);
        [SerializeField, Range(0f, 1f)] private float spatialBlend = 1f;
        [SerializeField, Min(0.1f)] private float maxDistance = 7f;

        private Vector3 _initialLocalPosition;
        private Quaternion _initialLocalRotation;
        private bool[] _initialColliderStates;
        private bool _dropped;
        private Coroutine _dropRoutine;

        public event Action<PinballBall> BallHit;

        private void Awake()
        {
            ResolveReferences();
            ResolveDefaultSfx();
            CaptureInitialState();
        }

        private void OnValidate()
        {
            ResolveReferences();
            pitchRange = ArcadeSfxPlayer.NormalizePitchRange(pitchRange);
            maxDistance = Mathf.Max(0.1f, maxDistance);
            ResolveDefaultSfx();
        }

        private void OnCollisionEnter(Collision collision)
        {
            var ball = collision.rigidbody != null
                ? collision.rigidbody.GetComponent<PinballBall>()
                : collision.collider.GetComponentInParent<PinballBall>();

            if (ball == null || collision.relativeVelocity.magnitude < minHitSpeed)
            {
                return;
            }

            Drop(ball);
        }

        private void OnTriggerEnter(Collider other)
        {
            var ball = other.GetComponentInParent<PinballBall>();
            if (ball == null || ball.Body == null || ball.Body.linearVelocity.magnitude < minHitSpeed)
            {
                return;
            }

            Drop(ball);
        }

        public void Drop(PinballBall ball)
        {
            if (ball == null || oneShot && _dropped)
            {
                return;
            }

            _dropped = true;
            SetTargetCollidersEnabled(!disableCollidersWhenDropped);

            if (_dropRoutine != null)
            {
                StopCoroutine(_dropRoutine);
            }

            _dropRoutine = StartCoroutine(AnimateDrop());
            ArcadeSfxPlayer.PlayOneShot(
                dropTargetClip,
                ball.transform.position,
                dropTargetVolume,
                ArcadeSfxPlayer.RandomPitch(pitchRange),
                spatialBlend,
                maxDistance,
                "Pinball_SFX_DropTarget");
            onDropped?.Invoke();
            onHitByBall?.Invoke(ball);
            BallHit?.Invoke(ball);
        }

        public void ResetState()
        {
            ResolveReferences();

            if (_dropRoutine != null)
            {
                StopCoroutine(_dropRoutine);
                _dropRoutine = null;
            }

            _dropped = false;
            if (targetRoot != null && !targetRoot.gameObject.activeSelf)
            {
                targetRoot.gameObject.SetActive(true);
            }

            targetRoot.localPosition = _initialLocalPosition;
            targetRoot.localRotation = _initialLocalRotation;
            RestoreColliderStates();
        }

        private IEnumerator AnimateDrop()
        {
            var startPosition = targetRoot.localPosition;
            var startRotation = targetRoot.localRotation;
            var endPosition = _initialLocalPosition + droppedLocalOffset;
            var endRotation = _initialLocalRotation * Quaternion.Euler(droppedLocalEulerOffset);

            if (dropDuration <= 0f)
            {
                targetRoot.localPosition = endPosition;
                targetRoot.localRotation = endRotation;
                _dropRoutine = null;
                yield break;
            }

            var elapsed = 0f;
            while (elapsed < dropDuration)
            {
                elapsed += Time.deltaTime;
                var t = Mathf.Clamp01(elapsed / dropDuration);
                targetRoot.localPosition = Vector3.Lerp(startPosition, endPosition, t);
                targetRoot.localRotation = Quaternion.Slerp(startRotation, endRotation, t);
                yield return null;
            }

            targetRoot.localPosition = endPosition;
            targetRoot.localRotation = endRotation;
            _dropRoutine = null;
        }

        private void CaptureInitialState()
        {
            if (targetRoot == null)
            {
                return;
            }

            _initialLocalPosition = targetRoot.localPosition;
            _initialLocalRotation = targetRoot.localRotation;
            _initialColliderStates = new bool[targetColliders.Length];

            for (var i = 0; i < targetColliders.Length; i++)
            {
                _initialColliderStates[i] = targetColliders[i] != null && targetColliders[i].enabled;
            }
        }

        private void ResolveReferences()
        {
            if (targetRoot == null)
            {
                targetRoot = transform;
            }

            if (targetColliders == null || targetColliders.Length == 0)
            {
                targetColliders = GetComponentsInChildren<Collider>(true);
            }
        }

        private void SetTargetCollidersEnabled(bool enabled)
        {
            for (var i = 0; i < targetColliders.Length; i++)
            {
                if (targetColliders[i] != null)
                {
                    targetColliders[i].enabled = enabled;
                }
            }
        }

        private void RestoreColliderStates()
        {
            for (var i = 0; i < targetColliders.Length; i++)
            {
                if (targetColliders[i] == null)
                {
                    continue;
                }

                var initialStateKnown = _initialColliderStates != null && i < _initialColliderStates.Length;
                targetColliders[i].enabled = !initialStateKnown || _initialColliderStates[i];
            }
        }

        private void ResolveDefaultSfx()
        {
            if (dropTargetClip == null)
            {
                dropTargetClip = ArcadeSfxPlayer.LoadEditorClip(DefaultDropTargetClipPath);
            }
        }
    }
}
