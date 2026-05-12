using Unity.XR.CoreUtils;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Inputs;

namespace XRMultiplayer
{
    /// <summary>
    /// Marks the existing scene XR Origin as the local rig used by the network avatar.
    /// This lets the VR Multiplayer Sample follow our arcade rig without replacing it.
    /// </summary>
    [DefaultExecutionOrder(-1000)]
    public sealed class ArcadeLocalRigReference : MonoBehaviour
    {
        static ArcadeLocalRigReference s_Active;

        [Header("Rig References")]
        [SerializeField] XROrigin m_XROrigin;
        [SerializeField] Transform m_HeadTransform;
        [SerializeField] Transform m_LeftControllerTransform;
        [SerializeField] Transform m_RightControllerTransform;
        [SerializeField] XRInputModalityManager m_InputModalityManager;

        public XROrigin xrOrigin => ResolveXROrigin();
        public Transform headTransform => ResolveHeadTransform();
        public Transform leftControllerTransform => ResolveLeftControllerTransform();
        public Transform rightControllerTransform => ResolveRightControllerTransform();
        public XRInputModalityManager inputModalityManager => ResolveInputModalityManager();

        void Awake()
        {
            if (s_Active != null && s_Active != this)
            {
                Debug.LogWarning($"Multiple {nameof(ArcadeLocalRigReference)} components found. Using {s_Active.name}.");
                return;
            }

            s_Active = this;
            CacheMissingReferences();
        }

        void OnValidate()
        {
            CacheMissingReferences();
        }

        void OnDestroy()
        {
            if (s_Active == this)
                s_Active = null;
        }

        public static ArcadeLocalRigReference Find()
        {
            if (s_Active != null)
                return s_Active;

            s_Active = FindFirstObjectByType<ArcadeLocalRigReference>();
            if (s_Active != null)
                s_Active.CacheMissingReferences();

            return s_Active;
        }

        public static XROrigin FindXROrigin()
        {
            ArcadeLocalRigReference localRig = Find();
            if (localRig != null && localRig.xrOrigin != null)
                return localRig.xrOrigin;

            return FindFirstObjectByType<XROrigin>();
        }

        public static Transform FindHeadTransform()
        {
            ArcadeLocalRigReference localRig = Find();
            if (localRig != null && localRig.headTransform != null)
                return localRig.headTransform;

            XROrigin origin = FindXROrigin();
            return origin != null && origin.Camera != null ? origin.Camera.transform : null;
        }

        public static XRInputModalityManager FindInputModalityManager()
        {
            ArcadeLocalRigReference localRig = Find();
            if (localRig != null && localRig.inputModalityManager != null)
                return localRig.inputModalityManager;

            XROrigin origin = FindXROrigin();
            if (origin != null && origin.TryGetComponent(out XRInputModalityManager manager))
                return manager;

            return FindFirstObjectByType<XRInputModalityManager>();
        }

        public static bool TryFindControllerTransforms(out Transform left, out Transform right)
        {
            left = null;
            right = null;

            ArcadeLocalRigReference localRig = Find();
            if (localRig != null)
            {
                left = localRig.leftControllerTransform;
                right = localRig.rightControllerTransform;
            }

            XRInputModalityManager manager = FindInputModalityManager();
            if (manager != null)
            {
                if (left == null && manager.leftController != null)
                    left = manager.leftController.transform;

                if (right == null && manager.rightController != null)
                    right = manager.rightController.transform;
            }

            return left != null || right != null;
        }

        void CacheMissingReferences()
        {
            if (m_XROrigin == null)
                m_XROrigin = GetComponent<XROrigin>();

            if (m_InputModalityManager == null)
                TryGetComponent(out m_InputModalityManager);

            if (m_HeadTransform == null && m_XROrigin != null && m_XROrigin.Camera != null)
                m_HeadTransform = m_XROrigin.Camera.transform;

            if (m_InputModalityManager != null)
            {
                if (m_LeftControllerTransform == null && m_InputModalityManager.leftController != null)
                    m_LeftControllerTransform = m_InputModalityManager.leftController.transform;

                if (m_RightControllerTransform == null && m_InputModalityManager.rightController != null)
                    m_RightControllerTransform = m_InputModalityManager.rightController.transform;
            }
        }

        XROrigin ResolveXROrigin()
        {
            if (m_XROrigin == null)
                m_XROrigin = GetComponent<XROrigin>();

            return m_XROrigin;
        }

        Transform ResolveHeadTransform()
        {
            if (m_HeadTransform == null && xrOrigin != null && xrOrigin.Camera != null)
                m_HeadTransform = xrOrigin.Camera.transform;

            return m_HeadTransform;
        }

        Transform ResolveLeftControllerTransform()
        {
            XRInputModalityManager manager = inputModalityManager;
            if (m_LeftControllerTransform == null && manager != null && manager.leftController != null)
                m_LeftControllerTransform = manager.leftController.transform;

            return m_LeftControllerTransform;
        }

        Transform ResolveRightControllerTransform()
        {
            XRInputModalityManager manager = inputModalityManager;
            if (m_RightControllerTransform == null && manager != null && manager.rightController != null)
                m_RightControllerTransform = manager.rightController.transform;

            return m_RightControllerTransform;
        }

        XRInputModalityManager ResolveInputModalityManager()
        {
            if (m_InputModalityManager == null)
                TryGetComponent(out m_InputModalityManager);

            return m_InputModalityManager;
        }
    }
}
