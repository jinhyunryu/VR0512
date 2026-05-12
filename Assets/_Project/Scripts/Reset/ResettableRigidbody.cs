using ArcadeRoom.Core;
using UnityEngine;

namespace ArcadeRoom.Reset
{
    [RequireComponent(typeof(Rigidbody))]
    public class ResettableRigidbody : MonoBehaviour, IArcadeResettable
    {
        [SerializeField] private bool captureOnAwake = true;

        private Rigidbody _rigidbody;
        private Transform _initialParent;
        private Vector3 _initialLocalPosition;
        private Quaternion _initialLocalRotation;
        private bool _initialActiveState;
        private bool _initialUseGravity;
        private bool _initialIsKinematic;

        private void Awake()
        {
            _rigidbody = GetComponent<Rigidbody>();

            if (captureOnAwake)
            {
                CaptureInitialState();
            }
        }

        public void CaptureInitialState()
        {
            if (_rigidbody == null)
            {
                _rigidbody = GetComponent<Rigidbody>();
            }

            _initialParent = transform.parent;
            _initialLocalPosition = transform.localPosition;
            _initialLocalRotation = transform.localRotation;
            _initialActiveState = gameObject.activeSelf;
            _initialUseGravity = _rigidbody.useGravity;
            _initialIsKinematic = _rigidbody.isKinematic;
        }

        public void ResetState()
        {
            if (_rigidbody == null)
            {
                _rigidbody = GetComponent<Rigidbody>();
            }

            if (_initialParent != transform.parent)
            {
                transform.SetParent(_initialParent, false);
            }

            gameObject.SetActive(_initialActiveState);
            transform.localPosition = _initialLocalPosition;
            transform.localRotation = _initialLocalRotation;

            _rigidbody.linearVelocity = Vector3.zero;
            _rigidbody.angularVelocity = Vector3.zero;
            _rigidbody.useGravity = _initialUseGravity;
            _rigidbody.isKinematic = _initialIsKinematic;
            _rigidbody.Sleep();
        }
    }
}
