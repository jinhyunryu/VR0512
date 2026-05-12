using ArcadeRoom.Audio;
using ArcadeRoom.Putting;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

[DisallowMultipleComponent]
[RequireComponent(typeof(Rigidbody))]
public class GolfClubImpactHaptics : MonoBehaviour
{
    private const string DefaultClubHitClipPath = "Assets/_Project/Audio/SFX/Putting/Putting Club Hit.mp3";

    [SerializeField] private HapticClub hapticClub;
    [SerializeField] private XRGrabInteractable grabInteractable;
    [SerializeField] private Collider hitPointCollider;
    [SerializeField] private PuttingStationController puttingStation;
    [SerializeField] private string golfBallNamePrefix = "GolfBall";
    [SerializeField, Min(0f)] private float minImpactSpeed = 0.2f;
    [SerializeField, Min(0f)] private float triggerCooldown = 0.05f;
    [Header("SFX")]
    [SerializeField] private AudioClip clubHitClip;
    [SerializeField, Range(0f, 1f)] private float clubHitVolume = 0.75f;
    [SerializeField] private Vector2 pitchRange = new(0.96f, 1.05f);
    [SerializeField, Range(0f, 1f)] private float spatialBlend = 1f;
    [SerializeField, Min(0.1f)] private float maxDistance = 6f;

    private float _lastTriggerTime = float.NegativeInfinity;

    private void Awake()
    {
        ResolveReferences(true);
        ResolveDefaultSfx();
    }

    private void OnValidate()
    {
        ResolveReferences(false);
        pitchRange = ArcadeSfxPlayer.NormalizePitchRange(pitchRange);
        maxDistance = Mathf.Max(0.1f, maxDistance);
        ResolveDefaultSfx();
    }

    private void OnCollisionEnter(Collision collision)
    {
        if (!CanTrigger(collision))
        {
            return;
        }

        if (!HasHitPointContact(collision))
        {
            return;
        }

        if (puttingStation == null)
        {
            puttingStation = ResolvePuttingStation();
        }

        puttingStation?.NotifyBallHit(GetGolfBallTarget(collision));

        if (hapticClub != null)
        {
            hapticClub.HapticFx();
        }

        ArcadeSfxPlayer.PlayOneShot(
            clubHitClip,
            GetCollisionPosition(collision),
            clubHitVolume,
            ArcadeSfxPlayer.RandomPitch(pitchRange),
            spatialBlend,
            maxDistance,
            "Putting_SFX_ClubHit");

        _lastTriggerTime = Time.time;
    }

    private bool CanTrigger(Collision collision)
    {
        if (Time.time - _lastTriggerTime < triggerCooldown)
        {
            return false;
        }

        if (grabInteractable != null && !grabInteractable.isSelected)
        {
            return false;
        }

        if (collision.relativeVelocity.magnitude < minImpactSpeed)
        {
            return false;
        }

        return IsGolfBallCollision(collision);
    }

    private bool HasHitPointContact(Collision collision)
    {
        if (hitPointCollider == null)
        {
            return true;
        }

        for (var i = 0; i < collision.contactCount; i++)
        {
            var contact = collision.GetContact(i);
            if (contact.thisCollider == hitPointCollider)
            {
                return true;
            }
        }

        return false;
    }

    private bool IsGolfBallCollision(Collision collision)
    {
        var target = GetGolfBallTarget(collision);
        return target != null && target.name.StartsWith(golfBallNamePrefix);
    }

    private GameObject GetGolfBallTarget(Collision collision)
    {
        return collision.rigidbody != null
            ? collision.rigidbody.gameObject
            : collision.collider.gameObject;
    }

    private void ResolveReferences(bool includeSceneReferences)
    {
        if (hapticClub == null)
        {
            hapticClub = GetComponent<HapticClub>();
        }

        if (grabInteractable == null)
        {
            grabInteractable = GetComponent<XRGrabInteractable>();
        }

        if (hitPointCollider == null)
        {
            var hitPoint = transform.Find("Golf_HitPoint");
            if (hitPoint != null)
            {
                hitPointCollider = hitPoint.GetComponent<Collider>();
            }
        }

        if (includeSceneReferences && puttingStation == null)
        {
            puttingStation = ResolvePuttingStation();
        }
    }

    private PuttingStationController ResolvePuttingStation()
    {
        var stations = FindObjectsByType<PuttingStationController>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        return stations.Length > 0 ? stations[0] : null;
    }

    private void ResolveDefaultSfx()
    {
        if (clubHitClip == null)
        {
            clubHitClip = ArcadeSfxPlayer.LoadEditorClip(DefaultClubHitClipPath);
        }
    }

    private static Vector3 GetCollisionPosition(Collision collision)
    {
        return collision.contactCount > 0
            ? collision.GetContact(0).point
            : collision.transform.position;
    }
}
