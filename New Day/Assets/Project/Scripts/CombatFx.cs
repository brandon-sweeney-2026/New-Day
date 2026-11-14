using UnityEngine;

/// <summary>
/// Little generated pictures and effects used by the weapons: slash arcs, shockwave rings, quick fading shapes and damage numbers.
/// No art files needed. WeaponController uses all of this.
/// </summary>
public static class CombatFx
{
    private static Sprite arc, ring, streak;

    /// <summary>A crescent pointing right (+X), about 1.3 units from the middle.</summary>
    public static Sprite Arc
    {
        get
        {
            if (arc != null) return arc;
            const int n = 48;
            var tex = new Texture2D(n, n, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp };
            float c = n * 0.5f;
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    float dx = x + 0.5f - c, dy = y + 0.5f - c;
                    float r = Mathf.Sqrt(dx * dx + dy * dy);
                    float ang = Mathf.Atan2(dy, dx);
                    float a = 0f;
                    if (Mathf.Abs(ang) <= 1.1f)
                    {
                        float edge = 1f - Mathf.Abs(ang) / 1.1f;                 // thinner toward the ends
                        float inner = 17f + (1f - edge) * 3f, outer = 23f;
                        if (r >= inner && r <= outer) a = r > outer - 1.2f || r < inner + 1.2f ? 0.55f : 1f;
                    }
                    tex.SetPixel(x, y, new Color(1f, 1f, 1f, a));
                }
            tex.Apply();
            arc = Sprite.Create(tex, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f), 16f);
            arc.name = "SlashArc";
            return arc;
        }
    }

    /// <summary>A thin circle, about 1 unit wide (radius 0.5 units).</summary>
    public static Sprite Ring
    {
        get
        {
            if (ring != null) return ring;
            const int n = 64;
            var tex = new Texture2D(n, n, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp };
            float c = n * 0.5f;
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    float dx = x + 0.5f - c, dy = y + 0.5f - c;
                    float r = Mathf.Sqrt(dx * dx + dy * dy);
                    tex.SetPixel(x, y, new Color(1f, 1f, 1f, r >= 28f && r <= 31f ? 1f : 0f));
                }
            tex.Apply();
            ring = Sprite.Create(tex, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f), 64f);
            ring.name = "ShockRing";
            return ring;
        }
    }

    private static readonly System.Collections.Generic.Dictionary<Sprite, Sprite> whites = new System.Collections.Generic.Dictionary<Sprite, Sprite>();

    /// <summary>A flat white copy of a sprite (same shape, same pivot). Tint it for glowing after-images (blue-white) or shadow frames (dark).</summary>
    public static Sprite White(Sprite s)
    {
        if (s == null) return null;
        Sprite w;
        if (whites.TryGetValue(s, out w) && w != null) return w;
        int tw, th;
        Color[] px = GroundSampler.ReadAll(s, out tw, out th);
        if (px == null) return s;
        for (int i = 0; i < px.Length; i++) px[i] = new Color(1f, 1f, 1f, px[i].a > 0.5f ? 1f : 0f);
        var tex = new Texture2D(tw, th, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp };
        tex.SetPixels(px);
        tex.Apply();
        w = Sprite.Create(tex, new Rect(0, 0, tw, th), new Vector2(s.pivot.x / tw, s.pivot.y / th), s.pixelsPerUnit);
        whites[s] = w;
        return w;
    }

    /// <summary>Spawns a picture that scales, drifts and fades out by itself.</summary>
    public static FxFade Spawn(Sprite sprite, Vector3 pos, float rotZ, Vector2 scaleFrom, Vector2 scaleTo, Color color, float life, int order, int sortingLayerId)
    {
        var go = new GameObject("Fx");
        go.transform.position = pos;
        go.transform.rotation = Quaternion.Euler(0f, 0f, rotZ);
        go.transform.localScale = new Vector3(scaleFrom.x, scaleFrom.y, 1f);
        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = sprite;
        sr.color = color;
        sr.sortingLayerID = sortingLayerId;
        sr.sortingOrder = order;
        go.AddComponent<LightingExempt>();
        var f = go.AddComponent<FxFade>();
        f.life = Mathf.Max(0.02f, life);
        f.scaleFrom = scaleFrom; f.scaleTo = scaleTo;
        f.baseColor = color;
        return f;
    }

    /// <summary>A number that floats up from a hit. Critical hits are bigger and yellow.</summary>
    public static void Popup(Vector3 pos, string text, bool crit)
    {
        var go = new GameObject("DamageNumber");
        go.transform.position = pos + new Vector3(Random.Range(-0.15f, 0.15f), 0.4f, 0f);
        go.AddComponent<LightingExempt>();
        var p = go.AddComponent<DamagePopup>();
        p.Setup(text, crit);
    }
}

/// <summary>Fades (and optionally grows or drifts) a quick effect, then deletes it.</summary>
public class FxFade : MonoBehaviour
{
    public float life = 0.2f;
    public Vector2 scaleFrom = Vector2.one, scaleTo = Vector2.one;
    public Color baseColor = Color.white;
    public Vector2 drift;
    private float t;
    private SpriteRenderer sr;

    private void Awake() { sr = GetComponent<SpriteRenderer>(); }

    private void Update()
    {
        t += Time.unscaledDeltaTime;                       // keeps fading during the tiny hit-stop freeze
        float u = Mathf.Clamp01(t / life);
        transform.localScale = new Vector3(Mathf.Lerp(scaleFrom.x, scaleTo.x, 1f - (1f - u) * (1f - u)), Mathf.Lerp(scaleFrom.y, scaleTo.y, 1f - (1f - u) * (1f - u)), 1f);
        transform.position += (Vector3)(drift * Time.unscaledDeltaTime);
        if (sr != null) { Color c = baseColor; c.a = baseColor.a * (1f - u); sr.color = c; }
        if (u >= 1f) Destroy(gameObject);
    }
}

/// <summary>The floating damage number.</summary>
public class DamagePopup : MonoBehaviour
{
    private TextMesh main, shadow;
    private float t;
    private float life = 0.7f;

    public void Setup(string text, bool crit)
    {
        Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        shadow = Make("Shadow", font, text, crit, new Color(0.1f, 0.05f, 0.1f, 1f), new Vector3(0.03f, -0.03f, 0f), 201);
        main = Make("Text", font, text, crit, crit ? new Color(1f, 0.9f, 0.25f) : Color.white, Vector3.zero, 202);
        life = crit ? 0.95f : 0.65f;
    }

    private TextMesh Make(string name, Font font, string text, bool crit, Color color, Vector3 offset, int order)
    {
        var go = new GameObject(name);
        go.transform.SetParent(transform, false);
        go.transform.localPosition = offset;
        var tm = go.AddComponent<TextMesh>();
        tm.font = font;
        tm.text = text;
        tm.fontSize = 48;
        tm.characterSize = crit ? 0.085f : 0.06f;
        tm.anchor = TextAnchor.MiddleCenter;
        tm.alignment = TextAlignment.Center;
        tm.color = color;
        var mr = go.GetComponent<MeshRenderer>();
        mr.sharedMaterial = font.material;
        mr.sortingOrder = order;
        return tm;
    }

    private void Update()
    {
        t += Time.unscaledDeltaTime;
        float u = Mathf.Clamp01(t / life);
        transform.position += Vector3.up * (1.2f * (1f - u) + 0.2f) * Time.unscaledDeltaTime;
        float pop = u < 0.12f ? Mathf.Lerp(0.6f, 1.25f, u / 0.12f) : Mathf.Lerp(1.25f, 1f, Mathf.Clamp01((u - 0.12f) / 0.15f));
        transform.localScale = Vector3.one * pop;
        float a = u < 0.7f ? 1f : 1f - (u - 0.7f) / 0.3f;
        if (main != null) { Color c = main.color; c.a = a; main.color = c; }
        if (shadow != null) { Color c = shadow.color; c.a = a; shadow.color = c; }
        if (u >= 1f) Destroy(gameObject);
    }
}

/// <summary>
/// Looks at the ground under a spot and gives you its colours, so a hammer slam can throw up bits that match what was hit
/// (grass green, soil brown, forest dirt, sand...). It reads the tile (or floor picture) under the point; when it finds nothing it uses dusty brown.
/// </summary>
public static class GroundSampler
{
    private static readonly System.Collections.Generic.Dictionary<Sprite, Color[]> cache = new System.Collections.Generic.Dictionary<Sprite, Color[]>();
    private static readonly Color[] fallback = { new Color(0.55f, 0.42f, 0.3f), new Color(0.42f, 0.32f, 0.22f), new Color(0.68f, 0.56f, 0.4f), new Color(0.5f, 0.38f, 0.26f) };

    /// <summary>4 colours from the ground picture at a world position: [0] the average, [1] a dark one, [2] a light one, [3] a mid one.</summary>
    public static Color[] Palette(Vector2 pos)
    {
        Sprite best = null;
        int bestOrder = int.MinValue;
        foreach (UnityEngine.Tilemaps.Tilemap tm in Object.FindObjectsByType<UnityEngine.Tilemaps.Tilemap>(FindObjectsInactive.Exclude))
        {
            var rend = tm.GetComponent<UnityEngine.Tilemaps.TilemapRenderer>();
            if (rend == null || !rend.enabled) continue;
            string n = tm.name.ToLowerInvariant();
            if (n.Contains("pollution") || n.Contains("shadow") || n.Contains("fog") || n.Contains("light") || n.Contains("water")) continue;
            if (rend.sortingOrder > 50) continue;                                        // overlays are not the ground
            Sprite s = tm.GetSprite(tm.WorldToCell(pos));
            if (s != null && rend.sortingOrder >= bestOrder) { best = s; bestOrder = rend.sortingOrder; }
        }
        if (best == null)
        {
            // floors that are plain pictures (the forest rooms), not tiles
            foreach (SpriteRenderer sr in Object.FindObjectsByType<SpriteRenderer>(FindObjectsInactive.Exclude))
            {
                if (sr.sprite == null || sr.sortingOrder > -5) continue;
                if (!sr.bounds.Contains(new Vector3(pos.x, pos.y, sr.bounds.center.z))) continue;
                if (sr.sortingOrder >= bestOrder) { best = sr.sprite; bestOrder = sr.sortingOrder; }
            }
        }
        if (best == null) return fallback;
        Color[] pal;
        if (!cache.TryGetValue(best, out pal) || pal == null) { pal = Read(best); cache[best] = pal; }
        return pal ?? fallback;
    }

    /// <summary>All the pixels of a sprite's rectangle (works even when the texture isn't marked Read/Write).</summary>
    public static Color[] ReadAll(Sprite sprite, out int width, out int height)
    {
        width = height = 0;
        Texture2D tex = sprite != null ? sprite.texture : null;
        if (tex == null) return null;
        Rect r = sprite.textureRect;
        width = Mathf.Max(1, (int)r.width); height = Mathf.Max(1, (int)r.height);
        RenderTexture rt = RenderTexture.GetTemporary(tex.width, tex.height, 0, RenderTextureFormat.ARGB32);
        Graphics.Blit(tex, rt);
        RenderTexture prev = RenderTexture.active;
        RenderTexture.active = rt;
        var read = new Texture2D(width, height, TextureFormat.RGBA32, false);
        read.ReadPixels(new Rect(r.x, r.y, r.width, r.height), 0, 0);
        read.Apply();
        RenderTexture.active = prev;
        RenderTexture.ReleaseTemporary(rt);
        Color[] px = read.GetPixels();
        Object.Destroy(read);
        return px;
    }

    private static Color[] Read(Sprite sprite)
    {
        int w, h;
        Color[] px = ReadAll(sprite, out w, out h);
        if (px == null) return null;

        var list = new System.Collections.Generic.List<Color>();
        foreach (Color c in px) if (c.a > 0.6f) list.Add(c);
        if (list.Count == 0) return null;
        list.Sort((a, b) => (a.r + a.g + a.b).CompareTo(b.r + b.g + b.b));
        Color avg = Color.black;
        foreach (Color c in list) avg += c;
        avg /= list.Count;
        var pal = new Color[4];
        pal[0] = avg;
        pal[1] = list[Mathf.Clamp(list.Count / 8, 0, list.Count - 1)];
        pal[2] = list[Mathf.Clamp(list.Count * 7 / 8, 0, list.Count - 1)];
        pal[3] = list[list.Count / 2];
        for (int i = 0; i < 4; i++) pal[i].a = 1f;
        return pal;
    }

    /// <summary>Throws up chunks and a dust cloud in the ground's colours.</summary>
    public static void Burst(Vector2 pos, float radius, int count, float power, int order, int sortingLayerId)
    {
        Color[] pal = Palette(pos);
        for (int i = 0; i < count; i++)
        {
            float a = Random.Range(0f, Mathf.PI * 2f);
            float sp = Random.Range(0.6f, 1f) * power * Mathf.Max(0.5f, radius);
            var go = new GameObject("GroundChunk");
            go.transform.position = pos + new Vector2(Mathf.Cos(a), Mathf.Sin(a) * 0.6f) * Random.Range(0.1f, radius * 0.5f);
            go.AddComponent<LightingExempt>();
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = RuntimeSprites.Square;
            Color c = pal[Random.Range(1, 4)];
            sr.color = Color.Lerp(c, pal[0], Random.value * 0.4f);
            sr.sortingOrder = order;
            sr.sortingLayerID = sortingLayerId;
            float size = Random.Range(0.06f, 0.14f);
            go.transform.localScale = new Vector3(size, size, 1f);
            var ch = go.AddComponent<GroundChunk>();
            ch.vel = new Vector2(Mathf.Cos(a), Mathf.Sin(a) * 0.55f) * sp * 0.7f;
            ch.vh = Random.Range(2.2f, 4.6f) * power * 0.6f;
        }
        // a soft cloud in the same colour (lighter)
        for (int i = 0; i < Mathf.Max(3, count / 2); i++)
        {
            float a = i * Mathf.PI * 2f / Mathf.Max(3, count / 2) + Random.Range(0f, 0.5f);
            Color c = Color.Lerp(pal[2], Color.white, 0.2f); c.a = 0.75f;
            var puff = CombatFx.Spawn(RuntimeSprites.Circle, pos + new Vector2(Mathf.Cos(a), Mathf.Sin(a) * 0.6f) * 0.2f, 0f,
                                      Vector2.one * 0.16f, Vector2.one * Random.Range(0.35f, 0.6f), c, Random.Range(0.35f, 0.55f), order - 1, sortingLayerId);
            puff.drift = new Vector2(Mathf.Cos(a), Mathf.Sin(a) * 0.6f) * 1.3f + Vector2.up * 0.5f;
        }
    }

    /// <summary>A small cloud at the feet (used by the flail when it is spinning fast).</summary>
    public static void Scuff(Vector2 pos, int order, int sortingLayerId)
    {
        Color[] pal = Palette(pos);
        Color c = Color.Lerp(pal[2], Color.white, 0.15f); c.a = 0.6f;
        var puff = CombatFx.Spawn(RuntimeSprites.Circle, pos + new Vector2(Random.Range(-0.2f, 0.2f), 0f), 0f, Vector2.one * 0.1f, Vector2.one * Random.Range(0.22f, 0.34f), c, 0.35f, order, sortingLayerId);
        puff.drift = new Vector2(Random.Range(-0.4f, 0.4f), 0.4f);
    }
}

/// <summary>One flying bit of ground: it arcs up, lands, bounces a little and fades.</summary>
public class GroundChunk : MonoBehaviour
{
    public Vector2 vel;
    public float vh;                 // upward speed (a fake height above the ground)
    private float h, t;
    private Vector2 basePos;
    private SpriteRenderer sr;
    private bool started;

    private void Update()
    {
        if (!started) { started = true; basePos = transform.position; sr = GetComponent<SpriteRenderer>(); }
        float dt = Time.deltaTime;
        t += dt;
        basePos += vel * dt;
        vh -= 15f * dt;
        h += vh * dt;
        if (h < 0f) { h = 0f; vh = -vh * 0.3f; vel *= 0.55f; if (Mathf.Abs(vh) < 0.4f) vh = 0f; }
        transform.position = new Vector3(basePos.x, basePos.y + h, transform.position.z);
        if (t > 0.45f && sr != null)
        {
            Color c = sr.color; c.a = Mathf.Clamp01(1f - (t - 0.45f) / 0.35f); sr.color = c;
        }
        if (t > 0.8f) Destroy(gameObject);
    }
}
