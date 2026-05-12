using System.Collections.Generic;
using ArcadeRoom.Reset;
using ArcadeRoom.Stations;
using UnityEngine;

namespace ArcadeRoom.Core
{
    [DisallowMultipleComponent]
    public class ArcadeRoomManager : MonoBehaviour
    {
        [SerializeField] private ResetManager resetManager;
        [SerializeField] private DebugManager debugManager;
        [SerializeField] private List<ArcadeStationRoot> registeredStations = new();

        public ResetManager ResetManager => resetManager;
        public DebugManager DebugManager => debugManager;
        public IReadOnlyList<ArcadeStationRoot> RegisteredStations => registeredStations;

        private void Awake()
        {
            ResolveDependencies();
            AutoRegisterStationsIfNeeded();
        }

        public void RegisterStation(ArcadeStationRoot station)
        {
            if (station == null || registeredStations.Contains(station))
            {
                return;
            }

            registeredStations.Add(station);
        }

        public void ResetAllStations()
        {
            if (resetManager == null)
            {
                ResolveDependencies();
            }

            resetManager?.ResetAllStations();
        }

        private void ResolveDependencies()
        {
            if (resetManager == null)
            {
                var resetManagers = FindObjectsByType<ResetManager>(FindObjectsInactive.Include, FindObjectsSortMode.None);
                if (resetManagers.Length > 0)
                {
                    resetManager = resetManagers[0];
                }
            }

            if (debugManager == null)
            {
                var debugManagers = FindObjectsByType<DebugManager>(FindObjectsInactive.Include, FindObjectsSortMode.None);
                if (debugManagers.Length > 0)
                {
                    debugManager = debugManagers[0];
                }
            }
        }

        private void AutoRegisterStationsIfNeeded()
        {
            if (registeredStations.Count > 0)
            {
                return;
            }

            var discoveredStations = FindObjectsByType<ArcadeStationRoot>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            for (var i = 0; i < discoveredStations.Length; i++)
            {
                RegisterStation(discoveredStations[i]);
            }
        }
    }
}
