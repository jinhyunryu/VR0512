using UnityEngine;

namespace ArcadeRoom.Cornhole
{
    [DisallowMultipleComponent]
    public class CornholeRimTrigger : MonoBehaviour
    {
        private void OnTriggerEnter(Collider other)
        {
            SetRimState(other, true);
        }

        private void OnTriggerStay(Collider other)
        {
            SetRimState(other, true);
        }

        private void OnTriggerExit(Collider other)
        {
            SetRimState(other, false);
        }

        private static void SetRimState(Collider other, bool active)
        {
            if (other == null)
            {
                return;
            }

            var beanbag = other.GetComponentInParent<CornholeBeanbagVisualController>();
            if (beanbag == null || beanbag.IsInHole)
            {
                return;
            }

            beanbag.SetRimHangState(active);
        }
    }
}
