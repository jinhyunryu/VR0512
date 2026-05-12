using Unity.Netcode;
using UnityEngine;

namespace XRMultiplayer
{
    /// <summary>
    /// Lightweight fallback visuals for the network avatar while the imported sample
    /// materials are being normalized for this project.
    /// </summary>
    public sealed class ArcadeNetworkAvatarDebugVisuals : MonoBehaviour
    {
        [Header("Visibility")]
        [SerializeField] bool m_ShowForOwner;
        [SerializeField] bool m_HideOriginalRemoteRenderers = true;

        [Header("Visual Size")]
        [SerializeField] float m_HeadRadius = 0.13f;
        [SerializeField] float m_HandRadius = 0.055f;

        [Header("Visual Offset")]
        [SerializeField] Vector3 m_HeadLocalOffset;
        [SerializeField] Vector3 m_LeftHandLocalOffset;
        [SerializeField] Vector3 m_RightHandLocalOffset;

        [Header("Visual Color")]
        [SerializeField] Color m_HeadColor = new(0.18f, 0.75f, 1f, 1f);
        [SerializeField] Color m_LeftHandColor = new(1f, 0.55f, 0.28f, 1f);
        [SerializeField] Color m_RightHandColor = new(0.28f, 0.62f, 1f, 1f);

        XRINetworkPlayer m_NetworkPlayer;
        Material m_HeadMaterial;
        Material m_LeftHandMaterial;
        Material m_RightHandMaterial;
        GameObject m_HeadVisual;
        GameObject m_LeftHandVisual;
        GameObject m_RightHandVisual;
        NetworkObject m_NetworkObject;
        bool m_VisualsCreated;

        void Awake()
        {
            m_NetworkObject = GetComponent<NetworkObject>();
        }

        void Update()
        {
            if (m_VisualsCreated)
                return;

            if (m_NetworkObject != null)
            {
                if (!m_NetworkObject.IsSpawned)
                    return;

                if (m_NetworkObject.IsOwner && !m_ShowForOwner)
                {
                    enabled = false;
                    return;
                }
            }

            CreateVisuals();
        }

        void OnDestroy()
        {
            DestroyVisuals();
        }

        void CreateVisuals()
        {
            if (m_VisualsCreated)
                return;

            m_NetworkPlayer = GetComponent<XRINetworkPlayer>();
            if (m_NetworkPlayer == null)
                return;

            if (m_HideOriginalRemoteRenderers)
                HideOriginalRenderers();

            m_HeadMaterial = CreateMaterial(m_HeadColor);
            m_LeftHandMaterial = CreateMaterial(m_LeftHandColor);
            m_RightHandMaterial = CreateMaterial(m_RightHandColor);

            m_HeadVisual = CreateSphereVisual("Network Debug Head", m_NetworkPlayer.head, m_HeadRadius, m_HeadLocalOffset, m_HeadMaterial);
            m_LeftHandVisual = CreateSphereVisual("Network Debug Left Hand", m_NetworkPlayer.leftHand, m_HandRadius, m_LeftHandLocalOffset, m_LeftHandMaterial);
            m_RightHandVisual = CreateSphereVisual("Network Debug Right Hand", m_NetworkPlayer.rightHand, m_HandRadius, m_RightHandLocalOffset, m_RightHandMaterial);

            m_VisualsCreated = true;
        }

        void HideOriginalRenderers()
        {
            foreach (var rend in GetComponentsInChildren<Renderer>(true))
            {
                if (rend != null)
                    rend.enabled = false;
            }
        }

        static GameObject CreateSphereVisual(string objectName, Transform parent, float radius, Vector3 localOffset, Material material)
        {
            if (parent == null)
                return null;

            var visual = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            visual.name = objectName;
            visual.transform.SetParent(parent, false);
            visual.transform.localPosition = localOffset;
            visual.transform.localRotation = Quaternion.identity;
            visual.transform.localScale = Vector3.one * (radius * 2f);

            if (visual.TryGetComponent(out Collider visualCollider))
                Destroy(visualCollider);

            if (visual.TryGetComponent(out Renderer renderer))
                renderer.sharedMaterial = material;

            return visual;
        }

        static Material CreateMaterial(Color color)
        {
            var shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null)
                shader = Shader.Find("Standard");

            var material = new Material(shader)
            {
                color = color
            };

            if (material.HasProperty("_BaseColor"))
                material.SetColor("_BaseColor", color);

            if (material.HasProperty("_Color"))
                material.SetColor("_Color", color);

            return material;
        }

        void DestroyVisuals()
        {
            DestroyUnityObject(m_HeadVisual);
            DestroyUnityObject(m_LeftHandVisual);
            DestroyUnityObject(m_RightHandVisual);
            DestroyUnityObject(m_HeadMaterial);
            DestroyUnityObject(m_LeftHandMaterial);
            DestroyUnityObject(m_RightHandMaterial);

            m_HeadVisual = null;
            m_LeftHandVisual = null;
            m_RightHandVisual = null;
            m_HeadMaterial = null;
            m_LeftHandMaterial = null;
            m_RightHandMaterial = null;
            m_VisualsCreated = false;
        }

        static void DestroyUnityObject(Object target)
        {
            if (target == null)
                return;

            if (Application.isPlaying)
                Destroy(target);
            else
                DestroyImmediate(target);
        }
    }
}
