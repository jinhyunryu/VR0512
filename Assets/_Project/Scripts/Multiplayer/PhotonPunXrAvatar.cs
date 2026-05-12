#if PHOTON_UNITY_NETWORKING || PUN_2_OR_NEWER
using Photon.Pun;
using UnityEngine;

namespace ArcadeRoom.Multiplayer
{
    [DisallowMultipleComponent]
    public sealed class PhotonPunXrAvatar : MonoBehaviourPun, IPunObservable
    {
        [Header("Local XR Targets")]
        [SerializeField] private Transform localHead;
        [SerializeField] private Transform localLeftHand;
        [SerializeField] private Transform localRightHand;

        [Header("Network Visuals")]
        [SerializeField] private Transform headVisual;
        [SerializeField] private Transform leftHandVisual;
        [SerializeField] private Transform rightHandVisual;
        [SerializeField, Range(1f, 30f)] private float remoteLerpSpeed = 18f;

        private Pose _remoteHead;
        private Pose _remoteLeftHand;
        private Pose _remoteRightHand;
        private bool _hasRemotePose;

        private void LateUpdate()
        {
            if (photonView.IsMine)
            {
                CopyLocalPose();
                return;
            }

            ApplyRemotePose();
        }

        public void OnPhotonSerializeView(PhotonStream stream, PhotonMessageInfo info)
        {
            if (stream.IsWriting)
            {
                WriteTransform(stream, headVisual);
                WriteTransform(stream, leftHandVisual);
                WriteTransform(stream, rightHandVisual);
                return;
            }

            _remoteHead = ReadPose(stream);
            _remoteLeftHand = ReadPose(stream);
            _remoteRightHand = ReadPose(stream);
            _hasRemotePose = true;
        }

        private void CopyLocalPose()
        {
            CopyTransform(localHead, headVisual);
            CopyTransform(localLeftHand, leftHandVisual);
            CopyTransform(localRightHand, rightHandVisual);
        }

        private void ApplyRemotePose()
        {
            if (!_hasRemotePose)
            {
                return;
            }

            ApplyPose(headVisual, _remoteHead);
            ApplyPose(leftHandVisual, _remoteLeftHand);
            ApplyPose(rightHandVisual, _remoteRightHand);
        }

        private void ApplyPose(Transform target, Pose pose)
        {
            if (target == null)
            {
                return;
            }

            var t = 1f - Mathf.Exp(-remoteLerpSpeed * Time.deltaTime);
            target.SetPositionAndRotation(
                Vector3.Lerp(target.position, pose.position, t),
                Quaternion.Slerp(target.rotation, pose.rotation, t));
        }

        private static void CopyTransform(Transform source, Transform target)
        {
            if (source == null || target == null)
            {
                return;
            }

            target.SetPositionAndRotation(source.position, source.rotation);
        }

        private static void WriteTransform(PhotonStream stream, Transform target)
        {
            stream.SendNext(target != null ? target.position : Vector3.zero);
            stream.SendNext(target != null ? target.rotation : Quaternion.identity);
        }

        private static Pose ReadPose(PhotonStream stream)
        {
            return new Pose(
                (Vector3)stream.ReceiveNext(),
                (Quaternion)stream.ReceiveNext());
        }
    }
}
#endif
