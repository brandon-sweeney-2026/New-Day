using UnityEngine;
using UnityEngine.Rendering.Universal;

public enum LightBehaviour
{
    /// <summary>Always shining the same.</summary>
    AlwaysOn,
    /// <summary>Switches on at dusk, off at dawn (street lamps, lanterns). Times are set on the LightingManager.</summary>
    DuskToDawn,
    /// <summary>Faint in daylight, full strength as it gets dark (mushrooms, crystals, torches).</summary>
    DimmerByDay
}

/// <summary>
/// Makes an object glow: it adds a coloured point light (a Light2D) plus an optional "lit" glow overlay that stays bright at night.
/// Put it on a prefab with a SpriteRenderer. Everything is editable in the Inspector.
/// </summary>
public class LightSource : MonoBehaviour
{
    [Header("Light")]
    public Color lightColor = new Color(1f, 0.7f, 0.35f);
    [Tooltip("How bright the light is at full strength.")]
    public float intensity = 1.2f;
    [Tooltip("Distance (world units) where the light fades to nothing.")]
    public float outerRadius = 5f;
    [Tooltip("Distance where the light is at full brightness.")]
    public float innerRadius = 0.4f;
    [Range(0f, 1f)] public float falloff = 0.6f;
    [Tooltip("Where the light sits, relative to the object's pivot (feet). Put it at the flame / bulb / crystal.")]
    public Vector2 lightOffset = new Vector2(0f, 1.5f);

    [Header("When is it on?")]
    public LightBehaviour behaviour = LightBehaviour.DimmerByDay;
    [Tooltip("DimmerByDay only: how bright it is in full daylight (0 = off, 1 = same as night).")]
    [Range(0f, 1f)] public float daytimeStrength = 0.3f;

    [Header("Life (flicker and pulse)")]
    [Range(0f, 1f)] public float flickerAmount = 0.15f;
    public float flickerSpeed = 8f;
    [Range(0f, 1f)] public float pulseAmount = 0f;
    public float pulseSpeed = 1.5f;

    [Header("Glow overlay (the 'lit' version of the art)")]
    [Tooltip("Frames drawn on top of the object, ignoring darkness, so flames and lit glass stay bright at night. Leave empty for none.")]
    public Sprite[] glowFrames;
    public float glowFramesPerSecond = 6f;
    [Tooltip("Fade the overlay with the light (so a lamp's glass goes dull when it is off).")]
    public bool glowFollowsLight = true;

    private Light2D lamp;
    private SpriteRenderer glow;
    private SpriteRenderer baseRenderer;
    private float seed;
    private float phase;

    private void Awake()
    {
        seed = Random.value * 100f;
        phase = Random.value * 6.28f;
        baseRenderer = GetComponent<SpriteRenderer>();

        var lg = new GameObject("PointLight");
        lg.transform.SetParent(transform, false);
        lg.transform.localPosition = new Vector3(lightOffset.x, lightOffset.y, 0f);
        lamp = lg.AddComponent<Light2D>();
        lamp.lightType = Light2D.LightType.Point;
        lamp.color = lightColor;
        lamp.intensity = intensity;
        lamp.pointLightOuterRadius = outerRadius;
        lamp.pointLightInnerRadius = Mathf.Min(innerRadius, outerRadius * 0.9f);
        lamp.falloffIntensity = falloff;
        lamp.shadowsEnabled = false;

        if (glowFrames != null && glowFrames.Length > 0 && glowFrames[0] != null)
        {
            var gg = new GameObject("Glow");
            gg.transform.SetParent(transform, false);
            gg.AddComponent<LightingExempt>().skipDarkness = true;
            glow = gg.AddComponent<SpriteRenderer>();
            glow.sprite = glowFrames[0];
            glow.sharedMaterial = LightingManager.UnlitMaterial != null ? LightingManager.UnlitMaterial : glow.sharedMaterial;
            if (baseRenderer != null)
            {
                glow.sortingLayerID = baseRenderer.sortingLayerID;
                glow.sortingOrder = baseRenderer.sortingOrder + 1;
            }
        }
    }

    private void LateUpdate()
    {
        if (lamp == null) return;

        float level;
        switch (behaviour)
        {
            case LightBehaviour.DuskToDawn: level = LightingManager.LampLevel; break;
            case LightBehaviour.DimmerByDay: level = Mathf.Lerp(daytimeStrength, 1f, LightingManager.NightAmount); break;
            default: level = 1f; break;
        }

        float flick = 1f + (Mathf.PerlinNoise(seed, Time.time * flickerSpeed) - 0.5f) * 2f * flickerAmount;
        float pulse = 1f + Mathf.Sin(Time.time * pulseSpeed + phase) * pulseAmount;

        // the sprite keeps its sorting order in sync (BehindPlayerSorter changes it at runtime)
        if (glow != null && baseRenderer != null) glow.sortingOrder = baseRenderer.sortingOrder + 1;

        lamp.enabled = level > 0.01f;
        lamp.intensity = intensity * level * flick * pulse;
        lamp.pointLightOuterRadius = outerRadius * (1f + (flick - 1f) * 0.4f);
        lamp.color = lightColor;

        if (glow != null)
        {
            int n = glowFrames.Length;
            int idx = n > 1 ? Mathf.FloorToInt(Time.time * glowFramesPerSecond + seed) % n : 0;
            if (glowFrames[idx] != null) glow.sprite = glowFrames[idx];
            float a = glowFollowsLight ? Mathf.Clamp01(level) : 1f;
            glow.color = new Color(1f, 1f, 1f, a);
            glow.enabled = a > 0.01f;
        }
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(lightColor.r, lightColor.g, lightColor.b, 0.6f);
        Vector3 c = transform.position + new Vector3(lightOffset.x, lightOffset.y, 0f);
        Gizmos.DrawWireSphere(c, outerRadius);
        Gizmos.DrawWireSphere(c, innerRadius);
    }
}
