using ArcadeRoom.Audio;
using UnityEngine;
using UnityEngine.InputSystem;

namespace ArcadeRoom.Pinball
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(HingeJoint))]
    public sealed class PinballFlipper : MonoBehaviour
    {
        private const string DefaultFlipperClipPath = "Assets/_Project/Audio/SFX/Pinball/pinball-flipper-hits.wav";

        private enum DriveMode
        {
            Motor,
            Spring
        }

        [SerializeField] private HingeJoint hinge;
        [SerializeField] private InputActionReference pressAction;
        [SerializeField] private bool pressed;
        [SerializeField] private bool releaseOnEnable = true;
        [SerializeField] private DriveMode driveMode = DriveMode.Motor;
        [Header("Angles")]
        [SerializeField] private float restAngle;
        [SerializeField] private float pressedAngle = 45f;
        [Header("Motor")]
        [SerializeField, Min(0f)] private float pressVelocity = 900f;
        [SerializeField, Min(0f)] private float releaseVelocity = 900f;
        [SerializeField, Min(0f)] private float motorForce = 3500f;
        [Header("Spring")]
        [SerializeField, Min(0f)] private float spring = 6500f;
        [SerializeField, Min(0f)] private float damper = 80f;
        [SerializeField] private bool useLimits = true;
        [Header("SFX")]
        [SerializeField] private AudioClip flipperClip;
        [SerializeField, Range(0f, 1f)] private float flipperVolume = 0.35f;
        [SerializeField] private Vector2 pitchRange = new(0.98f, 1.04f);
        [SerializeField, Range(0f, 1f)] private float spatialBlend = 1f;
        [SerializeField, Min(0.1f)] private float maxDistance = 6f;

        private void Awake()
        {
            ResolveReferences();
            ResolveDefaultSfx();
            ApplyJointSettings();
        }

        private void OnEnable()
        {
            if (releaseOnEnable)
            {
                pressed = false;
            }

            if (pressAction != null && pressAction.action != null)
            {
                pressAction.action.performed += HandlePressed;
                pressAction.action.canceled += HandleReleased;
                pressAction.action.Enable();
            }

            ApplyJointSettings();
        }

        private void OnDisable()
        {
            if (pressAction != null && pressAction.action != null)
            {
                pressAction.action.performed -= HandlePressed;
                pressAction.action.canceled -= HandleReleased;
            }
        }

        private void OnValidate()
        {
            ResolveReferences();
            pitchRange = ArcadeSfxPlayer.NormalizePitchRange(pitchRange);
            maxDistance = Mathf.Max(0.1f, maxDistance);
            ResolveDefaultSfx();
            ApplyJointSettings();
        }

        public void Press()
        {
            if (!pressed)
            {
                PlayFlipperSfx();
            }

            pressed = true;
            ApplyJointSettings();
        }

        public void Release()
        {
            pressed = false;
            ApplyJointSettings();
        }

        public void SetPressed(bool value)
        {
            pressed = value;
            ApplyJointSettings();
        }

        private void HandlePressed(InputAction.CallbackContext context)
        {
            Press();
        }

        private void HandleReleased(InputAction.CallbackContext context)
        {
            Release();
        }

        private void ApplyJointSettings()
        {
            if (hinge == null)
            {
                return;
            }

            ApplyLimits();

            if (driveMode == DriveMode.Motor)
            {
                ApplyMotor();
                return;
            }

            ApplySpring();
        }

        private void ApplyMotor()
        {
            hinge.useSpring = false;
            hinge.useMotor = true;

            var direction = Mathf.Sign(pressedAngle - restAngle);
            if (Mathf.Approximately(direction, 0f))
            {
                direction = 1f;
            }

            var motor = hinge.motor;
            motor.force = motorForce;
            motor.freeSpin = false;
            motor.targetVelocity = pressed ? pressVelocity * direction : -releaseVelocity * direction;
            hinge.motor = motor;
        }

        private void ApplySpring()
        {
            hinge.useMotor = false;
            hinge.useSpring = true;

            var jointSpring = hinge.spring;
            jointSpring.spring = spring;
            jointSpring.damper = damper;
            jointSpring.targetPosition = pressed ? pressedAngle : restAngle;
            hinge.spring = jointSpring;
        }

        private void ApplyLimits()
        {
            if (!useLimits)
            {
                hinge.useLimits = false;
                return;
            }

            var min = Mathf.Min(restAngle, pressedAngle);
            var max = Mathf.Max(restAngle, pressedAngle);
            var limits = hinge.limits;
            limits.min = min;
            limits.max = max;
            hinge.limits = limits;
            hinge.useLimits = true;
        }

        private void ResolveReferences()
        {
            if (hinge == null)
            {
                hinge = GetComponent<HingeJoint>();
            }
        }

        private void PlayFlipperSfx()
        {
            ArcadeSfxPlayer.PlayOneShot(
                flipperClip,
                transform.position,
                flipperVolume,
                ArcadeSfxPlayer.RandomPitch(pitchRange),
                spatialBlend,
                maxDistance,
                "Pinball_SFX_Flipper");
        }

        private void ResolveDefaultSfx()
        {
            if (flipperClip == null)
            {
                flipperClip = ArcadeSfxPlayer.LoadEditorClip(DefaultFlipperClipPath);
            }
        }
    }
}
