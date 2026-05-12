using UnityEngine;
using UnityEngine.UI;

namespace ArcadeRoom.Arcade
{
    /// <summary>
    /// Binds a manual Sit / Stand page to the wrist-menu play mode controller.
    /// 
    /// Recommended hierarchy names:
    /// - PlayModeTabButton
    /// - SystemTabButton
    /// - TeleportTabButton
    /// - SystemPage
    /// - TeleportPage
    /// - PlayModePage or HeightSection
    /// - SitButton
    /// - StandButton
    /// 
    /// Attach this to GeneratedWristMenuCanvas, SettingsPanel, WristMenuRoot, or any parent
    /// that contains the menu objects. You can also assign all references manually.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class WristPlayModePageController : MonoBehaviour
    {
        [Header("Controller")]
        [SerializeField] private WristMenuController wristMenuController;
        [SerializeField] private ArcadePlayModeHeightController playModeHeightController;

        [Header("Tabs")]
        [SerializeField] private Button playModeTabButton;
        [SerializeField] private Button systemTabButton;
        [SerializeField] private Button teleportTabButton;

        [Header("Pages")]
        [SerializeField] private GameObject playModePage;
        [SerializeField] private GameObject systemPage;
        [SerializeField] private GameObject teleportPage;

        [Header("Play Mode Buttons")]
        [SerializeField] private Button sitButton;
        [SerializeField] private Button standButton;

        [Header("Options")]
        [SerializeField] private bool autoFindReferences = true;
        [SerializeField] private bool showSystemPageOnStart = true;
        [SerializeField] private bool closePlayModePageAfterSelection = false;

        private void Awake()
        {
            if (autoFindReferences)
            {
                ResolveReferences();
            }
        }

        private void Start()
        {
            // WristMenuController can build/rebind the generated menu in OnEnable,
            // so binding here is safer than Awake/OnEnable.
            if (autoFindReferences)
            {
                ResolveReferences();
            }

            BindButtons();

            if (showSystemPageOnStart)
            {
                ShowSystemPage();
            }
            else
            {
                HideAllPages();
            }
        }

        private void OnDestroy()
        {
            UnbindButtons();
        }

        private void OnValidate()
        {
            if (!autoFindReferences)
            {
                return;
            }

            ResolveReferences();
        }

        public void ShowPlayModePage()
        {
            SetPageActive(systemPage, false);
            SetPageActive(teleportPage, false);
            SetPageActive(playModePage, true);
        }

        public void ShowSystemPage()
        {
            SetPageActive(systemPage, true);
            SetPageActive(teleportPage, false);
            SetPageActive(playModePage, false);
        }

        public void ShowTeleportPage()
        {
            SetPageActive(systemPage, false);
            SetPageActive(teleportPage, true);
            SetPageActive(playModePage, false);
        }

        public void SetSitMode()
        {
            if (wristMenuController != null)
            {
                wristMenuController.SetSitMode();
            }
            else if (playModeHeightController != null)
            {
                playModeHeightController.SetSitMode();
            }

            if (closePlayModePageAfterSelection)
            {
                ShowSystemPage();
            }
        }

        public void SetStandMode()
        {
            if (wristMenuController != null)
            {
                wristMenuController.SetStandMode();
            }
            else if (playModeHeightController != null)
            {
                playModeHeightController.SetStandMode();
            }

            if (closePlayModePageAfterSelection)
            {
                ShowSystemPage();
            }
        }

        private void HideAllPages()
        {
            SetPageActive(systemPage, false);
            SetPageActive(teleportPage, false);
            SetPageActive(playModePage, false);
        }

        private void BindButtons()
        {
            if (playModeTabButton != null)
            {
                playModeTabButton.onClick.RemoveListener(ShowPlayModePage);
                playModeTabButton.onClick.AddListener(ShowPlayModePage);
            }

            if (systemTabButton != null)
            {
                systemTabButton.onClick.RemoveListener(ShowSystemPage);
                systemTabButton.onClick.AddListener(ShowSystemPage);
            }

            if (teleportTabButton != null)
            {
                teleportTabButton.onClick.RemoveListener(ShowTeleportPage);
                teleportTabButton.onClick.AddListener(ShowTeleportPage);
            }

            if (sitButton != null)
            {
                sitButton.onClick.RemoveListener(SetSitMode);
                sitButton.onClick.AddListener(SetSitMode);
            }

            if (standButton != null)
            {
                standButton.onClick.RemoveListener(SetStandMode);
                standButton.onClick.AddListener(SetStandMode);
            }
        }

        private void UnbindButtons()
        {
            if (playModeTabButton != null)
            {
                playModeTabButton.onClick.RemoveListener(ShowPlayModePage);
            }

            if (systemTabButton != null)
            {
                systemTabButton.onClick.RemoveListener(ShowSystemPage);
            }

            if (teleportTabButton != null)
            {
                teleportTabButton.onClick.RemoveListener(ShowTeleportPage);
            }

            if (sitButton != null)
            {
                sitButton.onClick.RemoveListener(SetSitMode);
            }

            if (standButton != null)
            {
                standButton.onClick.RemoveListener(SetStandMode);
            }
        }

        private void ResolveReferences()
        {
            if (wristMenuController == null)
            {
                var wristControllers = FindObjectsByType<WristMenuController>(
                    FindObjectsInactive.Include,
                    FindObjectsSortMode.None);

                if (wristControllers.Length > 0)
                {
                    wristMenuController = wristControllers[0];
                }
            }

            if (playModeHeightController == null)
            {
                var controllers = FindObjectsByType<ArcadePlayModeHeightController>(
                    FindObjectsInactive.Include,
                    FindObjectsSortMode.None);

                if (controllers.Length > 0)
                {
                    playModeHeightController = controllers[0];
                }
            }

            if (playModeTabButton == null)
            {
                playModeTabButton = FindButton("PlayModeTabButton");
            }

            if (systemTabButton == null)
            {
                systemTabButton = FindButton("SystemTabButton");
            }

            if (teleportTabButton == null)
            {
                teleportTabButton = FindButton("TeleportTabButton");
            }

            if (sitButton == null)
            {
                sitButton = FindButton("SitButton");
            }

            if (standButton == null)
            {
                standButton = FindButton("StandButton");
            }

            if (systemPage == null)
            {
                systemPage = FindChildGameObject("SystemPage");
            }

            if (teleportPage == null)
            {
                teleportPage = FindChildGameObject("TeleportPage");
            }

            if (playModePage == null)
            {
                playModePage = FindChildGameObject("PlayModePage");
            }

            // Your screenshot already has HeightSection, so use it as the play mode page
            // when PlayModePage does not exist.
            if (playModePage == null)
            {
                playModePage = FindChildGameObject("HeightSection");
            }
        }

        private Button FindButton(string objectName)
        {
            var child = FindChildTransform(transform, objectName);
            return child != null ? child.GetComponent<Button>() : null;
        }

        private GameObject FindChildGameObject(string objectName)
        {
            var child = FindChildTransform(transform, objectName);
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

        private static void SetPageActive(GameObject page, bool isActive)
        {
            if (page != null && page.activeSelf != isActive)
            {
                page.SetActive(isActive);
            }
        }
    }
}
