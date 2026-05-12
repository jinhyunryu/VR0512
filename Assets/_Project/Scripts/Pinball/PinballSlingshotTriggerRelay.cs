using UnityEngine;

namespace ArcadeRoom.Pinball
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Collider))]
    public sealed class PinballSlingshotTriggerRelay : MonoBehaviour
    {
        [SerializeField] private PinballSlingshot slingshot;
        [SerializeField] private bool forceColliderAsTrigger = true;

        private Collider _triggerCollider;

        private void Awake()
        {
            ResolveReferences();
        }

        private void OnValidate()
        {
            ResolveReferences();
        }

        private void OnTriggerEnter(Collider other)
        {
            ResolveReferences();
            slingshot?.TryLaunchFromTrigger(other);
        }

        private void ResolveReferences()
        {
            if (_triggerCollider == null)
            {
                _triggerCollider = GetComponent<Collider>();
            }

            if (_triggerCollider != null && forceColliderAsTrigger)
            {
                _triggerCollider.isTrigger = true;
            }

            if (slingshot == null)
            {
                slingshot = GetComponentInParent<PinballSlingshot>();
            }
        }
    }
}
