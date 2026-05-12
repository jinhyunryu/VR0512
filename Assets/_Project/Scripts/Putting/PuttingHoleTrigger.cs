using UnityEngine;

namespace ArcadeRoom.Putting
{
    [RequireComponent(typeof(Collider))]
    [DisallowMultipleComponent]
    public sealed class PuttingHoleTrigger : MonoBehaviour
    {
        [SerializeField] private PuttingStationController station;
        [SerializeField] private string golfBallNamePrefix = "GolfBall";

        public void Configure(PuttingStationController ownerStation, string ballNamePrefix)
        {
            station = ownerStation;
            golfBallNamePrefix = string.IsNullOrWhiteSpace(ballNamePrefix) ? "GolfBall" : ballNamePrefix;
            EnsureTrigger();
        }

        private void Reset()
        {
            EnsureTrigger();
        }

        private void OnValidate()
        {
            EnsureTrigger();
        }

        private void OnTriggerEnter(Collider other)
        {
            var ball = ResolveGolfBallRoot(other);
            if (ball == null)
            {
                return;
            }

            if (station == null)
            {
                station = ResolveStation();
            }

            if (station == null)
            {
                return;
            }

            station.NotifyBallHoled(ball);
        }

        private GameObject ResolveGolfBallRoot(Collider other)
        {
            if (other == null)
            {
                return null;
            }

            var candidate = other.attachedRigidbody != null
                ? other.attachedRigidbody.gameObject
                : other.gameObject;

            return candidate != null && candidate.name.StartsWith(golfBallNamePrefix)
                ? candidate
                : null;
        }

        private PuttingStationController ResolveStation()
        {
            var stations = FindObjectsByType<PuttingStationController>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            return stations.Length > 0 ? stations[0] : null;
        }

        private void EnsureTrigger()
        {
            var trigger = GetComponent<Collider>();
            if (trigger != null)
            {
                trigger.isTrigger = true;
            }
        }
    }
}
