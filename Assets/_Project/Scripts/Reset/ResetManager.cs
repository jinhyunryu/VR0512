using System.Collections.Generic;
using ArcadeRoom.Core;
using ArcadeRoom.Stations;
using UnityEngine;

namespace ArcadeRoom.Reset
{
    [DisallowMultipleComponent]
    public class ResetManager : MonoBehaviour
    {
        [SerializeField] private ArcadeRoomManager arcadeRoomManager;
        [SerializeField] private bool refreshStationsOnAwake = true;

        private readonly List<ArcadeStationRoot> _stations = new();

        public IReadOnlyList<ArcadeStationRoot> Stations => _stations;

        private void Awake()
        {
            ResolveArcadeRoomManager();

            if (refreshStationsOnAwake)
            {
                RefreshStations();
            }
        }

        public void RefreshStations()
        {
            _stations.Clear();

            if (arcadeRoomManager != null && arcadeRoomManager.RegisteredStations.Count > 0)
            {
                for (var i = 0; i < arcadeRoomManager.RegisteredStations.Count; i++)
                {
                    var station = arcadeRoomManager.RegisteredStations[i];
                    if (station != null && !_stations.Contains(station))
                    {
                        _stations.Add(station);
                    }
                }

                return;
            }

            var discoveredStations = FindObjectsByType<ArcadeStationRoot>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            for (var i = 0; i < discoveredStations.Length; i++)
            {
                if (!_stations.Contains(discoveredStations[i]))
                {
                    _stations.Add(discoveredStations[i]);
                }
            }
        }

        public void ResetAllStations()
        {
            if (_stations.Count == 0)
            {
                RefreshStations();
            }

            for (var i = 0; i < _stations.Count; i++)
            {
                _stations[i].ResetState();
            }
        }

        public void ResetStation(ArcadeStationRoot station)
        {
            if (station == null)
            {
                return;
            }

            station.ResetState();
        }

        private void ResolveArcadeRoomManager()
        {
            if (arcadeRoomManager != null)
            {
                return;
            }

            var managers = FindObjectsByType<ArcadeRoomManager>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            if (managers.Length > 0)
            {
                arcadeRoomManager = managers[0];
            }
        }
    }
}
