using ArcadeRoom.Reset;
using ArcadeRoom.Stations;
using MikeNspired.XRIStarterKit;
using UnityEngine;

namespace ArcadeRoom.Carrom
{
    [DisallowMultipleComponent]
    public class CarromStationController : ArcadeStationRoot
    {
        [SerializeField] private CarromBoardController boardController;

        [Header("Layout")]
        [SerializeField] private bool overrideStationRootLayout;
        [SerializeField] private Vector3 startAnchorLocalPosition = new Vector3(0f, 0f, -0.7f);
        [SerializeField] private Vector3 resetButtonLocalPosition = new Vector3(0.58f, 0.92f, -0.16f);
        [SerializeField] private Vector3 hintPanelLocalPosition = new Vector3(0f, 1.32f, -0.42f);
        [SerializeField] private Vector3 hintPanelLocalEulerAngles = new Vector3(0f, 180f, 0f);

        private bool _prototypeBuilt;

        public CarromBoardController BoardController => boardController;

        protected override void Awake()
        {
            base.Awake();
            ResolveBoardController(true);
        }

        protected override void OnValidate()
        {
            base.OnValidate();
            ResolveBoardController(false);
        }

        private void Start()
        {
            EnsurePrototypeBuilt();
        }

        public override void ResetState()
        {
            ResolveBoardController(true);
            EnsurePrototypeBuilt();

            if (boardController != null)
            {
                boardController.ForceReadyState();
            }

            RefreshResettables();
        }

        private void EnsurePrototypeBuilt()
        {
            if (_prototypeBuilt && boardController != null)
            {
                return;
            }

            ResolveBoardController(true);
            LayoutStationAnchors();

            if (boardController != null)
            {
                boardController.EnsureRuntimeSetup();
            }

            BuildResetButtonPrototype();
            DisableHintPanel();

            RefreshResettables();
            _prototypeBuilt = true;
        }

        private void ResolveBoardController(bool createIfMissing)
        {
            if (boardController == null)
            {
                boardController = FindChildComponent<CarromBoardController>();
            }

            if (boardController != null)
            {
                return;
            }

            if (GameplayRoot != null)
            {
                boardController = GameplayRoot.GetComponentInChildren<CarromBoardController>(true);
            }

            if (boardController != null)
            {
                return;
            }

            var boardTransform = ResolveGameplayTransform("CarromBoard");
            if (boardTransform == null || !createIfMissing)
            {
                var discoveredBoards = FindObjectsByType<CarromBoardController>(FindObjectsInactive.Include, FindObjectsSortMode.None);
                for (var i = 0; i < discoveredBoards.Length; i++)
                {
                    var discoveredBoard = discoveredBoards[i];
                    if (discoveredBoard != null && discoveredBoard.transform.IsChildOf(transform))
                    {
                        boardController = discoveredBoard;
                        return;
                    }
                }

                if (discoveredBoards.Length > 0)
                {
                    boardController = discoveredBoards[0];
                }

                return;
            }

            boardController = GetOrAddComponent<CarromBoardController>(boardTransform.gameObject);
        }

        private void LayoutStationAnchors()
        {
            if (!overrideStationRootLayout)
            {
                return;
            }

            if (StartAnchor != null)
            {
                StartAnchor.localPosition = startAnchorLocalPosition;
            }

            if (ResetButton != null)
            {
                ResetButton.localPosition = resetButtonLocalPosition;
            }

            if (HintPanel != null)
            {
                HintPanel.localPosition = hintPanelLocalPosition;
                HintPanel.localRotation = Quaternion.Euler(hintPanelLocalEulerAngles);
            }
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

            var buttonTarget = ResolveResetButtonTarget();
            if (buttonTarget == null)
            {
                return;
            }

            var trigger = GetOrAddComponent<ResetButtonTrigger>(buttonTarget);
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
                if (child == null)
                {
                    continue;
                }

                return child.gameObject;
            }

            var button = CreatePrimitive(
                "RuntimeResetButton",
                PrimitiveType.Cube,
                ResetButton,
                Vector3.zero,
                new Vector3(0.2f, 0.08f, 0.2f),
                new Color(0.85f, 0.24f, 0.18f));

            var boxCollider = button.GetComponent<BoxCollider>();
            if (boxCollider != null)
            {
                boxCollider.isTrigger = false;
            }

            return button;
        }

        private void DisableHintPanel()
        {
            if (HintPanel == null)
            {
                return;
            }

            HintPanel.gameObject.SetActive(false);
        }

        private Transform ResolveGameplayTransform(string childName)
        {
            if (GameplayRoot != null)
            {
                var foundInGameplay = GameplayRoot.Find(childName);
                if (foundInGameplay != null)
                {
                    return foundInGameplay;
                }
            }

            return transform.Find($"StationRoot/GameplayRoot/{childName}");
        }

        private static GameObject CreatePrimitive(
            string name,
            PrimitiveType primitiveType,
            Transform parent,
            Vector3 localPosition,
            Vector3 localScale,
            Color color)
        {
            var instance = GameObject.CreatePrimitive(primitiveType);
            instance.name = name;
            instance.transform.SetParent(parent, false);
            instance.transform.localPosition = localPosition;
            instance.transform.localRotation = Quaternion.identity;
            instance.transform.localScale = localScale;

            var renderer = instance.GetComponent<Renderer>();
            if (renderer != null)
            {
                renderer.material.color = color;
            }

            return instance;
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


