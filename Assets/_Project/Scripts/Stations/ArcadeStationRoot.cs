using System.Collections.Generic;
using ArcadeRoom.Core;
using UnityEngine;

namespace ArcadeRoom.Stations
{
    [DisallowMultipleComponent]
    public class ArcadeStationRoot : MonoBehaviour, IArcadeResettable
    {
        [SerializeField] private string stationId;
        [SerializeField] private Transform stationRoot;
        [SerializeField] private Transform startAnchor;
        [SerializeField] private Transform resetButton;
        [SerializeField] private Transform hintPanel;
        [SerializeField] private Transform gameplayRoot;
        [SerializeField] private Transform spawnRoot;
        [SerializeField] private Transform boundsRoot;
        [SerializeField] private bool collectChildrenOnAwake = true;

        private readonly List<IArcadeResettable> _cachedResettables = new();

        public string StationId => string.IsNullOrWhiteSpace(stationId) ? gameObject.name : stationId;
        public Transform StationRootTransform => stationRoot;
        public Transform StartAnchor => startAnchor;
        public Transform ResetButton => resetButton;
        public Transform HintPanel => hintPanel;
        public Transform GameplayRoot => gameplayRoot;
        public Transform SpawnRoot => spawnRoot;
        public Transform BoundsRoot => boundsRoot;

        protected virtual void Awake()
        {
            CacheNamedReferences();

            if (collectChildrenOnAwake)
            {
                RefreshResettables();
            }
        }

        protected virtual void OnValidate()
        {
            CacheNamedReferences();
        }

        public virtual void ResetState()
        {
            if (_cachedResettables.Count == 0)
            {
                RefreshResettables();
            }

            for (var i = 0; i < _cachedResettables.Count; i++)
            {
                _cachedResettables[i].ResetState();
            }
        }

        public void RefreshResettables()
        {
            _cachedResettables.Clear();

            var behaviours = GetComponentsInChildren<MonoBehaviour>(true);
            for (var i = 0; i < behaviours.Length; i++)
            {
                var behaviour = behaviours[i];
                if (ReferenceEquals(behaviour, this))
                {
                    continue;
                }

                if (behaviour is IArcadeResettable resettable)
                {
                    _cachedResettables.Add(resettable);
                }
            }
        }

        protected T FindChildComponent<T>() where T : Component
        {
            return GetComponentInChildren<T>(true);
        }

        private void CacheNamedReferences()
        {
            stationRoot = ResolveChild(stationRoot, "StationRoot");
            startAnchor = ResolveChild(startAnchor, "StartAnchor");
            resetButton = ResolveChild(resetButton, "ResetButton");
            hintPanel = ResolveChild(hintPanel, "HintPanel");
            gameplayRoot = ResolveChild(gameplayRoot, "GameplayRoot");
            spawnRoot = ResolveChild(spawnRoot, "SpawnRoot");
            boundsRoot = ResolveChild(boundsRoot, "Bounds");
        }

        private Transform ResolveChild(Transform existing, string childName)
        {
            if (existing != null)
            {
                return existing;
            }

            var child = transform.Find(childName);
            if (child != null)
            {
                return child;
            }

            var station = transform.Find("StationRoot");
            if (station == null)
            {
                return null;
            }

            return station.Find(childName);
        }
    }
}
