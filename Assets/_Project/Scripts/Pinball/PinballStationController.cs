using System.Collections.Generic;
using ArcadeRoom.Reset;
using ArcadeRoom.Stations;
using MikeNspired.XRIStarterKit;
using UnityEngine;

namespace ArcadeRoom.Pinball
{
    [DisallowMultipleComponent]
    public sealed class PinballStationController : ArcadeStationRoot
    {
        [Header("Ball Spawn")]
        [SerializeField] private GameObject ballPrefab;
        [SerializeField] private Transform ballSpawnPoint;
        [SerializeField] private Transform ballSpawnRoot;
        [SerializeField] private bool spawnOnStart;
        [SerializeField] private bool hideSceneTemplateBall = true;
        [SerializeField] private string spawnedBallName = "Pinball_Ball";

        private readonly List<PinballBall> _spawnedBalls = new();
        private GameObject _resolvedBallTemplate;

        protected override void Awake()
        {
            base.Awake();
            ResolveReferences();
        }

        protected override void OnValidate()
        {
            base.OnValidate();
            ResolveReferences();
        }

        private void Start()
        {
            EnsureSetupComplete();

            if (spawnOnStart)
            {
                ResetState();
            }
        }

        public override void ResetState()
        {
            EnsureSetupComplete();
            base.ResetState();

            ClearSpawnedBalls();
            SpawnBall();
            RefreshResettables();
        }

        public void RespawnBall(PinballBall ball)
        {
            if (ball == null)
            {
                return;
            }

            EnsureSetupComplete();
            ball.ResetTo(GetSpawnPosition(), GetSpawnRotation());
        }

        public PinballBall SpawnBall()
        {
            EnsureSetupComplete();

            if (_resolvedBallTemplate == null)
            {
                return null;
            }

            var parent = ballSpawnRoot != null ? ballSpawnRoot : (SpawnRoot != null ? SpawnRoot : transform);
            var instance = Instantiate(_resolvedBallTemplate, GetSpawnPosition(), GetSpawnRotation(), parent);
            instance.name = spawnedBallName;
            instance.SetActive(true);

            var ball = GetOrAddComponent<PinballBall>(instance);
            ball.ResetTo(GetSpawnPosition(), GetSpawnRotation());
            _spawnedBalls.Add(ball);
            return ball;
        }

        private void EnsureSetupComplete()
        {
            ResolveReferences();
            BuildResetButtonPrototype();
            HideTemplateBall();
        }

        private void ResolveReferences()
        {
            ballSpawnRoot = ballSpawnRoot != null ? ballSpawnRoot : (SpawnRoot != null ? SpawnRoot : GameplayRoot);

            if (ballSpawnPoint == null)
            {
                ballSpawnPoint = ResolveNamedTransform("Ball_CreateZone")
                    ?? ResolveNamedTransform("BallCreateZone")
                    ?? ResolveNamedTransform("BallSpawnPoint")
                    ?? ResolveNamedTransform("Ball_SpawnPoint")
                    ?? ballSpawnRoot;
            }

            _resolvedBallTemplate = ResolveBallTemplate();
        }

        private GameObject ResolveBallTemplate()
        {
            if (ballPrefab != null)
            {
                return ballPrefab;
            }

            var childBall = GetComponentInChildren<PinballBall>(true);
            if (childBall != null)
            {
                return childBall.gameObject;
            }

            var namedBall = ResolveNamedTransform("Pinball_Ball") ?? ResolveNamedTransform("Ball");
            return namedBall != null ? namedBall.gameObject : null;
        }

        private Vector3 GetSpawnPosition()
        {
            return ballSpawnPoint != null ? ballSpawnPoint.position : transform.position;
        }

        private Quaternion GetSpawnRotation()
        {
            return ballSpawnPoint != null ? ballSpawnPoint.rotation : transform.rotation;
        }

        private void ClearSpawnedBalls()
        {
            for (var i = 0; i < _spawnedBalls.Count; i++)
            {
                var ball = _spawnedBalls[i];
                if (ball != null)
                {
                    Destroy(ball.gameObject);
                }
            }

            _spawnedBalls.Clear();
        }

        private void HideTemplateBall()
        {
            if (!hideSceneTemplateBall || _resolvedBallTemplate == null || !_resolvedBallTemplate.scene.IsValid())
            {
                return;
            }

            _resolvedBallTemplate.SetActive(false);
        }

        private void BuildResetButtonPrototype()
        {
            if (ResetButton == null)
            {
                return;
            }

            var existingTrigger = ResetButton.GetComponentInChildren<ResetButtonTrigger>(true);
            if (existingTrigger != null)
            {
                existingTrigger.Initialize(null, this, false, true, false);
                return;
            }

            var target = ResolveResetButtonTarget();
            if (target == null)
            {
                return;
            }

            var trigger = GetOrAddComponent<ResetButtonTrigger>(target);
            trigger.Initialize(null, this, false, true, false);
        }

        private GameObject ResolveResetButtonTarget()
        {
            if (ResetButton == null)
            {
                return null;
            }

            var pushButton = ResetButton.GetComponentInChildren<XRPushButton>(true);
            if (pushButton != null)
            {
                return pushButton.gameObject;
            }

            for (var i = 0; i < ResetButton.childCount; i++)
            {
                var child = ResetButton.GetChild(i);
                if (child != null)
                {
                    return child.gameObject;
                }
            }

            return ResetButton.gameObject;
        }

        private Transform ResolveNamedTransform(string objectName)
        {
            var transforms = GetComponentsInChildren<Transform>(true);
            for (var i = 0; i < transforms.Length; i++)
            {
                var candidate = transforms[i];
                if (candidate != null && candidate.name == objectName)
                {
                    return candidate;
                }
            }

            return null;
        }

        private static T GetOrAddComponent<T>(GameObject target) where T : Component
        {
            if (!target.TryGetComponent<T>(out var component))
            {
                component = target.AddComponent<T>();
            }

            return component;
        }
    }
}
