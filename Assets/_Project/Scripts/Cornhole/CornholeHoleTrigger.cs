using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

namespace ArcadeRoom.Cornhole
{
    [DisallowMultipleComponent]
    public class CornholeHoleTrigger : MonoBehaviour
    {
        [SerializeField] private Transform settlePoint;
        [SerializeField] private bool snapToSettlePoint = true;
        [SerializeField] private bool zeroVelocityOnEnter = true;
        [SerializeField] private bool disableGrabWhileInside = false;

        private void OnTriggerEnter(Collider other)
        {
            UpdateHoleState(other, true);
        }

        private void OnTriggerExit(Collider other)
        {
            UpdateHoleState(other, false);
        }

        private void UpdateHoleState(Collider other, bool inside)
        {
            if (other == null)
            {
                return;
            }

            var beanbag = other.GetComponentInParent<CornholeBeanbagVisualController>();
            if (beanbag == null)
            {
                return;
            }

            beanbag.SetInHoleState(inside);
            if (inside)
            {
                beanbag.SetRimHangState(false);
            }

            if (!beanbag.TryGetComponent<Rigidbody>(out var body))
            {
                return;
            }

            if (inside)
            {
                if (zeroVelocityOnEnter)
                {
                    body.linearVelocity = Vector3.zero;
                    body.angularVelocity = Vector3.zero;
                }

                if (snapToSettlePoint && settlePoint != null)
                {
                    body.position = settlePoint.position;
                    body.rotation = settlePoint.rotation;
                }
            }

            if (!beanbag.TryGetComponent<XRGrabInteractable>(out var grabInteractable))
            {
                return;
            }

            if (disableGrabWhileInside)
            {
                grabInteractable.enabled = !inside;
            }
        }
    }
}
