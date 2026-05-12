using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

namespace ArcadeRoom.Arcade
{
    /// <summary>
    /// Switches the arcade room between sitting and standing play height.
    /// 
    /// Recommended setup:
    /// - Put all height-sensitive gameplay objects under one empty root, for example "ArcadeContentRoot".
    /// - Do not move the XR Origin or Camera with this script.
    /// - Wire UI buttons directly:
    ///     Sit button   -> ArcadePlayModeHeightController.SetSitMode()
    ///     Stand button -> ArcadePlayModeHeightController.SetStandMode()
    /// 
    /// This script only offsets assigned roots on the Y axis and optionally clears Rigidbody velocity
    /// so physics objects do not continue moving after the height switch.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ArcadePlayModeHeightController : MonoBehaviour
    {
        public enum ArcadePlayMode
        {
            Sit = 0,
            Stand = 1
        }

        [System.Serializable]
        public sealed class PlayModeChangedEvent : UnityEvent<ArcadePlayMode> { }

        [System.Serializable]
        private struct RootState
        {
            public Transform root;
            public Vector3 baseLocalPosition;
            public Vector3 baseWorldPosition;

            public RootState(Transform root)
            {
                this.root = root;
                baseLocalPosition = root != null ? root.localPosition : Vector3.zero;
                baseWorldPosition = root != null ? root.position : Vector3.zero;
            }
        }

        [Header("Mode")]
        [SerializeField] private ArcadePlayMode startMode = ArcadePlayMode.Sit;
        [SerializeField] private bool applyStartModeOnAwake = true;

        [Header("Height Offsets")]
        [Tooltip("Y offset from the captured base position when Sit mode is active.")]
        [SerializeField] private float sitYOffset = 0f;

        [Tooltip("Y offset from the captured base position when Stand mode is active.")]
        [SerializeField] private float standYOffset = 0.5f;

        [Tooltip("Use localPosition for roots. Recommended when ArcadeContentRoot is under a scene parent.")]
        [SerializeField] private bool useLocalPosition = true;

        [Header("Roots To Move")]
        [Tooltip("Assign ArcadeContentRoot here. You can add more roots if UI/anchors need to move with the game stations.")]
        [SerializeField] private List<Transform> heightAdjustedRoots = new();

        [Tooltip("If Roots To Move is empty, the script will try to find this object by name.")]
        [SerializeField] private string autoFindRootName = "ArcadeContentRoot";

        [SerializeField] private bool autoFindRootIfEmpty = true;

        [Header("Physics Safety")]
        [Tooltip("Clears velocity for Rigidbody components under moved roots before and after changing height.")]
        [SerializeField] private bool zeroRigidbodyVelocityOnModeChange = true;

        [Tooltip("Puts Rigidbody components under moved roots to sleep after changing height.")]
        [SerializeField] private bool sleepRigidbodiesAfterMove = true;

        [Tooltip("Includes inactive child Rigidbodies when clearing velocity.")]
        [SerializeField] private bool includeInactiveRigidbodies = true;

        [Header("Optional Save")]
        [SerializeField] private bool saveModeToPlayerPrefs = false;
        [SerializeField] private string playerPrefsKey = "ArcadeRoom_PlayMode";

        [Header("Events")]
        [SerializeField] private UnityEvent onSitMode = new UnityEvent();
        [SerializeField] private UnityEvent onStandMode = new UnityEvent();
        [SerializeField] private PlayModeChangedEvent onModeChanged = new PlayModeChangedEvent();

        private readonly List<RootState> _rootStates = new();
        private ArcadePlayMode _currentMode;
        private bool _hasCapturedRoots;

        public ArcadePlayMode CurrentMode => _currentMode;
        public float CurrentYOffset => GetYOffset(_currentMode);

        private void Awake()
        {
            ResolveRoots();
            CaptureBasePositions();

            _currentMode = LoadStartMode();

            if (applyStartModeOnAwake)
            {
                ApplyMode(_currentMode, invokeEvents: false);
            }
        }

        private void OnValidate()
        {
            standYOffset = Mathf.Max(sitYOffset, standYOffset);

            if (!Application.isPlaying)
            {
                ResolveRoots();
            }
        }

        public void SetSitMode()
        {
            SetMode(ArcadePlayMode.Sit);
        }

        public void SetStandMode()
        {
            SetMode(ArcadePlayMode.Stand);
        }

        public void ToggleMode()
        {
            SetMode(_currentMode == ArcadePlayMode.Sit ? ArcadePlayMode.Stand : ArcadePlayMode.Sit);
        }

        /// <summary>
        /// Useful for Unity UI Dropdown or custom integer buttons.
        /// 0 = Sit, 1 = Stand.
        /// </summary>
        public void SetModeByIndex(int modeIndex)
        {
            SetMode(modeIndex == 1 ? ArcadePlayMode.Stand : ArcadePlayMode.Sit);
        }

        public void SetMode(ArcadePlayMode mode)
        {
            if (!_hasCapturedRoots)
            {
                ResolveRoots();
                CaptureBasePositions();
            }

            if (_currentMode == mode)
            {
                ApplyMode(mode, invokeEvents: false);
                return;
            }

            _currentMode = mode;
            ApplyMode(mode, invokeEvents: true);

            if (saveModeToPlayerPrefs)
            {
                PlayerPrefs.SetInt(playerPrefsKey, (int)_currentMode);
                PlayerPrefs.Save();
            }
        }

        /// <summary>
        /// Re-captures the current root positions as the neutral/base positions.
        /// Use this if you manually reposition ArcadeContentRoot in the editor or at runtime.
        /// </summary>
        public void CaptureCurrentPositionsAsBase()
        {
            ResolveRoots();
            CaptureBasePositions();
            ApplyMode(_currentMode, invokeEvents: false);
        }

        /// <summary>
        /// Re-applies the current mode height offset.
        /// Useful after manually resetting station roots.
        /// </summary>
        public void RefreshCurrentMode()
        {
            ApplyMode(_currentMode, invokeEvents: false);
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

        private void ResolveRoots()
        {
            heightAdjustedRoots.RemoveAll(root => root == null);

            if (heightAdjustedRoots.Count > 0 || !autoFindRootIfEmpty || string.IsNullOrWhiteSpace(autoFindRootName))
            {
                return;
            }

            var foundRoot = GameObject.Find(autoFindRootName);
            if (foundRoot != null)
            {
                heightAdjustedRoots.Add(foundRoot.transform);
            }
        }

        private void CaptureBasePositions()
        {
            _rootStates.Clear();

            for (var i = 0; i < heightAdjustedRoots.Count; i++)
            {
                var root = heightAdjustedRoots[i];
                if (root != null)
                {
                    _rootStates.Add(new RootState(root));
                }
            }

            _hasCapturedRoots = _rootStates.Count > 0;
        }

        private void ApplyMode(ArcadePlayMode mode, bool invokeEvents)
        {
            if (!_hasCapturedRoots)
            {
                return;
            }

            if (zeroRigidbodyVelocityOnModeChange)
            {
                ClearMovedRootRigidbodies();
            }

            var yOffset = GetYOffset(mode);

            for (var i = 0; i < _rootStates.Count; i++)
            {
                var state = _rootStates[i];
                if (state.root == null)
                {
                    continue;
                }

                if (useLocalPosition)
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

        private float GetYOffset(ArcadePlayMode mode)
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
