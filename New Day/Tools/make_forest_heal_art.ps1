# Draws the pixel art for the Healing Forest: every piece of the forest in TWO versions, "_sick" (polluted) and
# "_healthy" (after the zone is healed). Both versions of a piece are drawn from the same shape, so they line up
# exactly and the game can swap one for the other in place.
#
#     powershell -ExecutionPolicy Bypass -File Tools\make_forest_heal_art.ps1 -Project "<project folder>"
#
# It writes ONE PNG PER PICTURE into Assets\Project\Art\ForestHeal (no sprite sheets, so nothing to slice),
# except the animals, which are sheets of 24 x 24 frames (see "the animals" below).
# 16 pixels = 1 unit. Feet / roots are on the bottom edge of every picture.
#
# What it makes (name_sick + name_healthy unless noted):
#   ground_0..2, path_0..1, water_0..1   16 x 16 floor tiles (they tile seamlessly)
#   tree_broad 32x48, tree_pine 32x48, tree_oak 48x64, bush, tuft, puddle, mushroom, rock 16x16, log 32x16
#   heart_stump 24x24          the zone's stump that takes your offerings
#   blocker_bramble 48x32      zone 1: a bramble wall (sick) / brambles pulled back to the sides (healthy)
#   blocker_bridge 32x48       zone 2: a broken bridge / a whole bridge (it crosses a creek running left-right)
#   blocker_goo 48x32          zone 3: a goo pool / dried, cracked mud you can walk on
#   blocker_logpile 48x32      zone 4: a pile of trunks / the trunks rolled to the sides
#   blocker_arch 48x48         zone 5: a thorn arch full of thorns / an open, flowering arch
#   blocker_fog_sick 64x32     zone 6: a fog bank (only a sick version: it simply fades away)
#   critter_rabbit / squirrel / hedgehog / bird / turtle   144 x 72 sheets: 6 frames x 3 rows (idle, move, special),
#                              plus critter_<animal>_shadow, the same frames in shadow colours (before the zone heals)
#
# Change a colour in the palettes near the top of the C# code below and run the script again.
# Every random choice uses a fixed seed, so running it again gives exactly the same pictures.
# -OutDir writes somewhere else instead (handy for trying out colours without touching the project).
param([string]$Project = (Split-Path -Parent $PSScriptRoot), [string]$OutDir = "")

$out = if ($OutDir -ne "") { $OutDir } else { Join-Path $Project "Assets\Project\Art\ForestHeal" }
$shadowArt = Join-Path $Project "Assets\Project\Art\Forest"
New-Item -ItemType Directory -Force $out | Out-Null

$code = @'
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;

// A small picture we can draw on pixel by pixel. Colours are ARGB ints; 0 = see-through.
public class FhPic
{
    public int W, H;
    private int[] px;
    public FhPic(int w, int h) { W = w; H = h; px = new int[w * h]; }

    public bool In(int x, int y) { return x >= 0 && y >= 0 && x < W && y < H; }
    public void Set(int x, int y, int c) { if (In(x, y)) px[y * W + x] = c; }
    public void Clear(int x, int y) { if (In(x, y)) px[y * W + x] = 0; }
    // Set with wrap-around, for floor tiles (whatever leaves one edge comes back on the other, so tiles join seamlessly)
    public void SetW(int x, int y, int c) { x = ((x % W) + W) % W; y = ((y % H) + H) % H; px[y * W + x] = c; }
    public int Get(int x, int y) { return In(x, y) ? px[y * W + x] : 0; }
    public bool Solid(int x, int y) { return In(x, y) && ((px[y * W + x] >> 24) & 255) == 255; }

    // Gives the shape a dark edge: every see-through pixel next to a solid one gets that neighbour's colour, darkened.
    public void Outline(double darken)
    {
        int[] copy = (int[])px.Clone();
        int[] dx = { 0, 1, 0, -1 }, dy = { -1, 0, 1, 0 };
        for (int y = 0; y < H; y++)
            for (int x = 0; x < W; x++)
            {
                if (((copy[y * W + x] >> 24) & 255) != 0) continue;
                for (int k = 0; k < 4; k++)
                {
                    int nx = x + dx[k], ny = y + dy[k];
                    if (nx < 0 || ny < 0 || nx >= W || ny >= H) continue;
                    int c = copy[ny * W + nx];
                    if (((c >> 24) & 255) != 255) continue;
                    px[y * W + x] = FH.Dark(c, darken);
                    break;
                }
            }
    }

    // The same dark edge, but all in one colour (used for the animals, so they read clearly against any ground).
    public void OutlineWith(int color)
    {
        int[] copy = (int[])px.Clone();
        for (int y = 0; y < H; y++)
            for (int x = 0; x < W; x++)
            {
                if (((copy[y * W + x] >> 24) & 255) != 0) continue;
                bool edge = false;
                if (x > 0 && ((copy[y * W + x - 1] >> 24) & 255) == 255) edge = true;
                if (x < W - 1 && ((copy[y * W + x + 1] >> 24) & 255) == 255) edge = true;
                if (y > 0 && ((copy[(y - 1) * W + x] >> 24) & 255) == 255) edge = true;
                if (y < H - 1 && ((copy[(y + 1) * W + x] >> 24) & 255) == 255) edge = true;
                if (edge) px[y * W + x] = color;
            }
    }

    // Copies another picture onto this one with its top-left corner at (ox, oy).
    public void Blit(FhPic src, int ox, int oy)
    {
        for (int y = 0; y < src.H; y++)
            for (int x = 0; x < src.W; x++)
            {
                int c = src.Get(x, y);
                if (((c >> 24) & 255) != 0) Set(ox + x, oy + y, c);
            }
    }

    public void Save(string dir, string name)
    {
        using (var b = new Bitmap(W, H, PixelFormat.Format32bppArgb))
        {
            for (int y = 0; y < H; y++)
                for (int x = 0; x < W; x++)
                    b.SetPixel(x, y, Color.FromArgb(px[y * W + x]));
            b.Save(Path.Combine(dir, name + ".png"), ImageFormat.Png);
        }
        FH.Written++;
    }
}

public static class FH
{
    public static int Written;
    static string Out;

    // ------------------------------------------------------------------ colours
    public static int C(string hex) { return unchecked((int)0xFF000000) | Convert.ToInt32(hex.TrimStart('#'), 16); }
    public static int[] R(params string[] hex) { var a = new int[hex.Length]; for (int i = 0; i < hex.Length; i++) a[i] = C(hex[i]); return a; }
    public static int Alpha(int c, int a) { return (c & 0xFFFFFF) | (a << 24); }
    public static int Dark(int c, double f)
    {
        int r = (int)(((c >> 16) & 255) * f), g = (int)(((c >> 8) & 255) * f), b = (int)((c & 255) * f);
        return unchecked((int)0xFF000000) | (r << 16) | (g << 8) | b;
    }
    // t = 0 picks the darkest colour of a ramp, t = 1 the lightest
    static int Pick(int[] ramp, double t) { int i = (int)Math.Floor(t * ramp.Length); if (i < 0) i = 0; if (i >= ramp.Length) i = ramp.Length - 1; return ramp[i]; }
    // the same "random" number for the same (x, y, seed), every run
    static double Hash(int x, int y, int s)
    {
        unchecked
        {
            int h = x * 374761393 + y * 668265263 + s * 1442695040;
            h = (h ^ (h >> 13)) * 1274126177;
            h ^= h >> 16;
            return (h & 0x7fffffff) / 2147483647.0;
        }
    }

    // Each ramp goes from dark to light.
    static readonly int[] BarkSick    = R("#1e1826", "#2e2638", "#40364c", "#564a62");
    static readonly int[] BarkHealthy = R("#3a2414", "#5a3a22", "#7a5030", "#9a6a40");
    static readonly int[] Leaves      = R("#244a1c", "#336a26", "#4f8f34", "#6cae40", "#8cc850");
    static readonly int LeafShine     = C("#b4e070");
    static readonly int[] PineHealthy = R("#173a24", "#215032", "#2f6a3a", "#46884a", "#66a85a");
    static readonly int[] PineSick    = R("#2e2a2a", "#463c36", "#5e5046", "#786654");
    static readonly int[] Goo         = R("#3a1450", "#5a2080", "#8a2fb0", "#b860dc");
    static readonly int GooShine      = C("#e0a0f8");
    static readonly int GooGlow       = C("#ff46be");
    static readonly int[] Water       = R("#1e4a7a", "#2e6aa8", "#3f86c4", "#5aa0d8");
    static readonly int WaterShine    = C("#c8e8ff");
    static readonly int[] WaterSick   = R("#1e2e2c", "#2a3e3a", "#365048", "#466256");
    static readonly int[] GrassHealthy = R("#467830", "#538a38", "#609a42");
    static readonly int[] GrassSick   = R("#30283a", "#3e3442", "#4a3e4c");
    static readonly int[] DirtHealthy = R("#7a5634", "#8e6840", "#a07850", "#b48c60");
    static readonly int[] DirtSick    = R("#3a3036", "#463a40", "#54464a", "#62545a");
    static readonly int[] WoodHealthy = R("#4a2e18", "#6a4424", "#8a5c34", "#a87444");
    static readonly int[] WoodSick    = R("#2a2430", "#3c3440", "#4e4452", "#625668");
    static readonly int[] RingsHealthy = R("#a37a48", "#c9a06a");
    static readonly int[] RingsSick   = R("#463c54", "#5e5068");
    static readonly int[] Stone       = R("#3a3a44", "#54545e", "#6e6e78", "#8a8a94", "#a8a8b0");
    static readonly int[] Moss        = R("#3a6a28", "#5a9038", "#7ab048");
    static readonly int[] Mud         = R("#6a4a30", "#7e5c3c", "#927050", "#a88460");
    static readonly int[] CapHealthy  = R("#7a1e1e", "#a8302a", "#cc4a3a", "#e06a50");
    static readonly int[] CapSick     = R("#2a1e34", "#3e2c4c", "#544064", "#6a5480");
    static readonly int[] StemHealthy = R("#a89878", "#c8b898", "#e4d8bc");
    static readonly int[] StemSick    = R("#4a4250", "#5e5466", "#72687a");
    static readonly int[] VineSick    = R("#1a0e24", "#2a1838", "#3e2650");
    static readonly int[] VineHealthy = R("#24401a", "#345824", "#4a7432");
    static readonly int ThornSick = C("#a070c0"), ThornHealthy = C("#c8b080");
    static readonly int FlowerWhite = C("#f4ecf0"), FlowerPink = C("#f090b0"), FlowerMiddle = C("#f0c040");
    static readonly int DeadLeaf = C("#6a5040");

    // ------------------------------------------------------------------ drawing helpers
    // A round, shaded lump lit from the top left (leaves, rocks, mushroom caps).
    static void Blob(FhPic p, double cx, double cy, double rx, double ry, int[] ramp, Random rnd, double noise)
    {
        for (int y = (int)Math.Floor(cy - ry - 1); y <= (int)Math.Ceiling(cy + ry + 1); y++)
            for (int x = (int)Math.Floor(cx - rx - 1); x <= (int)Math.Ceiling(cx + rx + 1); x++)
            {
                double dx = (x + 0.5 - cx) / rx, dy = (y + 0.5 - cy) / ry, d2 = dx * dx + dy * dy;
                if (d2 > 1) continue;
                double t = 0.55 - 0.32 * dx - 0.42 * dy - 0.18 * d2 + (rnd.NextDouble() - 0.5) * noise;
                p.Set(x, y, Pick(ramp, t));
            }
    }

    static void Line(FhPic p, double x0, double y0, double x1, double y1, int c, int thick)
    {
        int n = (int)Math.Ceiling(Math.Max(Math.Abs(x1 - x0), Math.Abs(y1 - y0)));
        for (int i = 0; i <= n; i++)
        {
            double t = n == 0 ? 0 : (double)i / n;
            int x = (int)Math.Floor(x0 + (x1 - x0) * t), y = (int)Math.Floor(y0 + (y1 - y0) * t);
            p.Set(x, y, c);
            if (thick > 1) p.Set(x + 1, y, c);
        }
    }

    static bool InEllipse(double x, double y, double cx, double cy, double rx, double ry)
    {
        double dx = (x + 0.5 - cx) / rx, dy = (y + 0.5 - cy) / ry;
        return dx * dx + dy * dy <= 1;
    }

    // ------------------------------------------------------------------ entry point
    public static void Run(string outDir, string shadowDir)
    {
        Out = outDir;
        for (int v = 0; v < 3; v++) { Ground(v, false).Save(Out, "ground_" + v + "_sick"); Ground(v, true).Save(Out, "ground_" + v + "_healthy"); }
        for (int v = 0; v < 2; v++) { PathTile(v, false).Save(Out, "path_" + v + "_sick"); PathTile(v, true).Save(Out, "path_" + v + "_healthy"); }
        for (int v = 0; v < 2; v++) { WaterTile(v, false).Save(Out, "water_" + v + "_sick"); WaterTile(v, true).Save(Out, "water_" + v + "_healthy"); }

        Pair("tree_broad", delegate (bool h) { return BroadTree(32, 48, 11, h, 5, 9, 26, 7.5, new double[] { 10, 19, 7, 22, 18, 7, 16, 12, 8, 9, 11, 6, 23, 10, 6, 16, 6, 5, 16, 20, 6 }); });
        Pair("tree_oak", delegate (bool h) { return BroadTree(48, 64, 12, h, 8, 16, 34, 11, new double[] { 12, 26, 10, 36, 25, 10, 24, 16, 12, 11, 15, 9, 37, 14, 9, 24, 7, 8, 24, 27, 9, 17, 9, 7, 31, 8, 7 }); });
        Pair("tree_pine", Pine);
        Pair("bush", Bush);
        Pair("tuft", Tuft);
        Pair("puddle", Puddle);
        Pair("mushroom", Mushroom);
        Pair("rock", Rock);
        Pair("log", Log);
        Pair("heart_stump", HeartStump);
        Pair("blocker_bramble", Bramble);
        Pair("blocker_bridge", Bridge);
        Pair("blocker_goo", GooPool);
        Pair("blocker_logpile", LogPile);
        Pair("blocker_arch", Arch);
        Fog().Save(Out, "blocker_fog_sick");

        Animals();
    }

    delegate FhPic Maker(bool healthy);
    static void Pair(string name, Maker make) { make(false).Save(Out, name + "_sick"); make(true).Save(Out, name + "_healthy"); }

    // ------------------------------------------------------------------ floor tiles (16 x 16, seamless)
    static FhPic Ground(int v, bool healthy)
    {
        var p = new FhPic(16, 16);
        var r = new Random(100 + v * 7 + (healthy ? 0 : 50));
        int[] ramp = healthy ? GrassHealthy : GrassSick;
        for (int y = 0; y < 16; y++)
            for (int x = 0; x < 16; x++)
                p.Set(x, y, Pick(ramp, Hash(x / 2, y / 2, v * 13 + 1) * 0.5 + r.NextDouble() * 0.5));
        int bladeA = healthy ? C("#8cc850") : C("#6a5a5a"), bladeB = healthy ? C("#a8dc68") : C("#7a6a60");
        int blades = healthy ? 8 : 5;
        for (int i = 0; i < blades; i++)
        {
            int x = r.Next(16), y = r.Next(16);
            p.SetW(x, y, bladeA); p.SetW(x, y - 1, bladeB);
            if (healthy) p.SetW(x + 1, y - 2, bladeB); else p.SetW(x + 1, y - 1, ramp[0]);
        }
        if (!healthy && v == 1)
        {
            // a goo speck soaking into the ground
            int gx = 4 + r.Next(8), gy = 4 + r.Next(8);
            p.SetW(gx, gy, Goo[2]); p.SetW(gx + 1, gy, Goo[1]); p.SetW(gx, gy + 1, Goo[1]); p.SetW(gx + 1, gy - 1, GooShine);
        }
        if (!healthy && v == 2)
        {
            // a crack in the dry ground
            int cx = r.Next(16), cy = r.Next(16);
            for (int i = 0; i < 7; i++) { p.SetW(cx, cy, C("#221c2a")); cx += r.Next(2); cy += r.Next(3) - 1; }
        }
        if (healthy && v == 2)
        {
            // one tiny flower
            int fx = 4 + r.Next(8), fy = 4 + r.Next(8);
            p.SetW(fx, fy, FlowerMiddle); p.SetW(fx - 1, fy, FlowerWhite); p.SetW(fx + 1, fy, FlowerWhite); p.SetW(fx, fy - 1, FlowerWhite); p.SetW(fx, fy + 1, FlowerWhite);
        }
        return p;
    }

    static FhPic PathTile(int v, bool healthy)
    {
        var p = new FhPic(16, 16);
        var r = new Random(200 + v * 7 + (healthy ? 0 : 50));
        int[] ramp = healthy ? DirtHealthy : DirtSick;
        for (int y = 0; y < 16; y++)
            for (int x = 0; x < 16; x++)
                p.Set(x, y, Pick(ramp, 0.15 + Hash(x / 2, y / 2, v * 17 + 3) * 0.45 + r.NextDouble() * 0.35));
        int pebble = healthy ? C("#c0b8a8") : C("#6e6470"), pebbleDark = healthy ? C("#6a5040") : C("#2a2230");
        for (int i = 0; i < 3; i++)
        {
            int x = r.Next(16), y = r.Next(16);
            p.SetW(x, y, pebble); p.SetW(x + 1, y, pebble); p.SetW(x, y + 1, pebbleDark); p.SetW(x + 1, y + 1, pebbleDark);
        }
        if (!healthy && v == 1)
        {
            int gx = r.Next(16), gy = r.Next(16);
            for (int i = 0; i < 5; i++) p.SetW(gx + i % 3, gy + i / 3, i == 0 ? GooShine : Goo[1 + i % 2]);
        }
        return p;
    }

    static FhPic WaterTile(int v, bool healthy)
    {
        var p = new FhPic(16, 16);
        var r = new Random(300 + v * 7 + (healthy ? 0 : 50));
        int[] ramp = healthy ? Water : WaterSick;
        // mostly one colour, with short darker streaks running left to right (the way the creek flows)
        for (int y = 0; y < 16; y++)
            for (int x = 0; x < 16; x++)
                p.Set(x, y, ramp[2]);
        for (int i = 0; i < 9; i++)
        {
            int x = r.Next(16), y = r.Next(16), len = 3 + r.Next(4);
            for (int k = 0; k < len; k++) p.SetW(x + k, y, ramp[1]);
        }
        int shine = healthy ? WaterShine : C("#8a4aa8");
        for (int i = 0; i < 5; i++)
        {
            // little ripple lines
            int x = r.Next(16), y = r.Next(16), len = 2 + r.Next(3);
            for (int k = 0; k < len; k++) p.SetW(x + k, y, k == 0 || k == len - 1 ? ramp[3] : shine);
            p.SetW(x + 1, y + 1, ramp[0]);
        }
        if (!healthy)
        {
            // floating green scum
            int sx = r.Next(16), sy = r.Next(16);
            p.SetW(sx, sy, C("#4a6a3a")); p.SetW(sx + 1, sy, C("#5a7a44")); p.SetW(sx + 1, sy + 1, C("#3a5030"));
        }
        return p;
    }

    // ------------------------------------------------------------------ trees
    // A leafy tree. 'blobs' is a list of leaf clumps (x, y, radius, x, y, radius, ...) drawn back to front.
    // The trunk and the branches come from the same seed in both versions, so the sick tree is the healthy tree without leaves.
    static FhPic BroadTree(int W, int H, int seed, bool healthy, double trunkW, double rootW, int trunkTop, double branchLen, double[] blobs)
    {
        var p = new FhPic(W, H);
        var shape = new Random(seed);
        var tex = new Random(seed + 1000 + (healthy ? 0 : 1));
        int[] bark = healthy ? BarkHealthy : BarkSick;
        double cx = W / 2.0;

        // trunk: narrower towards the top, flaring into roots at the bottom
        for (int y = H - 1; y >= trunkTop; y--)
        {
            double t = (double)(H - 1 - y) / (H - 1 - trunkTop);
            double w = trunkW * (1 - 0.35 * t);
            if (y > H - 5) w += (rootW - trunkW) * ((y - (H - 5)) / 4.0);
            int x0 = (int)Math.Round(cx - w / 2), x1 = (int)Math.Round(cx + w / 2) - 1;
            for (int x = x0; x <= x1; x++)
            {
                double rel = (x1 == x0) ? 0.5 : (double)(x - x0) / (x1 - x0);
                int c = rel < 0.3 ? bark[3] : rel > 0.7 ? bark[1] : bark[2];
                if (shape.NextDouble() < 0.12) c = bark[0];
                p.Set(x, y, c);
            }
        }

        // branches: three main limbs that fork twice
        var tips = new List<int[]>();
        double[] angles = { -Math.PI / 2 - 0.65, -Math.PI / 2, -Math.PI / 2 + 0.65 };
        foreach (double a in angles) Branch(p, shape, cx - 0.5, trunkTop + 1, a, branchLen, 2, bark, tips);

        if (healthy)
        {
            for (int i = 0; i + 2 < blobs.Length; i += 3)
                Blob(p, blobs[i], blobs[i + 1], blobs[i + 2], blobs[i + 2] * 0.9, Leaves, tex, 0.25);
            // a few bright leaves where the sun hits
            for (int i = 0; i < W * H / 40; i++)
            {
                int x = tex.Next(W), y = tex.Next(trunkTop + 4);
                if (p.Get(x, y) == Leaves[3] || p.Get(x, y) == Leaves[4]) p.Set(x, y, LeafShine);
            }
            p.Outline(0.45);
        }
        else
        {
            // purple drips hanging from some twig ends, and a couple of dead leaves still clinging on
            for (int i = 0; i < tips.Count; i++)
            {
                int[] t = tips[i];
                if (i % 3 == 0) { p.Set(t[0], t[1] + 1, Goo[2]); p.Set(t[0], t[1] + 2, Goo[3]); }
                else if (i % 4 == 1) { p.Set(t[0], t[1], DeadLeaf); p.Set(t[0] + 1, t[1], Dark(DeadLeaf, 0.7)); }
            }
            // a little goo pooled around the roots
            for (int y = H - 2; y < H; y++)
                for (int x = 0; x < W; x++)
                    if (!p.Solid(x, y) && InEllipse(x, y, cx, H - 0.5, rootW * 0.85, 1.6)) p.Set(x, y, tex.NextDouble() < 0.2 ? GooShine : Goo[1]);
            p.Outline(0.5);
        }
        return p;
    }

    static void Branch(FhPic p, Random rnd, double x, double y, double ang, double len, int depth, int[] bark, List<int[]> tips)
    {
        double x2 = x + Math.Cos(ang) * len, y2 = y + Math.Sin(ang) * len;
        Line(p, x, y, x2, y2, depth >= 2 ? bark[2] : bark[3], depth >= 2 ? 2 : 1);
        if (depth == 0) { tips.Add(new int[] { (int)Math.Floor(x2), (int)Math.Floor(y2) }); return; }
        for (int k = 0; k < 2; k++)
            Branch(p, rnd, x2, y2, ang + (k == 0 ? -1 : 1) * (0.35 + rnd.NextDouble() * 0.35), len * (0.6 + rnd.NextDouble() * 0.15), depth - 1, bark, tips);
    }

    // A pine: five tiers of needles. The sick one is brown and full of holes.
    static FhPic Pine(bool healthy)
    {
        var p = new FhPic(32, 48);
        var tex = new Random(21 + (healthy ? 0 : 1));
        int[] bark = healthy ? BarkHealthy : BarkSick;
        int[] ramp = healthy ? PineHealthy : PineSick;
        for (int y = 8; y < 48; y++) { p.Set(15, y, bark[3]); p.Set(16, y, bark[2]); p.Set(17, y, bark[1]); }
        p.Set(14, 47, bark[2]); p.Set(18, 47, bark[1]);
        for (int i = 0; i < 5; i++)
        {
            int apex = 2 + i * 7, h = 13;
            double baseHalf = 4 + i * 2.5;
            for (int y = apex; y <= apex + h; y++)
            {
                double hw = baseHalf * (y - apex + 1) / (h + 1);
                for (int x = (int)Math.Floor(16 - hw); x <= (int)Math.Ceiling(16 + hw) - 1; x++)
                {
                    if (y >= apex + h - 1 && (x + i) % 3 == 0) continue;                 // jagged needle tips at the bottom of each tier
                    if (!healthy && Hash(x / 2, y / 2, 77) < 0.33) continue;            // bare patches
                    double rel = (x + 0.5 - 16) / Math.Max(1, hw);
                    double t = 0.6 - rel * 0.3 - ((double)(y - apex) / h) * 0.3 + (tex.NextDouble() - 0.5) * 0.2;
                    p.Set(x, y, Pick(ramp, t));
                }
            }
        }
        if (!healthy) { p.Set(9, 26, Goo[2]); p.Set(9, 27, Goo[3]); p.Set(22, 33, Goo[2]); p.Set(22, 34, Goo[3]); }
        p.Outline(healthy ? 0.45 : 0.5);
        return p;
    }

    // ------------------------------------------------------------------ small scenery (16 x 16)
    static FhPic Bush(bool healthy)
    {
        var p = new FhPic(16, 16);
        var shape = new Random(31);
        var tex = new Random(32 + (healthy ? 0 : 1));
        int[] bark = healthy ? BarkHealthy : BarkSick;
        // twigs (hidden by leaves on the healthy bush)
        for (int i = 0; i < 6; i++)
        {
            double a = -Math.PI / 2 + (shape.NextDouble() - 0.5) * 2.2;
            double len = 5 + shape.NextDouble() * 3;
            Line(p, 8, 15, 8 + Math.Cos(a) * len, 15 + Math.Sin(a) * len, bark[2 + i % 2], 1);
        }
        if (healthy)
        {
            Blob(p, 5, 11, 4, 3.6, Leaves, tex, 0.25);
            Blob(p, 11, 11, 4, 3.6, Leaves, tex, 0.25);
            Blob(p, 8, 8, 4.5, 4, Leaves, tex, 0.25);
            int berry = C("#c83a4a");
            p.Set(5, 9, berry); p.Set(10, 7, berry); p.Set(12, 11, berry);
            p.Outline(0.45);
        }
        else
        {
            p.Set(4, 8, DeadLeaf); p.Set(12, 9, DeadLeaf); p.Set(9, 6, Goo[2]); p.Set(9, 7, Goo[3]);
            p.Outline(0.5);
        }
        return p;
    }

    static FhPic Tuft(bool healthy)
    {
        var p = new FhPic(16, 16);
        var shape = new Random(41);
        int[] cols = healthy ? R("#3e7428", "#5a9438", "#8cc850", "#a8dc68") : R("#4a3e3a", "#6a5a4a", "#857060", "#9a8470");
        for (int i = 0; i < 7; i++)
        {
            int x = 3 + (int)(i * 1.6) + shape.Next(2);
            int h = 3 + shape.Next(5);
            int lean = shape.Next(3) - 1;
            if (!healthy) h = Math.Max(2, h * 3 / 5);
            for (int k = 0; k < h; k++)
            {
                int xx = x + (k * lean) / 3, yy = 15 - k;
                p.Set(xx, yy, cols[k == h - 1 ? 3 : (i % 2 == 0 ? 1 : 2)]);
            }
            if (!healthy) p.Set(x + (h * lean) / 3 + (lean >= 0 ? 1 : -1), 15 - h + 2, cols[0]);  // bent, dried tip
            p.Set(x, 15, cols[0]);
        }
        return p;
    }

    static FhPic Puddle(bool healthy)
    {
        var p = new FhPic(16, 16);
        var tex = new Random(51 + (healthy ? 0 : 1));
        int[] ramp = healthy ? Water : Goo;
        for (int y = 0; y < 16; y++)
            for (int x = 0; x < 16; x++)
            {
                double dx = (x + 0.5 - 8) / 6.5, dy = (y + 0.5 - 11.5) / 3.2, d2 = dx * dx + dy * dy;
                if (d2 > 1) continue;
                p.Set(x, y, d2 > 0.72 ? ramp[0] : ramp[1 + (tex.NextDouble() < 0.3 ? 0 : 1)]);
            }
        int shine = healthy ? WaterShine : GooShine;
        p.Set(5, 10, shine); p.Set(6, 10, shine); p.Set(7, 10, ramp[3]); p.Set(10, 12, ramp[3]);
        if (!healthy) { p.Set(10, 11, GooShine); p.Set(11, 11, Goo[3]); p.Set(9, 11, Goo[3]); }  // a bubble
        return p;
    }

    static FhPic Mushroom(bool healthy)
    {
        var p = new FhPic(16, 16);
        var tex = new Random(61 + (healthy ? 0 : 1));
        int[] cap = healthy ? CapHealthy : CapSick, stem = healthy ? StemHealthy : StemSick;
        // big one
        for (int y = 9; y <= 14; y++) { p.Set(8, y, stem[2]); p.Set(9, y, stem[1]); p.Set(10, y, stem[0]); }
        Cap(p, 9.5, 9.5, 5, 4, cap, tex);
        // small one
        for (int y = 12; y <= 14; y++) { p.Set(3, y, stem[2]); p.Set(4, y, stem[1]); }
        Cap(p, 4, 12, 3, 2.5, cap, tex);
        int spot = healthy ? C("#f4ecdc") : C("#c060e0");
        p.Set(7, 7, spot); p.Set(11, 6, spot); p.Set(12, 8, spot); p.Set(9, 5, spot); p.Set(3, 10, spot);
        if (!healthy)
        {
            // drooping cap rims
            p.Set(5, 10, cap[0]); p.Set(5, 11, cap[0]); p.Set(14, 10, cap[0]); p.Set(14, 11, cap[0]);
        }
        p.Outline(0.45);
        return p;
    }

    static void Cap(FhPic p, double cx, double cy, double rx, double ry, int[] ramp, Random rnd)
    {
        for (int y = (int)(cy - ry - 1); y <= (int)(cy + 1); y++)
            for (int x = (int)(cx - rx - 1); x <= (int)(cx + rx + 1); x++)
            {
                double dx = (x + 0.5 - cx) / rx, dy = (y + 0.5 - cy) / ry;
                if (dx * dx + dy * dy > 1 || y + 0.5 > cy + 0.6) continue;
                double t = 0.6 - 0.3 * dx - 0.35 * dy + (rnd.NextDouble() - 0.5) * 0.15;
                p.Set(x, y, Pick(ramp, t));
            }
    }

    static FhPic Rock(bool healthy)
    {
        var p = new FhPic(16, 16);
        var tex = new Random(71);
        Blob(p, 8, 10.5, 6.5, 4.8, Stone, tex, 0.2);
        var t2 = new Random(72 + (healthy ? 0 : 1));
        if (healthy)
        {
            // moss on top
            for (int y = 5; y < 10; y++)
                for (int x = 2; x < 14; x++)
                    if (p.Solid(x, y) && y < 8 + Hash(x, 0, 5) * 2) p.Set(x, y, Pick(Moss, 0.3 + t2.NextDouble() * 0.7 - (x - 2) * 0.03));
        }
        else
        {
            // purple stains dripping down
            int[] xs = { 5, 9, 11 };
            foreach (int x in xs)
            {
                int top = 6; while (top < 15 && !p.Solid(x, top)) top++;
                int len = 2 + t2.Next(3);
                for (int k = 0; k < len; k++) p.Set(x, top + k, k == 0 ? Goo[3] : Goo[1 + k % 2]);
            }
        }
        p.Outline(0.45);
        return p;
    }

    // A fallen log, 32 x 16, lying left to right with its cut end on the right.
    static FhPic Log(bool healthy)
    {
        var p = new FhPic(32, 16);
        var shape = new Random(81);
        var tex = new Random(82 + (healthy ? 0 : 1));
        int[] wood = healthy ? BarkHealthy : BarkSick;
        int[] rings = healthy ? RingsHealthy : RingsSick;
        for (int y = 7; y <= 13; y++)
            for (int x = 2; x <= 27; x++)
            {
                int c = y <= 8 ? wood[3] : y >= 12 ? wood[1] : wood[2];
                if (shape.NextDouble() < 0.1) c = wood[0];
                p.Set(x, y, c);
            }
        for (int y = 7; y <= 13; y++)
            for (int x = 26; x <= 30; x++)
                if (InEllipse(x, y, 28.5, 10.5, 2.6, 3.8))
                {
                    double d = Math.Sqrt(Math.Pow((x + 0.5 - 28.5) / 2.6, 2) + Math.Pow((y + 0.5 - 10.5) / 3.8, 2));
                    p.Set(x, y, d > 0.8 ? wood[1] : rings[((int)(d * 3)) % 2]);
                }
        if (healthy)
        {
            for (int x = 3; x <= 25; x++) if (Hash(x / 2, 0, 9) < 0.55) { p.Set(x, 7, Moss[1 + tex.Next(2)]); if (tex.NextDouble() < 0.4) p.Set(x, 6, Moss[1]); }
            // a little fern growing out of it
            int f = C("#5a9a3a"), f2 = C("#8cc850");
            p.Set(11, 6, f); p.Set(11, 5, f); p.Set(11, 4, f2); p.Set(10, 4, f); p.Set(12, 5, f); p.Set(13, 4, f2); p.Set(9, 3, f2);
        }
        else
        {
            // purple shelf fungus and a dark hole in the end
            p.Set(29, 10, C("#120e18")); p.Set(28, 10, C("#120e18")); p.Set(28, 11, C("#120e18"));
            int[] fx = { 8, 9, 10, 17, 18 };
            foreach (int x in fx) { p.Set(x, 11, Goo[2]); p.Set(x, 10, Goo[3]); }
            p.Set(8, 12, Goo[1]); p.Set(18, 12, Goo[1]);
        }
        p.Outline(0.45);
        return p;
    }

    // The zone's stump. Sick: dark with glowing purple cracks. Healthy: warm wood with a young sapling growing from the top.
    static FhPic HeartStump(bool healthy)
    {
        var p = new FhPic(24, 24);
        var shape = new Random(91);
        var tex = new Random(92 + (healthy ? 0 : 1));
        int[] bark = healthy ? BarkHealthy : BarkSick;
        int[] rings = healthy ? RingsHealthy : RingsSick;
        for (int y = 9; y <= 22; y++)
        {
            int flare = y >= 19 ? (y - 18) : 0;
            int x0 = 6 - flare, x1 = 17 + flare;
            for (int x = x0; x <= x1; x++)
            {
                if (y == 22 && (x == x0 + 2 || x == x1 - 2 || x == 11)) continue;   // gaps between the roots
                double rel = (double)(x - x0) / (x1 - x0);
                int c = rel < 0.25 ? bark[3] : rel > 0.75 ? bark[1] : bark[2];
                if (shape.NextDouble() < 0.12) c = bark[0];
                p.Set(x, y, c);
            }
        }
        // the cut top with its growth rings
        for (int y = 6; y <= 11; y++)
            for (int x = 5; x <= 18; x++)
                if (InEllipse(x, y, 12, 9, 6.5, 2.7))
                {
                    double d = Math.Sqrt(Math.Pow((x + 0.5 - 12) / 6.5, 2) + Math.Pow((y + 0.5 - 9) / 2.7, 2));
                    p.Set(x, y, d > 0.82 ? bark[3] : rings[((int)(d * 4)) % 2]);
                }
        if (healthy)
        {
            int stem = C("#4f8f34");
            for (int y = 2; y <= 8; y++) p.Set(12, y, stem);
            Blob(p, 9.5, 4.5, 2.2, 1.3, Leaves, tex, 0.1);
            Blob(p, 14.5, 3.5, 2.2, 1.3, Leaves, tex, 0.1);
            p.Set(12, 1, Leaves[4]);
            p.Outline(0.45);
            // soft green-gold sparkles around it (half see-through, added after the outline)
            int spark = Alpha(C("#e8f0a0"), 200);
            p.Set(3, 5, spark); p.Set(20, 7, spark); p.Set(17, 1, spark); p.Set(5, 14, spark);
        }
        else
        {
            // glowing cracks running down the front
            int[] starts = { 9, 14 };
            foreach (int sx in starts)
            {
                int x = sx;
                for (int y = 11; y <= 20; y++) { p.Set(x, y, y % 3 == 0 ? GooGlow : C("#c050e0")); if (tex.NextDouble() < 0.35) x += tex.Next(3) - 1; }
            }
            p.Outline(0.5);
            int wisp = Alpha(C("#b070e0"), 150);
            p.Set(10, 3, wisp); p.Set(11, 2, wisp); p.Set(15, 4, wisp); p.Set(14, 1, wisp);
        }
        return p;
    }

    // ------------------------------------------------------------------ the six blockers
    // Zone 1. A bramble hedge: a low, dense mound with thick canes arching over the top, covered in big hooked thorns.
    // Sick: one long dead hedge right across the path, purple-black, with glowing berries.
    // Healthy: the bramble has pulled back into two leafy bushes at the sides, with white flowers and blackberries.
    static FhPic Bramble(bool healthy)
    {
        var p = new FhPic(48, 32);
        var shape = new Random(101);
        var tex = new Random(102 + (healthy ? 0 : 1));
        int[] mound = healthy ? Leaves : R("#120a1a", "#1e1228", "#2a1a38", "#382448");
        int[] cane = healthy ? R("#4a1e1e", "#7a3028", "#a04a38") : R("#2a1838", "#4a2c60", "#6a4488");
        int thorn = healthy ? C("#e8d8b0") : C("#c8a0e8");

        // 1. the mound behind the canes: overlapping lumps along the bottom
        if (healthy)
        {
            Blob(p, 7, 23, 7, 7, mound, tex, 0.25); Blob(p, 4, 27, 5, 4.5, mound, tex, 0.25); Blob(p, 11, 27, 5, 4.5, mound, tex, 0.25);
            Blob(p, 40, 23, 7, 7, mound, tex, 0.25); Blob(p, 37, 27, 5, 4.5, mound, tex, 0.25); Blob(p, 44, 27, 5, 4.5, mound, tex, 0.25);
        }
        else
        {
            for (int i = 0; i < 7; i++)
                Blob(p, 4 + i * 6.6, 25 + (i % 2), 6, 6, mound, tex, 0.3);
            Blob(p, 24, 28, 23, 4, mound, tex, 0.3);
        }

        // 2. the arching canes in front: each one grows out of the ground, bends over in a wide hoop and roots
        //    itself again further along - the typical bramble shape. Each cane = left foot x, right foot x, height.
        var canes = new List<double[]>();
        if (healthy)
        {
            canes.Add(new double[] { 0, 12, 13 });
            canes.Add(new double[] { 4, 15, 9 });
            canes.Add(new double[] { 35, 47, 13 });
            canes.Add(new double[] { 32, 43, 9 });
        }
        else
        {
            // overlapping hoops right across, tall ones behind and lower ones in front
            double[][] hoops = { new double[] { 0, 18, 24 }, new double[] { 14, 34, 27 }, new double[] { 29, 47, 24 },
                                 new double[] { 6, 24, 17 }, new double[] { 22, 42, 18 }, new double[] { 1, 13, 11 }, new double[] { 34, 47, 12 } };
            foreach (double[] h in hoops) canes.Add(new double[] { h[0] + shape.Next(-1, 2), h[1] + shape.Next(-1, 2), h[2] });
        }
        foreach (double[] c in canes)
        {
            double cx = (c[0] + c[1]) / 2, rx = (c[1] - c[0]) / 2, ry = c[2];
            double travelled = 0, lastX = c[0], lastY = 31;
            int thornCount = 0;
            // so the hoops look grown, not built: each one leans a little to one side and wobbles
            double lean = (shape.NextDouble() - 0.5) * 6, wobble = shape.NextDouble() * 6;
            for (int k = 0; k <= 120; k++)
            {
                double a = Math.PI * k / 120.0;                       // 0 = left foot, PI = right foot
                double x = cx - rx * Math.Cos(a) + lean * Math.Sin(a);
                double y = 31 - ry * Math.Sin(a) + Math.Sin(a * 5 + wobble) * 0.9 * Math.Sin(a);
                int ix = (int)Math.Floor(x), iy = (int)Math.Floor(y);
                p.Set(ix, iy, cane[2]);                               // lit top edge of the cane
                p.Set(ix, iy + 1, cane[1]);                           // shaded underside: canes are 2 pixels thick
                travelled += Math.Sqrt((x - lastX) * (x - lastX) + (y - lastY) * (y - lastY));
                lastX = x; lastY = y;
                // a thorn every 4 pixels along the cane, pointing outwards (away from the middle of the hoop)
                if (travelled >= 4 * (thornCount + 1) && y < 30)
                {
                    thornCount++;
                    double nx = -Math.Cos(a) / rx, ny = -Math.Sin(a) / ry, len = Math.Sqrt(nx * nx + ny * ny);
                    nx /= len; ny /= len;
                    p.Set((int)Math.Floor(x + nx * 1.6), (int)Math.Floor(y + ny * 1.6), thorn);
                }
            }
        }

        // 3. details
        if (healthy)
        {
            int[][] flowers = { new int[] { 4, 18 }, new int[] { 10, 22 }, new int[] { 41, 18 }, new int[] { 37, 23 }, new int[] { 13, 27 }, new int[] { 44, 26 } };
            foreach (int[] f in flowers) Flower(p, f[0], f[1], FlowerWhite);
            // blackberries: little dark clusters with a shine
            int[][] berries = { new int[] { 6, 25 }, new int[] { 2, 21 }, new int[] { 39, 26 }, new int[] { 45, 21 } };
            foreach (int[] b in berries)
            {
                p.Set(b[0], b[1], C("#2a1430")); p.Set(b[0] + 1, b[1], C("#3a1e44")); p.Set(b[0], b[1] + 1, C("#3a1e44")); p.Set(b[0] + 1, b[1] + 1, C("#2a1430"));
                p.Set(b[0], b[1], C("#8a6aa0"));
            }
        }
        else
        {
            // glowing berries and a few dead leaves caught in the hedge
            for (int i = 0; i < 9; i++)
            {
                int x = 3 + tex.Next(42), y = 14 + tex.Next(14);
                if (!p.Solid(x, y)) continue;
                p.Set(x, y, GooGlow); p.Set(x + 1, y, C("#c050e0")); p.Set(x, y + 1, C("#c050e0"));
            }
            for (int i = 0; i < 5; i++) { int x = 3 + tex.Next(42), y = 16 + tex.Next(12); if (p.Solid(x, y)) p.Set(x, y, DeadLeaf); }
        }
        p.Outline(0.45);
        return p;
    }

    static void Flower(FhPic p, int x, int y, int petal)
    {
        p.Set(x, y, FlowerMiddle); p.Set(x - 1, y, petal); p.Set(x + 1, y, petal); p.Set(x, y - 1, petal); p.Set(x, y + 1, petal);
    }

    // Zone 2. A bridge running top to bottom over a creek that runs left to right. The gaps between planks show the water tiles below.
    static FhPic Bridge(bool healthy)
    {
        var p = new FhPic(32, 48);
        var shape = new Random(111);
        var tex = new Random(112 + (healthy ? 0 : 1));
        int[] wood = healthy ? WoodHealthy : WoodSick;
        int nail = healthy ? C("#3a3028") : C("#1a1620");
        for (int py = 1; py + 2 <= 47; py += 4)
        {
            bool gone = !healthy && py >= 13 && py <= 33;
            for (int x = 5; x <= 26; x++)
                for (int k = 0; k < 3; k++)
                {
                    int c = k == 0 ? wood[3] : k == 1 ? wood[2] : wood[1];
                    if (shape.NextDouble() < 0.08) c = wood[0];
                    if (!gone) p.Set(x, py + k, c);
                }
            if (!gone) { p.Set(6, py + 1, nail); p.Set(25, py + 1, nail); }
        }
        // side rails
        for (int y = 0; y < 48; y++)
        {
            bool leftGone = !healthy && y >= 16 && y <= 30, rightGone = !healthy && y >= 19 && y <= 28;
            if (!leftGone) { p.Set(2, y, wood[3]); p.Set(3, y, wood[2]); p.Set(4, y, wood[1]); }
            if (!rightGone) { p.Set(27, y, wood[3]); p.Set(28, y, wood[2]); p.Set(29, y, wood[1]); }
        }
        // posts
        int[] postY = { 0, 22, 44 };
        foreach (int y0 in postY)
            for (int y = y0; y < y0 + 4; y++)
                for (int x = 1; x <= 5; x++)
                {
                    if (!healthy && y0 == 22) continue;               // the middle posts have washed away
                    int c = y == y0 ? wood[3] : wood[0];
                    p.Set(x, y, c); p.Set(x + 25, y, c);
                }
        if (healthy)
        {
            for (int i = 0; i < 10; i++) { int y = tex.Next(48); p.Set(tex.Next(2) == 0 ? 2 : 27, y, Moss[1 + tex.Next(2)]); }
            Flower(p, 3, 43, FlowerWhite);
        }
        else
        {
            // one plank hanging at an angle, splintered rail ends, slime on two planks
            for (int k = 0; k < 3; k++) Line(p, 8, 19 + k, 21, 26 + k, k == 0 ? wood[3] : wood[1], 1);
            p.Set(3, 16, wood[2]); p.Set(2, 17, wood[3]); p.Set(4, 30, wood[1]); p.Set(28, 19, wood[2]); p.Set(29, 28, wood[1]);
            int[] slimeY = { 6, 38 };
            foreach (int y in slimeY) for (int x = 10; x < 16; x++) p.Set(x + (y % 3), y, x % 2 == 0 ? Goo[2] : Goo[3]);
        }
        p.Outline(0.5);
        return p;
    }

    // Zone 3. Sick: a pool of purple goo. Healthy: the same shape, dried into cracked mud with sprouts (walkable).
    static FhPic GooPool(bool healthy)
    {
        var p = new FhPic(48, 32);
        var tex = new Random(122 + (healthy ? 0 : 1));
        var cracks = new double[7][];
        var cr = new Random(121);
        for (int i = 0; i < 7; i++) cracks[i] = new double[] { 4 + cr.NextDouble() * 40, 4 + cr.NextDouble() * 24 };
        for (int y = 0; y < 32; y++)
            for (int x = 0; x < 48; x++)
            {
                double dx = (x + 0.5 - 24) / 22, dy = (y + 0.5 - 16) / 13;
                double ang = Math.Atan2(dy, dx), d = Math.Sqrt(dx * dx + dy * dy);
                double edge = 1 + 0.08 * Math.Sin(3 * ang + 1) + 0.06 * Math.Sin(5 * ang + 2);
                if (d > edge) continue;
                double rel = d / edge;
                if (!healthy)
                    p.Set(x, y, rel > 0.86 ? Goo[0] : rel > 0.7 ? Goo[1] : Goo[1 + (Hash(x / 3, y / 2, 4) < 0.6 ? 1 : 0)]);
                else
                {
                    // distance to the nearest and second-nearest crack point: where they are almost equal, there is a crack
                    double a = 1e9, b = 1e9;
                    foreach (double[] c in cracks)
                    {
                        double q = Math.Sqrt(Math.Pow(x - c[0], 2) + Math.Pow((y - c[1]) * 1.6, 2));
                        if (q < a) { b = a; a = q; } else if (q < b) b = q;
                    }
                    int col = b - a < 1.1 ? C("#4a3020") : rel > 0.85 ? Mud[3] : Pick(Mud, 0.25 + tex.NextDouble() * 0.5);
                    p.Set(x, y, col);
                }
            }
        if (!healthy)
        {
            int[][] streaks = { new int[] { 12, 9, 4 }, new int[] { 18, 7, 3 }, new int[] { 30, 12, 3 } };
            foreach (int[] s in streaks) for (int k = 0; k < s[2]; k++) p.Set(s[0] + k, s[1], GooShine);
            for (int i = 0; i < 6; i++)
            {
                int bx = 10 + tex.Next(28), by = 9 + tex.Next(14);
                p.Set(bx, by - 1, Goo[3]); p.Set(bx - 1, by, Goo[3]); p.Set(bx + 1, by, Goo[3]); p.Set(bx, by + 1, Goo[2]); p.Set(bx, by, GooShine);
            }
            p.Outline(0.6);
        }
        else
        {
            for (int i = 0; i < 6; i++)
            {
                int sx = 8 + tex.Next(32), sy = 8 + tex.Next(18);
                p.Set(sx, sy, C("#4f8f34")); p.Set(sx, sy - 1, C("#6cae40")); p.Set(sx + 1, sy - 2, C("#8cc850"));
            }
        }
        return p;
    }

    // Zone 4. Sick: three rotten trunks piled across the path. Healthy: the trunks stacked end-on at both sides.
    static FhPic LogPile(bool healthy)
    {
        var p = new FhPic(48, 32);
        var shape = new Random(131);
        var tex = new Random(132 + (healthy ? 0 : 1));
        int[] wood = healthy ? BarkHealthy : BarkSick;
        int[] rings = healthy ? RingsHealthy : RingsSick;
        if (!healthy)
        {
            int[][] logs = { new int[] { 22, 1, 46 }, new int[] { 15, 4, 43 }, new int[] { 8, 9, 38 } };
            foreach (int[] L in logs)
            {
                int y0 = L[0], x0 = L[1], x1 = L[2];
                for (int y = y0; y < y0 + 8; y++)
                    for (int x = x0; x <= x1 - 3; x++)
                    {
                        int row = y - y0;
                        int c = row <= 1 ? wood[3] : row <= 4 ? wood[2] : row <= 6 ? wood[1] : wood[0];
                        if (shape.NextDouble() < 0.08) c = wood[0];
                        p.Set(x, y, c);
                    }
                for (int y = y0; y < y0 + 8; y++)
                    for (int x = x1 - 5; x <= x1; x++)
                        if (InEllipse(x, y, x1 - 1.5, y0 + 4, 2.6, 4))
                        {
                            double d = Math.Sqrt(Math.Pow((x + 0.5 - (x1 - 1.5)) / 2.6, 2) + Math.Pow((y + 0.5 - (y0 + 4)) / 4, 2));
                            p.Set(x, y, d > 0.8 ? wood[1] : rings[((int)(d * 3)) % 2]);
                        }
                // goo dripping off each trunk
                int gx = x0 + 6 + tex.Next(Math.Max(1, x1 - x0 - 14));
                p.Set(gx, y0 + 7, Goo[2]); p.Set(gx, y0 + 8, Goo[3]); p.Set(gx + 1, y0 + 7, Goo[1]);
            }
        }
        else
        {
            double[][] ends = { new double[] { 6, 26 }, new double[] { 15, 26 }, new double[] { 10.5, 18.5 }, new double[] { 42, 26 }, new double[] { 33, 26 }, new double[] { 37.5, 18.5 } };
            foreach (double[] e in ends)
                for (int y = 0; y < 32; y++)
                    for (int x = 0; x < 48; x++)
                    {
                        double d = Math.Sqrt(Math.Pow((x + 0.5 - e[0]) / 4.4, 2) + Math.Pow((y + 0.5 - e[1]) / 4.4, 2));
                        if (d > 1) continue;
                        p.Set(x, y, d > 0.78 ? (y + 0.5 < e[1] ? wood[2] : wood[1]) : rings[((int)(d * 4)) % 2]);
                    }
            // moss on the top logs
            for (int x = 7; x <= 14; x++) p.Set(x, 14, Moss[1 + (x % 2)]);
            for (int x = 34; x <= 41; x++) p.Set(x, 14, Moss[1 + (x % 2)]);
        }
        p.Outline(0.45);
        return p;
    }

    // Zone 5. A thorn arch the path goes through. Sick: thorns woven across the opening. Healthy: open, green and flowering.
    static FhPic Arch(bool healthy)
    {
        var p = new FhPic(48, 48);
        var shape = new Random(141);
        var tex = new Random(142 + (healthy ? 0 : 1));
        int[] vine = healthy ? VineHealthy : VineSick;
        int thorn = healthy ? ThornHealthy : ThornSick;
        for (int y = 0; y < 48; y++)
            for (int x = 0; x < 48; x++)
            {
                double dist = Math.Sqrt(Math.Pow(x + 0.5 - 24, 2) + Math.Pow(y + 0.5 - 19, 2));
                bool pillar = y > 18 && ((x >= 2 && x <= 10) || (x >= 37 && x <= 45));
                if (y >= 45 && ((x >= 1 && x <= 11) || (x >= 36 && x <= 46))) pillar = true;   // wider at the foot
                bool arch = y <= 19 && dist >= 13.5 && dist <= 22;
                if (!pillar && !arch) continue;
                // a woven look: diagonal stripes in two shades, with darker seams
                int c = ((x + y) % 4 < 2) ? vine[1] : vine[2];
                if (((x - y) % 5 + 5) % 5 == 0) c = vine[0];
                p.Set(x, y, c);
            }
        // thorns sticking out of the outer edge
        var thornSpots = new List<int[]>();
        for (int y = 0; y < 48; y++)
            for (int x = 0; x < 48; x++)
                if (!p.Solid(x, y) && shape.NextDouble() < 0.25 && (p.Solid(x + 1, y) || p.Solid(x - 1, y) || p.Solid(x, y + 1))) thornSpots.Add(new int[] { x, y });
        foreach (int[] t in thornSpots) p.Set(t[0], t[1], thorn);

        if (!healthy)
        {
            // the web of thorns across the opening
            for (int i = 0; i < 10; i++)
            {
                double a1 = Math.PI * (0.9 + shape.NextDouble() * 1.2), a2 = Math.PI * (0.9 + shape.NextDouble() * 1.2);
                double x1 = 24 + Math.Cos(a1) * 13.5, y1 = 19 + Math.Sin(a1) * 13.5;
                double x2 = 24 + Math.Cos(a2 + Math.PI) * 13.5, y2 = 19 + 14 + shape.NextDouble() * 12;
                if (i % 2 == 0) { x2 = shape.Next(2) == 0 ? 11 : 36; }
                Line(p, x1, y1, x2, y2, vine[1], 1);
            }
            for (int i = 0; i < 14; i++) { int x = 12 + tex.Next(24), y = 8 + tex.Next(36); if (p.Solid(x, y)) p.Set(x, y, i % 3 == 0 ? GooGlow : thorn); }
        }
        else
        {
            // leaves and flowers growing on the arch
            for (int i = 0; i < 40; i++) { int x = tex.Next(48), y = tex.Next(48); if (p.Solid(x, y)) p.Set(x, y, Leaves[3 + tex.Next(2)]); }
            for (int i = 0; i < 14; i++)
            {
                int x = tex.Next(48), y = tex.Next(48);
                if (p.Solid(x, y) && p.Solid(x - 1, y) && p.Solid(x + 1, y)) Flower(p, x, y, i % 3 == 0 ? FlowerPink : FlowerWhite);
            }
        }
        p.Outline(0.5);
        return p;
    }

    // Zone 6. A bank of purple fog: soft see-through blobs, dithered into three levels so it still looks like pixel art.
    static FhPic Fog()
    {
        var p = new FhPic(64, 32);
        var shape = new Random(151);
        int[,] bayer = { { 0, 8, 2, 10 }, { 12, 4, 14, 6 }, { 3, 11, 1, 9 }, { 15, 7, 13, 5 } };
        var blobs = new double[8][];
        for (int i = 0; i < 8; i++) blobs[i] = new double[] { 10 + shape.NextDouble() * 44, 10 + shape.NextDouble() * 12, 7 + shape.NextDouble() * 6 };
        int light = C("#c8b0e0"), mid = C("#a888c8"), dark = C("#8a6aae");
        for (int y = 0; y < 32; y++)
            for (int x = 0; x < 64; x++)
            {
                double f = 0;
                foreach (double[] b in blobs) f += Math.Exp(-(Math.Pow(x - b[0], 2) + Math.Pow((y - b[1]) * 1.4, 2)) / (b[2] * b[2]));
                f = Math.Min(1, f * 0.8);
                int level = (int)Math.Floor(f * 3 + bayer[y % 4, x % 4] / 16.0 - 0.3);
                if (level <= 0) continue;
                if (level == 1) p.Set(x, y, Alpha(dark, 70));
                else if (level == 2) p.Set(x, y, Alpha(mid, 120));
                else p.Set(x, y, Alpha(light, 165));
            }
        return p;
    }

    // ------------------------------------------------------------------ the animals
    // Each animal is built from simple shaded ovals (body, head, ears, legs, tail...) and posed differently in every frame.
    // A sheet is 144 x 72: 6 frames of 24 x 24 in each of 3 rows (row 0 at the top), all facing right:
    //   row 0  idle     breathing, blinking, a twitch
    //   row 1  move     one full step / hop / waddle cycle (loops)
    //   row 2  special  rabbit sits up and sniffs, squirrel nibbles an acorn, hedgehog curls into a ball,
    //                   bird flaps its wings, turtle hides in its shell and peeks out
    // Every animal is drawn twice: critter_<animal> (healthy) and critter_<animal>_shadow (the same frames in shadow colours).

    public class Look
    {
        public int[] Body, Light, Dark, Pink;          // colour ramps, dark to light
        public int Outline, Eye, Shine, Blush, Nose, Feet, Extra;
    }

    delegate void Pose(FhPic p, int row, int f, Look c);

    static void Animals()
    {
        var rabbit = new Look { Body = R("#b08a68", "#cfa882", "#e6c8a4"), Light = R("#e8dcc8", "#f4ecdc", "#fffaf0"), Dark = R("#8a6a50", "#a0805e", "#b8987a"),
                                Pink = R("#e08a98", "#f0a8b4"), Outline = C("#4a3226"), Eye = C("#2a1a1e"), Shine = C("#ffffff"), Blush = C("#f4a0a8"), Nose = C("#e86a80"), Feet = C("#e6c8a4"), Extra = C("#ffffff") };
        var squirrel = new Look { Body = R("#b0582a", "#d07438", "#e8955a"), Light = R("#f0d0a0", "#f8e2c0", "#fff4e0"), Dark = R("#8a4220", "#a85428", "#c06a34"),
                                Pink = R("#9a6a34", "#c08a48", "#d8a860"), Outline = C("#4a2414"), Eye = C("#2a1a1e"), Shine = C("#ffffff"), Blush = C("#f09a90"), Nose = C("#5a2a20"), Feet = C("#c06a34"), Extra = C("#5a3a1e") };
        var hedgehog = new Look { Body = R("#d8b490", "#ecd0b0", "#f8e6cc"), Light = R("#ecd0b0", "#f8e6cc", "#fff6e8"), Dark = R("#4a3628", "#6a4e38", "#866448"),
                                Pink = R("#d89a90", "#eab0a6"), Outline = C("#2e2018"), Eye = C("#1e1416"), Shine = C("#ffffff"), Blush = C("#f4a0a0"), Nose = C("#2a1a1e"), Feet = C("#d89a90"), Extra = C("#d8c0a0") };
        var bird = new Look { Body = R("#3a6ab0", "#4f86cc", "#76a6e0"), Light = R("#e8d890", "#f4e8a8", "#fff8d0"), Dark = R("#2a4a80", "#34609a", "#4a78b4"),
                                Pink = R("#f0f0f4", "#ffffff"), Outline = C("#1e2c48"), Eye = C("#141a28"), Shine = C("#ffffff"), Blush = C("#f4a0b0"), Nose = C("#e89a3a"), Feet = C("#e89a3a"), Extra = C("#2a3a5a") };
        var turtle = new Look { Body = R("#7aa04a", "#94bc5c", "#b0d478"), Light = R("#c8e090", "#dcecaa", "#eef6c8"), Dark = R("#3e6a2e", "#5a8a3a", "#78a84c"),
                                Pink = R("#2e4e22", "#3e6a2e"), Outline = C("#24361a"), Eye = C("#1a2014"), Shine = C("#ffffff"), Blush = C("#f0a0a0"), Nose = C("#2a3a1e"), Feet = C("#94bc5c"), Extra = C("#d8c470") };

        Sheet("rabbit", Rabbit, rabbit);
        Sheet("squirrel", Squirrel, squirrel);
        Sheet("hedgehog", Hedgehog, hedgehog);
        Sheet("bird", Bird, bird);
        Sheet("turtle", Turtle, turtle);
    }

    static void Sheet(string animal, Pose pose, Look look)
    {
        Look[] looks = { look, ShadowLook(look) };
        string[] names = { "critter_" + animal, "critter_" + animal + "_shadow" };
        for (int v = 0; v < 2; v++)
        {
            var sheet = new FhPic(144, 72);
            for (int row = 0; row < 3; row++)
                for (int f = 0; f < 6; f++)
                {
                    var cell = new FhPic(24, 24);
                    pose(cell, row, f, looks[v]);
                    cell.OutlineWith(looks[v].Outline);
                    sheet.Blit(cell, f * 24, row * 24);
                }
            sheet.Save(Out, names[v]);
        }
    }

    // The shadow version: every colour turned into the shadow purples by its brightness, glowing pink eyes, no blush.
    static Look ShadowLook(Look c)
    {
        return new Look { Body = Shadow(c.Body), Light = Shadow(c.Light), Dark = Shadow(c.Dark), Pink = Shadow(c.Pink),
                          Outline = C("#100a16"), Eye = GooGlow, Shine = C("#ffd0f0"), Blush = 0, Nose = Shadow(c.Nose), Feet = Shadow(c.Feet), Extra = Shadow(c.Extra) };
    }
    static int[] Shadow(int[] ramp) { var a = new int[ramp.Length]; for (int i = 0; i < ramp.Length; i++) a[i] = Shadow(ramp[i]); return a; }
    static int Shadow(int c)
    {
        double lum = (0.3 * ((c >> 16) & 255) + 0.59 * ((c >> 8) & 255) + 0.11 * (c & 255)) / 255.0;
        int[] steps = R("#16121a", "#241a36", "#30224a", "#44306a", "#56407a", "#8064a8");
        return steps[Math.Min(steps.Length - 1, (int)(Math.Pow(lum, 1.3) * steps.Length))];
    }

    // a shaded oval, turned by 'ang' (radians; positive leans its top to the right), lit from the top left
    static void Ell(FhPic p, double cx, double cy, double rx, double ry, double ang, int[] ramp)
    {
        double ca = Math.Cos(ang), sa = Math.Sin(ang), m = Math.Max(rx, ry) + 1;
        for (int y = (int)Math.Floor(cy - m); y <= (int)Math.Ceiling(cy + m); y++)
            for (int x = (int)Math.Floor(cx - m); x <= (int)Math.Ceiling(cx + m); x++)
            {
                double dx = x + 0.5 - cx, dy = y + 0.5 - cy;
                double u = dx * ca + dy * sa, v = -dx * sa + dy * ca;
                double d2 = (u / rx) * (u / rx) + (v / ry) * (v / ry);
                if (d2 > 1) continue;
                double t = 0.62 - 0.22 * (dx / m) - 0.45 * (dy / m) - 0.12 * d2;
                p.Set(x, y, Pick(ramp, t));
            }
    }
    static void Dot(FhPic p, double x, double y, int c) { if (c != 0) p.Set((int)Math.Floor(x), (int)Math.Floor(y), c); }

    // a big cute eye: 2 wide, 'h' tall, with a white shine in the top-left. Closed = a short line.
    static void Eye(FhPic p, double x, double y, int h, bool closed, Look c)
    {
        int ex = (int)Math.Floor(x), ey = (int)Math.Floor(y);
        if (closed) { p.Set(ex, ey + h - 1, c.Eye); p.Set(ex + 1, ey + h - 1, c.Eye); return; }
        for (int j = 0; j < h; j++) { p.Set(ex, ey + j, c.Eye); p.Set(ex + 1, ey + j, c.Eye); }
        p.Set(ex, ey, c.Shine);
    }

    // breathing: a gentle up-and-down over the 6 idle frames
    static double Breath(int f) { return Math.Sin(f / 6.0 * 2 * Math.PI); }

    // ---- RABBIT: round body, big head, long ears, cotton tail. Moves by hopping.
    static void Rabbit(FhPic p, int row, int f, Look c)
    {
        double bodyX = 10, bodyY = 17.5, bodyRx = 6, bodyRy = 4.5, bodyAng = 0, headX = 15.5, headY = 13;
        double lift = 0, earBack = 0.2, earFront = 0.3, backFoot = 0, frontPaw = 0, nose = 0;
        bool blink = false, sitting = false;
        if (row == 0)
        {
            double b = Breath(f);
            bodyRy += b * 0.35; bodyY -= b * 0.25; headY -= b * 0.3;
            blink = f == 3; nose = f % 2;
            if (f == 4) earFront = 0.65; if (f == 5) earFront = 0.45;          // an ear twitch
        }
        else if (row == 1)
        {
            double[] up = { 0, 1.2, 2.8, 3.2, 1.5, 0 }, stretch = { -0.7, 0.7, 0.6, 0.1, -0.3, -0.5 }, tilt = { 0.08, -0.25, -0.15, 0.05, 0.2, 0.05 };
            lift = up[f]; bodyRx += stretch[f]; bodyRy -= stretch[f] * 0.5; bodyAng = tilt[f];
            backFoot = (f == 1 || f == 2) ? -3 : f == 3 ? -1.5 : 0;           // back feet push off and trail behind
            frontPaw = (f == 2 || f == 3) ? 2 : f == 4 ? 1 : 0;               // front paws reach for the landing
            earBack = (f >= 2 && f <= 3) ? 0.9 : 0.35; earFront = earBack + 0.1;   // ears stream back in the air
            if (f == 0) headY += 0.8; if (f == 5) headY += 0.4;
        }
        else
        {
            sitting = true;
            bodyX = 11; bodyY = 17.6; bodyRx = 4.6; bodyRy = 5.1; headX = 13.8; headY = 12.6;
            nose = f % 2; earBack = -0.05; earFront = 0.08;
            if (f == 2 || f == 3) earFront = 0.4;
            blink = f == 5;
        }
        bodyY -= lift; headY -= lift;

        Ell(p, bodyX - bodyRx + 0.6, bodyY - 1.2, 2.3, 2.2, 0, c.Light);                                     // cotton tail
        Ell(p, headX - 2.2 - Math.Sin(earBack) * 3.2, headY - 3 - Math.Cos(earBack) * 3.2, 1.5, 3.6, -earBack, c.Dark);   // far ear
        // feet stay tucked against the bottom of the body, so they never float loose in mid-hop
        double footY = Math.Min(22.4 - lift, bodyY + bodyRy - 0.4);
        Ell(p, bodyX - 2.5 + backFoot * 0.6 + 1.2, footY - 0.1, 2.6, 1.2, 0, c.Dark);                          // far back foot
        Ell(p, bodyX, bodyY, bodyRx, bodyRy, bodyAng, c.Body);
        Ell(p, bodyX + 2, bodyY + 1.4, bodyRx * 0.5, bodyRy * 0.5, bodyAng, c.Light);                          // tummy
        Ell(p, bodyX - 2.5 + backFoot * 0.6, footY, 2.8, 1.25, 0, c.Body);                                     // back foot
        if (sitting) { Ell(p, bodyX + 3, 16.5, 1.2, 1.6, 0, c.Body); Ell(p, bodyX + 3.5, 22.3, 1.6, 1.1, 0, c.Body); }   // paws held up, front feet
        else Ell(p, bodyX + bodyRx - 2 + frontPaw * 0.6, footY - 0.3, 1.5, 1.1, 0, c.Body);                    // front paw
        Ell(p, headX, headY, 4.6, 4.3, 0, c.Body);
        Ell(p, headX + 1.8, headY + 1.6, 2.3, 1.6, 0, c.Light);                                                // muzzle
        double ex = headX - 0.6 - Math.Sin(earFront) * 3.4, ey = headY - 3.2 - Math.Cos(earFront) * 3.4;
        Ell(p, ex, ey, 1.6, 3.8, -earFront, c.Body);                                                           // near ear
        Ell(p, ex + 0.2, ey + 0.3, 0.6, 2.5, -earFront, c.Pink);                                               // pink inside
        Eye(p, headX + 0.6, headY - 1.5, 3, blink, c);
        Dot(p, headX + 4, headY + 0.4 - nose, c.Nose);
        Dot(p, headX + 1.4, headY + 2, c.Blush); Dot(p, headX + 2.4, headY + 2, c.Blush);
    }

    // ---- SQUIRREL: small round body, big bushy curled tail. Moves in quick bounds.
    static void Squirrel(FhPic p, int row, int f, Look c)
    {
        double bodyX = 12.3, bodyY = 17.5, bodyAng = 0.15, headX = 16.3, headY = 11.8, lift = 0, tailSwing = 0, tailDrop = 0, legReach = 0;
        bool blink = false, nibble = false;
        if (row == 0)
        {
            double b = Breath(f);
            bodyY -= b * 0.2; headY -= b * 0.25; tailSwing = Math.Sin(f / 6.0 * 2 * Math.PI + 1) * 0.15;
            blink = f == 2;
        }
        else if (row == 1)
        {
            double[] up = { 0, 2, 3.5, 2.5, 1, 0 }, reach = { -1, 1.5, 2.5, 1, -0.5, -1.5 };
            lift = up[f]; legReach = reach[f]; bodyAng = 0.15 + (f == 1 || f == 2 ? -0.2 : 0.05);
            tailDrop = (f >= 1 && f <= 3) ? 2 : 0.5; tailSwing = -0.2 + up[f] * 0.06;
        }
        else
        {
            nibble = true;
            bodyX = 12.3; bodyY = 16.5; bodyAng = -0.05; headX = 15.5; headY = 10;
            headY += (f % 2 == 1) ? 0.6 : 0;                                    // nibbling: head bobs
            tailSwing = Math.Sin(f / 6.0 * 2 * Math.PI) * 0.1;
            blink = f == 4;
        }
        bodyY -= lift; headY -= lift;

        // the big tail: three overlapping lumps curling up and over, with a lighter tip. It is drawn on its own layer
        // and outlined first, so there is always a clear line between the tail and the head.
        var tail = new FhPic(24, 24);
        double tx = bodyX - 5 - tailDrop * 0.3, ty = bodyY - 2 + tailDrop;
        Ell(tail, tx, ty, 3.2, 5.2, -0.35 + tailSwing, c.Dark);
        Ell(tail, tx - 0.2 + tailSwing * 4, ty - 6 + tailDrop * 0.6, 3.3, 3.3, 0.2 + tailSwing, c.Dark);
        Ell(tail, tx + 1.6 + tailSwing * 6, ty - 8.5 + tailDrop, 2.4, 2.1, 0.3, c.Light);
        tail.OutlineWith(c.Outline);
        p.Blit(tail, 0, 0);
        double footY = Math.Min(22.4 - lift * 0.5, bodyY + 4.4);
        Ell(p, bodyX - 1.5 - legReach * 0.4, footY - 0.1, 2.2, 1.2, 0, c.Dark);                                 // far foot
        Ell(p, bodyX, bodyY, 4.4, 4.8, bodyAng, c.Body);
        Ell(p, bodyX + 1.6, bodyY + 1, 2.3, 3.2, bodyAng, c.Light);                                            // tummy
        Ell(p, bodyX - 2.2, bodyY + 2.4, 2.8, 2.4, 0, c.Body);                                                 // haunch
        Ell(p, bodyX - 1 - legReach * 0.4, footY, 2.5, 1.2, 0, c.Body);                                        // back foot
        if (!nibble) Ell(p, bodyX + 3 + legReach * 0.5, footY - 0.2, 1.3, 1.1, 0, c.Body);                     // front paw
        Ell(p, headX, headY, 4.2, 3.9, 0, c.Body);
        Ell(p, headX + 2, headY + 1.3, 2, 1.5, 0, c.Light);                                                    // muzzle
        // a pointed ear with a pale inside
        int ex = (int)Math.Floor(headX - 1.5), ey = (int)Math.Floor(headY - 4.2);
        p.Set(ex, ey, c.Body[1]); p.Set(ex + 1, ey, c.Body[2]); p.Set(ex, ey - 1, c.Body[1]); p.Set(ex + 1, ey - 1, c.Light[0]);
        p.Set(ex, ey - 2, c.Body[1]); p.Set(ex + 1, ey + 1, c.Body[2]); p.Set(ex - 1, ey + 1, c.Body[1]);
        Eye(p, headX + 0.4, headY - 1.4, 3, blink, c);
        Dot(p, headX + 3.9, headY + 0.4, c.Nose);
        Dot(p, headX + 1.4, headY + 1.8, c.Blush);
        if (nibble)
        {
            // an acorn held up to the mouth in both paws (it turns a little as it is nibbled)
            double ax = headX + 2.6 + ((f % 2 == 1) ? -0.5 : 0), ay = headY + 4.4;
            Ell(p, ax, ay, 1.8, 2, 0, c.Pink);
            Dot(p, ax - 0.6, ay - 0.6, c.Shine);
            for (int k = -2; k <= 1; k++) Dot(p, ax + k + 0.3, ay - 1.8, c.Extra);                              // cap
            Dot(p, ax - 0.7, ay - 2.6, c.Extra); Dot(p, ax + 0.3, ay - 2.6, c.Extra); Dot(p, ax + 0.3, ay - 3.4, c.Extra);   // cap top and stem
            Ell(p, ax - 1.8, ay + 1.4, 1.2, 1, 0, c.Body);                                                     // paws
        }
    }

    // ---- HEDGEHOG: a round dome of spines with a little pointy face and tiny feet. Waddles.
    static void Hedgehog(FhPic p, int row, int f, Look c)
    {
        double lift = 0, domeRy = 6, footA = 0, footB = 0, faceX = 16.2, nose = 0;
        bool blink = false, ball = false, peek = false, face = true;
        if (row == 0)
        {
            domeRy += Breath(f) * 0.3; nose = (f == 1 || f == 4) ? 1 : 0; blink = f == 3;
        }
        else if (row == 1)
        {
            double ph = f / 6.0 * 2 * Math.PI;
            footA = Math.Sin(ph) * 1.5; footB = -footA; lift = (f % 3 == 1) ? 1 : 0; nose = f % 2;
        }
        else
        {
            // curl up into a ball and peek out again
            if (f == 1 || f == 5) faceX = 14.8;
            if (f >= 2 && f <= 4) { ball = true; face = false; }
            if (f == 3) lift = -0.6;
            if (f == 4) peek = true;
        }

        if (ball)
        {
            double cy = 16.5 - lift;
            Ell(p, 12, cy, 7, 6.6, 0, c.Dark);
            Spines(p, 12, cy, 7, 6.6, 0, 360, c);
            if (peek) { Eye(p, 16.5, cy - 0.5, 2, false, c); Dot(p, 18.5, cy + 0.5, c.Nose); }
            return;
        }
        double by = 17.5 - lift;
        Ell(p, 8 + footB, 22.5 - lift, 1.6, 1, 0, c.Pink);                                                     // far feet
        Ell(p, 14 + footA, 22.5 - lift, 1.4, 1, 0, c.Pink);
        Ell(p, 10.8, by + 2.4, 6.4, 2.6, 0, c.Body);                                                           // soft belly under the spines
        // the dome of spines, cut off flat at the bottom
        var dome = new FhPic(24, 24);
        Ell(dome, 10.4, by, 7.2, domeRy, 0, c.Dark);
        Spines(dome, 10.4, by, 7.2, domeRy, 180, 360, c);
        for (int y = 0; y < 24; y++) for (int x = 0; x < 24; x++) if (dome.Solid(x, y) && y <= by + 2.5) p.Set(x, y, dome.Get(x, y));
        Ell(p, 7 + footA, 22.5 - lift, 1.6, 1, 0, c.Pink);                                                     // near feet
        Ell(p, 13.5 + footB, 22.5 - lift, 1.6, 1, 0, c.Pink);
        if (face)
        {
            Ell(p, faceX, by + 1, 4, 3.3, 0, c.Light);                                                         // face
            Ell(p, faceX + 3.4, by + 1.9, 2, 1.3, 0, c.Light);                                                 // snout
            Dot(p, faceX + 5.3, by + 1.4 - nose * 0.6, c.Nose);
            Dot(p, faceX + 5.3, by + 2.2 - nose * 0.6, c.Nose);
            Ell(p, faceX - 1.6, by - 2.2, 1.1, 1.1, 0, c.Light);                                               // tiny ear
            Eye(p, faceX + 0.4, by - 0.6, 2, blink, c);
            Dot(p, faceX + 1.6, by + 2.4, c.Blush);
        }
    }

    // spikes around part of an oval (angles in degrees, 180-360 = the top half), plus pale spine tips inside
    static void Spines(FhPic p, double cx, double cy, double rx, double ry, int from, int to, Look c)
    {
        for (int a = from; a < to; a += 20)
        {
            double r = a * Math.PI / 180;
            Dot(p, cx + Math.Cos(r) * (rx + 1), cy + Math.Sin(r) * (ry + 1), c.Dark[1]);
        }
        for (int y = (int)(cy - ry); y <= (int)(cy + ry); y++)
            for (int x = (int)(cx - rx); x <= (int)(cx + rx); x++)
                if (p.Solid(x, y) && (x + y * 2) % 5 == 0 && InEllipse(x, y, cx, cy, rx - 0.5, ry - 0.5)) p.Set(x, y, c.Extra);
    }

    // ---- BIRD: a round little songbird with a cream tummy. Hops; flaps its wings as its special move.
    static void Bird(FhPic p, int row, int f, Look c)
    {
        double lift = 0, headDx = 0, headDy = 0, tailAng = -0.4, wing = 0;     // wing: 0 folded, 1 up, -1 down
        bool blink = false, flying = false;
        if (row == 0)
        {
            double b = Breath(f);
            lift = b * 0.3;
            if (f == 2 || f == 3) { headDx = 0.5; headDy = 0.6; }              // tilts its head to look at you
            if (f == 4) tailAng = -0.75;                                       // tail flick
            blink = f == 5;
        }
        else if (row == 1)
        {
            double[] up = { 0, 1.5, 3, 2.5, 1, 0 };
            lift = up[f]; tailAng = -0.4 - up[f] * 0.08;
            if (f == 2 || f == 3) wing = 0.4;
        }
        else
        {
            flying = true;
            double[] up = { 2, 3.5, 4, 3.5, 2.5, 3 };
            double[] wings = { 1, 0.3, -1, -0.4, 1, 0.3 };
            lift = up[f]; wing = wings[f]; tailAng = -0.2;
        }
        double by = 16.5 - lift;

        Ell(p, 5.2, by - 1.5, 3.4, 1.4, tailAng, c.Dark);                                                      // tail
        if (!flying)
        {
            Line(p, 10.5, by + 3.5, 10.5, 22, c.Feet, 1); Line(p, 13, by + 3.5, 13, 22, c.Feet, 1);            // legs
            Dot(p, 11.5, 22.5, c.Feet); Dot(p, 14, 22.5, c.Feet); Dot(p, 9.5, 22.5, c.Feet); Dot(p, 12, 22.5, c.Feet);
        }
        else { Dot(p, 11, by + 4.5, c.Feet); Dot(p, 12.5, by + 4.5, c.Feet); }                                  // legs tucked up
        Ell(p, 11, by, 5.3, 4.6, 0, c.Body);
        Ell(p, 12.6, by + 1.6, 3.5, 2.6, 0, c.Light);                                                          // tummy
        double hx = 15.2 + headDx, hy = by - 4.8 + headDy;
        Ell(p, hx, hy, 4, 3.8, 0, c.Body);
        Ell(p, hx - 0.4, hy - 2.6, 3, 1.4, 0, c.Dark);                                                         // little cap
        Ell(p, hx + 1, hy + 1, 2.2, 1.6, 0, c.Pink);                                                           // white cheek
        Eye(p, hx + 0.6, hy - 1, 2, blink, c);
        Dot(p, hx + 3.8, hy - 0.2, c.Nose); Dot(p, hx + 4.8, hy + 0.3, c.Nose); Dot(p, hx + 3.8, hy + 0.8, c.Nose);   // beak
        Dot(p, hx + 1.6, hy + 2.2, c.Blush);
        // the wing: folded on the side, or raised / lowered while flapping
        if (wing > 0.6) Ell(p, 9.5, by - 5, 2.2, 4.4, -0.5, c.Dark);
        else if (wing < -0.6) Ell(p, 9.5, by + 3.5, 2.2, 3.8, 0.5, c.Dark);
        else Ell(p, 9.6, by - 0.6 - wing * 2, 3.6, 2.4, -0.2 - wing * 0.4, c.Dark);
    }

    // ---- TURTLE: a high patterned shell with a yellow rim, a round smiling head and stubby legs. Plods.
    static void Turtle(FhPic p, int row, int f, Look c)
    {
        double lift = 0, headX = 18.3, headY = 16.4, legA = 0, legB = 0;
        bool blink = false, hidden = false, peek = false;
        if (row == 0)
        {
            headY -= Breath(f) * 0.5;
            if (f == 2 || f == 3) headX += 0.5;                                // stretches its neck
            blink = f == 4;
        }
        else if (row == 1)
        {
            double ph = f / 6.0 * 2 * Math.PI;
            legA = Math.Sin(ph) * 1.2; legB = -legA; lift = (f % 3 == 0) ? 0 : 0.6;
            headX += Math.Sin(ph * 2) * 0.4;
        }
        else
        {
            // pulls into its shell, wobbles, peeks, comes back out
            if (f == 1 || f == 5) { headX = 17; legA = -0.5; }
            if (f >= 2 && f <= 4) hidden = true;
            if (f == 3) lift = 0.6;
            if (f == 4) peek = true;
        }
        double sy = 17.5 - lift;

        if (!hidden)
        {
            Ell(p, 9 + legB, 21.2, 1.9, 1.5, 0, c.Dark);                                                       // far legs
            Ell(p, 16.5 + legA, 21.2, 1.9, 1.5, 0, c.Dark);
            Ell(p, 3.4, sy + 1.6, 1.4, 0.8, 0.3, c.Body);                                                      // tail
        }
        // the shell: a dome cut flat at the bottom, with plates and a rim
        var shell = new FhPic(24, 24);
        Ell(shell, 11, sy, 7.6, 8, 0, c.Dark);
        double[][] plates = { new double[] { 11, sy - 4.8 }, new double[] { 7, sy - 1.8 }, new double[] { 15, sy - 1.8 }, new double[] { 4.8, sy + 1.2 }, new double[] { 11, sy + 0.6 }, new double[] { 17.2, sy + 1.2 } };
        for (int y = 0; y < 24; y++)
            for (int x = 0; x < 24; x++)
            {
                if (!shell.Solid(x, y)) continue;
                double a = 1e9, b = 1e9;
                foreach (double[] q in plates)
                {
                    double d = Math.Sqrt(Math.Pow(x + 0.5 - q[0], 2) + Math.Pow((y + 0.5 - q[1]) * 1.3, 2));
                    if (d < a) { b = a; a = d; } else if (d < b) b = d;
                }
                if (b - a < 0.9) shell.Set(x, y, c.Pink[0]);                   // the lines between plates
                else if (a < 1.4) shell.Set(x, y, c.Dark[2]);                  // a lighter middle on each plate
            }
        for (int y = 0; y < 24; y++)
            for (int x = 0; x < 24; x++)
                if (shell.Solid(x, y) && y <= sy + 1.5) p.Set(x, y, shell.Get(x, y));
        for (int x = 4; x <= 18; x++) { p.Set(x, (int)Math.Floor(sy + 1.5), c.Extra); p.Set(x, (int)Math.Floor(sy + 2.5), Dark(c.Extra, 0.8)); }   // rim
        p.Set(3, (int)Math.Floor(sy + 1.5), Dark(c.Extra, 0.8)); p.Set(19, (int)Math.Floor(sy + 1.5), Dark(c.Extra, 0.8));

        if (hidden)
        {
            // a dark opening at the front, with two shiny eyes looking out when it peeks
            Ell(p, 18.6, sy + 0.4, 1.4, 1.6, 0, R("#141a10"));
            if (peek) { Dot(p, 18.2, sy, c.Shine); Dot(p, 19.2, sy, c.Shine); }
            return;
        }
        Ell(p, 7 + legA, 21.6, 2, 1.6, 0, c.Body);                                                             // near legs
        Ell(p, 14.5 + legB, 21.6, 2, 1.6, 0, c.Body);
        double hy = headY - lift;
        Ell(p, headX - 1.8, hy + 0.8, 1.9, 1.7, 0, c.Body);                                                    // neck
        Ell(p, headX, hy, 3.5, 3.3, 0, c.Body);
        Eye(p, headX + 0.3, hy - 1.6, 3, blink, c);
        Dot(p, headX + 2, hy + 1.6, c.Nose); Dot(p, headX + 2.9, hy + 1.1, c.Nose);                            // smile
        Dot(p, headX + 0.4, hy + 1.6, c.Blush); Dot(p, headX - 0.6, hy + 1.6, c.Blush);
    }
}
'@

Add-Type -TypeDefinition $code -Language CSharp -ReferencedAssemblies System.Drawing
[FH]::Run($out, $shadowArt)
Write-Output ("Wrote " + [FH]::Written + " pictures to " + $out)
