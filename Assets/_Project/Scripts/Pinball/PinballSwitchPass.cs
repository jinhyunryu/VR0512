using System;
using System.Collections.Generic;
using ArcadeRoom.Audio;
using UnityEngine;
using UnityEngine.Events;

namespace ArcadeRoom.Pinball
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Collider))]
    public sealed class PinballSwitchPass : MonoBehaviour
    {
        private const string DefaultSwitchClipPath = "Assets/_Project/Audio/SFX/Pinball/Pinball Ball Hit.mp3";

        [SerializeField] private bool forceTrigger = true;
        [SerializeField, Min(0f)] private float perBallCooldown = 0.15f;
        [SerializeField] private UnityEvent onPassed = new UnityEvent();
        [SerializeField] private PinballBallUnityEvent onBallPassed = new PinballBallUnityEvent();
        [Header("SFX")]
        [SerializeField] private AudioClip passClip;
        [SerializeField, Range(0f, 1f)] private float passVolume = 0.45f;
        [SerializeField] private Vector2 pitchRange = new(0.96f, 1.06f);
        [SerializeField, Range(0f, 1f)] private float spatialBlend = 1f;
        [SerializeField, Min(0.1f)] private float maxDistance = 7f;

        private readonly Dictionary<int, float> _lastPassedTimeByBall = new();

        public event Action<PinballBall> BallPassed;

        private void Awake()
        {
            ApplyColliderSettings();
            ResolveDefaultSfx();
        }

        private void OnValidate()
        {
            ApplyColliderSettings();
            pitchRange = ArcadeSfxPlayer.NormalizePitchRange(pitchRange);
            maxDistance = Mathf.Max(0.1f, maxDistance);
            ResolveDefaultSfx();
        }

        private void OnTriggerEnter(Collider other)
        {
            var ball = other.GetComponentInParent<PinballBall>();
            if (ball == null)
            {
                return;
            }

            var ballId = ball.GetInstanceID();
            if (_lastPassedTimeByBall.TryGetValue(ballId, out var lastTime) &&
                Time.time - lastTime < perBallCooldown)
            {
                return;
            }

            _lastPassedTimeByBall[ballId] = Time.time;
            ArcadeSfxPlayer.PlayOneShot(
                passClip,
                ball.transform.position,
                passVolume,
                ArcadeSfxPlayer.RandomPitch(pitchRange),
                spatialBlend,
                maxDistance,
                "Pinball_SFX_Switch");
            onPassed?.Invoke();
            onBallPassed?.Invoke(ball);
            BallPassed?.Invoke(ball);
        }

        private void ApplyColliderSettings()
        {
            if (!forceTrigger)
            {
                return;
            }

            var targetColliders = GetComponents<Collider>();
            for (var i = 0; i < targetColliders.Length; i++)
            {
                if (targetColliders[i] != null)
                {
                    targetColliders[i].isTrigger = true;
                }
            }
        }

        private void ResolveDefaultSfx()
        {
            if (passClip == null)
            {
                passClip = ArcadeSfxPlayer.LoadEditorClip(DefaultSwitchClipPath);
            }
        }
    }
}
