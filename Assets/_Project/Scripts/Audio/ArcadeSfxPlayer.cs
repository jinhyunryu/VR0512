using UnityEngine;

namespace ArcadeRoom.Audio
{
    public static class ArcadeSfxPlayer
    {
        public static void PlayOneShot(
            AudioClip clip,
            Vector3 worldPosition,
            float volume = 1f,
            float pitch = 1f,
            float spatialBlend = 1f,
            float maxDistance = 8f,
            string sourceName = "Arcade_SFX_OneShot")
        {
            if (clip == null || volume <= 0f)
            {
                return;
            }

            var oneShot = new GameObject(sourceName);
            oneShot.transform.position = worldPosition;

            var source = oneShot.AddComponent<AudioSource>();
            source.clip = clip;
            source.volume = Mathf.Clamp01(volume);
            source.pitch = Mathf.Max(0.01f, pitch);
            source.spatialBlend = Mathf.Clamp01(spatialBlend);
            source.rolloffMode = AudioRolloffMode.Linear;
            source.maxDistance = Mathf.Max(0.1f, maxDistance);
            source.playOnAwake = false;
            source.Play();

            Object.Destroy(oneShot, (clip.length / Mathf.Max(0.01f, Mathf.Abs(source.pitch))) + 0.1f);
        }

        public static void PlayOneShot(
            AudioSource source,
            AudioClip clip,
            float volume = 1f,
            float pitch = 1f)
        {
            if (source == null || clip == null || volume <= 0f)
            {
                return;
            }

            source.pitch = Mathf.Max(0.01f, pitch);
            source.PlayOneShot(clip, Mathf.Clamp01(volume));
        }

        public static void ConfigureLoopSource(
            AudioSource source,
            AudioClip clip,
            float spatialBlend = 1f,
            float maxDistance = 8f)
        {
            if (source == null)
            {
                return;
            }

            source.clip = clip;
            source.loop = true;
            source.playOnAwake = false;
            source.volume = 0f;
            source.spatialBlend = Mathf.Clamp01(spatialBlend);
            source.rolloffMode = AudioRolloffMode.Linear;
            source.maxDistance = Mathf.Max(0.1f, maxDistance);
        }

        public static float RandomPitch(Vector2 pitchRange)
        {
            var normalizedRange = NormalizePitchRange(pitchRange);
            return Random.Range(normalizedRange.x, normalizedRange.y);
        }

        public static Vector2 NormalizePitchRange(Vector2 pitchRange)
        {
            var min = Mathf.Max(0.01f, Mathf.Min(pitchRange.x, pitchRange.y));
            var max = Mathf.Max(min, Mathf.Max(pitchRange.x, pitchRange.y));
            return new Vector2(min, max);
        }

        public static AudioClip LoadEditorClip(string assetPath)
        {
#if UNITY_EDITOR
            return UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>(assetPath);
#else
            return null;
#endif
        }
    }
}
