using System.Collections.Generic;
using Unity.XR.CoreUtils;
using UnityEngine;

namespace ArcadeRoom.Arcade
{
    [DisallowMultipleComponent]
    public sealed class ArcadeTeleportController : MonoBehaviour
    {
        [SerializeField] private XROrigin xrOrigin;
        [SerializeField] private Transform xrOriginRoot;
        [SerializeField] private Transform anchorsRoot;
        [SerializeField] private bool useAnchorY = false;
        [SerializeField] private bool alignToAnchorYaw = true;
        [SerializeField] private List<Transform> teleportAnchors = new();

        private readonly Dictionary<string, Transform> _anchorLookup = new();

        private void Awake()
        {
            ResolveReferences();
            RebuildLookup();
        }

        public void TeleportToName(string anchorName)
        {
            if (string.IsNullOrWhiteSpace(anchorName))
            {
                return;
            }

            if (_anchorLookup.Count == 0)
            {
                ResolveReferences();
                RebuildLookup();
            }

            if (_anchorLookup.TryGetValue(NormalizeName(anchorName), out var anchor))
            {
                TeleportTo(anchor);
            }
        }

        public void TeleportToCarrom()
        {
            TeleportToName("Carrom");
        }

        public void TeleportToCurling()
        {
            TeleportToName("Curling");
        }

        public void TeleportToPutting()
        {
            TeleportToName("Putting");
        }

        public void TeleportToYoTyan()
        {
            TeleportToName("YoTyan");
        }

        public void TeleportToPinball()
        {
            TeleportToName("Pinball");
        }

        public void TeleportTo(Transform anchor)
        {
            if (anchor == null)
            {
                return;
            }

            ResolveReferences();

            if (xrOrigin != null && xrOrigin.Camera != null)
            {
                if (alignToAnchorYaw)
                {
                    AlignCameraYaw(anchor);
                }

                var destination = anchor.position;
                if (!useAnchorY)
                {
                    destination.y = xrOrigin.Camera.transform.position.y;
                }

                xrOrigin.MoveCameraToWorldLocation(destination);
                return;
            }

            var root = xrOriginRoot != null ? xrOriginRoot : transform;
            var rootPosition = root.position;
            rootPosition.x = anchor.position.x;
            rootPosition.z = anchor.position.z;

            if (useAnchorY)
            {
                rootPosition.y = anchor.position.y;
            }

            root.position = rootPosition;

            if (alignToAnchorYaw)
            {
                var euler = root.eulerAngles;
                euler.y = anchor.eulerAngles.y;
                root.eulerAngles = euler;
            }
        }

        private void ResolveReferences()
        {
            if (xrOrigin == null)
            {
                var origins = FindObjectsByType<XROrigin>(FindObjectsInactive.Include, FindObjectsSortMode.None);
                if (origins.Length > 0)
                {
                    xrOrigin = origins[0];
                }
            }

            if (xrOriginRoot == null && xrOrigin != null)
            {
                xrOriginRoot = xrOrigin.Origin != null ? xrOrigin.Origin.transform : xrOrigin.transform;
            }

            if (anchorsRoot == null)
            {
                var foundRoot = GameObject.Find("TeleportAnchors");
                if (foundRoot != null)
                {
                    anchorsRoot = foundRoot.transform;
                }
            }

            if (teleportAnchors.Count == 0 && anchorsRoot != null)
            {
                for (var i = 0; i < anchorsRoot.childCount; i++)
                {
                    var child = anchorsRoot.GetChild(i);
                    if (child != null && child.name.StartsWith("TeleportAnchor_", System.StringComparison.Ordinal))
                    {
                        teleportAnchors.Add(child);
                    }
                }
            }
        }

        private void RebuildLookup()
        {
            _anchorLookup.Clear();

            for (var i = 0; i < teleportAnchors.Count; i++)
            {
                var anchor = teleportAnchors[i];
                if (anchor == null)
                {
                    continue;
                }

                RegisterAnchor(anchor.name, anchor);
                RegisterAnchor(anchor.name.Replace("TeleportAnchor_", string.Empty), anchor);
            }
        }

        private void RegisterAnchor(string anchorName, Transform anchor)
        {
            var key = NormalizeName(anchorName);
            if (!_anchorLookup.ContainsKey(key))
            {
                _anchorLookup.Add(key, anchor);
            }
        }

        private void AlignCameraYaw(Transform anchor)
        {
            var cameraForward = xrOrigin.Camera.transform.forward;
            cameraForward.y = 0f;

            var anchorForward = anchor.forward;
            anchorForward.y = 0f;

            if (cameraForward.sqrMagnitude < 0.0001f || anchorForward.sqrMagnitude < 0.0001f)
            {
                return;
            }

            var yawDelta = Vector3.SignedAngle(cameraForward.normalized, anchorForward.normalized, Vector3.up);
            xrOrigin.RotateAroundCameraUsingOriginUp(yawDelta);
        }

        private static string NormalizeName(string value)
        {
            return value.Replace("TeleportAnchor_", string.Empty)
                .Replace("_", string.Empty)
                .Trim()
                .ToLowerInvariant();
        }
    }
}
