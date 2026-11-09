using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering.Universal;
using UnityEngine.Tilemaps;

/// <summary>
/// The sun, the shadows and the darkness. One of these lives in your scene (Farm > Lighting > Set Up Lighting adds it, and it also
/// creates itself automatically if you forget).
///
/// THE SUN
///   6:00 am  light comes from the RIGHT, shadows lean LEFT, at the angle cap (45 degrees by default).
///   12:00    sun straight overhead: shadows are short and straight below the object.
///   6:00 pm  light comes from the LEFT, shadows lean RIGHT, at the angle cap.
/// It moves smoothly with the in-game clock (TimeManager).
///
/// THE DARKNESS
///   From 6 pm the scene fades into night over "duskHours". Make the night darker or brighter with "nightBrightness"
///   (0 = pitch black, 1 = no darkness) and change its colour with "nightTint". It lifts again from 5 to 6 am.
///   Light sources (torches, lamps, mushrooms, crystals) glow in the dark.
///
/// INDOORS: inside a house room there is no darkness and no shadows.
///
/// HOW TO TEST: tick "usePreviewHour" in the Inspector while the game runs and drag "previewHour".
/// </summary>
[DefaultExecutionOrder(-50)]
public class LightingManager : MonoBehaviour
{
    public static LightingManager Instance { get; private set; }

    // ---------------- Inspector settings ----------------
    [Header("Sun path")]
    [Tooltip("Hour the sun is at its far RIGHT (morning).")] public float sunriseHour = 6f;
    [Tooltip("Hour the sun is straight overhead (shadows point straight down).")] public float noonHour = 12f;
    [Tooltip("Hour the sun is at its far LEFT (evening). Darkness starts here.")] public float sunsetHour = 18f;
    [Tooltip("Biggest angle the sun can lean from straight overhead.")] [Range(10f, 80f)] public float shadowCapDegrees = 45f;

    [Header("Shadows")]
    public bool enableShadows = true;
    [Tooltip("Gives every sprite a shadow automatically.")] public bool autoShadowEverything = true;
    [Range(0f, 1f)] public float shadowDarkness = 0.38f;
    [Tooltip("1 = leans exactly like the angle. Raise for longer, more dramatic shadows.")] public float shadowStretch = 1f;
    [Tooltip("How long the shadow is straight below at noon (fraction of the object's height).")] [Range(0.05f, 1f)] public float noonShadowHeight = 0.3f;
    [Tooltip("How long the shadow runs down at morning/evening (fraction of the object's height).")] [Range(0.05f, 1f)] public float edgeShadowHeight = 0.45f;
    [Tooltip("Shadows are drawn above the ground tilemaps. Leave on to find the right value by itself.")] public bool autoShadowSortingOrder = true;
    public int shadowSortingOrder = 1;
    [Tooltip("Sprites with a sorting order below this get no shadow (ground decals).")] public int minCasterSortingOrder = 3;
    [Tooltip("Sprites with a sorting order above this get no shadow (screen overlays).")] public int maxCasterSortingOrder = 45;
    [Tooltip("Sprites shorter than this (world units) get no shadow.")] public float minCasterHeight = 0.3f;
    [Tooltip("Safety limit so a huge field of crops can't slow the game.")] public int maxShadowCasters = 600;
    [Tooltip("Objects whose name contains any of these words get no shadow.")]
    public string[] ignoreNames = new string[] { "highlight", "overlay", "sun_patch", "sunpatch", "cursor", "glow", "marker", "indicator", "targettile" };

    [Header("Day light")]
    [Tooltip("Sun colour at noon.")] public Color noonColor = new Color(1f, 1f, 0.96f);
    [Tooltip("Sun colour at sunrise / sunset (warmer).")] public Color horizonColor = new Color(1f, 0.86f, 0.72f);
    public float dayIntensity = 1f;

    [Header("Night darkness  (all adjustable)")]
    [Tooltip("0 = pitch black night, 1 = no darkness at all.")] [Range(0f, 1f)] public float nightBrightness = 0.3f;
    [Tooltip("The colour of the night. Blue is classic.")] public Color nightTint = new Color(0.35f, 0.42f, 0.9f);
    [Tooltip("Hours it takes to go from daylight to full night after sunset.")] public float duskHours = 3f;
    [Tooltip("Hour the night starts to lift in the morning (full day again at sunrise).")] public float dawnStartHour = 5f;

    [Header("Lamps")]
    [Tooltip("Lamps and lanterns switch on at this hour...")] public float lampsOnHour = 17.5f;
    [Tooltip("...and off at this hour.")] public float lampsOffHour = 6.5f;
    [Tooltip("Hours they take to fade on or off.")] public float lampFadeHours = 0.5f;

    [Header("Indoors")]
    public Color indoorColor = new Color(1f, 0.96f, 0.9f);
    public float indoorIntensity = 1f;

    [Header("Make sprites react to light")]
    [Tooltip("Swaps 'Unlit' sprite materials for the lit one so night darkness affects them. Turn off if you have special materials.")]
    public bool forceLitMaterials = true;
    [Tooltip("How often (seconds) the scene is scanned for new sprites.")] public float scanInterval = 1f;

    [Header("Testing")]
    public bool usePreviewHour = false;
    [Range(0f, 26f)] public float previewHour = 12f;
    [Tooltip("Press - and = to make the night darker / brighter while playing.")] public bool debugKeys = false;

    // ---------------- values other scripts read ----------------
    public float ShadowShear { get; private set; }
    public float ShadowYScale { get; private set; }
    public float ShadowAlpha { get; private set; }
    public int ShadowVersion { get; private set; }
    public bool ShadowsVisible { get { return enableShadows && indoorBlend < 0.5f && ShadowAlpha > 0.01f; } }
    public int ShadowSortingOrder { get { return shadowSortingOrder; } }

    /// <summary>0 = full daylight, 1 = full night.</summary>
    public static float NightAmount { get { return Instance != null ? Instance.nightAmount : 0f; } }
    /// <summary>0 = lamps off, 1 = lamps on.</summary>
    public static float LampLevel { get { return Instance != null ? Instance.lampLevel : 0f; } }
    /// <summary>-1 = sun far right (morning), 0 = overhead, +1 = far left (evening).</summary>
    public static float SunPosition { get { return Instance != null ? Instance.sunT : 0f; } }

    private static Material litMat, unlitMat, shadowMat;
    private static bool warnedMats;

    public static Material ShadowMaterial { get { EnsureMaterials(); return shadowMat; } }
    public static Material UnlitMaterial { get { EnsureMaterials(); return unlitMat; } }
    public static Material LitMaterial { get { EnsureMaterials(); return litMat; } }

    private Light2D sun;
    private float nightAmount, lampLevel, sunT, indoorBlend;
    private int stepAngle = int.MinValue, stepAlpha = -1;
    private float stepYScale = -1f;
    private float scanTimer;
    private readonly HashSet<Renderer> seen = new HashSet<Renderer>();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void AutoCreate()
    {
        if (FindAnyObjectByType<LightingManager>() == null)
            new GameObject("LightingManager").AddComponent<LightingManager>();
    }

    private static void EnsureMaterials()
    {
        if (litMat != null && unlitMat != null && shadowMat != null) return;
        if (litMat == null) litMat = Resources.Load<Material>("Lighting/SpriteLit");
        if (unlitMat == null) unlitMat = Resources.Load<Material>("Lighting/SpriteUnlit");
        if (shadowMat == null) shadowMat = Resources.Load<Material>("Lighting/ShadowMaterial");
        if (litMat == null) litMat = MakeMat("Universal Render Pipeline/2D/Sprite-Lit-Default", null);
        if (unlitMat == null) unlitMat = MakeMat("Universal Render Pipeline/2D/Sprite-Unlit-Default", "Sprites/Default");
        if (shadowMat == null) shadowMat = unlitMat;
        if ((litMat == null || unlitMat == null) && !warnedMats)
        {
            warnedMats = true;
            Debug.LogWarning("LightingManager: couldn't find the sprite materials. Run Farm > Lighting > Set Up Lighting once so they are created in Assets/Resources/Lighting.");
        }
    }

    private static Material MakeMat(string shaderName, string fallback)
    {
        Shader s = Shader.Find(shaderName);
        if (s == null && fallback != null) s = Shader.Find(fallback);
        return s != null ? new Material(s) : null;
    }

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        FindOrCreateSun();
    }

    private void OnDestroy() { if (Instance == this) Instance = null; }

    private void FindOrCreateSun()
    {
        foreach (Light2D l in FindObjectsByType<Light2D>())
            if (l.lightType == Light2D.LightType.Global) { sun = l; return; }
        var go = new GameObject("Sun (Global Light 2D)");
        go.transform.SetParent(transform, false);
        sun = go.AddComponent<Light2D>();
        sun.lightType = Light2D.LightType.Global;
    }

    private void Update()
    {
        if (sun == null) FindOrCreateSun();

        float hour = CurrentHour();
        UpdateSun(hour);
        UpdateNight(hour);
        UpdateIndoors();
        ApplyLight();
        HandleDebugKeys();

        scanTimer -= Time.unscaledDeltaTime;
        if (scanTimer <= 0f) { scanTimer = Mathf.Max(0.25f, scanInterval); ScanScene(); }
    }

    private float CurrentHour()
    {
        if (usePreviewHour) return previewHour;
        TimeManager tm = TimeManager.Instance;
        return tm != null ? tm.HoursDecimal : 12f;
    }

    // ---------------- the sun ----------------
    private void UpdateSun(float hour)
    {
        // -1 at sunrise, 0 at noon, +1 at sunset, held at the ends outside that window
        float half = Mathf.Max(0.1f, (sunsetHour - sunriseHour) * 0.5f);
        sunT = Mathf.Clamp((hour - noonHour) / half, -1f, 1f);

        float angle = sunT * shadowCapDegrees;
        int qAngle = Mathf.RoundToInt(angle * 2f);                       // half-degree steps
        float abs = Mathf.Abs(sunT);
        float yScale = Mathf.Round(Mathf.Lerp(noonShadowHeight, edgeShadowHeight, abs) * 100f) / 100f;
        float alpha = shadowDarkness * (1f - nightAmount);
        int qAlpha = Mathf.RoundToInt(alpha * 50f);

        if (qAngle != stepAngle || qAlpha != stepAlpha || !Mathf.Approximately(yScale, stepYScale))
        {
            stepAngle = qAngle; stepAlpha = qAlpha; stepYScale = yScale;
            // morning (t<0) = light from the right = shadow leans left (negative x)
            ShadowShear = Mathf.Tan(qAngle * 0.5f * Mathf.Deg2Rad) * shadowStretch;
            ShadowYScale = yScale;
            ShadowAlpha = qAlpha / 50f;
            ShadowVersion++;
        }
    }

    // ---------------- darkness ----------------
    private void UpdateNight(float hour)
    {
        float n;
        if (hour >= sunsetHour) n = Mathf.Clamp01((hour - sunsetHour) / Mathf.Max(0.1f, duskHours));
        else if (hour < dawnStartHour) n = 1f;
        else if (hour < sunriseHour) n = 1f - Mathf.Clamp01((hour - dawnStartHour) / Mathf.Max(0.1f, sunriseHour - dawnStartHour));
        else n = 0f;
        nightAmount = n;

        float fade = Mathf.Max(0.05f, lampFadeHours);
        float on;
        if (hour >= lampsOnHour) on = Mathf.Clamp01((hour - lampsOnHour) / fade);
        else if (hour < lampsOffHour) on = 1f - Mathf.Clamp01((hour - (lampsOffHour - fade)) / fade);
        else on = 0f;
        lampLevel = on;
    }

    // Reads HouseInterior's private "current" room field by name, so HouseInterior.cs needs no changes.
    // If your HouseInterior has no such field, indoors simply isn't detected (no crash).
    private static System.Reflection.FieldInfo currentRoomField;
    private static bool roomFieldLooked;
    private static bool PlayerIsIndoors()
    {
        if (!roomFieldLooked)
        {
            roomFieldLooked = true;
            currentRoomField = typeof(HouseInterior).GetField("current",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.Public);
        }
        return currentRoomField != null && currentRoomField.GetValue(null) != null;
    }

    private void UpdateIndoors()
    {
        float target = PlayerIsIndoors() ? 1f : 0f;
        indoorBlend = Mathf.MoveTowards(indoorBlend, target, Time.unscaledDeltaTime * 6f);
    }

    private void ApplyLight()
    {
        if (sun == null) return;
        Color dayCol = Color.Lerp(noonColor, horizonColor, Mathf.Abs(sunT));
        Color outCol = Color.Lerp(dayCol, nightTint, nightAmount);
        float outInt = Mathf.Lerp(dayIntensity, nightBrightness * dayIntensity, nightAmount);
        sun.color = Color.Lerp(outCol, indoorColor, indoorBlend);
        sun.intensity = Mathf.Lerp(outInt, indoorIntensity, indoorBlend);
    }

    private void HandleDebugKeys()
    {
        if (!debugKeys || Keyboard.current == null) return;
        if (Keyboard.current.minusKey.wasPressedThisFrame) nightBrightness = Mathf.Clamp01(nightBrightness - 0.05f);
        if (Keyboard.current.equalsKey.wasPressedThisFrame) nightBrightness = Mathf.Clamp01(nightBrightness + 0.05f);
    }

    /// <summary>Change how dark the night is from your own scripts (0 = pitch black, 1 = no darkness).</summary>
    public void SetNightBrightness(float value) { nightBrightness = Mathf.Clamp01(value); }

    // ---------------- scanning the scene ----------------
    private void ScanScene()
    {
        seen.RemoveWhere(r => r == null);
        if (autoShadowSortingOrder) shadowSortingOrder = DetectGroundOrder();

        foreach (TilemapRenderer tr in FindObjectsByType<TilemapRenderer>())
        {
            if (seen.Contains(tr)) continue;
            seen.Add(tr);
            MakeLit(tr);
        }

        bool room = ShadowCaster.ActiveCount < maxShadowCasters;
        foreach (SpriteRenderer r in FindObjectsByType<SpriteRenderer>())
        {
            if (r.sprite == null || seen.Contains(r)) continue;
            seen.Add(r);

            // built-in code-made helpers (the tool target square) are never shadowed or darkened
            if (r.gameObject.name == "TargetTile") continue;

            LightingExempt ex = r.GetComponentInParent<LightingExempt>();
            if (ex == null || !ex.skipDarkness) MakeLit(r);

            if (!autoShadowEverything || !room) continue;
            if (ex != null && ex.skipShadow) continue;
            if (r.GetComponent<ShadowCaster>() != null) continue;
            if (!WantsShadow(r)) continue;
            r.gameObject.AddComponent<ShadowCaster>();
            if (ShadowCaster.ActiveCount + 1 >= maxShadowCasters) room = false;
        }
    }

    private void MakeLit(Renderer r)
    {
        if (!forceLitMaterials) return;
        Material lit = LitMaterial;
        if (lit == null) return;
        Material m = r.sharedMaterial;
        if (m == null || m.shader == null) { r.sharedMaterial = lit; return; }
        string n = m.shader.name;
        if (n.Contains("Unlit") || n == "Sprites/Default") r.sharedMaterial = lit;
    }

    private bool WantsShadow(SpriteRenderer r)
    {
        if (r.sortingOrder < minCasterSortingOrder || r.sortingOrder > maxCasterSortingOrder) return false;
        if (r.transform.parent != null && r.transform.parent.GetComponentInParent<SpriteRenderer>() != null) return false; // held items etc.
        string lower = r.gameObject.name.ToLowerInvariant();
        for (int i = 0; i < ignoreNames.Length; i++)
            if (!string.IsNullOrEmpty(ignoreNames[i]) && lower.Contains(ignoreNames[i].ToLowerInvariant())) return false;
        float h = r.sprite.bounds.size.y * Mathf.Abs(r.transform.lossyScale.y);
        if (h < minCasterHeight) return false;
        if (r.GetComponentInParent<Canvas>() != null) return false;
        return true;
    }

    private int DetectGroundOrder()
    {
        int best = 0; bool any = false;
        foreach (TilemapRenderer tr in FindObjectsByType<TilemapRenderer>())
        {
            string n = tr.gameObject.name.ToLowerInvariant();
            if (n.Contains("ground") || n.Contains("path") || n.Contains("soil") || n.Contains("wet") || n.Contains("water"))
            {
                if (!any || tr.sortingOrder > best) best = tr.sortingOrder;
                any = true;
            }
        }
        return any ? best + 1 : shadowSortingOrder;
    }
}
