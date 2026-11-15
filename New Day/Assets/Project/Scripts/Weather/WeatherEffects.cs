using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Makes the weather something you can SEE and that the farm FEELS. (WeatherManager only decides what today's weather is.)
///
///   Rain / Storm - rain streaks fall across the screen, little splashes dot the ground, the day looks duller,
///                  the wind picks up (every WindSway plant and crop leans more), and all tilled soil is watered for you.
///   Snow         - snowflakes drift down.
///   Fog          - a pale haze, and hardly any wind.
///   Cloudy       - a slightly dull day with a bit more wind.
///
/// It adds itself to every scene when the game starts, so there is nothing to set up. To change the numbers, add this
/// component to an object in the scene yourself (the automatic one is then not made). Nothing is drawn while the player is
/// inside a house or shop.
/// </summary>
public class WeatherEffects : MonoBehaviour
{
    [Header("Rain")]
    [Tooltip("How many rain streaks are on screen in Rain.")]
    [SerializeField] private int rainDrops = 110;
    [Tooltip("How many in a Storm.")]
    [SerializeField] private int stormDrops = 200;
    [SerializeField] private float rainSpeed = 16f;
    [Tooltip("How far the rain slants (degrees). A storm slants twice as much.")]
    [SerializeField] private float rainSlant = 12f;
    [SerializeField] private Color rainColor = new Color(0.75f, 0.86f, 1f, 0.55f);
    [Tooltip("Splashes on the ground each second.")]
    [SerializeField] private float splashesPerSecond = 14f;
    [Tooltip("Rain waters every tilled tile, so crops don't need the watering can on a rainy day.")]
    [SerializeField] private bool rainWatersSoil = true;

    [Header("Snow")]
    [SerializeField] private int snowFlakes = 90;
    [SerializeField] private float snowSpeed = 1.6f;
    [SerializeField] private Color snowColor = new Color(1f, 1f, 1f, 0.9f);

    [Header("Mood (a see-through colour laid over the whole screen)")]
    [SerializeField] private Color cloudyTint = new Color(0.25f, 0.30f, 0.40f, 0.10f);
    [SerializeField] private Color rainTint = new Color(0.16f, 0.22f, 0.36f, 0.22f);
    [SerializeField] private Color stormTint = new Color(0.10f, 0.12f, 0.24f, 0.36f);
    [SerializeField] private Color snowTint = new Color(0.80f, 0.86f, 0.95f, 0.10f);
    [SerializeField] private Color fogTint = new Color(0.85f, 0.88f, 0.90f, 0.30f);
    [Tooltip("How quickly the screen colour changes when the weather changes (per second).")]
    [SerializeField] private float tintFadeSpeed = 0.5f;

    [Header("Wind (multiplies every WindSway: 1 = a normal day)")]
    [SerializeField] private float windClear = 1f;
    [SerializeField] private float windCloudy = 1.3f;
    [SerializeField] private float windRain = 1.6f;
    [SerializeField] private float windStorm = 2.6f;
    [SerializeField] private float windSnow = 0.8f;
    [SerializeField] private float windFog = 0.3f;

    [Header("Messages")]
    [Tooltip("Say what the weather is when it changes (\"It's raining today.\").")]
    [SerializeField] private bool announce = true;

    [Header("Drawing")]
    [Tooltip("Drawn above everything in the world (trees are 10 to 15).")]
    [SerializeField] private int sortingOrder = 60;

    private Camera cam;
    private Transform root;                         // follows the camera; every drop is a child of it
    private SpriteRenderer tint;
    private readonly List<Transform> drops = new List<Transform>();
    private readonly List<SpriteRenderer> dropRenderers = new List<SpriteRenderer>();
    private readonly List<float> dropSeed = new List<float>();
    private Weather shown = Weather.Clear;          // the weather the drops are set up for
    private bool announcedOnce;
    private float splashTimer, waterTimer;
    private Color tintNow = new Color(0f, 0f, 0f, 0f);
    private FarmManager farm;

    // ------------------------------------------------------------------ makes itself
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void AutoCreate()
    {
        UnityEngine.SceneManagement.SceneManager.sceneLoaded += (scene, mode) => EnsureExists();
        EnsureExists();
    }

    private static void EnsureExists()
    {
        if (FindAnyObjectByType<WeatherEffects>() != null) return;
        new GameObject("WeatherEffects").AddComponent<WeatherEffects>();
    }

    private void Awake()
    {
        root = new GameObject("WeatherDrops").transform;
        root.SetParent(transform, false);
        root.gameObject.AddComponent<LightingExempt>().skipDarkness = true;      // no shadows, and not dimmed at night

        var tintGo = new GameObject("WeatherTint");
        tintGo.transform.SetParent(root, false);
        tint = tintGo.AddComponent<SpriteRenderer>();
        tint.sprite = RuntimeSprites.Square;
        tint.sortingOrder = sortingOrder - 1;
        tint.color = tintNow;
    }

    private void OnDestroy()
    {
        WindSway.WindStrength = 1f;                 // don't leave a storm blowing in the next scene
    }

    // ------------------------------------------------------------------ every frame
    private void LateUpdate()
    {
        if (cam == null) cam = Camera.main;
        if (cam == null) return;

        Weather weather = WeatherManager.Current;
        bool indoors = HouseInterior.PlayerIsInside || Underground.Active;   // no weather inside a house or down the mine shaft

        // wind
        WindSway.WindStrength = indoors ? 0f : WindFor(weather);

        // the parent sits on the camera, so the weather always fills the screen
        Vector3 c = cam.transform.position;
        root.position = new Vector3(c.x, c.y, 0f);
        float halfH = cam.orthographicSize + 1f, halfW = halfH * cam.aspect + 1f;

        // screen colour: fade toward today's colour (clear when indoors)
        Color want = indoors ? new Color(0f, 0f, 0f, 0f) : TintFor(weather);
        tintNow = Color.Lerp(tintNow, want, 1f - Mathf.Exp(-tintFadeSpeed * 4f * Time.deltaTime));
        tint.color = tintNow;
        tint.transform.localScale = new Vector3(halfW * 2f + 2f, halfH * 2f + 2f, 1f);

        // drops: rebuild the set when the weather changes
        if (weather != shown)
        {
            shown = weather;
            Rebuild(weather, halfW, halfH);
            if (announce && (announcedOnce || weather != Weather.Clear)) Announce(weather);
            announcedOnce = true;
        }
        root.gameObject.SetActive(true);
        bool showDrops = !indoors && drops.Count > 0;
        for (int i = 0; i < dropRenderers.Count; i++) dropRenderers[i].enabled = showDrops;

        bool raining = weather == Weather.Rain || weather == Weather.Storm;
        if (showDrops)
        {
            if (raining) MoveRain(weather == Weather.Storm, halfW, halfH);
            else if (weather == Weather.Snow) MoveSnow(halfW, halfH);
        }

        if (raining && !indoors) Splashes(halfW, halfH);
        if (raining && rainWatersSoil) WaterSoil();
    }

    // ------------------------------------------------------------------ drops
    private void Rebuild(Weather weather, float halfW, float halfH)
    {
        foreach (Transform t in drops) if (t != null) Destroy(t.gameObject);
        drops.Clear(); dropRenderers.Clear(); dropSeed.Clear();

        bool rain = weather == Weather.Rain || weather == Weather.Storm;
        int count = weather == Weather.Rain ? rainDrops : weather == Weather.Storm ? stormDrops : weather == Weather.Snow ? snowFlakes : 0;
        float slant = weather == Weather.Storm ? rainSlant * 2f : rainSlant;

        for (int i = 0; i < count; i++)
        {
            var go = new GameObject(rain ? "Rain" : "Snow");
            go.transform.SetParent(root, false);
            go.transform.localPosition = new Vector3(Random.Range(-halfW, halfW), Random.Range(-halfH, halfH), 0f);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sortingOrder = sortingOrder;
            if (rain)
            {
                // a thin streak, tilted the way the wind blows
                sr.sprite = RuntimeSprites.Square;
                sr.color = rainColor;
                go.transform.localScale = new Vector3(0.04f, Random.Range(0.35f, 0.6f), 1f);
                go.transform.localRotation = Quaternion.Euler(0f, 0f, -slant);
            }
            else
            {
                sr.sprite = RuntimeSprites.Circle;
                sr.color = snowColor;
                float size = Random.Range(0.06f, 0.13f);
                go.transform.localScale = new Vector3(size, size, 1f);
            }
            drops.Add(go.transform);
            dropRenderers.Add(sr);
            dropSeed.Add(Random.value * 10f);
        }
    }

    private void MoveRain(bool storm, float halfW, float halfH)
    {
        float slant = (storm ? rainSlant * 2f : rainSlant) * Mathf.Deg2Rad;
        float speed = storm ? rainSpeed * 1.3f : rainSpeed;
        // "down" for a streak tilted by the slant: mostly down, a little sideways
        Vector3 step = new Vector3(Mathf.Sin(slant), -Mathf.Cos(slant), 0f) * speed * Time.deltaTime;
        for (int i = 0; i < drops.Count; i++)
        {
            Vector3 p = drops[i].localPosition + step * (0.8f + dropSeed[i] * 0.04f);
            if (p.y < -halfH) { p.y = halfH; p.x = Random.Range(-halfW, halfW); }      // back to the top at a new spot
            if (p.x > halfW) p.x -= halfW * 2f;
            drops[i].localPosition = p;
        }
    }

    private void MoveSnow(float halfW, float halfH)
    {
        float t = Time.time;
        for (int i = 0; i < drops.Count; i++)
        {
            float seed = dropSeed[i];
            Vector3 p = drops[i].localPosition;
            p.y -= snowSpeed * (0.6f + seed * 0.08f) * Time.deltaTime;
            p.x += Mathf.Sin(t * 0.9f + seed * 3f) * 0.5f * Time.deltaTime;              // flakes drift from side to side
            if (p.y < -halfH) { p.y = halfH; p.x = Random.Range(-halfW, halfW); }
            if (p.x > halfW) p.x -= halfW * 2f; else if (p.x < -halfW) p.x += halfW * 2f;
            drops[i].localPosition = p;
        }
    }

    private void Splashes(float halfW, float halfH)
    {
        splashTimer += Time.deltaTime * splashesPerSecond;
        while (splashTimer >= 1f)
        {
            splashTimer -= 1f;
            Vector3 at = root.position + new Vector3(Random.Range(-halfW, halfW), Random.Range(-halfH, halfH), 0f);
            // a tiny flat ring that spreads and fades, low down so it looks like it is on the ground
            CombatFx.Spawn(RuntimeSprites.Circle, at, 0f, new Vector2(0.05f, 0.03f), new Vector2(0.28f, 0.12f),
                           new Color(rainColor.r, rainColor.g, rainColor.b, 0.45f), 0.3f, 6, 0);
        }
    }

    /// <summary>Once a second while it rains, wet any tilled soil that is still dry (so soil you till in the rain gets wet too).</summary>
    private void WaterSoil()
    {
        waterTimer -= Time.deltaTime;
        if (waterTimer > 0f) return;
        waterTimer = 1f;
        if (farm == null) farm = FindAnyObjectByType<FarmManager>();
        if (farm != null) farm.WaterAllTilled();
    }

    // ------------------------------------------------------------------ lookups
    private float WindFor(Weather w)
    {
        switch (w)
        {
            case Weather.Cloudy: return windCloudy;
            case Weather.Rain: return windRain;
            case Weather.Storm: return windStorm;
            case Weather.Snow: return windSnow;
            case Weather.Fog: return windFog;
            default: return windClear;
        }
    }

    private Color TintFor(Weather w)
    {
        switch (w)
        {
            case Weather.Cloudy: return cloudyTint;
            case Weather.Rain: return rainTint;
            case Weather.Storm: return stormTint;
            case Weather.Snow: return snowTint;
            case Weather.Fog: return fogTint;
            default: return new Color(0f, 0f, 0f, 0f);
        }
    }

    private void Announce(Weather w)
    {
        switch (w)
        {
            case Weather.Rain: GameEvents.ShowToast(rainWatersSoil ? "It's raining. The crops are being watered for you." : "It's raining."); break;
            case Weather.Storm: GameEvents.ShowToast("A storm is blowing in!"); break;
            case Weather.Snow: GameEvents.ShowToast("It's snowing."); break;
            case Weather.Fog: GameEvents.ShowToast("A thick fog has rolled in."); break;
            case Weather.Cloudy: GameEvents.ShowToast("Clouds have rolled in."); break;
            default: GameEvents.ShowToast("The sky has cleared."); break;
        }
    }
}
