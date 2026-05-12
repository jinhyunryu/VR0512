using System.Collections.Generic;
using ArcadeRoom.Audio;
using UnityEngine;

namespace ArcadeRoom.Arcade
{
    [DisallowMultipleComponent]
    public sealed class ArcadeSettingsController : MonoBehaviour
    {
        [SerializeField, Range(0f, 1f)] private float masterVolume = 1f;
        [SerializeField, Range(0.1f, 2f)] private float brightness = 1f;
        [SerializeField] private float minBrightness = 0.4f;
        [SerializeField] private float maxBrightness = 1.5f;
        [SerializeField] private bool controlSceneLights = true;
        [SerializeField] private List<Light> controlledLights = new();

        private const string DefaultBackgroundMusicClipPath = "Assets/_Project/Audio/SFX/Game_BGM.mp3";

        [Header("Background Music")]
        [SerializeField] private AudioClip backgroundMusicClip;
        [SerializeField, Range(0f, 1f)] private float backgroundMusicVolume = 0.35f;
        [SerializeField] private bool playBackgroundMusicOnAwake = true;
        [SerializeField] private bool loopBackgroundMusic = true;

        private readonly List<float> _baseLightIntensities = new();
        private float _baseAmbientIntensity = 1f;
        private AudioSource _backgroundMusicSource;

        public float MasterVolume => masterVolume;
        public float Brightness => brightness;
        public float BackgroundMusicVolume => backgroundMusicVolume;

        private void Awake()
        {
            ResolveDefaultBackgroundMusicClip();
            ResolveLights();
            CaptureBaseValues();
            ApplyAll();
            InitializeBackgroundMusic();
        }

        public void SetMasterVolume(float value)
        {
            masterVolume = Mathf.Clamp01(value);
            AudioListener.volume = masterVolume;
        }

        public void SetBackgroundMusicVolume(float value)
        {
            backgroundMusicVolume = Mathf.Clamp01(value);
            ApplyBackgroundMusicVolume();
        }

        public void SetBrightness(float value)
        {
            brightness = Mathf.Clamp(value, minBrightness, maxBrightness);
            ApplyBrightness();
        }

        public void ApplyAll()
        {
            SetMasterVolume(masterVolume);
            ApplyBackgroundMusicVolume();
            ApplyBrightness();
        }

        private void OnValidate()
        {
            masterVolume = Mathf.Clamp01(masterVolume);
            backgroundMusicVolume = Mathf.Clamp01(backgroundMusicVolume);
            brightness = Mathf.Clamp(brightness, minBrightness, maxBrightness);
            ResolveDefaultBackgroundMusicClip();
            ApplyBackgroundMusicVolume();
        }

        private void ResolveDefaultBackgroundMusicClip()
        {
            if (backgroundMusicClip == null)
            {
                backgroundMusicClip = ArcadeSfxPlayer.LoadEditorClip(DefaultBackgroundMusicClipPath);
            }
        }

        private void ResolveLights()
        {
            if (controlledLights.Count > 0)
            {
                return;
            }

            var lights = FindObjectsByType<Light>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            for (var i = 0; i < lights.Length; i++)
            {
                if (lights[i] != null)
                {
                    controlledLights.Add(lights[i]);
                }
            }
        }

        private void CaptureBaseValues()
        {
            _baseAmbientIntensity = Mathf.Max(0.01f, RenderSettings.ambientIntensity);
            _baseLightIntensities.Clear();

            for (var i = 0; i < controlledLights.Count; i++)
            {
                var sceneLight = controlledLights[i];
                _baseLightIntensities.Add(sceneLight != null ? sceneLight.intensity : 0f);
            }
        }

        private void ApplyBrightness()
        {
            var clampedBrightness = Mathf.Clamp(brightness, minBrightness, maxBrightness);
            RenderSettings.ambientIntensity = _baseAmbientIntensity * clampedBrightness;

            if (!controlSceneLights)
            {
                return;
            }

            for (var i = 0; i < controlledLights.Count; i++)
            {
                var sceneLight = controlledLights[i];
                if (sceneLight == null || i >= _baseLightIntensities.Count)
                {
                    continue;
                }

                sceneLight.intensity = _baseLightIntensities[i] * clampedBrightness;
            }
        }

        private void InitializeBackgroundMusic()
        {
            if (!playBackgroundMusicOnAwake || backgroundMusicClip == null)
            {
                return;
            }

            _backgroundMusicSource = GetComponent<AudioSource>();
            if (_backgroundMusicSource == null)
            {
                _backgroundMusicSource = gameObject.AddComponent<AudioSource>();
            }

            _backgroundMusicSource.clip = backgroundMusicClip;
            _backgroundMusicSource.loop = loopBackgroundMusic;
            _backgroundMusicSource.playOnAwake = false;
            _backgroundMusicSource.spatialBlend = 0f;
            _backgroundMusicSource.rolloffMode = AudioRolloffMode.Linear;
            _backgroundMusicSource.volume = backgroundMusicVolume;

            if (!_backgroundMusicSource.isPlaying)
            {
                _backgroundMusicSource.Play();
            }
        }

        private void ApplyBackgroundMusicVolume()
        {
            if (_backgroundMusicSource != null)
            {
                _backgroundMusicSource.volume = backgroundMusicVolume;
            }
        }
    }
}
