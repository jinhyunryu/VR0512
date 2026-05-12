using UnityEngine;
using System.Collections;
using System.Collections.Generic;

namespace ArcadeRoom.YoTyan
{
    [RequireComponent(typeof(Collider))]
    [DisallowMultipleComponent]
    public sealed class YoTyanSlotTrigger : MonoBehaviour
    {
        [SerializeField] private YoTyanStationController station;
        [SerializeField, Min(1)] private int scoreValue = 1;
        [Header("Score Validation")]
        [SerializeField] private bool requireSettledBeforeScore = true;
        [SerializeField, Min(0f)] private float requiredStableTime = 0.2f;
        [SerializeField, Min(0f)] private float maxScoreSpeed = 0.12f;
        [SerializeField] private bool useHeightCheck = true;
        [SerializeField] private Transform heightReference;
        [SerializeField, Min(0f)] private float allowedBallCenterHeightAboveReference = 0.04f;

        private readonly HashSet<YoTyanBall> _insideBalls = new();
        private readonly Dictionary<YoTyanBall, Coroutine> _pendingScores = new();
        private Collider _trigger;

        public int ScoreValue => scoreValue;

        public void Configure(YoTyanStationController ownerStation, int points)
        {
            station = ownerStation;
            scoreValue = Mathf.Max(1, points);
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
            var ball = other.attachedRigidbody != null
                ? other.attachedRigidbody.GetComponent<YoTyanBall>()
                : other.GetComponent<YoTyanBall>();

            if (ball == null || ball.IsGrabbed || station == null)
            {
                return;
            }

            _insideBalls.Add(ball);

            if (!requireSettledBeforeScore)
            {
                TryScoreValidatedBall(ball);
                return;
            }

            if (!_pendingScores.ContainsKey(ball))
            {
                _pendingScores.Add(ball, StartCoroutine(WaitForStableScore(ball)));
            }
        }

        private void OnTriggerExit(Collider other)
        {
            var ball = other.attachedRigidbody != null
                ? other.attachedRigidbody.GetComponent<YoTyanBall>()
                : other.GetComponent<YoTyanBall>();

            if (ball == null)
            {
                return;
            }

            _insideBalls.Remove(ball);
            CancelPendingScore(ball);
        }

        private void OnDisable()
        {
            foreach (var pending in _pendingScores.Values)
            {
                if (pending != null)
                {
                    StopCoroutine(pending);
                }
            }

            _insideBalls.Clear();
            _pendingScores.Clear();
        }

        private IEnumerator WaitForStableScore(YoTyanBall ball)
        {
            var stableTime = 0f;

            while (ball != null && !ball.IsScored && _insideBalls.Contains(ball))
            {
                if (ball.IsGrabbed || station == null)
                {
                    stableTime = 0f;
                    yield return null;
                    continue;
                }

                if (IsBallStableForScore(ball))
                {
                    stableTime += Time.deltaTime;
                    if (stableTime >= requiredStableTime)
                    {
                        TryScoreValidatedBall(ball);
                        break;
                    }
                }
                else
                {
                    stableTime = 0f;
                }

                yield return null;
            }

            _pendingScores.Remove(ball);
        }

        private void TryScoreValidatedBall(YoTyanBall ball)
        {
            if (ball == null || ball.IsScored || ball.IsGrabbed || station == null || !IsBallStableForScore(ball))
            {
                return;
            }

            station.TryScoreBall(ball, scoreValue);
        }

        private bool IsBallStableForScore(YoTyanBall ball)
        {
            if (ball == null)
            {
                return false;
            }

            var body = ball.GetComponent<Rigidbody>();
            if (body != null && body.linearVelocity.magnitude > maxScoreSpeed)
            {
                return false;
            }

            if (!useHeightCheck)
            {
                return true;
            }

            var referenceY = heightReference != null
                ? heightReference.position.y
                : GetTrigger().bounds.center.y;
            var ballCenterY = body != null ? body.worldCenterOfMass.y : ball.transform.position.y;
            return ballCenterY <= referenceY + allowedBallCenterHeightAboveReference;
        }

        private void CancelPendingScore(YoTyanBall ball)
        {
            if (ball == null || !_pendingScores.TryGetValue(ball, out var pending))
            {
                return;
            }

            if (pending != null)
            {
                StopCoroutine(pending);
            }

            _pendingScores.Remove(ball);
        }

        private void EnsureTrigger()
        {
            _trigger = GetComponent<Collider>();
            if (_trigger != null)
            {
                _trigger.isTrigger = true;
            }
        }

        private Collider GetTrigger()
        {
            if (_trigger == null)
            {
                _trigger = GetComponent<Collider>();
            }

            return _trigger;
        }
    }
}
