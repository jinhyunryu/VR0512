using MikeNspired.XRIStarterKit;
using UnityEngine;

namespace ArcadeRoom.Pinball
{
    [DisallowMultipleComponent]
    public sealed class PinballFlipperButton : MonoBehaviour
    {
        [SerializeField] private XRPushButton pushButton;
        [SerializeField] private PinballFlipper[] targetFlippers;

        private void Awake()
        {
            ResolveReferences();
        }

        private void OnEnable()
        {
            RegisterCallbacks();
        }

        private void OnDisable()
        {
            UnregisterCallbacks();
            ReleaseFlippers();
        }

        private void OnValidate()
        {
            ResolveReferences();
        }

        public void PressFlippers()
        {
            if (targetFlippers == null)
            {
                return;
            }

            foreach (var flipper in targetFlippers)
            {
                if (flipper != null)
                {
                    flipper.Press();
                }
            }
        }

        public void ReleaseFlippers()
        {
            if (targetFlippers == null)
            {
                return;
            }

            foreach (var flipper in targetFlippers)
            {
                if (flipper != null)
                {
                    flipper.Release();
                }
            }
        }

        private void RegisterCallbacks()
        {
            if (pushButton == null)
            {
                return;
            }

            pushButton.OnPress.RemoveListener(PressFlippers);
            pushButton.OnRelease.RemoveListener(ReleaseFlippers);
            pushButton.OnPress.AddListener(PressFlippers);
            pushButton.OnRelease.AddListener(ReleaseFlippers);
        }

        private void UnregisterCallbacks()
        {
            if (pushButton == null)
            {
                return;
            }

            pushButton.OnPress.RemoveListener(PressFlippers);
            pushButton.OnRelease.RemoveListener(ReleaseFlippers);
        }

        private void ResolveReferences()
        {
            if (pushButton == null)
            {
                pushButton = GetComponent<XRPushButton>();
            }
        }
    }
}
