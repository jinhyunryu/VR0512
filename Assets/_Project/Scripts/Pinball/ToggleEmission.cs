using System.Collections;
using UnityEngine;

/// <summary>
/// Flashes this object's emission only when it is hit by an object on the selected ball layer.
/// Attach this to pinball bumpers, switches, targets, walls, or other objects that should glow on hit.
/// </summary>
[RequireComponent(typeof(Renderer))]
public class ToggleEmission : MonoBehaviour
{
    [Header("Collision Filter")]
    [Tooltip("Only objects on these layers can trigger the emission flash. Set this to PinballBall.")]
    [SerializeField] private LayerMask ballLayerMask;

    [Tooltip("Minimum collision speed required to trigger the flash.")]
    [SerializeField] private float minimumHitSpeed = 0.1f;

    [Tooltip("Ignore collision/trigger events briefly after play starts. Useful if objects overlap at scene start.")]
    [SerializeField] private float startupIgnoreTime = 0.15f;

    [Header("Emission Settings")]
    [SerializeField] private Color emissionColor = Color.red;
    [SerializeField] private float emissionIntensity = 2.0f;

    [Tooltip("How long the emission flash lasts in seconds.")]
    [SerializeField] private float emissionDuration = 0.25f;

    [Tooltip("If enabled, emission fades back to the idle color over the duration.")]
    [SerializeField] private bool fadeOut = true;

    [Header("Idle Emission")]
    [Tooltip("Emission color used when this object is not flashing. Keep black to prevent glow on play.")]
    [SerializeField] private Color idleEmissionColor = Color.black;

    [Tooltip("If true, the script will restore the material's original emission color after flashing instead of using Idle Emission Color.")]
    [SerializeField] private bool restoreOriginalEmissionAfterFlash = false;

    [Header("Renderer")]
    [Tooltip("Material index to flash if the renderer has multiple materials.")]
    [SerializeField] private int materialIndex = 0;

    private static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");

    private Renderer targetRenderer;
    private Material targetMaterial;
    private Color originalEmissionColor;
    private Coroutine flashCoroutine;
    private float enableTime;

    private void Awake()
    {
        targetRenderer = GetComponent<Renderer>();

        Material[] materials = targetRenderer.materials;
        if (materials == null || materials.Length == 0)
        {
            Debug.LogWarning($"{nameof(ToggleEmission)}: No material found on {name}.", this);
            enabled = false;
            return;
        }

        materialIndex = Mathf.Clamp(materialIndex, 0, materials.Length - 1);
        targetMaterial = materials[materialIndex];

        targetMaterial.EnableKeyword("_EMISSION");

        originalEmissionColor = targetMaterial.HasProperty(EmissionColorId)
            ? targetMaterial.GetColor(EmissionColorId)
            : Color.black;

        SetEmission(GetRestingEmissionColor());
    }

    private void OnEnable()
    {
        enableTime = Time.time;
    }

    private void OnDisable()
    {
        if (flashCoroutine != null)
        {
            StopCoroutine(flashCoroutine);
            flashCoroutine = null;
        }

        if (targetMaterial != null)
            SetEmission(GetRestingEmissionColor());
    }

    private void OnCollisionEnter(Collision collision)
    {
        if (ShouldIgnoreStartupEvent())
            return;

        if (!IsAllowedBall(collision.collider))
            return;

        if (collision.relativeVelocity.magnitude < minimumHitSpeed)
            return;

        FlashEmission();
    }

    private void OnTriggerEnter(Collider other)
    {
        if (ShouldIgnoreStartupEvent())
            return;

        if (!IsAllowedBall(other))
            return;

        Rigidbody ballRigidbody = other.attachedRigidbody;
        if (ballRigidbody != null && ballRigidbody.linearVelocity.magnitude < minimumHitSpeed)
            return;

        FlashEmission();
    }

    private bool ShouldIgnoreStartupEvent()
    {
        return Time.time - enableTime < startupIgnoreTime;
    }

    private bool IsAllowedBall(Collider other)
    {
        if (other == null)
            return false;

        if (IsInLayerMask(other.gameObject.layer))
            return true;

        Rigidbody attachedRigidbody = other.attachedRigidbody;
        if (attachedRigidbody != null && IsInLayerMask(attachedRigidbody.gameObject.layer))
            return true;

        return false;
    }

    private bool IsInLayerMask(int layer)
    {
        return (ballLayerMask.value & (1 << layer)) != 0;
    }

    private void FlashEmission()
    {
        if (flashCoroutine != null)
            StopCoroutine(flashCoroutine);

        flashCoroutine = StartCoroutine(FlashEmissionRoutine());
    }

    private IEnumerator FlashEmissionRoutine()
    {
        Color flashColor = emissionColor * emissionIntensity;
        Color restColor = GetRestingEmissionColor();

        float duration = Mathf.Max(0.01f, emissionDuration);
        float elapsed = 0f;

        SetEmission(flashColor);

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;

            if (fadeOut)
            {
                float t = Mathf.Clamp01(elapsed / duration);
                SetEmission(Color.Lerp(flashColor, restColor, t));
            }
            else
            {
                SetEmission(flashColor);
            }

            yield return null;
        }

        SetEmission(restColor);
        flashCoroutine = null;
    }

    private Color GetRestingEmissionColor()
    {
        return restoreOriginalEmissionAfterFlash ? originalEmissionColor : idleEmissionColor;
    }

    private void SetEmission(Color color)
    {
        if (targetMaterial == null)
            return;

        targetMaterial.EnableKeyword("_EMISSION");
        targetMaterial.SetColor(EmissionColorId, color);
    }
}