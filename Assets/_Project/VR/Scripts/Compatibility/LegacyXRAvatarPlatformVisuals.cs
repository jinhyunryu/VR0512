using UnityEngine;

namespace XRMultiplayer
{
    /// <summary>
    /// Compatibility shim for an optional VR Multiplayer Sample avatar visual component.
    /// The prototype does not need platform-specific HMD color changes, but keeping this
    /// script mapped to the imported GUID prevents missing-script warnings on the avatar.
    /// </summary>
    public sealed class LegacyXRAvatarPlatformVisuals : MonoBehaviour
    {
        [SerializeField] Renderer m_HMDRenderer;
        [SerializeField] Color m_QuestColor = Color.white;
        [SerializeField] Color m_AndroidColor = Color.white;
        [SerializeField] Color m_OtherColor = Color.white;
    }
}
