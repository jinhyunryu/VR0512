using ArcadeRoom.Audio;
using ArcadeRoom.Stations;
using MikeNspired.XRIStarterKit;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Filtering;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

namespace ArcadeRoom.Reset
{
    public class ResetButtonTrigger : MonoBehaviour
    {
        private const string DefaultUiClickClipPath = "Assets/_Project/Audio/SFX/UI_Click.wav";

        [SerializeField] private ResetManager resetManager;
        [SerializeField] private ArcadeStationRoot targetStation;
        [SerializeField] private bool resetAllStations;
        [Header("XR Press")]
        [SerializeField] private bool triggerOnXRSelect = true;
        [SerializeField] private bool triggerOnColliderEnter = true;
        [SerializeField] private float triggerCooldown = 0.15f;
        [SerializeField] private XRPushButton xrPushButton;
        [SerializeField] private Collider interactableCollider;
        [SerializeField] private XRSimpleInteractable xrInteractable;
        [SerializeField] private XRPokeFilter pokeFilter;
        [Header("SFX")]
        [SerializeField] private AudioClip clickClip;
        [SerializeField, Range(0f, 1f)] private float clickVolume = 0.65f;
        [SerializeField] private Vector2 clickPitchRange = new(0.98f, 1.04f);
        [SerializeField, Range(0f, 1f)] private float spatialBlend = 1f;
        [SerializeField, Min(0.1f)] private float maxDistance = 5f;

        private float _lastTriggerTime = float.NegativeInfinity;

        public void Initialize(
            ResetManager manager,
            ArcadeStationRoot station,
            bool resetEverything,
            bool useXRSelect = true,
            bool useColliderEnter = true)
        {
            resetManager = manager;
            targetStation = station;
            resetAllStations = resetEverything;
            triggerOnXRSelect = useXRSelect;
            triggerOnColliderEnter = useColliderEnter;

            EnsureXRSetup();
        }

        public void ConfigureInteractionMode(bool useXRSelect, bool useColliderEnter)
        {
            triggerOnXRSelect = useXRSelect;
            triggerOnColliderEnter = useColliderEnter;

            EnsureXRSetup();
        }

        private void Awake()
        {
            ResolveDefaultSfx();
            EnsureXRSetup();
        }

        private void OnEnable()
        {
            RegisterXRCallbacks();
        }

        private void OnDisable()
        {
            UnregisterXRCallbacks();
        }

        private void OnValidate()
        {
            if (interactableCollider == null)
            {
                interactableCollider = GetComponent<Collider>();
            }

            if (xrPushButton == null)
            {
                xrPushButton = GetComponent<XRPushButton>();
            }

            if (xrInteractable == null)
            {
                xrInteractable = GetComponent<XRSimpleInteractable>();
            }

            if (pokeFilter == null)
            {
                pokeFilter = GetComponent<XRPokeFilter>();
            }

            clickPitchRange = ArcadeSfxPlayer.NormalizePitchRange(clickPitchRange);
            maxDistance = Mathf.Max(0.1f, maxDistance);
            ResolveDefaultSfx();
        }

        private void OnTriggerEnter(Collider other)
        {
            if (!triggerOnColliderEnter)
            {
                return;
            }

            TriggerReset();
        }

        private void OnXRSelectEntered(SelectEnterEventArgs args)
        {
            if (!triggerOnXRSelect)
            {
                return;
            }

            TriggerReset();
        }

        public void TriggerReset()
        {
            if (!CanTrigger())
            {
                return;
            }

            PlayClickSfx();

            if (resetManager == null)
            {
                var managers = FindObjectsByType<ResetManager>(FindObjectsInactive.Include, FindObjectsSortMode.None);
                if (managers.Length > 0)
                {
                    resetManager = managers[0];
                }
            }

            if (resetManager == null)
            {
                if (targetStation != null)
                {
                    targetStation.ResetState();
                }

                return;
            }

            if (resetAllStations || targetStation == null)
            {
                resetManager.ResetAllStations();
                return;
            }

            resetManager.ResetStation(targetStation);
        }

        private bool CanTrigger()
        {
            if (Time.unscaledTime - _lastTriggerTime < triggerCooldown)
            {
                return false;
            }

            _lastTriggerTime = Time.unscaledTime;
            return true;
        }

        private void ResolveDefaultSfx()
        {
            if (clickClip == null)
            {
                clickClip = ArcadeSfxPlayer.LoadEditorClip(DefaultUiClickClipPath);
            }
        }

        private void PlayClickSfx()
        {
            ArcadeSfxPlayer.PlayOneShot(
                clickClip,
                transform.position,
                clickVolume,
                ArcadeSfxPlayer.RandomPitch(clickPitchRange),
                spatialBlend,
                maxDistance,
                "UI_SFX_ResetClick");
        }

        private void EnsureXRSetup()
        {
            if (!triggerOnXRSelect)
            {
                return;
            }

            if (xrPushButton == null)
            {
                xrPushButton = GetComponent<XRPushButton>();
            }

            if (xrPushButton != null)
            {
                RegisterXRCallbacks();
                return;
            }

            if (interactableCollider == null)
            {
                interactableCollider = GetComponent<Collider>();
            }

            if (interactableCollider == null)
            {
                interactableCollider = GetComponentInChildren<Collider>();
            }

            if (interactableCollider == null)
            {
                return;
            }

            if (xrInteractable == null)
            {
                xrInteractable = GetOrAddComponent<XRSimpleInteractable>();
            }

            xrInteractable.distanceCalculationMode = XRBaseInteractable.DistanceCalculationMode.ColliderVolume;
            xrInteractable.colliders.Clear();
            xrInteractable.colliders.Add(interactableCollider);

            if (pokeFilter == null)
            {
                pokeFilter = GetOrAddComponent<XRPokeFilter>();
            }

            pokeFilter.pokeInteractable = xrInteractable;
            pokeFilter.pokeCollider = interactableCollider;

            xrInteractable.selectFilters.Remove(pokeFilter);
            xrInteractable.selectFilters.Add(pokeFilter);

            RegisterXRCallbacks();
        }

        private void RegisterXRCallbacks()
        {
            if (xrPushButton != null)
            {
                xrPushButton.OnPress.RemoveListener(TriggerReset);
                xrPushButton.OnPress.AddListener(TriggerReset);
                return;
            }

            if (xrInteractable == null)
            {
                return;
            }

            xrInteractable.selectEntered.RemoveListener(OnXRSelectEntered);
            xrInteractable.selectEntered.AddListener(OnXRSelectEntered);
        }

        private void UnregisterXRCallbacks()
        {
            if (xrPushButton != null)
            {
                xrPushButton.OnPress.RemoveListener(TriggerReset);
                return;
            }

            if (xrInteractable == null)
            {
                return;
            }

            xrInteractable.selectEntered.RemoveListener(OnXRSelectEntered);
        }

        private T GetOrAddComponent<T>() where T : Component
        {
            if (!TryGetComponent<T>(out var component))
            {
                component = gameObject.AddComponent<T>();
            }

            return component;
        }
    }
}
