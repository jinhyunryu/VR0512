using System.Collections.Generic;
using ArcadeRoom.Interaction;
using ArcadeRoom.Reset;
using ArcadeRoom.Stations;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

namespace ArcadeRoom.Darts
{
    public class DartStationController : ArcadeStationRoot
    {
        [Header("Anchors")]
        [SerializeField] private Transform boardAnchor;
        [SerializeField] private Transform dartRackSpawn;
        [SerializeField] private Transform throwLine;

        [Header("Prototype Tuning")]
        [SerializeField] private int dartCount = 4;
        [SerializeField] private float dartSpacing = 0.09f;
        [SerializeField] private float autoResetBelowY = -1.5f;
        [SerializeField] private bool autoResetDroppedDarts = true;

        private readonly List<Rigidbody> _spawnedDartBodies = new();
        private bool _prototypeBuilt;

        protected override void Awake()
        {
            base.Awake();
            CacheAnchors();
        }

        protected override void OnValidate()
        {
            base.OnValidate();
            CacheAnchors();
        }

        private void Start()
        {
            EnsurePrototypeBuilt();
        }

        private void Update()
        {
            if (!autoResetDroppedDarts || !_prototypeBuilt)
            {
                return;
            }

            for (var i = 0; i < _spawnedDartBodies.Count; i++)
            {
                var body = _spawnedDartBodies[i];
                if (body != null && body.transform.position.y < autoResetBelowY)
                {
                    ResetState();
                    break;
                }
            }
        }

        public override void ResetState()
        {
            EnsurePrototypeBuilt();
            base.ResetState();
        }

        private void EnsurePrototypeBuilt()
        {
            if (_prototypeBuilt)
            {
                return;
            }

            CacheAnchors();
            LayoutAnchors();
            BuildBoardPrototype();
            BuildThrowLineMarker();
            BuildDartRackPrototype();
            BuildResetButtonPrototype();
            BuildHintPanelPrototype();
            BuildBoundsPrototype();
            RefreshResettables();
            _prototypeBuilt = true;
        }

        private void CacheAnchors()
        {
            boardAnchor = ResolveGameplayTransform(boardAnchor, "BoardAnchor");
            dartRackSpawn = ResolveGameplayTransform(dartRackSpawn, "DartRackSpawn");
            throwLine = ResolveGameplayTransform(throwLine, "ThrowLine");
        }

        private void LayoutAnchors()
        {
            if (boardAnchor != null)
            {
                boardAnchor.localPosition = new Vector3(0f, 1.45f, 1.8f);
            }

            if (dartRackSpawn != null)
            {
                dartRackSpawn.localPosition = new Vector3(-0.45f, 0.95f, -0.9f);
            }

            if (throwLine != null)
            {
                throwLine.localPosition = new Vector3(0f, 0.01f, -1.2f);
            }

            if (StartAnchor != null)
            {
                StartAnchor.localPosition = new Vector3(0f, 0f, -1.35f);
            }

            if (ResetButton != null)
            {
                ResetButton.localPosition = new Vector3(0.75f, 0.95f, -0.8f);
            }

            if (HintPanel != null)
            {
                HintPanel.localPosition = new Vector3(0f, 1.65f, -0.55f);
                HintPanel.localRotation = Quaternion.Euler(0f, 180f, 0f);
            }

            if (BoundsRoot != null)
            {
                BoundsRoot.localPosition = Vector3.zero;
            }
        }

        private void BuildBoardPrototype()
        {
            if (boardAnchor == null || boardAnchor.GetComponentInChildren<DartBoardHitReceiver>(true) != null)
            {
                return;
            }

            var boardFace = CreatePrimitive("PrototypeBoardFace", PrimitiveType.Cube, boardAnchor, new Vector3(0f, 0f, 0f), new Vector3(0.75f, 0.75f, 0.08f), new Color(0.65f, 0.18f, 0.18f));
            var receiver = boardFace.AddComponent<DartBoardHitReceiver>();
            receiver.Configure(boardFace.transform, 0.045f);

            CreatePrimitive("PrototypeBoardFrame", PrimitiveType.Cube, boardAnchor, new Vector3(0f, 0f, -0.06f), new Vector3(0.9f, 0.9f, 0.05f), new Color(0.2f, 0.12f, 0.08f));
            CreatePrimitive("PrototypeBoardStand", PrimitiveType.Cube, boardAnchor, new Vector3(0f, -0.95f, -0.08f), new Vector3(0.1f, 1.3f, 0.1f), new Color(0.2f, 0.2f, 0.22f));
            CreatePrimitive("PrototypeBoardBase", PrimitiveType.Cube, boardAnchor, new Vector3(0f, -1.62f, -0.1f), new Vector3(0.65f, 0.06f, 0.65f), new Color(0.18f, 0.18f, 0.2f));
        }

        private void BuildThrowLineMarker()
        {
            if (throwLine == null || throwLine.childCount > 0)
            {
                return;
            }

            CreatePrimitive("PrototypeThrowLine", PrimitiveType.Cube, throwLine, Vector3.zero, new Vector3(1.3f, 0.01f, 0.04f), new Color(0.95f, 0.95f, 0.95f));
        }

        private void BuildDartRackPrototype()
        {
            if (dartRackSpawn == null || dartRackSpawn.GetComponentInChildren<DartProjectile>(true) != null)
            {
                return;
            }

            CreatePrimitive("PrototypeRackBase", PrimitiveType.Cube, dartRackSpawn, new Vector3(0f, -0.04f, -0.03f), new Vector3(0.45f, 0.04f, 0.14f), new Color(0.18f, 0.18f, 0.2f));
            CreatePrimitive("PrototypeRackBack", PrimitiveType.Cube, dartRackSpawn, new Vector3(0f, 0.06f, -0.07f), new Vector3(0.45f, 0.12f, 0.02f), new Color(0.24f, 0.24f, 0.26f));

            _spawnedDartBodies.Clear();

            var startOffset = -((dartCount - 1) * dartSpacing) * 0.5f;
            for (var index = 0; index < dartCount; index++)
            {
                var dart = new GameObject($"PrototypeDart_{index + 1}");
                dart.transform.SetParent(dartRackSpawn, false);
                dart.transform.localPosition = new Vector3(startOffset + (index * dartSpacing), 0.045f, 0.02f);
                dart.transform.localRotation = Quaternion.identity;

                var collider = dart.AddComponent<CapsuleCollider>();
                collider.direction = 2;
                collider.radius = 0.014f;
                collider.height = 0.28f;
                collider.center = new Vector3(0f, 0f, 0.08f);

                var body = dart.AddComponent<Rigidbody>();
                body.mass = 0.08f;
                body.linearDamping = 0.05f;
                body.angularDamping = 0.05f;
                body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
                body.interpolation = RigidbodyInterpolation.Interpolate;

                var interactable = dart.AddComponent<SimpleGrabInteractable>();
                interactable.movementType = XRBaseInteractable.MovementType.VelocityTracking;
                interactable.throwOnDetach = true;
                interactable.useDynamicAttach = true;
                interactable.trackPosition = true;
                interactable.trackRotation = true;

                dart.AddComponent<DartProjectile>();
                dart.AddComponent<DartStickOnHit>();
                var resettable = dart.AddComponent<ResettableRigidbody>();

                CreatePrimitive("Body", PrimitiveType.Cube, dart.transform, new Vector3(0f, 0f, 0.07f), new Vector3(0.02f, 0.02f, 0.18f), new Color(0.55f, 0.55f, 0.58f), true);
                CreatePrimitive("Tip", PrimitiveType.Cube, dart.transform, new Vector3(0f, 0f, 0.17f), new Vector3(0.007f, 0.007f, 0.04f), new Color(0.75f, 0.75f, 0.78f), true);
                CreatePrimitive("Tail", PrimitiveType.Cube, dart.transform, new Vector3(0f, 0f, -0.03f), new Vector3(0.035f, 0.035f, 0.02f), new Color(0.1f, 0.35f, 0.85f), true);

                resettable.CaptureInitialState();
                _spawnedDartBodies.Add(body);
            }
        }

        private void BuildResetButtonPrototype()
        {
            if (ResetButton == null || ResetButton.GetComponentInChildren<ResetButtonTrigger>(true) != null)
            {
                return;
            }

            var button = CreatePrimitive("PrototypeResetButton", PrimitiveType.Cube, ResetButton, Vector3.zero, new Vector3(0.22f, 0.08f, 0.22f), new Color(0.85f, 0.22f, 0.18f));
            var buttonCollider = button.GetComponent<BoxCollider>();
            if (buttonCollider != null)
            {
                buttonCollider.isTrigger = true;
            }

            var trigger = button.AddComponent<ResetButtonTrigger>();
            trigger.Initialize(null, this, false);
        }

        private void BuildHintPanelPrototype()
        {
            if (HintPanel == null || HintPanel.childCount > 0)
            {
                return;
            }

            var textRoot = new GameObject("PrototypeHintText");
            textRoot.transform.SetParent(HintPanel, false);
            textRoot.transform.localPosition = Vector3.zero;
            textRoot.transform.localRotation = Quaternion.identity;

            var textMesh = textRoot.AddComponent<TextMesh>();
            textMesh.text = "Grab dart\nThrow at board\nTouch reset cube";
            textMesh.fontSize = 48;
            textMesh.characterSize = 0.04f;
            textMesh.anchor = TextAnchor.MiddleCenter;
            textMesh.alignment = TextAlignment.Center;
            textMesh.color = Color.white;
        }

        private void BuildBoundsPrototype()
        {
            if (BoundsRoot == null || BoundsRoot.childCount > 0)
            {
                return;
            }

            CreatePrimitive("PrototypeBackstop", PrimitiveType.Cube, BoundsRoot, new Vector3(0f, 1.4f, 2.3f), new Vector3(4f, 3f, 0.1f), new Color(0.1f, 0.1f, 0.12f));
            CreatePrimitive("PrototypeLeftWall", PrimitiveType.Cube, BoundsRoot, new Vector3(-2f, 1.4f, 0.8f), new Vector3(0.1f, 3f, 3.5f), new Color(0.1f, 0.1f, 0.12f));
            CreatePrimitive("PrototypeRightWall", PrimitiveType.Cube, BoundsRoot, new Vector3(2f, 1.4f, 0.8f), new Vector3(0.1f, 3f, 3.5f), new Color(0.1f, 0.1f, 0.12f));
        }

        private Transform ResolveGameplayTransform(Transform current, string childName)
        {
            if (current != null)
            {
                return current;
            }

            if (GameplayRoot != null)
            {
                var fromGameplay = GameplayRoot.Find(childName);
                if (fromGameplay != null)
                {
                    return fromGameplay;
                }
            }

            return transform.Find($"StationRoot/GameplayRoot/{childName}");
        }

        private static GameObject CreatePrimitive(string name, PrimitiveType primitiveType, Transform parent, Vector3 localPosition, Vector3 localScale, Color color, bool removeCollider = false)
        {
            var instance = GameObject.CreatePrimitive(primitiveType);
            instance.name = name;
            instance.transform.SetParent(parent, false);
            instance.transform.localPosition = localPosition;
            instance.transform.localRotation = Quaternion.identity;
            instance.transform.localScale = localScale;

            if (removeCollider)
            {
                var collider = instance.GetComponent<Collider>();
                if (collider != null)
                {
                    Object.Destroy(collider);
                }
            }

            var renderer = instance.GetComponent<Renderer>();
            if (renderer != null)
            {
                renderer.material.color = color;
            }

            return instance;
        }
    }
}
