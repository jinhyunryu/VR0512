using UnityEngine;
using UnityEngine.Events;

namespace ArcadeRoom.Pinball
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Collider))]
    public sealed class PinballGameOverZone : MonoBehaviour
    {
        [SerializeField] private bool forceTrigger = true;
        [SerializeField] private bool stopBallOnGameOver = true;
        [SerializeField] private bool disableBallOnGameOver;
        [SerializeField] private bool stopScoreTimerOnGameOver = true;
        [SerializeField] private PinballScoreController scoreController;
        [SerializeField] private UnityEvent onGameOver = new UnityEvent();
        [SerializeField] private PinballBallUnityEvent onBallEntered = new PinballBallUnityEvent();

        private bool _gameOver;

        private void Awake()
        {
            ApplyColliderSettings();
            ResolveReferences();
        }

        private void OnValidate()
        {
            ApplyColliderSettings();
            ResolveReferences();
        }

        private void OnTriggerEnter(Collider other)
        {
            if (_gameOver)
            {
                return;
            }

            var ball = other.GetComponentInParent<PinballBall>();
            if (ball == null)
            {
                return;
            }

            _gameOver = true;

            if (stopBallOnGameOver && ball.Body != null)
            {
                ball.Body.linearVelocity = Vector3.zero;
                ball.Body.angularVelocity = Vector3.zero;
                ball.Body.Sleep();
            }

            if (disableBallOnGameOver)
            {
                ball.gameObject.SetActive(false);
            }

            if (stopScoreTimerOnGameOver)
            {
                ResolveReferences();
                scoreController?.EndGame();
            }

            onGameOver?.Invoke();
            onBallEntered?.Invoke(ball);
        }

        public void ResetZone()
        {
            _gameOver = false;
        }

        private void ApplyColliderSettings()
        {
            if (!forceTrigger)
            {
                return;
            }

            var targetCollider = GetComponent<Collider>();
            if (targetCollider != null)
            {
                targetCollider.isTrigger = true;
            }
        }

        private void ResolveReferences()
        {
            if (scoreController != null)
            {
                return;
            }

            scoreController = GetComponentInParent<PinballScoreController>(true);
            if (scoreController != null || !gameObject.scene.IsValid() || !gameObject.scene.isLoaded)
            {
                return;
            }

            var roots = gameObject.scene.GetRootGameObjects();
            for (var i = 0; i < roots.Length; i++)
            {
                scoreController = roots[i].GetComponentInChildren<PinballScoreController>(true);
                if (scoreController != null)
                {
                    return;
                }
            }
        }
    }
}
