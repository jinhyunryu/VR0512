using System.Collections;
using UnityEngine;

namespace ArcadeRoom.Pinball
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Collider))]
    public sealed class PinballDrainTrigger : MonoBehaviour
    {
        [SerializeField] private PinballStationController station;
        [SerializeField, Min(0f)] private float respawnDelay = 0.4f;

        private void Awake()
        {
            ResolveReferences();
            EnsureTriggerCollider();
        }

        private void OnValidate()
        {
            ResolveReferences();
            EnsureTriggerCollider();
        }

        private void OnTriggerEnter(Collider other)
        {
            var ball = other.GetComponentInParent<PinballBall>();
            if (ball == null)
            {
                return;
            }

            if (respawnDelay <= 0f)
            {
                Respawn(ball);
                return;
            }

            StartCoroutine(RespawnAfterDelay(ball));
        }

        private IEnumerator RespawnAfterDelay(PinballBall ball)
        {
            yield return new WaitForSeconds(respawnDelay);
            Respawn(ball);
        }

        private void Respawn(PinballBall ball)
        {
            ResolveReferences();
            station?.RespawnBall(ball);
        }

        private void ResolveReferences()
        {
            if (station == null)
            {
                station = GetComponentInParent<PinballStationController>();
            }

            if (station == null)
            {
                station = FindFirstObjectByType<PinballStationController>();
            }
        }

        private void EnsureTriggerCollider()
        {
            var triggerCollider = GetComponent<Collider>();
            if (triggerCollider != null)
            {
                triggerCollider.isTrigger = true;
            }
        }
    }
}
