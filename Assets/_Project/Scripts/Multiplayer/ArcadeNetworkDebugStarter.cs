using System;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEngine;
using UnityEngine.InputSystem;

namespace XRMultiplayer
{
    /// <summary>
    /// Lightweight local network starter for early rig/avatar tests.
    /// Replace or disable this once the real lobby UI is connected.
    /// </summary>
    public sealed class ArcadeNetworkDebugStarter : MonoBehaviour
    {
        [Header("Connection")]
        [SerializeField] bool m_UseXriNetworkGameManager = true;
        [SerializeField] string m_ClientAddress = "127.0.0.1";
        [SerializeField] ushort m_Port = 7777;

        [Header("Keyboard Shortcuts")]
        [SerializeField] bool m_EnableKeyboardShortcuts = true;
        [SerializeField] KeyCode m_StartHostKey = KeyCode.H;
        [SerializeField] KeyCode m_StartClientKey = KeyCode.C;
        [SerializeField] KeyCode m_ShutdownKey = KeyCode.S;

        [Header("Runtime GUI")]
        [SerializeField] bool m_ShowRuntimeGui = true;
        [SerializeField] Vector2 m_GuiPosition = new Vector2(12f, 12f);
        [SerializeField] Vector2 m_GuiSize = new Vector2(300f, 210f);

        XRINetworkGameManager m_GameManager;
        string m_AddressInput;
        string m_PortInput;
        string m_LastNetworkEvent = "No network event yet.";
        NetworkManager m_SubscribedNetworkManager;

        void Awake()
        {
            m_AddressInput = string.IsNullOrWhiteSpace(m_ClientAddress) ? "127.0.0.1" : m_ClientAddress;
            m_PortInput = m_Port.ToString();
            ResolveGameManager();
            TrySubscribeNetworkEvents();
        }

        void OnDestroy()
        {
            UnsubscribeNetworkEvents();
        }

        void Update()
        {
            TrySubscribeNetworkEvents();

            if (!m_EnableKeyboardShortcuts)
                return;

            if (WasKeyPressedThisFrame(m_StartHostKey))
                StartHost();

            if (WasKeyPressedThisFrame(m_StartClientKey))
                StartClient();

            if (WasKeyPressedThisFrame(m_ShutdownKey))
                Shutdown();
        }

        void OnGUI()
        {
            if (!m_ShowRuntimeGui)
                return;

            Rect area = new Rect(m_GuiPosition.x, m_GuiPosition.y, m_GuiSize.x, m_GuiSize.y);
            GUILayout.BeginArea(area, GUI.skin.box);
            GUILayout.Label("Network Debug Starter");
            GUILayout.Label(GetStatusText());
            GUILayout.Label(m_LastNetworkEvent);
            GUILayout.Space(4f);

            GUILayout.Label("Host IP for Client");
            m_AddressInput = GUILayout.TextField(m_AddressInput ?? string.Empty);

            GUILayout.Label("Port");
            m_PortInput = GUILayout.TextField(m_PortInput ?? string.Empty);
            GUILayout.Space(4f);

            GUILayout.BeginHorizontal();
            if (GUILayout.Button($"Host ({m_StartHostKey})"))
                StartHost();

            if (GUILayout.Button($"Client ({m_StartClientKey})"))
                StartClient();
            GUILayout.EndHorizontal();

            if (GUILayout.Button($"Shutdown ({m_ShutdownKey})"))
                Shutdown();

            GUILayout.Label("Host PC: press Host. Other PC: enter Host IP, press Client.");

            GUILayout.EndArea();
        }

        [ContextMenu("Start Host")]
        public void StartHost()
        {
            if (!CanStartConnection())
                return;

            ApplyTransportSettings();

            bool started = false;
            XRINetworkGameManager gameManager = ResolveGameManager();
            if (m_UseXriNetworkGameManager && gameManager != null)
                started = gameManager.HostLocalConnection();
            else
                started = NetworkManager.Singleton.StartHost();

            Debug.Log(started ? "[NetworkDebugStarter] Host started." : "[NetworkDebugStarter] Failed to start host.");
            m_LastNetworkEvent = started ? "Host start requested." : "Host start failed.";
        }

        [ContextMenu("Start Client")]
        public void StartClient()
        {
            if (!CanStartConnection())
                return;

            ApplyTransportSettings();

            bool started = false;
            XRINetworkGameManager gameManager = ResolveGameManager();
            if (m_UseXriNetworkGameManager && gameManager != null)
                started = gameManager.JoinLocalConnection();
            else
                started = NetworkManager.Singleton.StartClient();

            Debug.Log(started ? "[NetworkDebugStarter] Client started." : "[NetworkDebugStarter] Failed to start client.");
            m_LastNetworkEvent = started ? $"Client start requested: {m_ClientAddress}:{m_Port}" : "Client start failed.";
        }

        [ContextMenu("Shutdown")]
        public void Shutdown()
        {
            if (NetworkManager.Singleton == null || !NetworkManager.Singleton.IsListening)
                return;

            XRINetworkGameManager gameManager = ResolveGameManager();
            if (m_UseXriNetworkGameManager && gameManager != null)
                gameManager.Disconnect();
            else
                NetworkManager.Singleton.Shutdown();

            Debug.Log("[NetworkDebugStarter] Network shutdown requested.");
            m_LastNetworkEvent = "Shutdown requested.";
        }

        bool CanStartConnection()
        {
            if (NetworkManager.Singleton == null)
            {
                Debug.LogWarning("[NetworkDebugStarter] No NetworkManager found in the scene.");
                return false;
            }

            if (NetworkManager.Singleton.IsListening)
            {
                Debug.LogWarning("[NetworkDebugStarter] Network is already running. Shutdown first if you want to restart.");
                return false;
            }

            return true;
        }

        XRINetworkGameManager ResolveGameManager()
        {
            if (m_GameManager == null)
                m_GameManager = XRINetworkGameManager.Instance != null
                    ? XRINetworkGameManager.Instance
                    : FindFirstObjectByType<XRINetworkGameManager>();

            return m_GameManager;
        }

        void ApplyTransportSettings()
        {
            if (NetworkManager.Singleton == null)
                return;

            if (NetworkManager.Singleton.NetworkConfig.NetworkTransport is not UnityTransport transport)
                return;

            m_ClientAddress = string.IsNullOrWhiteSpace(m_AddressInput) ? "127.0.0.1" : m_AddressInput.Trim();
            if (ushort.TryParse(m_PortInput, out ushort parsedPort))
                m_Port = parsedPort;

            transport.ConnectionData.Port = m_Port;
            transport.ConnectionData.ServerListenAddress = "0.0.0.0";
            transport.ConnectionData.Address = m_ClientAddress;

            Debug.Log($"[NetworkDebugStarter] Transport target {m_ClientAddress}:{m_Port}");
        }

        string GetStatusText()
        {
            NetworkManager networkManager = NetworkManager.Singleton;
            if (networkManager == null)
                return "Status: No NetworkManager";

            if (!networkManager.IsListening)
                return "Status: Offline";

            if (networkManager.IsHost)
                return $"Status: Host / ClientId {networkManager.LocalClientId} / Clients {networkManager.ConnectedClientsIds.Count}";

            if (networkManager.IsClient)
            {
                string connectionState = networkManager.IsConnectedClient ? "Connected" : "Pending";
                return $"Status: Client {connectionState} / ClientId {networkManager.LocalClientId}";
            }

            if (networkManager.IsServer)
                return $"Status: Server / Clients {networkManager.ConnectedClientsIds.Count}";

            return "Status: Listening";
        }

        void TrySubscribeNetworkEvents()
        {
            NetworkManager networkManager = NetworkManager.Singleton;
            if (networkManager == null || m_SubscribedNetworkManager == networkManager)
                return;

            UnsubscribeNetworkEvents();
            m_SubscribedNetworkManager = networkManager;
            m_SubscribedNetworkManager.OnClientConnectedCallback += OnClientConnected;
            m_SubscribedNetworkManager.OnClientDisconnectCallback += OnClientDisconnected;
        }

        void UnsubscribeNetworkEvents()
        {
            if (m_SubscribedNetworkManager == null)
                return;

            m_SubscribedNetworkManager.OnClientConnectedCallback -= OnClientConnected;
            m_SubscribedNetworkManager.OnClientDisconnectCallback -= OnClientDisconnected;
            m_SubscribedNetworkManager = null;
        }

        void OnClientConnected(ulong clientId)
        {
            m_LastNetworkEvent = $"Client connected: {clientId}";
            Debug.Log($"[NetworkDebugStarter] Client connected: {clientId}");
        }

        void OnClientDisconnected(ulong clientId)
        {
            m_LastNetworkEvent = $"Client disconnected: {clientId}";
            Debug.Log($"[NetworkDebugStarter] Client disconnected: {clientId}");
        }

        static bool WasKeyPressedThisFrame(KeyCode keyCode)
        {
            Keyboard keyboard = Keyboard.current;
            if (keyboard == null)
                return false;

            return TryConvertKeyCode(keyCode, out Key key) && keyboard[key].wasPressedThisFrame;
        }

        static bool TryConvertKeyCode(KeyCode keyCode, out Key key)
        {
            switch (keyCode)
            {
                case KeyCode.Alpha0:
                    key = Key.Digit0;
                    return true;
                case KeyCode.Alpha1:
                    key = Key.Digit1;
                    return true;
                case KeyCode.Alpha2:
                    key = Key.Digit2;
                    return true;
                case KeyCode.Alpha3:
                    key = Key.Digit3;
                    return true;
                case KeyCode.Alpha4:
                    key = Key.Digit4;
                    return true;
                case KeyCode.Alpha5:
                    key = Key.Digit5;
                    return true;
                case KeyCode.Alpha6:
                    key = Key.Digit6;
                    return true;
                case KeyCode.Alpha7:
                    key = Key.Digit7;
                    return true;
                case KeyCode.Alpha8:
                    key = Key.Digit8;
                    return true;
                case KeyCode.Alpha9:
                    key = Key.Digit9;
                    return true;
                default:
                    return Enum.TryParse(keyCode.ToString(), true, out key);
            }
        }
    }
}
