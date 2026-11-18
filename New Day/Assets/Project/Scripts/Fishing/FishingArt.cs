using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// All the pictures fishing needs, drawn in code the first time they are asked for (so no art files or importing).
///
///  WORLD pictures (16 pixels per unit, like the rest of the game's art):
///    Bobber, BobberAir, BobberSunk  - the float: floating (bottom half under water), flying through the air, pulled under by a bite
///    Exclaim                        - the "!" bubble over the player's head when a fish bites
///
///  SCREEN pictures for the fight and the catch card (one art pixel = 4 screen units, so they stay chunky pixel art):
///    UiWood, UiInset, UiTrack, UiPanel, UiPill, UiBarFill   - frames and bars (9-sliced: the corners keep their size when stretched)
///    UiSea                                                   - the whole underwater background (water, light, sand, rocks, weeds)
///    UiRay, UiSeaweed, UiBubble, UiNoteBody, UiNoteShine, UiRing, UiGlow, UiSpark, UiStar, UiSunburst
///
/// To change a picture, change the colours or the little character maps below. Each map row is one row of pixels, top row first;
/// each character is looked up in the palette next to it ('.' = see-through).
/// </summary>
public static class FishingArt
{
    private const float WorldPpu = 16f;
    private const float UiPpu = 25f;        // the Canvas uses 100 units per Unity unit, so 25 = each art pixel is 4 units wide

    private static readonly Dictionary<string, Sprite> cache = new Dictionary<string, Sprite>();

    private static Sprite Cached(string key, Func<Sprite> make)
    {
        Sprite s;
        if (cache.TryGetValue(key, out s) && s != null) return s;
        s = make();
        s.name = "Fishing_" + key;
        cache[key] = s;
        return s;
    }

    // ================================================================== WORLD
    private static readonly Dictionary<char, Color> BobberPalette = new Dictionary<char, Color>
    {
        { 'k', new Color(0.16f, 0.09f, 0.10f) },          // outline
        { 'R', new Color(0.87f, 0.20f, 0.20f) },          // red top
        { 'r', new Color(1.00f, 0.58f, 0.50f) },          // shine on the red
        { 'd', new Color(0.60f, 0.10f, 0.14f) },          // red in shadow
        { 'W', new Color(0.97f, 0.96f, 0.90f) },          // white band
        { 'w', new Color(0.74f, 0.77f, 0.80f) },          // white in shadow
        { '~', new Color(0.80f, 0.94f, 1.00f, 0.85f) },   // bright ring where it meets the water
        { '-', new Color(0.55f, 0.82f, 1.00f, 0.40f) },   // faint ripple / reflection
    };

    /// <summary>The float sitting on the water. Its pivot is the water line, so put it exactly where the water surface is.</summary>
    public static Sprite Bobber
    {
        get
        {
            return Cached("Bobber", () => FromMap(new[]
            {
                "....k....",
                "....k....",
                "...kRk...",
                "..kRrRk..",
                ".kRrRRdk.",
                ".kRRRRdk.",
                ".kWWWWwk.",
                "~~kWWwk~~",
                "-~~~~~~~-",
                "..-----..",
            }, BobberPalette, new Vector2(4.5f, 2f), WorldPpu));
        }
    }

    /// <summary>The whole float, for when it flies through the air.</summary>
    public static Sprite BobberAir
    {
        get
        {
            return Cached("BobberAir", () => FromMap(new[]
            {
                "....k....",
                "....k....",
                "...kRk...",
                "..kRrRk..",
                ".kRrRRdk.",
                ".kRRRRdk.",
                ".kWWWWwk.",
                ".kWWWWwk.",
                "..kWWwk..",
                "...kkk...",
            }, BobberPalette, new Vector2(4.5f, 5f), WorldPpu));
        }
    }

    /// <summary>The float pulled under: only the tip shows, with a swirl of water around it.</summary>
    public static Sprite BobberSunk
    {
        get
        {
            return Cached("BobberSunk", () => FromMap(new[]
            {
                "....k....",
                "...kRk...",
                ".~~kdk~~.",
                "~-~~~~~-~",
                "-~-----~-",
                ".--...--.",
            }, BobberPalette, new Vector2(4.5f, 2.5f), WorldPpu));
        }
    }

    /// <summary>The "!" speech bubble shown over the player's head when something bites. Pivot = the bottom tip.</summary>
    public static Sprite Exclaim
    {
        get
        {
            return Cached("Exclaim", () => FromMap(new[]
            {
                ".kkkkkkk.",
                "kWWWWWWWk",
                "kWWWkWWWk",
                "kWWkRkWWk",
                "kWWkRkWWk",
                "kWWkRkWWk",
                "kWWWkWWWk",
                "kWWkRkWWk",
                "kwWWkWWwk",
                ".kkkWkkk.",
                "...kWk...",
                "....k....",
            }, new Dictionary<char, Color>
            {
                { 'k', new Color(0.16f, 0.10f, 0.10f) },
                { 'W', new Color(1f, 0.98f, 0.92f) },
                { 'w', new Color(0.82f, 0.80f, 0.76f) },
                { 'R', new Color(0.90f, 0.18f, 0.18f) },
            }, new Vector2(4.5f, 0f), WorldPpu));
        }
    }

    // ================================================================== SCREEN: frames and bars (9-sliced)
    /// <summary>A wooden frame with brass corner studs. Stretch it with Image.Type = Sliced.</summary>
    public static Sprite UiWood
    {
        get
        {
            return Cached("UiWood", () =>
            {
                const int n = 16;
                var outline = new Color(0.17f, 0.09f, 0.04f);
                var light = new Color(0.86f, 0.62f, 0.36f);
                var shade = new Color(0.47f, 0.27f, 0.12f);
                var wood = new Color(0.68f, 0.44f, 0.22f);
                var grain = new Color(0.58f, 0.36f, 0.17f);
                var inner = new Color(0.24f, 0.13f, 0.06f);
                var middle = new Color(0.07f, 0.11f, 0.17f);
                var brass = new Color(1f, 0.84f, 0.42f);
                var brassDark = new Color(0.70f, 0.50f, 0.18f);
                Color[] px = Blank(n, n);
                for (int y = 0; y < n; y++)
                    for (int x = 0; x < n; x++)
                    {
                        int d = Mathf.Min(Mathf.Min(x, y), Mathf.Min(n - 1 - x, n - 1 - y));
                        Color c;
                        if (d == 0) c = outline;
                        else if (d == 1) c = (y == n - 2 || x == 1) ? light : shade;      // lit from the top-left
                        else if (d <= 4) c = ((x * 3 + y * 7) % 11 == 0) ? grain : wood;
                        else if (d == 5) c = inner;
                        else c = middle;
                        px[y * n + x] = c;
                    }
                // round the outside corners
                foreach (Vector2Int p in new[] { new Vector2Int(0, 0), new Vector2Int(n - 1, 0), new Vector2Int(0, n - 1), new Vector2Int(n - 1, n - 1) })
                    px[p.y * n + p.x] = Color.clear;
                // brass studs in the corners
                foreach (Vector2Int p in new[] { new Vector2Int(3, 3), new Vector2Int(n - 4, 3), new Vector2Int(3, n - 4), new Vector2Int(n - 4, n - 4) })
                {
                    px[p.y * n + p.x] = brass;
                    px[(p.y - 1) * n + p.x] = brassDark;
                    px[p.y * n + p.x + 1] = brassDark;
                }
                return Make(px, n, n, new Vector2(0.5f, 0.5f), UiPpu, new Vector4(6, 6, 6, 6));
            });
        }
    }

    /// <summary>A dark sunken slot (the back of the stamina bar).</summary>
    public static Sprite UiInset
    {
        get
        {
            return Cached("UiInset", () => RoundBox(12, new Color(0.04f, 0.04f, 0.07f), new Color(0.12f, 0.12f, 0.17f), new Color(0.07f, 0.07f, 0.11f), 4));
        }
    }

    /// <summary>A plain rounded box in greys, meant to be tinted (the note track).</summary>
    public static Sprite UiTrack
    {
        get
        {
            return Cached("UiTrack", () => RoundBox(12, new Color(1f, 1f, 1f, 0.9f), new Color(0.55f, 0.55f, 0.55f, 0.85f), new Color(0.75f, 0.75f, 0.75f, 0.9f), 4));
        }
    }

    /// <summary>A deep blue panel with a light edge (the catch card).</summary>
    public static Sprite UiPanel
    {
        get
        {
            return Cached("UiPanel", () => RoundBox(12, new Color(0.04f, 0.10f, 0.17f), new Color(0.11f, 0.27f, 0.41f), new Color(0.40f, 0.66f, 0.80f), 4));
        }
    }

    /// <summary>A white rounded pill, meant to be tinted (labels, the "NEW CATCH!" ribbon).</summary>
    public static Sprite UiPill
    {
        get
        {
            return Cached("UiPill", () => RoundBox(10, new Color(0.55f, 0.55f, 0.55f), new Color(1f, 1f, 1f), new Color(1f, 1f, 1f), 4));
        }
    }

    /// <summary>The bar filling: grey with a shiny top so any tint looks rounded.</summary>
    public static Sprite UiBarFill
    {
        get
        {
            return Cached("UiBarFill", () =>
            {
                const int n = 8;
                Color[] px = Blank(n, n);
                for (int y = 0; y < n; y++)
                    for (int x = 0; x < n; x++)
                    {
                        float g = y >= n - 2 ? 1f : y >= n - 4 ? 0.88f : y >= 2 ? 0.78f : 0.6f;
                        px[y * n + x] = new Color(g, g, g, 1f);
                    }
                px[0] = px[n - 1] = px[(n - 1) * n] = px[n * n - 1] = Color.clear;
                return Make(px, n, n, new Vector2(0.5f, 0.5f), UiPpu, new Vector4(3, 3, 3, 3));
            });
        }
    }

    /// <summary>
    /// A rounded box: 'outline' on the outside edge, 'edge' just inside it (a 1-pixel rim), 'fill' in the middle.
    /// </summary>
    private static Sprite RoundBox(int n, Color outline, Color fill, Color edge, int border)
    {
        Color[] px = Blank(n, n);
        for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                int d = Mathf.Min(Mathf.Min(x, y), Mathf.Min(n - 1 - x, n - 1 - y));
                px[y * n + x] = d == 0 ? outline : d == 1 ? edge : fill;
            }
        foreach (Vector2Int p in new[] { new Vector2Int(0, 0), new Vector2Int(n - 1, 0), new Vector2Int(0, n - 1), new Vector2Int(n - 1, n - 1) })
            px[p.y * n + p.x] = Color.clear;
        return Make(px, n, n, new Vector2(0.5f, 0.5f), UiPpu, new Vector4(border, border, border, border));
    }

    // ================================================================== SCREEN: the underwater background
    /// <summary>The underwater scene behind the fight: light water at the top fading to deep blue, a wavy surface, sand, rocks and far-off weeds.</summary>
    public static Sprite UiSea
    {
        get
        {
            return Cached("UiSea", () =>
            {
                const int w = 220, h = 72;
                var px = new Color[w * h];
                var rng = new System.Random(7);

                // The water is a few flat colours mixed with a checker-like dither (the classic pixel-art gradient).
                Color[] bands =
                {
                    new Color(0.03f, 0.11f, 0.24f), new Color(0.04f, 0.16f, 0.31f), new Color(0.06f, 0.22f, 0.38f),
                    new Color(0.08f, 0.29f, 0.45f), new Color(0.11f, 0.37f, 0.53f), new Color(0.15f, 0.46f, 0.61f),
                    new Color(0.21f, 0.56f, 0.69f),
                };
                int[,] bayer = { { 0, 8, 2, 10 }, { 12, 4, 14, 6 }, { 3, 11, 1, 9 }, { 15, 7, 13, 5 } };
                for (int y = 0; y < h; y++)
                    for (int x = 0; x < w; x++)
                    {
                        float f = (float)y / (h - 1) * (bands.Length - 1);
                        int i = Mathf.FloorToInt(f);
                        float frac = f - i;
                        bool up = frac * 16f > bayer[y & 3, x & 3] + 0.5f;
                        Color c = bands[Mathf.Clamp(i + (up ? 1 : 0), 0, bands.Length - 1)];

                        // dancing light ("caustics") in the upper half
                        float caustic = Mathf.Sin(x * 0.21f + y * 0.13f) + Mathf.Sin(x * 0.13f - y * 0.31f) + Mathf.Sin(x * 0.07f + y * 0.05f);
                        if (y > h / 2 && caustic > 2.2f) c = Color.Lerp(c, new Color(0.55f, 0.85f, 0.92f), 0.35f);
                        px[y * w + x] = c;
                    }

                // the surface: a bright wavy line at the very top
                for (int x = 0; x < w; x++)
                {
                    int wave = Mathf.RoundToInt(Mathf.Sin(x * 0.28f) * 0.8f + Mathf.Sin(x * 0.11f + 1f) * 0.8f);
                    for (int y = h - 3 + wave; y < h; y++)
                        if (y >= 0 && y < h) px[y * w + x] = y == h - 3 + wave ? new Color(0.78f, 0.95f, 1f) : new Color(0.45f, 0.78f, 0.88f);
                }

                // far-off weeds (dark, behind everything)
                for (int k = 0; k < 14; k++)
                {
                    int bx = rng.Next(4, w - 4);
                    int tall = rng.Next(14, 30);
                    float phase = (float)rng.NextDouble() * 6f;
                    for (int y = 0; y < tall; y++)
                    {
                        int x = bx + Mathf.RoundToInt(Mathf.Sin(y * 0.3f + phase) * 1.6f);
                        Color c = Color.Lerp(new Color(0.03f, 0.18f, 0.20f), new Color(0.05f, 0.26f, 0.27f), (float)y / tall);
                        Put(px, w, h, x, y, c);
                        if (y < tall - 3) Put(px, w, h, x + 1, y, c);
                    }
                }

                // the sand floor, with darker specks and a light top edge
                for (int x = 0; x < w; x++)
                {
                    int top = 7 + Mathf.RoundToInt(Mathf.Sin(x * 0.06f) * 2f + Mathf.Sin(x * 0.23f) * 0.8f);
                    for (int y = 0; y <= top; y++)
                    {
                        Color c = y == top ? new Color(0.84f, 0.74f, 0.50f)
                                : y == top - 1 ? new Color(0.72f, 0.62f, 0.40f)
                                : new Color(0.58f, 0.49f, 0.31f);
                        if (y < top - 1 && rng.Next(9) == 0) c = new Color(0.44f, 0.37f, 0.24f);
                        px[y * w + x] = c;
                    }
                }

                // a few rocks sitting on the sand
                for (int k = 0; k < 6; k++)
                {
                    int cx = rng.Next(10, w - 10), cy = 7, rx = rng.Next(3, 7), ry = rng.Next(2, 4);
                    for (int y = -ry; y <= ry; y++)
                        for (int x = -rx; x <= rx; x++)
                        {
                            float e = (x * x) / (float)(rx * rx) + (y * y) / (float)(ry * ry);
                            if (e > 1f) continue;
                            Color c = e > 0.7f ? new Color(0.16f, 0.20f, 0.26f) : (y > 0 && x < 0 ? new Color(0.42f, 0.47f, 0.52f) : new Color(0.30f, 0.35f, 0.42f));
                            Put(px, w, h, cx + x, cy + y, c);
                        }
                }

                return Make(px, w, h, new Vector2(0.5f, 0.5f), UiPpu, Vector4.zero);
            });
        }
    }

    /// <summary>A soft beam of sunlight coming down through the water (white, fades toward the bottom).</summary>
    public static Sprite UiRay
    {
        get
        {
            return Cached("UiRay", () =>
            {
                const int w = 12, h = 48;
                var px = new Color[w * h];
                for (int y = 0; y < h; y++)
                    for (int x = 0; x < w; x++)
                    {
                        float a = (1f - Mathf.Abs(x - 5.5f) / 6f) * ((float)y / h);
                        a = Mathf.Floor(a * 4f) / 4f;              // in steps, so it stays pixel-art
                        px[y * w + x] = new Color(1f, 1f, 1f, a);
                    }
                return Make(px, w, h, new Vector2(0.5f, 1f), UiPpu, Vector4.zero);
            });
        }
    }

    /// <summary>One strand of seaweed. Pivot = the root, so rotating it makes it sway.</summary>
    public static Sprite UiSeaweed
    {
        get
        {
            return Cached("UiSeaweed", () =>
            {
                const int w = 10, h = 40;
                Color[] px = Blank(w, h);
                var dark = new Color(0.08f, 0.30f, 0.20f);
                for (int y = 0; y < h; y++)
                {
                    float k = (float)y / h;
                    float cx = 4.5f + Mathf.Sin(y * 0.33f) * 1.6f;
                    Color body = Color.Lerp(new Color(0.20f, 0.52f, 0.30f), new Color(0.50f, 0.84f, 0.44f), k);
                    for (int x = 0; x < w; x++)
                    {
                        float dx = Mathf.Abs(x - cx);
                        float half = Mathf.Lerp(1.6f, 0.8f, k);
                        if (dx <= half) px[y * w + x] = dx > half - 0.7f ? dark : body;
                    }
                    // little leaves off to alternate sides
                    if (y % 7 == 3 && y < h - 3)
                    {
                        int side = (y / 7) % 2 == 0 ? 1 : -1;
                        for (int i = 1; i <= 3; i++) Put(px, w, h, Mathf.RoundToInt(cx) + side * (i + 1), y + i / 2, body);
                    }
                }
                return Make(px, w, h, new Vector2(0.5f, 0f), UiPpu, Vector4.zero);
            });
        }
    }

    /// <summary>A small air bubble.</summary>
    public static Sprite UiBubble
    {
        get
        {
            return Cached("UiBubble", () => FromMap(new[]
            {
                "..ooo..",
                ".oWiio.",
                "oWiiiio",
                "oiiiiio",
                "oiiiiio",
                ".oiiio.",
                "..ooo..",
            }, new Dictionary<char, Color>
            {
                { 'o', new Color(0.80f, 0.95f, 1f, 0.9f) },
                { 'i', new Color(0.60f, 0.85f, 1f, 0.22f) },
                { 'W', Color.white },
            }, new Vector2(3.5f, 3.5f), UiPpu));
        }
    }

    // ================================================================== SCREEN: notes, ring, effects
    /// <summary>A round "gem" for a note, shaded in greys so a tint gives it a lit, rounded look. Its rim is darker.</summary>
    public static Sprite UiNoteBody
    {
        get
        {
            return Cached("UiNoteBody", () =>
            {
                const int n = 22;
                Color[] px = Blank(n, n);
                float r = n * 0.5f;
                for (int y = 0; y < n; y++)
                    for (int x = 0; x < n; x++)
                    {
                        float dx = x + 0.5f - r, dy = y + 0.5f - r;
                        float d = Mathf.Sqrt(dx * dx + dy * dy);
                        if (d > r) continue;
                        float g;
                        if (d > r - 1.3f) g = 0.32f;                                  // dark rim
                        else
                        {
                            float light = (-dx + dy) / (r * 1.6f);                    // light from the top-left
                            g = Mathf.Clamp(0.78f + light * 0.3f - (d / r) * 0.12f, 0.5f, 1f);
                            g = Mathf.Round(g * 6f) / 6f;                             // a few flat steps
                        }
                        px[y * n + x] = new Color(g, g, g, 1f);
                    }
                return Make(px, n, n, new Vector2(0.5f, 0.5f), UiPpu, Vector4.zero);
            });
        }
    }

    /// <summary>The white shine drawn on top of a note (not tinted).</summary>
    public static Sprite UiNoteShine
    {
        get
        {
            return Cached("UiNoteShine", () =>
            {
                const int n = 22;
                Color[] px = Blank(n, n);
                for (int y = 0; y < n; y++)
                    for (int x = 0; x < n; x++)
                    {
                        float ex = (x + 0.5f - 7f) / 3.4f, ey = (y + 0.5f - 16f) / 2f;
                        if (ex * ex + ey * ey <= 1f) px[y * n + x] = new Color(1f, 1f, 1f, 0.85f);
                    }
                px[4 * n + 16] = px[5 * n + 17] = new Color(1f, 1f, 1f, 0.45f);
                return Make(px, n, n, new Vector2(0.5f, 0.5f), UiPpu, Vector4.zero);
            });
        }
    }

    /// <summary>The target ring notes must be hit in.</summary>
    public static Sprite UiRing
    {
        get
        {
            return Cached("UiRing", () =>
            {
                const int n = 32;
                Color[] px = Blank(n, n);
                float r = n * 0.5f;
                for (int y = 0; y < n; y++)
                    for (int x = 0; x < n; x++)
                    {
                        float dx = x + 0.5f - r, dy = y + 0.5f - r;
                        float d = Mathf.Sqrt(dx * dx + dy * dy);
                        Color c = Color.clear;
                        if (d <= r && d > r - 1.2f) c = new Color(0.15f, 0.15f, 0.2f, 1f);         // dark outside edge
                        else if (d <= r - 1.2f && d > r - 3.6f) c = dy > 0 ? Color.white : new Color(0.82f, 0.82f, 0.86f);
                        else if (d <= r - 3.6f && d > r - 4.6f) c = new Color(0.15f, 0.15f, 0.2f, 1f); // dark inside edge
                        else if (d <= r - 4.6f) c = new Color(1f, 1f, 1f, 0.10f);                    // faint glassy middle
                        px[y * n + x] = c;
                    }
                return Make(px, n, n, new Vector2(0.5f, 0.5f), UiPpu, Vector4.zero);
            });
        }
    }

    /// <summary>A soft round glow (white; tint it).</summary>
    public static Sprite UiGlow
    {
        get
        {
            return Cached("UiGlow", () =>
            {
                const int n = 24;
                Color[] px = Blank(n, n);
                float r = n * 0.5f;
                for (int y = 0; y < n; y++)
                    for (int x = 0; x < n; x++)
                    {
                        float dx = x + 0.5f - r, dy = y + 0.5f - r;
                        float a = Mathf.Clamp01(1f - Mathf.Sqrt(dx * dx + dy * dy) / r);
                        a = Mathf.Floor(a * a * 6f) / 6f;
                        px[y * n + x] = new Color(1f, 1f, 1f, a);
                    }
                return Make(px, n, n, new Vector2(0.5f, 0.5f), UiPpu, Vector4.zero);
            });
        }
    }

    /// <summary>A tiny four-pointed sparkle (white; tint it).</summary>
    public static Sprite UiSpark
    {
        get
        {
            return Cached("UiSpark", () => FromMap(new[]
            {
                "...a...",
                "...b...",
                "..aWa..",
                "abWWWba",
                "..aWa..",
                "...b...",
                "...a...",
            }, new Dictionary<char, Color>
            {
                { 'W', Color.white },
                { 'b', new Color(1f, 1f, 1f, 0.8f) },
                { 'a', new Color(1f, 1f, 1f, 0.4f) },
            }, new Vector2(3.5f, 3.5f), UiPpu));
        }
    }

    /// <summary>A five-pointed star for the rating (white with a dark edge; tint it gold or grey).</summary>
    public static Sprite UiStar
    {
        get
        {
            return Cached("UiStar", () =>
            {
                const int n = 13;
                Color[] px = Blank(n, n);
                float c = n * 0.5f;
                for (int y = 0; y < n; y++)
                    for (int x = 0; x < n; x++)
                    {
                        float dx = x + 0.5f - c, dy = y + 0.5f - c;
                        if (InStar(dx, dy, 6.4f)) px[y * n + x] = InStar(dx, dy, 4.9f) ? (dy > 1f ? Color.white : new Color(0.86f, 0.86f, 0.86f)) : new Color(0.35f, 0.25f, 0.1f);
                    }
                return Make(px, n, n, new Vector2(0.5f, 0.5f), UiPpu, Vector4.zero);
            });
        }
    }

    /// <summary>Light rays spreading from the middle (behind a caught fish).</summary>
    public static Sprite UiSunburst
    {
        get
        {
            return Cached("UiSunburst", () =>
            {
                const int n = 48;
                Color[] px = Blank(n, n);
                float c = n * 0.5f;
                for (int y = 0; y < n; y++)
                    for (int x = 0; x < n; x++)
                    {
                        float dx = x + 0.5f - c, dy = y + 0.5f - c;
                        float r = Mathf.Sqrt(dx * dx + dy * dy) / c;
                        if (r > 1f) continue;
                        float ray = Mathf.Pow(Mathf.Max(0f, Mathf.Cos(Mathf.Atan2(dy, dx) * 6f)), 3f);
                        float a = Mathf.Floor(ray * (1f - r) * 4f) / 4f;
                        px[y * n + x] = new Color(1f, 1f, 1f, a);
                    }
                return Make(px, n, n, new Vector2(0.5f, 0.5f), UiPpu, Vector4.zero);
            });
        }
    }

    // ================================================================== helpers
    /// <summary>True if (dx, dy) is inside a five-pointed star with the given outer radius, one point straight up.</summary>
    private static bool InStar(float dx, float dy, float outer)
    {
        float r = Mathf.Sqrt(dx * dx + dy * dy);
        if (r < 0.01f) return true;
        float a = Mathf.Atan2(dx, dy) * Mathf.Rad2Deg;      // 0 = straight up
        a = Mathf.Repeat(a, 72f);
        float k = Mathf.Abs(a - 36f) / 36f;                  // 1 at a point, 0 between two points
        float inner = outer * 0.45f;
        return r <= Mathf.Lerp(inner, outer, k * k);
    }

    private static Color[] Blank(int w, int h)
    {
        var px = new Color[w * h];
        for (int i = 0; i < px.Length; i++) px[i] = Color.clear;
        return px;
    }

    private static void Put(Color[] px, int w, int h, int x, int y, Color c)
    {
        if (x >= 0 && y >= 0 && x < w && y < h) px[y * w + x] = c;
    }

    /// <summary>Turns a character map into a sprite. 'pivotPixels' is measured from the bottom-left corner.</summary>
    private static Sprite FromMap(string[] rows, Dictionary<char, Color> palette, Vector2 pivotPixels, float ppu)
    {
        int h = rows.Length, w = 0;
        foreach (string r in rows) w = Mathf.Max(w, r.Length);
        Color[] px = Blank(w, h);
        for (int row = 0; row < h; row++)
        {
            int y = h - 1 - row;                             // the first row of the map is the TOP of the picture
            for (int x = 0; x < rows[row].Length; x++)
            {
                Color c;
                if (palette.TryGetValue(rows[row][x], out c)) px[y * w + x] = c;
            }
        }
        return Make(px, w, h, new Vector2(pivotPixels.x / w, pivotPixels.y / h), ppu, Vector4.zero);
    }

    private static Sprite Make(Color[] px, int w, int h, Vector2 pivot, float ppu, Vector4 border)
    {
        var tex = new Texture2D(w, h, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp };
        tex.SetPixels(px);
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, w, h), pivot, ppu, 0, SpriteMeshType.FullRect, border);
    }
}
