using System.Collections.Generic;
using Unity.XR.CoreUtils.Bindings.Variables;
using UnityEngine;

namespace XRMultiplayer
{
    public enum AudioFadeModel
    {
        InverseByDistance,
        LinearByDistance,
        ExponentialByDistance
    }

    /// <summary>
    /// No-op replacement for the VR Multiplayer Sample voice manager.
    /// Voice chat is intentionally disabled for this arcade prototype.
    /// </summary>
    public class VoiceChatManager : MonoBehaviour
    {
        public static BindableVariable<bool> s_HasMicrophonePermission = new(false);
        public static Dictionary<string, XRINetworkPlayer> m_PlayersDictionary = new();

        public IReadOnlyBindableVariable<bool> selfMuted => m_SelfMuted;
        public IReadOnlyBindableVariable<string> connectionStatus => m_ConnectionStatus;

        readonly BindableVariable<bool> m_SelfMuted = new(true);
        readonly BindableVariable<string> m_ConnectionStatus = new("Voice chat disabled");

        [Header("Voice Chat Disabled")]
        [SerializeField] int m_AudibleDistance = 32;
        [SerializeField] int m_ConversationalDistance = 7;
        [SerializeField] float m_AudioFadeIntensity = 1f;
        [SerializeField] AudioFadeModel m_AudioFadeModel = AudioFadeModel.LinearByDistance;

        public int AudibleDistance
        {
            get => m_AudibleDistance;
            set => m_AudibleDistance = value;
        }

        public int ConversationalDistance
        {
            get => m_ConversationalDistance;
            set => m_ConversationalDistance = value;
        }

        public float AudioFadeIntensity
        {
            get => m_AudioFadeIntensity;
            set => m_AudioFadeIntensity = value;
        }

        public AudioFadeModel AudioFadeModel
        {
            get => m_AudioFadeModel;
            set => m_AudioFadeModel = value;
        }

        private void Awake()
        {
            s_HasMicrophonePermission.Value = false;
            m_SelfMuted.Value = true;
            m_ConnectionStatus.Value = "Voice chat disabled";
        }

        public void ToggleSelfMute(bool setManual = false, bool mutedOverrideValue = false)
        {
            m_SelfMuted.Value = setManual ? mutedOverrideValue : !m_SelfMuted.Value;
        }

        public void SetInputVolume(float volume)
        {
        }

        public void SetOutputVolume(float volume)
        {
        }

        public void Set3DAudio(Transform localPlayerHeadTransform)
        {
        }

        public static void AddNewVivoxPlayer(string participantID, XRINetworkPlayer networkPlayer)
        {
            if (!string.IsNullOrEmpty(participantID) && networkPlayer != null)
            {
                m_PlayersDictionary[participantID] = networkPlayer;
            }
        }

        public static void RemoveVivoxPlayer(string participantID)
        {
            if (!string.IsNullOrEmpty(participantID))
            {
                m_PlayersDictionary.Remove(participantID);
            }
        }
    }
}
