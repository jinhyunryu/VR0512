using UnityEngine;

namespace ArcadeRoom.Carrom
{
    [DisallowMultipleComponent]
    public sealed class CarromSfxController : MonoBehaviour
    {
        [Header("Sources")]
        [SerializeField] private AudioSource audioSource;
        [SerializeField] private AudioClip coinHitClip;
        [SerializeField] private AudioClip pocketClip;

        [Header("Mix")]
        [SerializeField, Range(0f, 1f)] private float masterVolume = 1f;
        [SerializeField, Range(0f, 1f)] private float coinHitVolume = 0.75f;
        [SerializeField, Range(0f, 1f)] private float pocketVolume = 0.85f;

        [Header("Collision Feel")]
        [SerializeField, Min(0f)] private float minHitSpeed = 0.08f;
        [SerializeField, Min(0.01f)] private float fullHitSpeed = 1.2f;
        [SerializeField, Min(0f)] private float hitCooldown = 0.04f;
        [SerializeField] private Vector2 coinHitPitchRange = new(0.96f, 1.06f);
        [SerializeField] private Vector2 pocketPitchRange = new(0.95f, 1.03f);

        [Header("Spatial")]
        [SerializeField] private bool useSpatialOneShots = true;
        [SerializeField, Range(0f, 1f)] private float spatialBlend = 1f;
        [SerializeField, Min(0.1f)] private float maxDistance = 7f;

        private float _nextHitTime;

        private void Awake()
        {
            ResolveAudioSource();
        }

        private void OnValidate()
        {
            fullHitSpeed = Mathf.Max(0.01f, fullHitSpeed);
            maxDistance = Mathf.Max(0.1f, maxDistance);
            coinHitPitchRange = NormalizePitchRange(coinHitPitchRange);
            pocketPitchRange = NormalizePitchRange(pocketPitchRange);
        }

        public void PlayPieceCollision(Collision collision)
        {
            if (coinHitClip == null || collision == null)
            {
                return;
            }

            var hitSpeed = collision.relativeVelocity.magnitude;
            if (hitSpeed < minHitSpeed || Time.time < _nextHitTime)
            {
                return;
            }

            _nextHitTime = Time.time + hitCooldown;
            var volume = Mathf.InverseLerp(minHitSpeed, fullHitSpeed, hitSpeed) * coinHitVolume * masterVolume;
            PlayClip(coinHitClip, GetCollisionPosition(collision), volume, coinHitPitchRange);
        }

        public void PlayPocket(Vector3 worldPosition)
        {
            if (pocketClip == null)
            {
                return;
            }

            PlayClip(pocketClip, worldPosition, pocketVolume * masterVolume, pocketPitchRange);
        }

        private void PlayClip(AudioClip clip, Vector3 worldPosition, float volume, Vector2 pitchRange)
        {
            if (clip == null || volume <= 0f)
            {
                return;
            }

            var pitch = Random.Range(pitchRange.x, pitchRange.y);
            if (useSpatialOneShots)
            {
                PlaySpatialOneShot(clip, worldPosition, volume, pitch);
                return;
            }

            ResolveAudioSource();
            if (audioSource == null)
            {
                return;
            }

            audioSource.pitch = pitch;
            audioSource.PlayOneShot(clip, volume);
        }

        private void PlaySpatialOneShot(AudioClip clip, Vector3 worldPosition, float volume, float pitch)
        {
            var oneShot = new GameObject("Carrom_SFX_OneShot");
            oneShot.transform.position = worldPosition;

            var source = oneShot.AddComponent<AudioSource>();
            source.clip = clip;
            source.volume = volume;
            source.pitch = pitch;
            source.spatialBlend = spatialBlend;
            source.rolloffMode = AudioRolloffMode.Linear;
            source.maxDistance = maxDistance;
            source.playOnAwake = false;
            source.Play();

            Destroy(oneShot, (clip.length / Mathf.Max(0.01f, Mathf.Abs(pitch))) + 0.1f);
        }

        private void ResolveAudioSource()
        {
            if (audioSource == null)
            {
                audioSource = GetComponent<AudioSource>();
            }

            if (audioSource == null)
            {
                audioSource = gameObject.AddComponent<AudioSource>();
            }

            audioSource.playOnAwake = false;
            audioSource.spatialBlend = useSpatialOneShots ? 0f : spatialBlend;
            audioSource.rolloffMode = AudioRolloffMode.Linear;
            audioSource.maxDistance = maxDistance;
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
