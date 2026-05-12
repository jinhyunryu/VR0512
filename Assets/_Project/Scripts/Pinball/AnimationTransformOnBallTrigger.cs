using System.Collections;
using UnityEngine;

namespace MikeNspired.XRIStarterKit
{
    /// <summary>
    /// Moves a switch visual between a start transform and pressed transform when a pinball enters a trigger.
    /// Attach this to a trigger collider object, or to a parent object that has a trigger collider.
    /// The MovingObject and EndPosition should share the same parent for predictable localPosition/localRotation lerping.
    /// </summary>
    public class AnimationTransformOnBallTrigger : MonoBehaviour
    {
        [Header("Objects")]
        [Tooltip("The switch plate/ring/visual that should move when the ball passes over the trigger.")]
        [SerializeField] private Transform movingObject;

        [Tooltip("An empty transform representing the fully pressed position/rotation.")]
        [SerializeField] private Transform endPosition;

        [Header("Ball Filter")]
        [Tooltip("Only objects on these layers can press the switch. Set this to PinballBall.")]
        [SerializeField] private LayerMask ballLayerMask;

        [Tooltip("Ignore trigger events shortly after the scene starts, useful if the ball overlaps a switch at Play.")]
        [SerializeField] private float startupIgnoreTime = 0.15f;

        [Header("Press Feel")]
        [Tooltip("How fast the switch moves down into the pressed position.")]
        [SerializeField] private float pressSpeed = 18f;

        [Tooltip("How fast the switch returns to its start position.")]
        [SerializeField] private float releaseSpeed = 10f;

        [Tooltip("Minimum time the switch remains pressed even if the ball passes very quickly.")]
        [SerializeField] private float minimumPressTime = 0.08f;

        [Tooltip("Extra delay before the switch starts returning after the ball leaves.")]
        [SerializeField] private float releaseDelay = 0.02f;

        [Header("Animation")]
        [SerializeField] private bool animatePosition = true;
        [SerializeField] private bool animateRotation = true;

        private Vector3 startLocalPosition;
        private Quaternion startLocalRotation;
        private float currentValue;
        private float targetValue;
        private int overlappingBallCount;
        private float enableTime;
        private Coroutine releaseCoroutine;

        private void Awake()
        {
            if (movingObject == null || endPosition == null)
            {
                Debug.LogWarning($"{nameof(AnimationTransformOnBallTrigger)} on {name} is missing references.", this);
                enabled = false;
                return;
            }

            startLocalPosition = movingObject.localPosition;
            startLocalRotation = movingObject.localRotation;
        }

        private void OnEnable()
        {
            enableTime = Time.time;
            currentValue = 0f;
            targetValue = 0f;
            overlappingBallCount = 0;
            ApplyValue(0f);
        }

        private void Update()
        {
            float speed = targetValue > currentValue ? pressSpeed : releaseSpeed;
            currentValue = Mathf.MoveTowards(currentValue, targetValue, speed * Time.deltaTime);
            ApplyValue(currentValue);
        }

        private void OnTriggerEnter(Collider other)
        {
            if (ShouldIgnoreStartupEvent() || !IsAllowedBall(other))
                return;

            overlappingBallCount++;

            if (releaseCoroutine != null)
            {
                StopCoroutine(releaseCoroutine);
                releaseCoroutine = null;
            }

            targetValue = 1f;
        }

        private void OnTriggerExit(Collider other)
        {
            if (!IsAllowedBall(other))
                return;

            overlappingBallCount = Mathf.Max(0, overlappingBallCount - 1);

            if (overlappingBallCount == 0)
            {
                if (releaseCoroutine != null)
                    StopCoroutine(releaseCoroutine);

                releaseCoroutine = StartCoroutine(ReleaseAfterDelay());
            }
        }

        private IEnumerator ReleaseAfterDelay()
        {
            float waitTime = Mathf.Max(minimumPressTime, releaseDelay);
            if (waitTime > 0f)
                yield return new WaitForSeconds(waitTime);

            if (overlappingBallCount == 0)
                targetValue = 0f;

            releaseCoroutine = null;
        }

        private bool ShouldIgnoreStartupEvent()
        {
            return Time.time - enableTime < startupIgnoreTime;
        }

        private bool IsAllowedBall(Collider other)
        {
            if (other == null)
                return false;

            if (IsInLayerMask(other.gameObject.layer))
                return true;

            Rigidbody attachedRigidbody = other.attachedRigidbody;
            return attachedRigidbody != null && IsInLayerMask(attachedRigidbody.gameObject.layer);
        }

        private bool IsInLayerMask(int layer)
        {
            return (ballLayerMask.value & (1 << layer)) != 0;
        }

        private void ApplyValue(float value)
        {
            if (animatePosition)
                movingObject.localPosition = Vector3.Lerp(startLocalPosition, endPosition.localPosition, value);

            if (animateRotation)
                movingObject.localRotation = Quaternion.Lerp(startLocalRotation, endPosition.localRotation, value);
        }
    }
}
