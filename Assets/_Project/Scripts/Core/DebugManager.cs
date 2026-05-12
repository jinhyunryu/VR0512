using UnityEngine;

namespace ArcadeRoom.Core
{
    [DisallowMultipleComponent]
    public class DebugManager : MonoBehaviour
    {
        [SerializeField] private bool enableLogs = true;
        [SerializeField] private bool enableDebugGizmos = false;

        public static DebugManager Instance { get; private set; }

        public bool EnableLogs => enableLogs;
        public bool EnableDebugGizmos => enableDebugGizmos;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                return;
            }

            Instance = this;
        }

        public void Log(string message, Object context = null)
        {
            if (!enableLogs)
            {
                return;
            }

            if (context != null)
            {
                Debug.Log(message, context);
                return;
            }

            Debug.Log(message);
        }

        public void LogWarning(string message, Object context = null)
        {
            if (!enableLogs)
            {
                return;
            }

            if (context != null)
            {
                Debug.LogWarning(message, context);
                return;
            }

            Debug.LogWarning(message);
        }
    }
}
