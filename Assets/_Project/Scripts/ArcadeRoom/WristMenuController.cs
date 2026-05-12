using System.Collections.Generic;
using ArcadeRoom.Audio;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;
using UnityEngine.XR.Interaction.Toolkit.Interactors;
using UnityEngine.XR.Interaction.Toolkit.UI;

namespace ArcadeRoom.Arcade
{
    [ExecuteAlways]
    [DisallowMultipleComponent]
    public sealed class WristMenuController : MonoBehaviour
    {
        private const string GeneratedCanvasName = "GeneratedWristMenuCanvas";
        private const string DefaultUiClickClipPath = "Assets/_Project/Audio/SFX/UI_Click.wav";

        public enum ArcadePlayMode
        {
            Sit = 0,
            Stand = 1
        }

        [System.Serializable]
        public sealed class PlayModeChangedEvent : UnityEvent<ArcadePlayMode> { }

        [System.Serializable]
        private struct HeightRootState
        {
            public Transform root;
            public Vector3 baseLocalPosition;
            public Vector3 baseWorldPosition;

            public HeightRootState(Transform root)
            {
                this.root = root;
                baseLocalPosition = root != null ? root.localPosition : Vector3.zero;
                baseWorldPosition = root != null ? root.position : Vector3.zero;
            }
        }

        [Header("References")]
        [SerializeField] private Transform wristPanelRoot;
        [SerializeField] private ArcadeSettingsController settingsController;
        [SerializeField] private ArcadeTeleportController teleportController;

        [Header("Canvas")]
        [SerializeField] private Vector3 canvasLocalPosition = new(0.07f, -0.03f, 0.06f);
        [SerializeField] private Vector3 canvasLocalEulerAngles = new(65f, 0f, 0f);
        [SerializeField] private float canvasScale = 0.0018f;
        [SerializeField] private bool showPreviewInEditor = true;
        [SerializeField] private bool previewPanelOpenInEditor = true;
        [SerializeField] private bool preserveManualCanvasLayout = true;
        [SerializeField] private Vector2 defaultToggleButtonPosition = new(0f, 80f);
        [SerializeField] private Vector2 defaultToggleButtonSize = new(130f, 54f);
        [SerializeField] private Sprite menuToggleIcon;
        [SerializeField] private bool hideMenuToggleTextWhenIconAssigned = true;

        [Header("SFX")]
        [SerializeField] private AudioClip uiClickClip;
        [SerializeField, Range(0f, 1f)] private float uiClickVolume = 0.65f;
        [SerializeField] private Vector2 uiClickPitchRange = new(0.98f, 1.04f);

        [Header("Visibility")]
        [SerializeField] private bool hideWhileGrabbing = true;
        [SerializeField] private bool restorePanelAfterGrab = true;
        [SerializeField] private List<XRBaseInteractor> grabInteractors = new();

        [Header("Play Mode")]
        [Tooltip("Initial play-height mode when the scene starts.")]
        [SerializeField] private ArcadePlayMode startMode = ArcadePlayMode.Sit;
        [SerializeField] private bool applyStartModeOnAwake = true;

        [Tooltip("Y offset from the captured base position when Sit mode is active.")]
        [SerializeField] private float sitYOffset = 0f;

        [Tooltip("Y offset from the captured base position when Stand mode is active.")]
        [SerializeField] private float standYOffset = 0.5f;

        [Tooltip("Use localPosition for roots. Recommended when Stations are under a common scene parent.")]
        [SerializeField] private bool useLocalPositionForHeightRoots = true;

        [Header("Play Mode Roots")]
        [Tooltip("Assign the root that should move up/down for Sit/Stand mode. Usually this is Stations or ArcadeContentRoot, not XR_Rig.")]
        [SerializeField] private List<Transform> heightAdjustedRoots = new();

        [Tooltip("If Roots To Move is empty, the script will try these names in order.")]
        [SerializeField] private List<string> autoFindHeightRootNames = new() { "ArcadeContentRoot", "Stations" };

        [SerializeField] private bool autoFindHeightRootsIfEmpty = true;

        [Header("Play Mode Physics Safety")]
        [SerializeField] private bool zeroRigidbodyVelocityOnModeChange = true;
        [SerializeField] private bool sleepRigidbodiesAfterMove = true;
        [SerializeField] private bool includeInactiveRigidbodies = true;

        [Header("Play Mode Save")]
        [SerializeField] private bool saveModeToPlayerPrefs = false;
        [SerializeField] private string playerPrefsKey = "ArcadeRoom_PlayMode";

        [Header("Play Mode Events")]
        [SerializeField] private UnityEvent onSitMode = new UnityEvent();
        [SerializeField] private UnityEvent onStandMode = new UnityEvent();
        [SerializeField] private PlayModeChangedEvent onModeChanged = new PlayModeChangedEvent();

        private readonly List<HeightRootState> _heightRootStates = new();
        private ArcadePlayMode _currentPlayMode;
        private bool _hasCapturedHeightRoots;
        private bool _playModeInitialized;

        private GameObject _canvasObject;
        private GameObject _toggleRoot;
        private GameObject _panelRoot;
        private GameObject _playModePage;
        private GameObject _systemPage;
        private GameObject _teleportPage;
        private bool _isPanelOpen;
        private bool _wasPanelOpenBeforeGrab;
        private bool _wasGrabbing;

        public ArcadePlayMode CurrentPlayMode => _currentPlayMode;
        public float CurrentPlayModeYOffset => GetPlayModeYOffset(_currentPlayMode);

        private void Awake()
        {
            if (Application.isPlaying)
            {
                InitializePlayModeHeight();
            }
        }

        private void OnEnable()
        {
            ResolveReferences();
            ResolveDefaultSfx();

            if (Application.isPlaying)
            {
                InitializePlayModeHeight();
            }

            if (!Application.isPlaying && !showPreviewInEditor)
            {
                DestroyGeneratedCanvas();
                return;
            }

            BuildMenu();
            SetPanelOpen(Application.isPlaying ? false : previewPanelOpenInEditor);
        }

        private void Update()
        {
            if (!Application.isPlaying)
            {
                ApplyCanvasTransform();
                return;
            }

            var isGrabbing = hideWhileGrabbing && IsAnyInteractorSelecting();
            if (isGrabbing == _wasGrabbing)
            {
                return;
            }

            _wasGrabbing = isGrabbing;

            if (isGrabbing)
            {
                _wasPanelOpenBeforeGrab = _isPanelOpen;
                SetCanvasVisible(false);
                return;
            }

            SetCanvasVisible(true);
            SetPanelOpen(restorePanelAfterGrab && _wasPanelOpenBeforeGrab);
        }

        public void TogglePanel()
        {
            SetPanelOpen(!_isPanelOpen);
        }

        public void ClosePanel()
        {
            SetPanelOpen(false);
        }

        public void ShowPlayModePage()
        {
            SetPageActive(_playModePage, true);
            SetPageActive(_systemPage, false);
            SetPageActive(_teleportPage, false);
        }

        public void ShowSystemPage()
        {
            SetPageActive(_playModePage, false);
            SetPageActive(_systemPage, true);
            SetPageActive(_teleportPage, false);
        }

        public void ShowTeleportPage()
        {
            SetPageActive(_playModePage, false);
            SetPageActive(_systemPage, false);
            SetPageActive(_teleportPage, true);
        }

        public void SetSitMode()
        {
            SetPlayMode(ArcadePlayMode.Sit);
        }

        public void SetStandMode()
        {
            SetPlayMode(ArcadePlayMode.Stand);
        }

        public void TogglePlayMode()
        {
            SetPlayMode(_currentPlayMode == ArcadePlayMode.Sit ? ArcadePlayMode.Stand : ArcadePlayMode.Sit);
        }

        public void SetPlayModeByIndex(int modeIndex)
        {
            SetPlayMode(modeIndex == 1 ? ArcadePlayMode.Stand : ArcadePlayMode.Sit);
        }

        public void SetPlayMode(ArcadePlayMode mode)
        {
            if (!_hasCapturedHeightRoots)
            {
                ResolveHeightRoots();
                CaptureHeightRootBasePositions();
            }

            if (_currentPlayMode == mode)
            {
                ApplyPlayMode(mode, invokeEvents: false);
                return;
            }

            _currentPlayMode = mode;
            ApplyPlayMode(mode, invokeEvents: true);

            if (saveModeToPlayerPrefs)
            {
                PlayerPrefs.SetInt(playerPrefsKey, (int)_currentPlayMode);
                PlayerPrefs.Save();
            }
        }

        public void CaptureCurrentHeightPositionsAsBase()
        {
            ResolveHeightRoots();
            CaptureHeightRootBasePositions();
            ApplyPlayMode(_currentPlayMode, invokeEvents: false);
        }

        public void RefreshCurrentPlayModeHeight()
        {
            ApplyPlayMode(_currentPlayMode, invokeEvents: false);
        }

        private void OnValidate()
        {
            canvasScale = Mathf.Max(0.0001f, canvasScale);
            standYOffset = Mathf.Max(sitYOffset, standYOffset);

            if (Application.isPlaying)
            {
                return;
            }

            uiClickPitchRange = ArcadeSfxPlayer.NormalizePitchRange(uiClickPitchRange);
            ResolveDefaultSfx();
            ResolveWristPanelRoot();
            ResolveHeightRoots();
            FindGeneratedMenuParts();
            ApplyCanvasTransform();

            if (_canvasObject != null)
            {
                SetCanvasVisible(showPreviewInEditor);
                SetPanelOpen(showPreviewInEditor && previewPanelOpenInEditor);
            }
        }

        private void ResolveReferences()
        {
            ResolveWristPanelRoot();

            if (settingsController == null)
            {
                var settings = FindObjectsByType<ArcadeSettingsController>(FindObjectsInactive.Include, FindObjectsSortMode.None);
                if (settings.Length > 0)
                {
                    settingsController = settings[0];
                }
            }

            if (teleportController == null)
            {
                var teleports = FindObjectsByType<ArcadeTeleportController>(FindObjectsInactive.Include, FindObjectsSortMode.None);
                if (teleports.Length > 0)
                {
                    teleportController = teleports[0];
                }
            }

            ResolveHeightRoots();

            if (Application.isPlaying)
            {
                ResolveGrabInteractors();
            }
        }

        private void ResolveWristPanelRoot()
        {
            if (wristPanelRoot != null)
            {
                return;
            }

            var panel = transform.Find("WristMenuPanel");
            wristPanelRoot = panel != null ? panel : transform;
        }

        private void ResolveGrabInteractors()
        {
            grabInteractors.RemoveAll(interactor => interactor == null);
            if (grabInteractors.Count > 0)
            {
                return;
            }

            var interactors = FindObjectsByType<XRBaseInteractor>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            for (var i = 0; i < interactors.Length; i++)
            {
                grabInteractors.Add(interactors[i]);
            }
        }

        private void BuildMenu()
        {
            if (preserveManualCanvasLayout && TryUseExistingMenu())
            {
                EnsurePlayModeMenuParts();
                RebindGeneratedMenuEvents();
                ShowSystemPage();
                return;
            }

            DestroyGeneratedCanvas();

            var canvasTransform = CreateRect(GeneratedCanvasName, wristPanelRoot);
            _canvasObject = canvasTransform.gameObject;
            canvasTransform.localPosition = canvasLocalPosition;
            canvasTransform.localEulerAngles = canvasLocalEulerAngles;
            canvasTransform.localScale = Vector3.one * canvasScale;
            canvasTransform.sizeDelta = new Vector2(360f, 270f);

            var canvas = _canvasObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            canvas.sortingOrder = 100;
            canvas.worldCamera = Camera.main;

            var scaler = _canvasObject.AddComponent<CanvasScaler>();
            scaler.dynamicPixelsPerUnit = 20f;

            var raycaster = _canvasObject.AddComponent<TrackedDeviceGraphicRaycaster>();
            raycaster.checkFor3DOcclusion = false;

            _toggleRoot = CreateButton(
                "WristMenuToggle",
                canvasTransform,
                "MENU",
                defaultToggleButtonPosition,
                defaultToggleButtonSize,
                new Color(0.05f, 0.12f, 0.16f, 0.92f),
                new Color(0.45f, 1f, 0.76f, 1f),
                TogglePanel,
                menuToggleIcon,
                hideMenuToggleTextWhenIconAssigned).gameObject;

            _panelRoot = CreatePanel("SettingsPanel", canvasTransform, new Vector2(0f, 0f), new Vector2(340f, 230f), new Color(0.02f, 0.025f, 0.03f, 0.92f)).gameObject;
            BuildPanel(_panelRoot.transform as RectTransform);
        }

        private void ApplyCanvasTransform()
        {
            if (_canvasObject == null)
            {
                FindGeneratedMenuParts();
            }

            if (_canvasObject == null)
            {
                return;
            }

            var rectTransform = _canvasObject.transform as RectTransform;
            if (rectTransform == null)
            {
                return;
            }

            rectTransform.localPosition = canvasLocalPosition;
            rectTransform.localEulerAngles = canvasLocalEulerAngles;
            rectTransform.localScale = Vector3.one * canvasScale;
        }

        private void BuildPanel(RectTransform panel)
        {
            CreateLabel("Title", panel, "WRIST SETTINGS", new Vector2(0f, 92f), new Vector2(230f, 34f), 20f, new Color(0.88f, 1f, 0.94f, 1f));

            CreateButton("CloseButton", panel, "X", new Vector2(145f, 92f), new Vector2(34f, 34f), new Color(0.36f, 0.08f, 0.07f, 0.95f), Color.white, ClosePanel);
            CreateButton("SystemTabButton", panel, "SYSTEM", new Vector2(-110f, 52f), new Vector2(96f, 34f), new Color(0.08f, 0.18f, 0.22f, 0.95f), Color.white, ShowSystemPage);
            CreateButton("PlayModeTabButton", panel, "PLAY", new Vector2(0f, 52f), new Vector2(110f, 34f), new Color(0.08f, 0.18f, 0.22f, 0.95f), Color.white, ShowPlayModePage);
            CreateButton("TeleportTabButton", panel, "TELEPORT", new Vector2(110f, 52f), new Vector2(96f, 34f), new Color(0.08f, 0.18f, 0.22f, 0.95f), Color.white, ShowTeleportPage);

            _systemPage = CreatePanel("SystemPage", panel, new Vector2(0f, -35f), new Vector2(300f, 130f), new Color(0f, 0f, 0f, 0f)).gameObject;
            _playModePage = CreatePanel("PlayModePage", panel, new Vector2(0f, -35f), new Vector2(300f, 130f), new Color(0f, 0f, 0f, 0f)).gameObject;
            _teleportPage = CreatePanel("TeleportPage", panel, new Vector2(0f, -35f), new Vector2(300f, 130f), new Color(0f, 0f, 0f, 0f)).gameObject;

            BuildSystemPage(_systemPage.transform as RectTransform);
            BuildPlayModePage(_playModePage.transform as RectTransform);
            BuildTeleportPage(_teleportPage.transform as RectTransform);
            ShowSystemPage();
        }

        private void BuildPlayModePage(RectTransform page)
        {
            CreateLabel("PlayModeLabel", page, "PLAY MODE", new Vector2(0f, 38f), new Vector2(260f, 30f), 20f, new Color(0.88f, 1f, 0.94f, 1f));

            CreateButton(
                "SitButton",
                page,
                "SIT",
                new Vector2(-70f, -20f),
                new Vector2(110f, 48f),
                new Color(0.09f, 0.19f, 0.16f, 0.95f),
                new Color(0.7f, 1f, 0.82f, 1f),
                SetSitMode);

            CreateButton(
                "StandButton",
                page,
                "STAND",
                new Vector2(70f, -20f),
                new Vector2(110f, 48f),
                new Color(0.09f, 0.19f, 0.16f, 0.95f),
                new Color(0.7f, 1f, 0.82f, 1f),
                SetStandMode);
        }

        private void BuildSystemPage(RectTransform page)
        {
            CreateSliderRow(
                "SoundSlider",
                page,
                "SOUND",
                new Vector2(0f, 30f),
                0f,
                1f,
                settingsController != null ? settingsController.MasterVolume : AudioListener.volume,
                value => settingsController?.SetMasterVolume(value));

            CreateSliderRow(
                "BrightnessSlider",
                page,
                "BRIGHT",
                new Vector2(0f, -34f),
                0.4f,
                1.5f,
                settingsController != null ? settingsController.Brightness : 1f,
                value => settingsController?.SetBrightness(value));
        }

        private void BuildTeleportPage(RectTransform page)
        {
            CreateTeleportButton(page, "CARROM", "Carrom", new Vector2(-98f, 32f));
            CreateTeleportButton(page, "CURLING", "Curling", new Vector2(0f, 32f));
            CreateTeleportButton(page, "PUTTING", "Putting", new Vector2(98f, 32f));
            CreateTeleportButton(page, "YOTYAN", "YoTyan", new Vector2(-50f, -34f));
            CreateTeleportButton(page, "PINBALL", "Pinball", new Vector2(50f, -34f));
        }

        private void CreateTeleportButton(RectTransform parent, string label, string anchorName, Vector2 anchoredPosition)
        {
            CreateButton(
                label + "Button",
                parent,
                label,
                anchoredPosition,
                new Vector2(88f, 42f),
                new Color(0.09f, 0.19f, 0.16f, 0.95f),
                new Color(0.7f, 1f, 0.82f, 1f),
                () => teleportController?.TeleportToName(anchorName));
        }

        private void CreateSliderRow(
            string objectName,
            RectTransform parent,
            string label,
            Vector2 anchoredPosition,
            float minValue,
            float maxValue,
            float value,
            UnityEngine.Events.UnityAction<float> onChanged)
        {
            var row = CreateRect(objectName, parent);
            row.anchoredPosition = anchoredPosition;
            row.sizeDelta = new Vector2(280f, 48f);

            CreateLabel(label + "Label", row, label, new Vector2(-102f, 0f), new Vector2(72f, 28f), 16f, Color.white);

            var sliderRect = CreateRect(label + "Slider", row);
            sliderRect.anchoredPosition = new Vector2(42f, 0f);
            sliderRect.sizeDelta = new Vector2(180f, 22f);

            var background = CreateImage("Background", sliderRect, new Color(0.12f, 0.16f, 0.17f, 0.95f));
            Stretch(background.rectTransform);

            var fillArea = CreateRect("Fill Area", sliderRect);
            Stretch(fillArea, new Vector2(6f, 0f), new Vector2(-6f, 0f));

            var fill = CreateImage("Fill", fillArea, new Color(0.45f, 1f, 0.76f, 1f));
            Stretch(fill.rectTransform);

            var handleArea = CreateRect("Handle Slide Area", sliderRect);
            Stretch(handleArea, new Vector2(8f, 0f), new Vector2(-8f, 0f));

            var handle = CreateImage("Handle", handleArea, new Color(0.95f, 1f, 0.98f, 1f));
            handle.rectTransform.sizeDelta = new Vector2(18f, 28f);

            var slider = sliderRect.gameObject.AddComponent<Slider>();
            slider.minValue = minValue;
            slider.maxValue = maxValue;
            slider.value = Mathf.Clamp(value, minValue, maxValue);
            slider.fillRect = fill.rectTransform;
            slider.handleRect = handle.rectTransform;
            slider.targetGraphic = handle;
            slider.direction = Slider.Direction.LeftToRight;
            slider.onValueChanged.AddListener(onChanged);
        }

        private Button CreateButton(
            string objectName,
            Transform parent,
            string label,
            Vector2 anchoredPosition,
            Vector2 size,
            Color backgroundColor,
            Color textColor,
            UnityEngine.Events.UnityAction onClick)
        {
            return CreateButton(objectName, parent, label, anchoredPosition, size, backgroundColor, textColor, onClick, null, false);
        }

        private Button CreateButton(
            string objectName,
            Transform parent,
            string label,
            Vector2 anchoredPosition,
            Vector2 size,
            Color backgroundColor,
            Color textColor,
            UnityEngine.Events.UnityAction onClick,
            Sprite icon,
            bool hideTextWhenIconAssigned)
        {
            var rect = CreateRect(objectName, parent);
            rect.anchoredPosition = anchoredPosition;
            rect.sizeDelta = size;

            var image = rect.gameObject.AddComponent<Image>();
            image.color = backgroundColor;

            var button = rect.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            button.onClick.AddListener(PlayUiClick);
            button.onClick.AddListener(onClick);

            if (icon != null)
            {
                var iconImage = CreateImage("Icon", rect, Color.white);
                iconImage.sprite = icon;
                iconImage.color = textColor;
                iconImage.preserveAspect = true;
                iconImage.raycastTarget = false;
                iconImage.rectTransform.anchoredPosition = Vector2.zero;
                iconImage.rectTransform.sizeDelta = size * 0.62f;
            }

            if (icon == null || !hideTextWhenIconAssigned)
            {
                CreateLabel("Label", rect, label, Vector2.zero, size, 17f, textColor);
            }

            return button;
        }

        private RectTransform CreatePanel(string objectName, Transform parent, Vector2 anchoredPosition, Vector2 size, Color color)
        {
            var rect = CreateRect(objectName, parent);
            rect.anchoredPosition = anchoredPosition;
            rect.sizeDelta = size;

            var image = rect.gameObject.AddComponent<Image>();
            image.color = color;
            return rect;
        }

        private TMP_Text CreateLabel(string objectName, Transform parent, string text, Vector2 anchoredPosition, Vector2 size, float fontSize, Color color)
        {
            var rect = CreateRect(objectName, parent);
            rect.anchoredPosition = anchoredPosition;
            rect.sizeDelta = size;

            var label = rect.gameObject.AddComponent<TextMeshProUGUI>();
            label.text = text;
            label.fontSize = fontSize;
            label.color = color;
            label.alignment = TextAlignmentOptions.Center;
            label.raycastTarget = false;
            return label;
        }

        private static Image CreateImage(string objectName, Transform parent, Color color)
        {
            var rect = CreateRect(objectName, parent);
            var image = rect.gameObject.AddComponent<Image>();
            image.color = color;
            return image;
        }

        private static RectTransform CreateRect(string objectName, Transform parent)
        {
            var gameObject = new GameObject(objectName, typeof(RectTransform));
            var rect = gameObject.GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.localScale = Vector3.one;
            return rect;
        }

        private static void Stretch(RectTransform rect)
        {
            Stretch(rect, Vector2.zero, Vector2.zero);
        }

        private static void Stretch(RectTransform rect, Vector2 offsetMin, Vector2 offsetMax)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = offsetMin;
            rect.offsetMax = offsetMax;
        }

        private void SetPanelOpen(bool isOpen)
        {
            _isPanelOpen = isOpen;

            if (_toggleRoot != null)
            {
                _toggleRoot.SetActive(!isOpen);
            }

            if (_panelRoot != null)
            {
                _panelRoot.SetActive(isOpen);
            }
        }

        private void SetCanvasVisible(bool isVisible)
        {
            if (_canvasObject != null)
            {
                _canvasObject.SetActive(isVisible);
            }
        }

        private bool IsAnyInteractorSelecting()
        {
            ResolveGrabInteractors();

            for (var i = 0; i < grabInteractors.Count; i++)
            {
                var interactor = grabInteractors[i];
                if (interactor != null && interactor.hasSelection)
                {
                    return true;
                }
            }

            return false;
        }

        private void FindGeneratedMenuParts()
        {
            if (wristPanelRoot == null)
            {
                return;
            }

            var existing = wristPanelRoot.Find(GeneratedCanvasName);
            if (existing == null)
            {
                _canvasObject = null;
                _toggleRoot = null;
                _panelRoot = null;
                _playModePage = null;
                _systemPage = null;
                _teleportPage = null;
                return;
            }

            _canvasObject = existing.gameObject;
            _toggleRoot = existing.Find("WristMenuToggle")?.gameObject;
            _panelRoot = existing.Find("SettingsPanel")?.gameObject;
            _playModePage = existing.Find("SettingsPanel/PlayModePage")?.gameObject;
            if (_playModePage == null)
            {
                _playModePage = existing.Find("SettingsPanel/HeightSection")?.gameObject;
            }
            _systemPage = existing.Find("SettingsPanel/SystemPage")?.gameObject;
            _teleportPage = existing.Find("SettingsPanel/TeleportPage")?.gameObject;
        }

        private bool TryUseExistingMenu()
        {
            FindGeneratedMenuParts();
            if (_canvasObject == null)
            {
                return false;
            }

            EnsureCanvasComponents();
            return _toggleRoot != null && _panelRoot != null;
        }

        private void EnsureCanvasComponents()
        {
            if (_canvasObject == null)
            {
                return;
            }

            var canvas = _canvasObject.GetComponent<Canvas>();
            if (canvas == null)
            {
                canvas = _canvasObject.AddComponent<Canvas>();
            }

            canvas.renderMode = RenderMode.WorldSpace;
            canvas.sortingOrder = 100;
            canvas.worldCamera = Camera.main;

            var scaler = _canvasObject.GetComponent<CanvasScaler>();
            if (scaler == null)
            {
                scaler = _canvasObject.AddComponent<CanvasScaler>();
            }

            scaler.dynamicPixelsPerUnit = 20f;

            var raycaster = _canvasObject.GetComponent<TrackedDeviceGraphicRaycaster>();
            if (raycaster == null)
            {
                raycaster = _canvasObject.AddComponent<TrackedDeviceGraphicRaycaster>();
            }

            raycaster.checkFor3DOcclusion = false;
        }

        private void EnsurePlayModeMenuParts()
        {
            if (_panelRoot == null)
            {
                return;
            }

            var panelTransform = _panelRoot.transform as RectTransform;
            if (panelTransform == null)
            {
                return;
            }

            var systemTab = FindMenuObject("SettingsPanel/SystemTabButton");
            if (systemTab == null)
            {
                systemTab = CreateButton(
                    "SystemTabButton",
                    panelTransform,
                    "SYSTEM",
                    new Vector2(-110f, 52f),
                    new Vector2(96f, 34f),
                    new Color(0.08f, 0.18f, 0.22f, 0.95f),
                    Color.white,
                    ShowSystemPage).gameObject;
            }
            else
            {
                SetButtonLabel(systemTab, "SYSTEM");
            }

            var playModeTab = FindMenuObject("SettingsPanel/PlayModeTabButton");
            if (playModeTab == null)
            {
                playModeTab = CreateButton(
                    "PlayModeTabButton",
                    panelTransform,
                    "PLAY",
                    new Vector2(0f, 52f),
                    new Vector2(110f, 34f),
                    new Color(0.08f, 0.18f, 0.22f, 0.95f),
                    Color.white,
                    ShowPlayModePage).gameObject;
            }
            else
            {
                SetButtonLabel(playModeTab, "PLAY");
            }

            var teleportTab = FindMenuObject("SettingsPanel/TeleportTabButton");
            if (teleportTab == null)
            {
                teleportTab = CreateButton(
                    "TeleportTabButton",
                    panelTransform,
                    "TELEPORT",
                    new Vector2(110f, 52f),
                    new Vector2(96f, 34f),
                    new Color(0.08f, 0.18f, 0.22f, 0.95f),
                    Color.white,
                    ShowTeleportPage).gameObject;
            }
            else
            {
                SetButtonLabel(teleportTab, "TELEPORT");
            }

            LayoutTabButton(systemTab, new Vector2(-110f, 52f), new Vector2(96f, 34f));
            LayoutTabButton(playModeTab, new Vector2(0f, 52f), new Vector2(110f, 34f));
            LayoutTabButton(teleportTab, new Vector2(110f, 52f), new Vector2(96f, 34f));

            if (_systemPage == null)
            {
                _systemPage = CreatePanel("SystemPage", panelTransform, new Vector2(0f, -35f), new Vector2(300f, 130f), new Color(0f, 0f, 0f, 0f)).gameObject;
                BuildSystemPage(_systemPage.transform as RectTransform);
            }

            if (_playModePage == null)
            {
                _playModePage = CreatePanel("PlayModePage", panelTransform, new Vector2(0f, -35f), new Vector2(300f, 130f), new Color(0f, 0f, 0f, 0f)).gameObject;
                BuildPlayModePage(_playModePage.transform as RectTransform);
            }
            else
            {
                EnsurePlayModeButtons(_playModePage.transform as RectTransform);
            }
        }

        private static void LayoutTabButton(GameObject target, Vector2 anchoredPosition, Vector2 size)
        {
            if (target == null || target.transform is not RectTransform rectTransform)
            {
                return;
            }

            rectTransform.anchoredPosition = anchoredPosition;
            rectTransform.sizeDelta = size;
        }

        private void EnsurePlayModeButtons(RectTransform page)
        {
            if (page == null)
            {
                return;
            }

            if (FindChildTransform(page, "PlayModeLabel") == null)
            {
                CreateLabel("PlayModeLabel", page, "PLAY MODE", new Vector2(0f, 38f), new Vector2(260f, 30f), 20f, new Color(0.88f, 1f, 0.94f, 1f));
            }

            if (FindChildTransform(page, "SitButton") == null)
            {
                CreateButton("SitButton", page, "SIT", new Vector2(-70f, -20f), new Vector2(110f, 48f), new Color(0.09f, 0.19f, 0.16f, 0.95f), new Color(0.7f, 1f, 0.82f, 1f), SetSitMode);
            }

            if (FindChildTransform(page, "StandButton") == null)
            {
                CreateButton("StandButton", page, "STAND", new Vector2(70f, -20f), new Vector2(110f, 48f), new Color(0.09f, 0.19f, 0.16f, 0.95f), new Color(0.7f, 1f, 0.82f, 1f), SetStandMode);
            }
        }

        private void RebindGeneratedMenuEvents()
        {
            BindButton(_toggleRoot, TogglePanel);
            BindButton(FindMenuObject("SettingsPanel/CloseButton"), ClosePanel);
            BindButton(FindMenuObject("SettingsPanel/SystemTabButton"), ShowSystemPage);
            BindButton(FindMenuObject("SettingsPanel/PlayModeTabButton"), ShowPlayModePage);
            BindButton(FindMenuObject("SettingsPanel/TeleportTabButton"), ShowTeleportPage);
            BindButton(FindMenuObjectDeep("SitButton"), SetSitMode);
            BindButton(FindMenuObjectDeep("StandButton"), SetStandMode);
            BindButton(FindMenuObject("SettingsPanel/TeleportPage/CARROMButton"), () => teleportController?.TeleportToName("Carrom"));
            BindButton(FindMenuObject("SettingsPanel/TeleportPage/CURLINGButton"), () => teleportController?.TeleportToName("Curling"));
            BindButton(FindMenuObject("SettingsPanel/TeleportPage/PUTTINGButton"), () => teleportController?.TeleportToName("Putting"));
            BindButton(FindMenuObject("SettingsPanel/TeleportPage/YOTYANButton"), () => teleportController?.TeleportToName("YoTyan"));
            BindButton(FindMenuObject("SettingsPanel/TeleportPage/PINBALLButton"), () => teleportController?.TeleportToName("Pinball"));

            // Keep old sliders functional if a manual legacy SystemPage still exists.
            BindSlider(
                "SettingsPanel/SystemPage/SoundSlider",
                settingsController != null ? settingsController.MasterVolume : AudioListener.volume,
                value => settingsController?.SetMasterVolume(value));

            BindSlider(
                "SettingsPanel/SystemPage/BrightnessSlider",
                settingsController != null ? settingsController.Brightness : 1f,
                value => settingsController?.SetBrightness(value));
        }

        private GameObject FindMenuObject(string path)
        {
            if (_canvasObject == null)
            {
                return null;
            }

            var child = _canvasObject.transform.Find(path);
            return child != null ? child.gameObject : null;
        }

        private GameObject FindMenuObjectDeep(string objectName)
        {
            if (_canvasObject == null)
            {
                return null;
            }

            var child = FindChildTransform(_canvasObject.transform, objectName);
            return child != null ? child.gameObject : null;
        }

        private static Transform FindChildTransform(Transform parent, string objectName)
        {
            if (parent == null || string.IsNullOrWhiteSpace(objectName))
            {
                return null;
            }

            if (parent.name == objectName)
            {
                return parent;
            }

            for (var i = 0; i < parent.childCount; i++)
            {
                var child = parent.GetChild(i);
                var found = FindChildTransform(child, objectName);
                if (found != null)
                {
                    return found;
                }
            }

            return null;
        }

        private void BindButton(GameObject target, UnityEngine.Events.UnityAction onClick)
        {
            if (target == null || !target.TryGetComponent<Button>(out var button))
            {
                return;
            }

            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(PlayUiClick);
            button.onClick.AddListener(onClick);
        }

        private void SetButtonLabel(GameObject target, string label)
        {
            if (target == null)
            {
                return;
            }

            var text = target.GetComponentInChildren<TMP_Text>(true);
            if (text != null)
            {
                text.text = label;
            }
        }

        private void ResolveDefaultSfx()
        {
            if (uiClickClip == null)
            {
                uiClickClip = ArcadeSfxPlayer.LoadEditorClip(DefaultUiClickClipPath);
            }
        }

        private void PlayUiClick()
        {
            ArcadeSfxPlayer.PlayOneShot(
                uiClickClip,
                transform.position,
                uiClickVolume,
                ArcadeSfxPlayer.RandomPitch(uiClickPitchRange),
                0f,
                3f,
                "UI_SFX_Click");
        }

        private void BindSlider(string rowPath, float value, UnityEngine.Events.UnityAction<float> onChanged)
        {
            var row = FindMenuObject(rowPath);
            if (row == null)
            {
                return;
            }

            var slider = row.GetComponentInChildren<Slider>(true);
            if (slider == null)
            {
                return;
            }

            slider.SetValueWithoutNotify(Mathf.Clamp(value, slider.minValue, slider.maxValue));
            slider.onValueChanged.RemoveAllListeners();
            slider.onValueChanged.AddListener(onChanged);
        }

        private void DestroyGeneratedCanvas()
        {
            if (wristPanelRoot == null)
            {
                return;
            }

            var existing = wristPanelRoot.Find(GeneratedCanvasName);
            if (existing == null)
            {
                return;
            }

            if (Application.isPlaying)
            {
                Destroy(existing.gameObject);
            }
            else
            {
                DestroyImmediate(existing.gameObject);
            }
        }

        private static void SetPageActive(GameObject page, bool isActive)
        {
            if (page != null && page.activeSelf != isActive)
            {
                page.SetActive(isActive);
            }
        }

        private void InitializePlayModeHeight()
        {
            if (_playModeInitialized)
            {
                return;
            }

            ResolveHeightRoots();
            CaptureHeightRootBasePositions();
            _currentPlayMode = LoadStartMode();

            if (applyStartModeOnAwake)
            {
                ApplyPlayMode(_currentPlayMode, invokeEvents: false);
            }

            _playModeInitialized = true;
        }

        private ArcadePlayMode LoadStartMode()
        {
            if (!saveModeToPlayerPrefs || !PlayerPrefs.HasKey(playerPrefsKey))
            {
                return startMode;
            }

            return PlayerPrefs.GetInt(playerPrefsKey, (int)startMode) == 1
                ? ArcadePlayMode.Stand
                : ArcadePlayMode.Sit;
        }

        private void ResolveHeightRoots()
        {
            heightAdjustedRoots.RemoveAll(root => root == null);

            if (heightAdjustedRoots.Count > 0 || !autoFindHeightRootsIfEmpty)
            {
                return;
            }

            for (var i = 0; i < autoFindHeightRootNames.Count; i++)
            {
                var rootName = autoFindHeightRootNames[i];
                if (string.IsNullOrWhiteSpace(rootName))
                {
                    continue;
                }

                var foundRoot = GameObject.Find(rootName);
                if (foundRoot != null)
                {
                    heightAdjustedRoots.Add(foundRoot.transform);
                    return;
                }
            }
        }

        private void CaptureHeightRootBasePositions()
        {
            _heightRootStates.Clear();

            for (var i = 0; i < heightAdjustedRoots.Count; i++)
            {
                var root = heightAdjustedRoots[i];
                if (root != null)
                {
                    _heightRootStates.Add(new HeightRootState(root));
                }
            }

            _hasCapturedHeightRoots = _heightRootStates.Count > 0;
        }

        private void ApplyPlayMode(ArcadePlayMode mode, bool invokeEvents)
        {
            if (!_hasCapturedHeightRoots)
            {
                return;
            }

            if (zeroRigidbodyVelocityOnModeChange)
            {
                ClearMovedRootRigidbodies();
            }

            var yOffset = GetPlayModeYOffset(mode);

            for (var i = 0; i < _heightRootStates.Count; i++)
            {
                var state = _heightRootStates[i];
                if (state.root == null)
                {
                    continue;
                }

                if (useLocalPositionForHeightRoots)
                {
                    var position = state.baseLocalPosition;
                    position.y += yOffset;
                    state.root.localPosition = position;
                }
                else
                {
                    var position = state.baseWorldPosition;
                    position.y += yOffset;
                    state.root.position = position;
                }
            }

            if (zeroRigidbodyVelocityOnModeChange || sleepRigidbodiesAfterMove)
            {
                ClearMovedRootRigidbodies();
            }

            if (!invokeEvents)
            {
                return;
            }

            if (mode == ArcadePlayMode.Sit)
            {
                onSitMode?.Invoke();
            }
            else
            {
                onStandMode?.Invoke();
            }

            onModeChanged?.Invoke(mode);
        }

        private float GetPlayModeYOffset(ArcadePlayMode mode)
        {
            return mode == ArcadePlayMode.Stand ? standYOffset : sitYOffset;
        }

        private void ClearMovedRootRigidbodies()
        {
            for (var i = 0; i < heightAdjustedRoots.Count; i++)
            {
                var root = heightAdjustedRoots[i];
                if (root == null)
                {
                    continue;
                }

                var rigidbodies = root.GetComponentsInChildren<Rigidbody>(includeInactiveRigidbodies);
                for (var j = 0; j < rigidbodies.Length; j++)
                {
                    var body = rigidbodies[j];
                    if (body == null || body.isKinematic)
                    {
                        continue;
                    }

#if UNITY_6000_0_OR_NEWER
                    body.linearVelocity = Vector3.zero;
#else
                    body.velocity = Vector3.zero;
#endif
                    body.angularVelocity = Vector3.zero;

                    if (sleepRigidbodiesAfterMove)
                    {
                        body.Sleep();
                    }
                }
            }
        }
    }
}
