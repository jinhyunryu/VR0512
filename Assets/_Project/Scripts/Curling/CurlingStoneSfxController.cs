using UnityEngine;

namespace ArcadeRoom.Curling
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Rigidbody), typeof(AudioSource))]
    public sealed class CurlingStoneSfxController : MonoBehaviour
    {
        [Header("Sources")]
        [SerializeField] private AudioSource slideSource;
        [SerializeField] private AudioClip hitClip;
        [SerializeField] private AudioClip slideClip;
        [SerializeField] private Rigidbody stoneBody;

        [Header("Mix")]
        [SerializeField, Range(0f, 1f)] private float masterVolume = 1f;
        [SerializeField, Range(0f, 1f)] private float hitVolume = 0.8f;
        [SerializeField, Range(0f, 1f)] private float slideVolume = 0.45f;

        [Header("Hit Feel")]
        [SerializeField, Min(0f)] private float minHitSpeed = 0.18f;
        [SerializeField, Min(0.01f)] private float fullHitSpeed = 2.4f;
        [SerializeField, Min(0f)] private float hitCooldown = 0.08f;
        [SerializeField] private Vector2 hitPitchRange = new(0.94f, 1.05f);

        [Header("Slide Feel")]
        [SerializeField, Min(0f)] private float minSlideSpeed = 0.04f;
        [SerializeField, Min(0.01f)] private float fullSlideSpeed = 1.6f;
        [SerializeField, Min(0f)] private float slideFadeSpeed = 5f;
        [SerializeField] private Vector2 slidePitchRange = new(0.88f, 1.08f);

        [Header("Spatial")]
        [SerializeField, Range(0f, 1f)] private float spatialBlend = 1f;
        [SerializeField, Min(0.1f)] private float maxDistance = 9f;

        private float _targetSlideVolume;
        private float _lastHitTime = float.NegativeInfinity;

        private void Awake()
        {
            ResolveReferences();
            ConfigureSlideSource();
        }

        private void OnValidate()
        {
            fullHitSpeed = Mathf.Max(0.01f, fullHitSpeed);
            fullSlideSpeed = Mathf.Max(0.01f, fullSlideSpeed);
            maxDistance = Mathf.Max(0.1f, maxDistance);
            hitPitchRange = NormalizePitchRange(hitPitchRange);
            slidePitchRange = NormalizePitchRange(slidePitchRange);
            ResolveReferences();
            ConfigureSlideSource();
        }

        private void Update()
        {
            UpdateSlideAudio();
        }

        private void OnDisable()
        {
            if (slideSource != null)
            {
                slideSource.Stop();
            }
        }

        private void OnCollisionEnter(Collision collision)
        {
            if (hitClip == null || collision == null || Time.time - _lastHitTime < hitCooldown)
            {
                return;
            }

            if (collision.rigidbody != null &&
                collision.rigidbody.TryGetComponent<CurlingStoneSfxController>(out var otherSfx) &&
                otherSfx != null &&
                otherSfx.GetInstanceID() < GetInstanceID())
            {
                return;
            }

            var speed = collision.relativeVelocity.magnitude;
            if (speed < minHitSpeed)
            {
                return;
            }

            _lastHitTime = Time.time;
            var volume = Mathf.InverseLerp(minHitSpeed, fullHitSpeed, speed) * hitVolume * masterVolume;
            PlayHitOneShot(hitClip, GetCollisionPosition(collision), volume);
        }

        private void UpdateSlideAudio()
        {
            if (slideSource == null || slideClip == null || stoneBody == null)
            {
                return;
            }

            var horizontalVelocity = Vector3.ProjectOnPlane(stoneBody.linearVelocity, Vector3.up);
            var speed = horizontalVelocity.magnitude;
            var speedT = Mathf.InverseLerp(minSlideSpeed, fullSlideSpeed, speed);
            _targetSlideVolume = speed >= minSlideSpeed ? speedT * slideVolume * masterVolume : 0f;
            slideSource.volume = Mathf.MoveTowards(slideSource.volume, _targetSlideVolume, slideFadeSpeed * Time.deltaTime);
            slideSource.pitch = Mathf.Lerp(slidePitchRange.x, slidePitchRange.y, speedT);

            if (slideSource.volume > 0.001f)
            {
                if (!slideSource.isPlaying)
                {
                    slideSource.Play();
                }

                return;
            }

            if (slideSource.isPlaying)
            {
                slideSource.Stop();
            }
        }

        private void PlayHitOneShot(AudioClip clip, Vector3 worldPosition, float volume)
        {
            if (clip == null || volume <= 0f)
            {
                return;
            }

            var oneShot = new GameObject("Curling_SFX_Hit");
            oneShot.transform.position = worldPosition;

            var source = oneShot.AddComponent<AudioSource>();
            source.clip = clip;
            source.volume = volume;
            source.pitch = Random.Range(hitPitchRange.x, hitPitchRange.y);
            source.spatialBlend = spatialBlend;
            source.rolloffMode = AudioRolloffMode.Linear;
            source.maxDistance = maxDistance;
            source.playOnAwake = false;
            source.Play();

            Destroy(oneShot, (clip.length / Mathf.Max(0.01f, Mathf.Abs(source.pitch))) + 0.1f);
        }

        private void ResolveReferences()
        {
            if (stoneBody == null)
            {
                stoneBody = GetComponent<Rigidbody>();
            }

            if (slideSource == null)
            {
                slideSource = GetComponent<AudioSource>();
            }

            if (slideSource == null)
            {
                slideSource = gameObject.AddComponent<AudioSource>();
            }
        }

        private void ConfigureSlideSource()
        {
            if (slideSource == null)
            {
                return;
            }

            slideSource.clip = slideClip;
            slideSource.loop = true;
            slideSource.playOnAwake = false;
            slideSource.volume = 0f;
            slideSource.spatialBlend = spatialBlend;
            slideSource.rolloffMode = AudioRolloffMode.Linear;
            slideSource.maxDistance = maxDistance;
        }

        private static Vector3 GetCollisionPosition(Collision collision)
        {
            return collision.contactCount > 0
                ? collision.GetContact(0).point
                : collision.transform.position;
        }

        private static Vector2 NormalizePitchRange(Vector2 pitchRange)
        {
            var min = Mathf.Max(0.01f, Mathf.Min(pitchRange.x, pitchRange.y));
            var max = Mathf.Max(min, Mathf.Max(pitchRange.x, pitchRange.y));
            return new Vector2(min, max);
        }
    }
}
