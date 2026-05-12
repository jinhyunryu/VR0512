using UnityEngine;
using UnityEngine.Events;

namespace XRMultiplayer
{
    /// <summary>
    /// Compatibility shim for imported name-tag gaze events from the VR Multiplayer Sample.
    /// It is intentionally lightweight because gaze-based name-tag popouts are not required
    /// for the current arcade multiplayer rig test.
    /// </summary>
    public sealed class LegacyCalloutGazeController : MonoBehaviour
    {
        [SerializeField] Transform m_GazeTransform;
        [SerializeField, Range(0f, 1f)] float m_FacingThreshold = 0.95f;
        [SerializeField] UnityEvent m_FacingEntered;
        [SerializeField] UnityEvent m_FacingExited;
    }
}
