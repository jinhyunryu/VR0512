using System.Collections.Generic;
using MikeNspired.XRIStarterKit;
using UnityEngine;

namespace ArcadeRoom.Pinball
{
    [DisallowMultipleComponent]
    public sealed class PinballBallStartSpawner : MonoBehaviour
    {
        [Header("Ball Spawn")]
        [SerializeField] private GameObject ballPrefab;
        [SerializeField] private Transform ballCreateZone;
        [SerializeField] private Transform ballSpawnRoot;
        [SerializeField] private string spawnedBallName = "Pinball_Ball";
        [SerializeField] private bool spawnOnAwake;
        [SerializeField] private bool clearExistingBallsOnPress = true;
        [SerializeField] private bool hideSceneTemplateBall = true;

        [Header("Start Button")]
        [SerializeField] private Transform startButtonRoot;
        [SerializeField] private XRPushButton startButton;

        private readonly List<PinballBall> _spawnedBalls = new();
        private GameObject _resolvedBallTemplate;

        private void Awake()
        {
            ResolveReferences();
            HideTemplateBall();
        }

        private void Start()
        {
            if (spawnOnAwake)
            {
                SpawnFromButton();
            }
        }

        private void OnEnable()
        {
            ResolveReferences();
            RegisterButton();
        }

        private void OnDisable()
        {
            UnregisterButton();
        }

        private void OnValidate()
        {
            ResolveReferences();
        }

        public void SpawnFromButton()
        {
            ResolveReferences();
            HideTemplateBall();

            if (clearExistingBallsOnPress)
            {
                ClearSpawnedBalls();
            }

            SpawnBall();
        }

        public PinballBall SpawnBall()
        {
            ResolveReferences();

            if (_resolvedBallTemplate == null || ballCreateZone == null)
            {
                return null;
            }

            var parent = ballSpawnRoot != null ? ballSpawnRoot : transform;
            var instance = Instantiate(_resolvedBallTemplate, ballCreateZone.position, ballCreateZone.rotation, parent);
            instance.name = spawnedBallName;
            instance.SetActive(true);

            var ball = GetOrAddComponent<PinballBall>(instance);
            ball.ResetTo(ballCreateZone.position, ballCreateZone.rotation);
            _spawnedBalls.Add(ball);
            return ball;
        }

        private void ResolveReferences()
        {
            ballSpawnRoot = ballSpawnRoot != null ? ballSpawnRoot : ResolveNamedTransform("SpawnRoot");
            ballCreateZone = ballCreateZone != null
                ? ballCreateZone
                : ResolveNamedTransform("Ball_CreateZone")
                    ?? ResolveNamedTransform("BallCreateZone")
                    ?? ResolveNamedTransform("BallSpawnPoint")
                    ?? ResolveNamedTransform("Ball_SpawnPoint");

            startButtonRoot = startButtonRoot != null ? startButtonRoot : ResolveNamedTransform("StartButton");
            if (startButton == null && startButtonRoot != null)
            {
                startButton = startButtonRoot.GetComponentInChildren<XRPushButton>(true);
            }

            _resolvedBallTemplate = ResolveBallTemplate();
        }

        private GameObject ResolveBallTemplate()
        {
            if (ballPrefab != null)
            {
                return ballPrefab;
            }

            var balls = GetComponentsInChildren<PinballBall>(true);
            for (var i = 0; i < balls.Length; i++)
            {
                var candidate = balls[i];
                if (candidate != null && !_spawnedBalls.Contains(candidate))
                {
                    return candidate.gameObject;
                }
            }

            var namedBall = ResolveNamedTransform("Pinball_Ball") ?? ResolveNamedTransform("Ball");
            return namedBall != null ? namedBall.gameObject : null;
        }

        private void HideTemplateBall()
        {
            if (!hideSceneTemplateBall || _resolvedBallTemplate == null || !_resolvedBallTemplate.scene.IsValid())
            {
                return;
            }

            _resolvedBallTemplate.SetActive(false);
        }

        private void ClearSpawnedBalls()
        {
            for (var i = _spawnedBalls.Count - 1; i >= 0; i--)
            {
                var ball = _spawnedBalls[i];
                if (ball != null)
                {
                    Destroy(ball.gameObject);
                }
            }

            _spawnedBalls.Clear();
        }

        private void RegisterButton()
        {
            if (startButton == null)
            {
                return;
            }

            startButton.OnPress.RemoveListener(SpawnFromButton);
            startButton.OnPress.AddListener(SpawnFromButton);
        }

        private void UnregisterButton()
        {
            if (startButton == null)
            {
                return;
            }

            startButton.OnPress.RemoveListener(SpawnFromButton);
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

            if (!gameObject.scene.IsValid() || !gameObject.scene.isLoaded)
            {
                return null;
            }

            var roots = gameObject.scene.GetRootGameObjects();
            for (var i = 0; i < roots.Length; i++)
            {
                var root = roots[i];
                if (root == null)
                {
                    continue;
                }

                transforms = root.GetComponentsInChildren<Transform>(true);
                for (var j = 0; j < transforms.Length; j++)
                {
                    var candidate = transforms[j];
                    if (candidate != null && candidate.name == objectName)
                    {
                        return candidate;
                    }
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
