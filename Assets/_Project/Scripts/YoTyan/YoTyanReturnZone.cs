using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace ArcadeRoom.YoTyan
{
    [RequireComponent(typeof(Collider))]
    [DisallowMultipleComponent]
    public sealed class YoTyanReturnZone : MonoBehaviour
    {
        [SerializeField] private float returnDelay = 0.2f;
        [SerializeField] private YoTyanStationController station;

        private readonly HashSet<int> _pendingReturns = new();

        public void Configure(YoTyanStationController ownerStation)
        {
            station = ownerStation;
            EnsureTrigger();
        }

        private void Reset()
        {
            EnsureTrigger();
        }

        private void OnValidate()
        {
            EnsureTrigger();
            station = station != null ? station : FindFirstObjectByType<YoTyanStationController>(FindObjectsInactive.Include);
        }

        private void OnTriggerEnter(Collider other)
        {
            ScheduleReturnBall(other.attachedRigidbody != null
                ? other.attachedRigidbody.GetComponent<YoTyanBall>()
                : other.GetComponent<YoTyanBall>());
        }

        private void ScheduleReturnBall(YoTyanBall ball)
        {
            if (ball == null || ball.IsScored || ball.IsReturnBlocked || !ball.CanLocalReturnZoneControl)
            {
                return;
            }

            var key = ball.GetInstanceID();
            if (_pendingReturns.Contains(key))
            {
                return;
            }

            _pendingReturns.Add(key);

            StartCoroutine(ReturnBallAfterDelay(ball, key));
        }

        private void EnsureTrigger()
        {
            var zoneCollider = GetComponent<Collider>();
            if (zoneCollider != null)
            {
                zoneCollider.isTrigger = true;
            }
        }

        private IEnumerator ReturnBallAfterDelay(YoTyanBall ball, int key)
        {
            yield return new WaitForSeconds(returnDelay);

            if (ball != null && !ball.IsScored && !ball.IsReturnBlocked && ball.CanLocalReturnZoneControl)
            {
                if (station == null)
                {
                    station = FindFirstObjectByType<YoTyanStationController>(FindObjectsInactive.Include);
                }

                if (station != null && station.TryReturnBallToHome(ball))
                {
                    _pendingReturns.Remove(key);
                    yield break;
                }

                ball.ResetToHomeSpawn();
            }

            _pendingReturns.Remove(key);
        }
    }
}
