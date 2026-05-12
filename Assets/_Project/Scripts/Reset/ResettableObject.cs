using ArcadeRoom.Core;
using UnityEngine;

namespace ArcadeRoom.Reset
{
    public class ResettableObject : MonoBehaviour, IArcadeResettable
    {
        [SerializeField] private bool captureOnAwake = true;

        private Transform _initialParent;
        private Vector3 _initialLocalPosition;
        private Quaternion _initialLocalRotation;
        private Vector3 _initialLocalScale;
        private bool _initialActiveState;

        protected virtual void Awake()
        {
            if (captureOnAwake)
            {
                CaptureInitialState();
            }
        }

        public void CaptureInitialState()
        {
            _initialParent = transform.parent;
            _initialLocalPosition = transform.localPosition;
            _initialLocalRotation = transform.localRotation;
            _initialLocalScale = transform.localScale;
            _initialActiveState = gameObject.activeSelf;
        }

        public virtual void ResetState()
        {
            if (_initialParent != transform.parent)
            {
                transform.SetParent(_initialParent, false);
            }

            transform.localPosition = _initialLocalPosition;
            transform.localRotation = _initialLocalRotation;
            transform.localScale = _initialLocalScale;
            gameObject.SetActive(_initialActiveState);
        }
    }
}
