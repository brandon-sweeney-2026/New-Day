# Draws every picture of the interior decor catalogue into Assets\Project\Art\Interior (16 pixels per tile, one PNG per picture).
#
#     powershell -ExecutionPolicy Bypass -File Tools\make_interior_art.ps1 -Project "<project folder>"
#
# Then run Farm > Set Up > Interior Decor in Unity (it reads decor_manifest.txt, written next to the pictures).
#
# HOW THE FURNITURE IS DRAWN
#   Each piece is described as a few 3D blocks (boxes, cylinders, blobs) on a grid of 1 pixel cubes: x = across, y = depth
#   (0 = the front, facing the camera), z = height. The script then draws that little model from the FRONT, the RIGHT, the BACK
#   and the LEFT, so every piece can be turned in the game and its back side always matches its front.
#   Every category has one piece per THEME (Cottage, Modern, Rustic, Retro, Coastal) drawn with the same colour set,
#   so a room furnished from one theme looks like it belongs together.
#
# Also drawn here: floors (16 x 16 tiles), wall coverings (a top, middle and bottom tile each), windows, pictures and wall
# decorations (front only, they hang on the back wall), and the room frame / shadow pieces that give the rooms depth.
# A picture of the whole catalogue is saved to Logs\interior_catalogue.png so you can look it over without Unity.
#
# Running this again OVERWRITES every PNG in Assets\Project\Art\Interior (the sprite IDs stay the same, so nothing in Unity breaks).
param([string]$Project = (Split-Path -Parent $PSScriptRoot))

$out = Join-Path $Project "Assets\Project\Art\Interior"
$preview = Join-Path $Project "Logs\interior_catalogue.png"
New-Item -ItemType Directory -Force $out | Out-Null
New-Item -ItemType Directory -Force (Split-Path $preview) | Out-Null

$code = @'
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Text;

// How a surface is coloured (wood grain, fabric weave, bricks...).
public enum Pat { Plain, Wood, Fabric, Leather, Tufted, Metal, Glass, Screen, Lit, Glow, Brick, Stone, Rattan, Plaid, Stripes, Leaves, Rings, Shag, Checker, Diag, Water, Tile, Folds }

// A material: main colour, two helper colours and a pattern.
public class Mat
{
    public Color c, c2, c3; public Pat p;
    public Mat(Color c, Pat p) { this.c = c; this.p = p; c2 = InteriorArt.Mul(c, 0.75); c3 = InteriorArt.Mul(c, 1.2); }
    public Mat(Color c, Pat p, Color c2) : this(c, p) { this.c2 = c2; }
    public Mat(Color c, Pat p, Color c2, Color c3) : this(c, p) { this.c2 = c2; this.c3 = c3; }
}

// One building block of a model. kind 0 = box (optionally with rounded corners seen from above), 1 = upright cylinder (oval allowed),
// 2 = blob (ellipsoid), 3 = cone (round, radius changes with height). 'carve' blocks cut holes instead of adding.
public class Prim
{
    public int kind; public Mat m; public bool carve;
    public int x0, y0, z0, x1, y1, z1, r;
    public double cx, cy, cz, rx, ry, rz, r1, noise;

    public bool In(int x, int y, int z)
    {
        if (x < x0 || x >= x1 || y < y0 || y >= y1 || z < z0 || z >= z1) return false;
        double px = x + 0.5, py = y + 0.5, pz = z + 0.5;
        switch (kind)
        {
            case 0:
            {
                if (r <= 0) return true;
                double qx = Math.Max(x0 + r, Math.Min(x1 - r, px)), qy = Math.Max(y0 + r, Math.Min(y1 - r, py));
                return (px - qx) * (px - qx) + (py - qy) * (py - qy) <= r * r + 0.3;
            }
            case 1: { double dx = (px - cx) / rx, dy = (py - cy) / ry; return dx * dx + dy * dy <= 1.0; }
            case 2:
            {
                double dx = (px - cx) / rx, dy = (py - cy) / ry, dz = (pz - cz) / rz;
                double lim = 1.0; if (noise > 0) lim += noise * (InteriorArt.Rnd(x, y, z, 99) - 0.5);
                return dx * dx + dy * dy + dz * dz <= lim;
            }
            case 3:
            {
                double t = (pz - z0) / (double)(z1 - z0); double rr = rx + (r1 - rx) * t;
                double dx = px - cx, dy = py - cy; return dx * dx + dy * dy <= rr * rr;
            }
        }
        return false;
    }
}

// One catalogue piece: what it is called and its model.
public class Obj
{
    public string id, name, cat, theme, mount = "Floor", desc = "";
    public int w = 1, d = 1, price = 100, top = 0;      // top = height (pixels) of the surface other things can stand on (0 = none)
    public bool rotates = true;
    public List<Prim> P = new List<Prim>();
    public int W { get { return w * 16; } }
    public int D { get { return d * 16; } }

    Obj Add(Prim p) { P.Add(p); return this; }
    public Obj Box(int x, int y, int z, int bw, int bd, int bh, Mat m) { return Box(x, y, z, bw, bd, bh, m, 0); }
    public Obj Box(int x, int y, int z, int bw, int bd, int bh, Mat m, int r)
    { return Add(new Prim { kind = 0, m = m, r = r, x0 = x, y0 = y, z0 = z, x1 = x + bw, y1 = y + bd, z1 = z + bh }); }
    public Obj Cut(int x, int y, int z, int bw, int bd, int bh) { return Cut(x, y, z, bw, bd, bh, 0); }
    public Obj Cut(int x, int y, int z, int bw, int bd, int bh, int r)
    { return Add(new Prim { kind = 0, carve = true, r = r, x0 = x, y0 = y, z0 = z, x1 = x + bw, y1 = y + bd, z1 = z + bh }); }
    public Obj Cyl(double cx, double cy, int z, double rx, double ry, int h, Mat m)
    { return Add(MakeCyl(cx, cy, z, rx, ry, h, m, false)); }
    public Obj CutCyl(double cx, double cy, int z, double rx, double ry, int h)
    { return Add(MakeCyl(cx, cy, z, rx, ry, h, null, true)); }
    static Prim MakeCyl(double cx, double cy, int z, double rx, double ry, int h, Mat m, bool carve)
    {
        return new Prim { kind = 1, m = m, carve = carve, cx = cx, cy = cy, rx = rx, ry = ry,
            x0 = (int)Math.Floor(cx - rx), x1 = (int)Math.Ceiling(cx + rx), y0 = (int)Math.Floor(cy - ry), y1 = (int)Math.Ceiling(cy + ry), z0 = z, z1 = z + h };
    }
    public Obj Ell(double cx, double cy, double cz, double rx, double ry, double rz, Mat m) { return Ell(cx, cy, cz, rx, ry, rz, m, 0); }
    public Obj Ell(double cx, double cy, double cz, double rx, double ry, double rz, Mat m, double noise)
    {
        double g = noise > 0 ? 1 : 0;
        return Add(new Prim { kind = 2, m = m, noise = noise, cx = cx, cy = cy, cz = cz, rx = rx, ry = ry, rz = rz,
            x0 = (int)Math.Floor(cx - rx - g), x1 = (int)Math.Ceiling(cx + rx + g), y0 = (int)Math.Floor(cy - ry - g), y1 = (int)Math.Ceiling(cy + ry + g),
            z0 = (int)Math.Floor(cz - rz - g), z1 = (int)Math.Ceiling(cz + rz + g) });
    }
    public Obj Cone(double cx, double cy, int z, double r0, double r1, int h, Mat m)
    {
        double R = Math.Max(r0, r1);
        return Add(new Prim { kind = 3, m = m, cx = cx, cy = cy, rx = r0, r1 = r1,
            x0 = (int)Math.Floor(cx - R), x1 = (int)Math.Ceiling(cx + R), y0 = (int)Math.Floor(cy - R), y1 = (int)Math.Ceiling(cy + R), z0 = z, z1 = z + h });
    }
    // Four square legs at the corners of a rectangle.
    public Obj Legs(int x, int y, int bw, int bd, int h, int t, Mat m)
    {
        Box(x, y, 0, t, t, h, m); Box(x + bw - t, y, 0, t, t, h, m); Box(x, y + bd - t, 0, t, t, h, m);
        return Box(x + bw - t, y + bd - t, 0, t, t, h, m);
    }
    public Obj Top(int h) { top = h; return this; }
}

// A colour theme: every category has one piece drawn with each theme's colours.
public class Th { public int i; public string key, name; public Color wood, woodDk, fab, fab2, acc, metal, light, dark, cab, top; }

// A small picture canvas for the flat things (windows, pictures, floors, walls). x, y from the TOP-LEFT.
public class Cv
{
    public int w, h; public Color[] a;
    public Cv(int w, int h) { this.w = w; this.h = h; a = new Color[w * h]; }
    public bool Has(int x, int y) { return x >= 0 && y >= 0 && x < w && y < h && a[y * w + x].A > 0; }
    public Color G(int x, int y) { return x >= 0 && y >= 0 && x < w && y < h ? a[y * w + x] : Color.Transparent; }
    public void P(int x, int y, Color c) { if (x >= 0 && y >= 0 && x < w && y < h) a[y * w + x] = c; }
    public void R(int x, int y, int rw, int rh, Color c) { for (int j = y; j < y + rh; j++) for (int i = x; i < x + rw; i++) P(i, j, c); }
    public void Frame(int x, int y, int rw, int rh, Color c) { R(x, y, rw, 1, c); R(x, y + rh - 1, rw, 1, c); R(x, y, 1, rh, c); R(x + rw - 1, y, 1, rh, c); }
    public void Disc(double cx, double cy, double rad, Color c)
    { for (int j = (int)(cy - rad - 1); j <= cy + rad + 1; j++) for (int i = (int)(cx - rad - 1); i <= cx + rad + 1; i++) { double dx = i + 0.5 - cx, dy = j + 0.5 - cy; if (dx * dx + dy * dy <= rad * rad) P(i, j, c); } }
    public void Ring(double cx, double cy, double r0, double r1, Color c)
    { for (int j = (int)(cy - r1 - 1); j <= cy + r1 + 1; j++) for (int i = (int)(cx - r1 - 1); i <= cx + r1 + 1; i++) { double dx = i + 0.5 - cx, dy = j + 0.5 - cy, d = Math.Sqrt(dx * dx + dy * dy); if (d <= r1 && d >= r0) P(i, j, c); } }
    // A dark edge around everything drawn (the outline the rest of the project's art has).
    public void Outline()
    {
        var todo = new List<int>();
        for (int y = 0; y < h; y++) for (int x = 0; x < w; x++)
        {
            if (Has(x, y)) continue;
            Color n = Color.Transparent;
            if (Has(x + 1, y)) n = G(x + 1, y); else if (Has(x - 1, y)) n = G(x - 1, y); else if (Has(x, y + 1)) n = G(x, y + 1); else if (Has(x, y - 1)) n = G(x, y - 1);
            if (n.A > 200) todo.Add(y * w + x);
        }
        foreach (int i in todo) a[i] = InteriorArt.Ink(Color.FromArgb(255, 90, 80, 80));
    }
    // A soft shadow one pixel down-right on the wall behind a hanging thing.
    public void WallShadow()
    {
        var todo = new List<int>();
        for (int y = 0; y < h; y++) for (int x = 0; x < w; x++) if (!Has(x, y) && Has(x - 1, y - 1)) todo.Add(y * w + x);
        foreach (int i in todo) a[i] = Color.FromArgb(70, 20, 12, 18);
    }
    public Bitmap Bmp()
    {
        var b = new Bitmap(w, h, PixelFormat.Format32bppArgb);
        for (int y = 0; y < h; y++) for (int x = 0; x < w; x++) b.SetPixel(x, y, a[y * w + x]);
        return b;
    }
}

public static class InteriorArt
{
    static string outDir;
    static readonly List<Obj> all = new List<Obj>();
    static readonly StringBuilder man = new StringBuilder();
    static readonly List<KeyValuePair<string, List<Bitmap>>> previews = new List<KeyValuePair<string, List<Bitmap>>>();
    static readonly string[] FaceName = { "front", "right", "back", "left" };
    public static readonly Color INK = X("2a1e2a");

    // ---------------------------------------------------------------- colour helpers
    public static Color X(string h) { int v = Convert.ToInt32(h, 16); return Color.FromArgb(255, (v >> 16) & 255, (v >> 8) & 255, v & 255); }
    static int Cl(double v) { return v < 0 ? 0 : v > 255 ? 255 : (int)Math.Round(v); }
    public static Color Mul(Color c, double k) { return Color.FromArgb(c.A, Cl(c.R * k), Cl(c.G * k), Cl(c.B * k)); }
    public static Color Mix(Color a, Color b, double t) { return Color.FromArgb(255, Cl(a.R + (b.R - a.R) * t), Cl(a.G + (b.G - a.G) * t), Cl(a.B + (b.B - a.B) * t)); }
    public static Color Ink(Color near) { return Mix(Mul(near, 0.4), INK, 0.6); }
    public static int Mod(int a, int m) { int r = a % m; return r < 0 ? r + m : r; }
    static uint Hash(int x, int y, int z, int s)
    {
        unchecked
        {
            uint h = (uint)(x * 374761393 + y * 668265263 + z * 1442695041 + s * 1274126177);
            h = (h ^ (h >> 13)) * 1274126177u; return h ^ (h >> 16);
        }
    }
    public static double Rnd(int x, int y, int z, int s) { return (Hash(x, y, z, s) & 0xFFFF) / 65535.0; }

    static Mat Pl(Color c) { return new Mat(c, Pat.Plain); }
    static Mat Pl(string h) { return new Mat(X(h), Pat.Plain); }
    static Mat Fab(Color c) { return new Mat(c, Pat.Fabric); }
    static Mat Met(Color c) { return new Mat(c, Pat.Metal); }
    static Mat Met(string h) { return new Mat(X(h), Pat.Metal); }
    static Mat Wd(Th t) { return new Mat(t.wood, Pat.Wood); }
    static Mat WdD(Th t) { return new Mat(t.woodDk, Pat.Wood); }
    static Mat Groove(Color c) { return Pl(Mul(c, 0.68)); }

    // Shared materials.
    static readonly Mat Porc = Pl("f3f3ee");
    static readonly Mat Chrome = Met("d0d4da");
    static readonly Mat Brass = Met("c9a25a");
    static readonly Mat Iron = Met("3a3a40");
    static readonly Mat Black = Pl("2b2d33");
    static readonly Mat Soot = Pl("1f1a1c");
    static readonly Mat Scr = new Mat(X("1d2433"), Pat.Screen, X("34405a"));
    static readonly Mat LitBlue = new Mat(X("7fb6e6"), Pat.Lit, X("c4e2ff"));
    static readonly Mat Fire = new Mat(X("ff8a2a"), Pat.Glow, X("ffd84a"), X("e0441c"));
    static readonly Mat GlassM = new Mat(X("a9d3e3"), Pat.Glass, X("8ab8cc"), X("e4f4fa"));
    static readonly Mat Leaves = new Mat(X("4f8f3a"), Pat.Leaves, X("2f5e2a"), X("8cc860"));
    static readonly Mat Water = new Mat(X("8fc8e0"), Pat.Water, X("6fa8c8"), X("e8f6fc"));
    static readonly Mat Rattan = new Mat(X("c9a46a"), Pat.Rattan);

    static Th[] Themes()
    {
        return new[]
        {
            new Th { i = 0, key = "cottage", name = "Cottage", wood = X("b07a48"), woodDk = X("7d5232"), fab = X("8fae7e"), fab2 = X("efe2c4"), acc = X("d9877f"), metal = X("c9a25a"), light = X("f3ead6"), dark = X("4a3a30"), cab = X("efe8d6"), top = X("b98552") },
            new Th { i = 1, key = "modern", name = "Modern", wood = X("3c3f46"), woodDk = X("26282d"), fab = X("9aa1ab"), fab2 = X("dfe3e8"), acc = X("e0a030"), metal = X("c2c7cf"), light = X("eef0f2"), dark = X("2a2c31"), cab = X("4a4e57"), top = X("e9ecef") },
            new Th { i = 2, key = "rustic", name = "Rustic", wood = X("7a4e2c"), woodDk = X("4e301b"), fab = X("8a4f2e"), fab2 = X("c9b48e"), acc = X("a8323a"), metal = X("4a4a50"), light = X("d8cbb0"), dark = X("2e2420"), cab = X("8a5a32"), top = X("8d8a83") },
            new Th { i = 3, key = "retro", name = "Retro", wood = X("8b5a32"), woodDk = X("5e3b20"), fab = X("e07b39"), fab2 = X("3f9a96"), acc = X("e2b23a"), metal = X("d0d4da"), light = X("f2e6c8"), dark = X("3a2a22"), cab = X("86c9b8"), top = X("ecd36a") },
            new Th { i = 4, key = "coastal", name = "Coastal", wood = X("e6e2d8"), woodDk = X("bdb5a3"), fab = X("8fbcd6"), fab2 = X("f4f1ea"), acc = X("34507a"), metal = X("c9a46a"), light = X("fbfaf6"), dark = X("3c4a5a"), cab = X("a9cbe0"), top = X("f4f1ea") },
        };
    }

    static Obj New(Th t, string cat, string idBase, string[] names, int[] prices, string[] descs, int w, int d)
    {
        var o = new Obj { cat = cat, id = idBase + "_" + t.key, name = names[t.i], theme = t.key, w = w, d = d, price = prices[t.i], desc = descs[t.i] };
        all.Add(o); return o;
    }
    static Obj NewAny(string cat, string id, string theme, string name, int w, int d, int price, string desc)
    {
        var o = new Obj { cat = cat, id = id, name = name, theme = theme, w = w, d = d, price = price, desc = desc };
        all.Add(o); return o;
    }

    // ================================================================ LIVING ROOM
    static void Couch(Th t)
    {
        Obj o = New(t, "Couch", "couch",
            new[] { "Sage Cottage Sofa", "Slate Modern Sofa", "Leather Chesterfield", "Tangerine Retro Sofa", "Seaside Slipcover Sofa" },
            new[] { 450, 520, 600, 480, 460 },
            new[] { "Soft sage cushions on turned oak legs.", "Low, square and calm.", "Deep buttoned leather with rolled arms.", "Bright orange on tall walnut legs.", "A loose white slipcover and navy pillows." }, 3, 1);
        Mat F = Fab(t.fab);
        switch (t.i)
        {
            case 0:
                o.Legs(3, 3, 42, 10, 2, 2, Wd(t)).Box(1, 2, 2, 46, 13, 7, F).Box(3, 10, 9, 42, 6, 13, F, 2)
                 .Box(0, 1, 2, 6, 15, 13, F, 2).Box(42, 1, 2, 6, 15, 13, F, 2)
                 .Box(6, 1, 9, 12, 10, 3, F).Box(18, 1, 9, 12, 10, 3, F).Box(30, 1, 9, 12, 10, 3, F)
                 .Box(7, 8, 12, 7, 3, 7, Fab(t.acc), 1).Box(34, 8, 12, 7, 3, 7, Fab(t.fab2), 1);
                break;
            case 1:
                o.Legs(3, 3, 42, 10, 4, 1, Black).Box(1, 2, 4, 46, 13, 5, F).Box(0, 1, 4, 4, 15, 9, F).Box(44, 1, 4, 4, 15, 9, F)
                 .Box(4, 11, 9, 40, 5, 9, F).Box(4, 1, 9, 20, 11, 3, F).Box(24, 1, 9, 20, 11, 3, F).Box(36, 8, 12, 6, 3, 6, Fab(t.acc), 1);
                break;
            case 2:
            {
                Mat L = new Mat(t.fab, Pat.Leather), LT = new Mat(t.fab, Pat.Tufted);
                o.Legs(3, 3, 42, 10, 2, 3, WdD(t)).Box(1, 2, 2, 46, 13, 8, L).Box(3, 10, 10, 42, 6, 12, LT, 1)
                 .Box(0, 1, 2, 6, 15, 12, LT, 2).Box(42, 1, 2, 6, 15, 12, LT, 2).Ell(3, 8, 14, 3.5, 7.5, 2.5, L).Ell(45, 8, 14, 3.5, 7.5, 2.5, L)
                 .Box(6, 1, 10, 12, 10, 3, L).Box(18, 1, 10, 12, 10, 3, L).Box(30, 1, 10, 12, 10, 3, L)
                 .Box(8, 8, 13, 7, 3, 7, new Mat(t.acc, Pat.Plaid, X("2e2a30")), 1);
                break;
            }
            case 3:
                o.Legs(4, 4, 40, 9, 5, 2, Wd(t)).Box(1, 2, 5, 46, 13, 5, F).Box(0, 2, 5, 3, 13, 11, Wd(t)).Box(45, 2, 5, 3, 13, 11, Wd(t))
                 .Box(3, 11, 10, 42, 5, 13, new Mat(t.fab, Pat.Tufted), 1)
                 .Box(3, 1, 10, 14, 11, 3, F).Box(17, 1, 10, 14, 11, 3, F).Box(31, 1, 10, 14, 11, 3, F)
                 .Box(6, 9, 13, 7, 3, 7, Fab(t.fab2), 1).Box(35, 9, 13, 7, 3, 7, Fab(t.acc), 1);
                break;
            default:
            {
                Mat S = Fab(t.fab2);
                o.Box(1, 2, 0, 46, 13, 10, S).Box(0, 1, 0, 6, 15, 15, S, 2).Box(42, 1, 0, 6, 15, 15, S, 2).Box(3, 10, 10, 42, 6, 12, S, 2)
                 .Box(6, 1, 10, 12, 10, 3, S).Box(18, 1, 10, 12, 10, 3, S).Box(30, 1, 10, 12, 10, 3, S)
                 .Box(8, 8, 13, 7, 3, 7, new Mat(t.acc, Pat.Stripes, t.fab2), 1).Box(33, 8, 13, 7, 3, 7, Fab(t.fab), 1);
                break;
            }
        }
    }

    static void Armchair(Th t)
    {
        Obj o = New(t, "Armchair", "armchair",
            new[] { "Sage Reading Chair", "Slate Lounge Chair", "Leather Wingback", "Tangerine Club Chair", "Rattan Peacock Chair" },
            new[] { 260, 300, 340, 280, 290 },
            new[] { "Made for a long book and a cup of tea.", "Clean lines, thin black legs.", "High wings keep the draughts out.", "Buttoned back, walnut arms.", "A fan of woven rattan." }, 1, 1);
        Mat F = Fab(t.fab);
        switch (t.i)
        {
            case 0:
                o.Legs(2, 3, 12, 10, 2, 1, Wd(t)).Box(1, 2, 2, 14, 12, 7, F).Box(0, 2, 2, 3, 13, 12, F, 1).Box(13, 2, 2, 3, 13, 12, F, 1)
                 .Box(2, 10, 9, 12, 5, 13, F, 1).Box(3, 1, 9, 10, 10, 2, F).Box(5, 8, 11, 6, 2, 5, Fab(t.acc), 1);
                break;
            case 1:
                o.Legs(2, 3, 12, 10, 4, 1, Black).Box(1, 2, 4, 14, 12, 5, F).Box(0, 2, 4, 2, 13, 8, F).Box(14, 2, 4, 2, 13, 8, F)
                 .Box(2, 11, 9, 12, 4, 9, F).Box(2, 1, 9, 12, 11, 2, F);
                break;
            case 2:
            {
                Mat L = new Mat(t.fab, Pat.Leather), LT = new Mat(t.fab, Pat.Tufted);
                o.Legs(2, 3, 12, 10, 2, 2, WdD(t)).Box(1, 2, 2, 14, 12, 8, L).Box(0, 2, 2, 3, 12, 12, LT, 1).Box(13, 2, 2, 3, 12, 12, LT, 1)
                 .Box(1, 11, 10, 14, 4, 16, LT, 1).Box(0, 9, 14, 3, 5, 10, LT, 1).Box(13, 9, 14, 3, 5, 10, LT, 1).Box(3, 1, 10, 10, 10, 2, L);
                break;
            }
            case 3:
                o.Legs(3, 3, 10, 9, 5, 1, Wd(t)).Box(1, 2, 5, 14, 12, 4, F).Box(0, 2, 5, 2, 12, 10, Wd(t)).Box(14, 2, 5, 2, 12, 10, Wd(t))
                 .Box(2, 11, 9, 12, 4, 12, new Mat(t.fab, Pat.Tufted), 1).Box(2, 1, 9, 12, 11, 2, F);
                break;
            default:
                o.Cyl(8, 8, 0, 6, 6, 8, Rattan).Ell(8, 13, 16, 8, 2, 12, Rattan).Cyl(8, 7.5, 8, 5.5, 5, 2, Fab(t.fab2)).Box(5, 10, 10, 6, 2, 5, Fab(t.fab), 1);
                break;
        }
    }

    static void Ottoman(Th t)
    {
        Obj o = New(t, "Ottoman", "ottoman",
            new[] { "Tufted Footstool", "Grey Cube Ottoman", "Leather Pouf", "Tangerine Drum Ottoman", "Rattan Pouf" },
            new[] { 110, 120, 140, 120, 115 },
            new[] { "Put your feet up.", "A soft grey block.", "Worn brown leather, stitched round.", "A bright drum on peg legs.", "Woven and topped with a cushion." }, 1, 1);
        switch (t.i)
        {
            case 0: o.Legs(3, 3, 10, 9, 2, 1, Wd(t)).Box(2, 2, 2, 12, 11, 7, new Mat(t.fab, Pat.Tufted), 1); break;
            case 1: o.Box(2, 2, 0, 12, 11, 8, Fab(t.fab)); break;
            case 2: o.Cyl(8, 7.5, 0, 6, 5.5, 8, new Mat(t.fab, Pat.Leather)).Cyl(8, 7.5, 7, 3, 2.5, 1, new Mat(Mul(t.fab, 0.8), Pat.Leather)); break;
            case 3: o.Legs(4, 4, 8, 7, 3, 1, Wd(t)).Cyl(8, 7.5, 3, 6, 5, 6, Fab(t.fab)); break;
            default: o.Cyl(8, 7.5, 0, 6, 5.5, 7, Rattan).Cyl(8, 7.5, 7, 5, 4.5, 1, Fab(t.fab2)); break;
        }
    }

    static void Fireplace(Th t)
    {
        Obj o = New(t, "Fireplace", "fireplace",
            new[] { "Fieldstone Hearth", "Linear Gas Fireplace", "Brick Hearth", "Cone Fireplace", "Tiled White Mantel" },
            new[] { 900, 1100, 950, 850, 1000 },
            new[] { "Round river stones and an oak mantel.", "A long flame in a charcoal wall.", "Red brick and a heavy beam.", "A bright orange cone with a black chimney.", "Blue tiles around the fire, a white mantel above." }, 2, 1);
        switch (t.i)
        {
            case 0:
            {
                Mat S = new Mat(X("b3aa9c"), Pat.Stone, X("857d70"));
                o.Box(1, 4, 0, 30, 12, 24, S).Box(0, 3, 24, 32, 13, 3, Wd(t)).Cut(9, 4, 2, 14, 7, 13).Box(9, 11, 2, 14, 1, 13, Soot).Box(9, 4, 1, 14, 7, 1, Soot)
                 .Box(11, 6, 2, 10, 3, 2, WdD(t)).Ell(16, 7.5, 6, 5, 1.8, 4, Fire).Box(4, 0, 0, 24, 4, 1, new Mat(X("9a9184"), Pat.Stone, X("7a7266")))
                 .Box(4, 9, 27, 2, 2, 4, Pl(t.light)).Box(25, 8, 27, 4, 3, 5, Met(t.metal));
                break;
            }
            case 1:
                o.Box(0, 3, 0, 32, 5, 3, Pl("cfd2d6")).Box(0, 8, 0, 32, 8, 30, Pl("3a3d44")).Cut(4, 8, 8, 24, 4, 6).Box(4, 11, 8, 24, 1, 6, Soot)
                 .Box(4, 8, 7, 24, 3, 1, Pl("8d9097")).Ell(16, 9.5, 9.5, 11, 1.3, 1.6, Fire);
                break;
            case 2:
            {
                Mat B = new Mat(X("a14a32"), Pat.Brick, X("d8c6b0"));
                o.Box(1, 4, 0, 30, 12, 30, B).Box(0, 3, 21, 32, 13, 3, WdD(t)).Cut(8, 4, 2, 16, 7, 14).Box(8, 11, 2, 16, 1, 14, Soot).Box(8, 4, 1, 16, 7, 1, Soot)
                 .Box(10, 6, 2, 12, 3, 2, WdD(t)).Ell(16, 7.5, 6, 5.5, 1.8, 4.5, Fire).Box(3, 0, 0, 26, 4, 2, new Mat(X("8d8a83"), Pat.Stone, X("6d6a63")));
                break;
            }
            case 3:
            {
                Mat O = Pl(t.fab);
                o.Cyl(16, 8.5, 0, 8, 6, 2, Pl(t.dark)).Cyl(16, 8.5, 2, 7, 5.5, 10, O).Cut(10, 3, 4, 12, 5, 7).Box(10, 8, 4, 12, 1, 7, Soot).Box(10, 3, 3, 12, 5, 1, Soot)
                 .Ell(16, 6, 6, 3.5, 1.5, 2.5, Fire).Cone(16, 8.5, 12, 7, 2.5, 9, O).Cyl(16, 8.5, 21, 2, 2, 11, Pl(t.dark));
                break;
            }
            default:
            {
                Mat Wt = Wd(t), Tl = new Mat(t.cab, Pat.Tile, t.light);
                o.Box(1, 4, 0, 30, 12, 26, Wt).Box(7, 3, 1, 18, 1, 17, Tl).Cut(10, 3, 2, 12, 8, 12).Box(10, 10, 2, 12, 1, 12, Soot).Box(10, 3, 1, 12, 7, 1, Soot)
                 .Box(12, 5, 2, 8, 3, 2, Pl("8a7a66")).Ell(16, 6.5, 5.5, 4, 1.6, 3.5, Fire).Box(0, 3, 24, 32, 13, 2, Wt)
                 .Box(5, 10, 26, 2, 2, 6, Pl("b9a88a")).Box(24, 9, 26, 3, 3, 3, new Mat(X("7fb0c8"), Pat.Glass));
                break;
            }
        }
    }

    static void TV(Th t)
    {
        Obj o = New(t, "TV", "tv",
            new[] { "Oak Cabinet and Little TV", "Wide Flatscreen", "Wooden Console TV", "Rabbit-Ear Tube TV", "Whitewashed Media Stand" },
            new[] { 380, 650, 420, 300, 480 },
            new[] { "A cosy little set on a two-door cabinet.", "A huge thin screen on a low black stand.", "The whole TV is a piece of furniture.", "Turn the antenna until the picture clears.", "Woven baskets below, a flat screen above." }, 2, 1);
        switch (t.i)
        {
            case 0:
                o.Box(1, 4, 0, 30, 10, 10, Wd(t)).Box(15, 4, 1, 1, 1, 8, Groove(t.wood)).Box(13, 3, 5, 1, 1, 1, Met(t.metal)).Box(18, 3, 5, 1, 1, 1, Met(t.metal))
                 .Box(8, 6, 10, 16, 8, 12, Pl(t.light), 1).Box(10, 5, 12, 12, 1, 8, Scr).Box(22, 5, 14, 1, 1, 1, Pl(t.dark)).Box(22, 5, 16, 1, 1, 1, Pl(t.dark));
                break;
            case 1:
                o.Legs(1, 6, 30, 7, 2, 1, Met(t.metal)).Box(0, 5, 2, 32, 9, 5, WdD(t)).Box(15, 5, 3, 2, 1, 3, Groove(t.woodDk))
                 .Box(14, 9, 7, 4, 2, 2, Pl(t.dark)).Box(2, 9, 9, 28, 2, 17, Pl("1b1c20")).Box(3, 8, 10, 26, 1, 15, Scr);
                break;
            case 2:
                o.Legs(2, 4, 28, 9, 2, 2, WdD(t)).Box(1, 3, 2, 30, 11, 18, Wd(t)).Box(4, 2, 5, 17, 1, 12, Scr).Box(23, 2, 5, 6, 1, 12, new Mat(X("5a4636"), Pat.Fabric))
                 .Box(24, 1, 15, 1, 1, 1, Met(t.metal)).Box(27, 1, 15, 1, 1, 1, Met(t.metal)).Box(9, 6, 20, 14, 6, 1, Pl(t.light)).Cyl(16, 9, 21, 1.5, 1.5, 4, Pl(t.acc));
                break;
            case 3:
                o.Legs(8, 6, 16, 6, 6, 1, Wd(t)).Box(6, 5, 6, 20, 9, 15, Wd(t), 1).Box(8, 4, 8, 13, 1, 11, Scr)
                 .Box(23, 4, 16, 2, 1, 2, Chrome).Box(23, 4, 12, 2, 1, 2, Chrome).Box(22, 4, 8, 3, 1, 2, Pl(t.dark)).Box(15, 10, 21, 2, 1, 1, Pl(t.dark));
                for (int k = 0; k < 7; k++) { o.Box(14 - k, 10, 21 + k, 1, 1, 1, Chrome); o.Box(17 + k, 10, 21 + k, 1, 1, 1, Chrome); }
                break;
            default:
                o.Box(1, 4, 0, 30, 10, 9, Wd(t)).Box(15, 4, 1, 1, 1, 7, Groove(t.wood)).Box(3, 3, 1, 10, 1, 6, Rattan).Box(19, 3, 1, 10, 1, 6, Rattan)
                 .Box(14, 9, 9, 4, 2, 1, Pl(t.dark)).Box(4, 9, 10, 24, 2, 15, Pl("1b1c20")).Box(5, 8, 11, 22, 1, 13, Scr);
                break;
        }
    }

    static void ComputerDesk(Th t)
    {
        Obj o = New(t, "ComputerDesk", "computer",
            new[] { "Writing Desk and Laptop", "Dual-Monitor Workstation", "Heavy Desk and Old Terminal", "Walnut Desk and Beige PC", "White Desk and All-in-One" },
            new[] { 420, 800, 460, 520, 600 },
            new[] { "A little laptop, a mug and some books.", "Two screens, a glowing tower.", "Green text on a black screen. It still works.", "Floppy disks not included.", "One slim screen and a plant." }, 2, 1);
        switch (t.i)
        {
            case 0:
            {
                Mat W = Wd(t), lap = Met("c8ccd2");
                o.Legs(1, 4, 30, 11, 12, 2, W).Box(0, 3, 12, 32, 13, 2, W).Box(2, 4, 9, 10, 11, 3, W).Box(6, 3, 10, 2, 1, 1, Met(t.metal))
                 .Box(10, 6, 14, 12, 6, 1, lap).Box(10, 12, 14, 12, 1, 9, lap).Box(11, 11, 15, 10, 1, 7, LitBlue)
                 .Cyl(26, 8, 14, 1.5, 1.5, 3, Pl(t.acc)).Box(2, 9, 14, 6, 4, 2, Pl("6d8fb0")).Box(2, 9, 16, 6, 4, 1, Pl(t.acc));
                break;
            }
            case 1:
            {
                Mat Wh = Pl(t.light);
                o.Box(1, 4, 0, 1, 11, 12, Black).Box(30, 4, 0, 1, 11, 12, Black).Box(0, 3, 12, 32, 13, 1, Wh)
                 .Box(24, 7, 0, 5, 8, 11, Black).Box(25, 6, 3, 1, 1, 6, LitBlue)
                 .Box(8, 13, 13, 2, 2, 3, Black).Box(3, 13, 16, 12, 2, 9, Black).Box(4, 12, 17, 10, 1, 7, LitBlue)
                 .Box(22, 13, 13, 2, 2, 3, Black).Box(17, 13, 16, 12, 2, 9, Black).Box(18, 12, 17, 10, 1, 7, LitBlue)
                 .Box(9, 5, 13, 12, 3, 1, Pl("cfd3d8")).Box(23, 6, 13, 2, 2, 1, Pl("cfd3d8"));
                break;
            }
            case 2:
            {
                Mat W = WdD(t), G = Groove(t.woodDk), beige = Pl("d8cfb4");
                o.Box(0, 3, 0, 7, 13, 12, W).Box(25, 3, 0, 7, 13, 12, W).Box(0, 3, 8, 7, 1, 1, G).Box(0, 3, 4, 7, 1, 1, G).Box(25, 3, 8, 7, 1, 1, G).Box(25, 3, 4, 7, 1, 1, G)
                 .Box(3, 2, 10, 1, 1, 1, Brass).Box(3, 2, 6, 1, 1, 1, Brass).Box(28, 2, 10, 1, 1, 1, Brass).Box(28, 2, 6, 1, 1, 1, Brass)
                 .Box(0, 2, 12, 32, 14, 2, W).Box(9, 8, 14, 14, 8, 11, beige, 1).Box(11, 7, 16, 10, 1, 7, new Mat(X("3f7a44"), Pat.Lit, X("8fd08f")))
                 .Box(9, 3, 14, 12, 4, 1, Pl("cbbf9e")).Box(27, 10, 14, 2, 2, 1, Brass).Box(27, 11, 15, 1, 1, 6, Brass).Ell(28, 9.5, 21, 2.5, 2, 1.5, Pl(t.acc));
                break;
            }
            case 3:
            {
                Mat beige = Pl("ddd3b8");
                o.Legs(1, 4, 30, 11, 12, 1, Black).Box(0, 3, 12, 32, 13, 2, Wd(t)).Box(6, 7, 14, 16, 8, 4, beige).Box(16, 6, 15, 4, 1, 1, Pl(t.dark))
                 .Box(8, 8, 18, 12, 7, 9, beige, 1).Box(9, 7, 19, 10, 1, 7, new Mat(X("b8742a"), Pat.Lit, X("ffc060")))
                 .Box(7, 3, 14, 14, 3, 1, beige).Cone(27, 9, 14, 3, 1.5, 6, Pl(t.fab));
                break;
            }
            default:
            {
                Mat Wt = Wd(t);
                o.Legs(1, 4, 30, 11, 12, 1, Wt).Box(0, 3, 12, 32, 13, 2, Wt).Box(10, 3, 9, 12, 1, 3, Wt).Box(16, 2, 10, 1, 1, 1, Pl(t.acc))
                 .Box(15, 12, 14, 4, 2, 1, Chrome).Box(9, 13, 15, 16, 1, 11, Pl("eef0f2")).Box(10, 12, 16, 14, 1, 9, LitBlue)
                 .Box(11, 5, 14, 10, 3, 1, Pl("ffffff")).Cyl(4, 9, 14, 2, 2, 3, Pl(t.fab)).Ell(4, 9, 19, 2.5, 2.5, 2.5, Leaves, 0.3);
                break;
            }
        }
        o.Top(14);
    }

    static void SideTable(Th t)
    {
        Obj o = New(t, "SideTable", "sidetable",
            new[] { "Round Tea Table", "Black Cube Table", "Log Stump Table", "Tulip Side Table", "Whitewash Nightstand" },
            new[] { 90, 110, 70, 120, 100 },
            new[] { "A lace doily and a teacup.", "An open black cube with a tiny plant.", "A slice of old oak with a candle on top.", "One slim white stem.", "One drawer and a sea shell." }, 1, 1);
        switch (t.i)
        {
            case 0: o.Cyl(8, 8, 0, 4, 4, 1, Wd(t)).Cyl(8, 8, 1, 1.5, 1.5, 8, Wd(t)).Cyl(8, 8, 9, 6, 5.5, 2, Wd(t)).Cyl(8, 8, 11, 4, 3.5, 1, Pl("f6f1e6")).Cyl(9, 7, 12, 1.5, 1.5, 2, Pl(t.acc)).Top(11); break;
            case 1: o.Box(2, 3, 0, 12, 11, 10, Pl(t.wood)).Cut(3, 3, 2, 10, 9, 6).Cyl(8, 8, 10, 2, 2, 3, Pl(t.light)).Ell(8, 8, 14, 2.5, 2.5, 2, Leaves, 0.3).Top(10); break;
            case 2:
                o.Cyl(8, 8, 0, 6, 6, 10, new Mat(X("5e3b20"), Pat.Wood)).Cyl(8, 8, 10, 5.5, 5.5, 1, new Mat(X("c79a62"), Pat.Rings, X("a87a48"), X("b88a55")))
                 .Cyl(5.5, 9.5, 11, 1, 1, 3, Pl(t.light)).Box(5, 9, 14, 1, 1, 1, Fire).Top(11);
                break;
            case 3: o.Cyl(8, 8, 0, 4, 3.5, 1, Pl(t.light)).Cyl(8, 8, 1, 1, 1, 8, Pl(t.light)).Cyl(8, 8, 9, 6, 5.5, 1, Wd(t)).Cyl(8, 8, 10, 1.5, 1.5, 4, Pl(t.fab)).Top(10); break;
            default: o.Legs(3, 3, 10, 10, 9, 1, Wd(t)).Box(3, 3, 6, 10, 10, 3, Wd(t)).Box(2, 3, 9, 12, 11, 2, Wd(t)).Box(8, 2, 7, 1, 1, 1, Pl(t.acc)).Ell(8, 8, 11.5, 2, 1.5, 1, Pl("f0c8b8")).Top(11); break;
        }
    }

    static void CoffeeTable(Th t)
    {
        Obj o = New(t, "CoffeeTable", "coffeetable",
            new[] { "Oval Oak Coffee Table", "Glass Coffee Table", "Plank Coffee Table", "Kidney Coffee Table", "Rattan Coffee Table" },
            new[] { 180, 240, 160, 210, 190 },
            new[] { "Books, a vase and a rosebud.", "Black steel, clear glass, a shelf below.", "Three thick planks on two blocks.", "A curvy walnut top on peg legs.", "Woven sides and a white top." }, 2, 1);
        switch (t.i)
        {
            case 0:
                o.Cyl(6, 5, 0, 1, 1, 8, Wd(t)).Cyl(26, 5, 0, 1, 1, 8, Wd(t)).Cyl(6, 11, 0, 1, 1, 8, Wd(t)).Cyl(26, 11, 0, 1, 1, 8, Wd(t)).Cyl(16, 8, 8, 14, 6, 2, Wd(t))
                 .Box(19, 6, 10, 7, 4, 1, Pl(t.acc)).Box(19, 6, 11, 7, 4, 1, Pl(t.fab2)).Cyl(10, 8, 10, 1.5, 1.5, 3, Pl(t.fab)).Ell(10, 8, 14, 2, 2, 1.5, Pl(t.acc));
                break;
            case 1:
                o.Box(2, 3, 0, 1, 1, 8, Black).Box(29, 3, 0, 1, 1, 8, Black).Box(2, 12, 0, 1, 1, 8, Black).Box(29, 12, 0, 1, 1, 8, Black)
                 .Box(2, 3, 7, 28, 1, 1, Black).Box(2, 12, 7, 28, 1, 1, Black).Box(3, 4, 2, 26, 8, 1, Pl(t.wood))
                 .Box(2, 3, 8, 28, 10, 1, new Mat(X("b8dbe6"), Pat.Glass, X("98bbc6"), X("eaf6fa"))).Box(6, 6, 9, 6, 4, 1, Pl(t.acc)).Cyl(22, 8, 9, 2.5, 2, 2, Pl(t.light));
                break;
            case 2:
                o.Box(3, 3, 0, 4, 10, 7, WdD(t)).Box(25, 3, 0, 4, 10, 7, WdD(t)).Box(1, 2, 7, 30, 4, 3, WdD(t)).Box(1, 6, 7, 30, 4, 3, WdD(t)).Box(1, 10, 7, 30, 4, 3, WdD(t))
                 .Box(20, 6, 10, 3, 3, 4, Iron).Box(21, 5, 11, 1, 1, 2, Fire).Box(6, 5, 10, 6, 4, 1, Pl(t.acc));
                break;
            case 3:
                o.Box(6, 5, 0, 1, 1, 8, Wd(t)).Box(25, 6, 0, 1, 1, 8, Wd(t)).Box(14, 11, 0, 1, 1, 8, Wd(t)).Cyl(11, 8, 8, 9, 5, 1, Wd(t)).Cyl(21, 8.5, 8, 9, 4.5, 1, Wd(t))
                 .Cyl(12, 8, 9, 3, 2, 2, Pl(t.fab)).Cyl(22, 9, 9, 1.5, 1.5, 1, Pl(t.fab2));
                break;
            default:
                o.Box(2, 3, 0, 28, 10, 8, Rattan, 1).Box(2, 3, 8, 28, 10, 1, Wd(t)).Box(5, 5, 9, 7, 4, 1, Pl(t.acc)).Ell(23, 8, 10, 2, 1.5, 1, Pl("f0c8b8"));
                break;
        }
        o.Top(t.i == 2 ? 10 : 9);
    }

    static void Chair(Th t)
    {
        Obj o = New(t, "Chair", "chair",
            new[] { "Ladderback Chair", "Moulded Chair", "Heart-Cut Farm Chair", "Diner Chair", "Rattan Dining Chair" },
            new[] { 80, 95, 85, 90, 90 },
            new[] { "Oak rungs and a sage seat pad.", "One smooth white shell.", "A heart cut into the backrest.", "Chrome legs and teal vinyl.", "Woven seat with a blue cushion." }, 1, 1);
        switch (t.i)
        {
            case 0:
            {
                Mat W = Wd(t);
                o.Legs(3, 3, 10, 9, 7, 1, W).Box(2, 2, 7, 12, 10, 2, W).Box(3, 3, 9, 10, 8, 1, Fab(t.fab)).Box(2, 11, 9, 1, 2, 13, W).Box(13, 11, 9, 1, 2, 13, W)
                 .Box(3, 11, 12, 10, 1, 1, W).Box(3, 11, 15, 10, 1, 1, W).Box(3, 11, 18, 10, 1, 2, W);
                break;
            }
            case 1: o.Legs(3, 3, 10, 8, 7, 1, Met(t.metal)).Box(2, 2, 7, 12, 10, 2, Pl(t.light), 2).Box(2, 10, 9, 12, 3, 10, Pl(t.light), 1); break;
            case 2: o.Legs(2, 2, 12, 10, 7, 2, Wd(t)).Box(1, 2, 7, 14, 11, 3, Wd(t)).Box(1, 11, 10, 14, 2, 12, Wd(t)).Cut(6, 11, 15, 4, 2, 3).Cut(7, 11, 14, 2, 2, 1); break;
            case 3:
                o.Legs(3, 3, 10, 9, 7, 1, Chrome).Box(2, 2, 7, 12, 10, 2, Pl(t.fab2)).Box(3, 12, 9, 1, 1, 4, Chrome).Box(12, 12, 9, 1, 1, 4, Chrome)
                 .Box(2, 11, 12, 12, 2, 8, Pl(t.fab2), 1);
                break;
            default: o.Legs(3, 3, 10, 9, 7, 1, Wd(t)).Box(2, 2, 7, 12, 10, 2, Rattan).Box(2, 11, 9, 12, 2, 11, Rattan, 1).Box(3, 3, 9, 10, 8, 1, Fab(t.fab)); break;
        }
    }

    static void Rug(Th t)
    {
        Obj o = New(t, "Rug", "rug",
            new[] { "Braided Oval Rug", "Geometric Rug", "Red Plaid Rug", "Shag Circle Rug", "Navy Stripe Rug" },
            new[] { 150, 170, 140, 160, 150 },
            new[] { "Rings of rust, cream and sage.", "Thin lines on soft grey.", "A warm cabin plaid with fringe.", "Thick, soft and very orange.", "Like a deck chair, but on the floor." }, 3, 2);
        o.mount = "Flat";
        switch (t.i)
        {
            case 0: o.Cyl(24, 16, 0, 23, 15, 1, new Mat(X("b5654a"), Pat.Rings, X("e6d3a8"), X("8fae7e"))); break;
            case 1: o.Box(1, 2, 0, 46, 28, 1, Pl("5b6069")).Box(3, 4, 0, 42, 24, 1, new Mat(X("c9ccd1"), Pat.Diag, X("8e939b"))); break;
            case 2:
            {
                Mat fr = new Mat(X("e8dcc0"), Pat.Stripes, X("c9b48e"));
                o.Box(2, 2, 0, 44, 28, 1, new Mat(X("a8323a"), Pat.Plaid, X("2e2a30"))).Box(0, 4, 0, 2, 24, 1, fr).Box(46, 4, 0, 2, 24, 1, fr);
                break;
            }
            case 3: o.Cyl(24, 16, 0, 16, 15, 1, new Mat(t.fab, Pat.Shag)).Cyl(24, 16, 0, 10.5, 9.5, 1, new Mat(t.acc, Pat.Shag)).Cyl(24, 16, 0, 5, 4.5, 1, new Mat(t.fab2, Pat.Shag)); break;
            default:
            {
                Mat fr = new Mat(X("f4f1ea"), Pat.Stripes, X("d8d2c4"));
                o.Box(3, 2, 0, 42, 28, 1, new Mat(t.acc, Pat.Stripes, t.fab2)).Box(1, 3, 0, 2, 26, 1, fr).Box(45, 3, 0, 2, 26, 1, fr);
                break;
            }
        }
    }

    static void Plant(Th t)
    {
        Obj o = New(t, "Plant", "plant",
            new[] { "Fern in Terracotta", "Snake Plant", "Barrel Olive Tree", "Monstera on a Stand", "Palm in a Basket" },
            new[] { 60, 70, 140, 110, 90 },
            new[] { "Feathery fronds in a clay pot.", "Tall, stiff and impossible to kill.", "A small olive tree in half a barrel.", "Big split leaves on a mustard pot.", "A little palm in a woven basket." }, 1, 1);
        switch (t.i)
        {
            case 0:
                o.Cyl(8, 8, 0, 3.5, 3.5, 6, Pl("c46a3d")).Cyl(8, 8, 5, 4.5, 4.5, 2, Pl("d27a4a")).Ell(8, 8, 11, 6, 5.5, 4, Leaves, 0.35)
                 .Ell(4.5, 8, 9, 3, 3, 2.5, Leaves, 0.3).Ell(11.5, 8, 9, 3, 3, 2.5, Leaves, 0.3).Ell(8, 7, 14, 3.5, 3, 2.5, Leaves, 0.3);
                break;
            case 1:
            {
                Mat sn = new Mat(X("3f7a3a"), Pat.Leaves, X("2a5a2a"), X("c9c060"));
                o.Cyl(8, 8, 0, 3.5, 3.5, 8, Pl(t.light)).Box(5, 8, 8, 2, 1, 12, sn).Box(8, 7, 8, 2, 1, 16, sn).Box(10, 9, 8, 2, 1, 13, sn).Box(7, 10, 8, 2, 1, 10, sn).Box(6, 6, 8, 1, 1, 9, sn);
                break;
            }
            case 2:
                o.Cyl(8, 8, 0, 5, 5, 7, new Mat(X("7a4e2c"), Pat.Wood)).Cyl(8, 8, 1, 5.4, 5.4, 1, Iron).Cyl(8, 8, 5, 5.4, 5.4, 1, Iron)
                 .Box(7, 7, 7, 2, 2, 10, new Mat(X("6b5040"), Pat.Wood)).Ell(8, 8, 21, 6.5, 6, 6, new Mat(X("7d9a5a"), Pat.Leaves, X("5a7a40"), X("a8c080")), 0.35);
                break;
            case 3:
            {
                Mat mon = new Mat(X("3d8a45"), Pat.Leaves, X("2a6a32"), X("7fc070"));
                o.Legs(4, 4, 8, 8, 4, 1, Wd(t)).Cyl(8, 8, 4, 4, 4, 6, Pl(t.acc)).Box(8, 8, 10, 1, 1, 6, Pl("3f7a3a"))
                 .Ell(4.5, 8, 13, 4, 3, 1.5, mon).Ell(11.5, 9, 14, 4, 3, 1.5, mon).Ell(8, 6.5, 17, 3.5, 3, 1.5, mon).Ell(8, 10, 12, 3, 3, 2, mon);
                break;
            }
            default:
            {
                Mat palm = new Mat(X("5f9a40"), Pat.Leaves, X("3f7a30"), X("9cd060"));
                o.Cyl(8, 8, 0, 4.5, 4.5, 6, Rattan).Box(8, 8, 6, 1, 1, 7, Pl("6b8a3a"))
                 .Ell(4, 8, 12, 4, 1.3, 1.2, palm).Ell(12, 8, 12, 4, 1.3, 1.2, palm).Ell(8, 4, 13, 1.3, 4, 1.2, palm).Ell(8, 12, 11, 1.3, 4, 1.2, palm)
                 .Ell(5, 5, 14, 2.5, 2.5, 1, palm).Ell(11, 11, 14, 2.5, 2.5, 1, palm).Ell(11, 5, 13, 2.5, 2.5, 1, palm).Ell(5, 11, 13, 2.5, 2.5, 1, palm);
                break;
            }
        }
    }

    static void FloorLamp(Th t)
    {
        Obj o = New(t, "FloorLamp", "floorlamp",
            new[] { "Pleated Floor Lamp", "Arc Floor Lamp", "Iron Tripod Lamp", "Drum Tripod Lamp", "Rattan Floor Lamp" },
            new[] { 120, 180, 130, 140, 130 },
            new[] { "A brass pole and a pleated cream shade.", "Leans over the sofa like a reading light.", "Three black iron legs and a burlap shade.", "Peg legs and a big orange drum.", "A woven shade on a white pole." }, 1, 1);
        switch (t.i)
        {
            case 0: o.Cyl(8, 8, 0, 3, 3, 1, Met(t.metal)).Box(8, 8, 1, 1, 1, 20, Met(t.metal)).Cone(8.5, 8.5, 19, 5, 3, 7, new Mat(X("f6e7c4"), Pat.Folds)); break;
            case 1:
                o.Box(1, 10, 0, 5, 5, 2, Pl("e8e8e4")).Box(3, 12, 2, 1, 1, 23, Met(t.metal)).Box(4, 12, 24, 4, 1, 1, Met(t.metal)).Box(8, 12, 23, 2, 1, 1, Met(t.metal))
                 .Box(10, 12, 22, 1, 1, 1, Met(t.metal)).Ell(11, 11.5, 19.5, 3.5, 3, 2.5, Pl(t.dark)).Box(10, 10, 17, 2, 1, 1, new Mat(X("ffe7a0"), Pat.Lit, X("fff4d0")));
                break;
            case 2:
                o.Box(5, 5, 0, 1, 1, 6, Iron).Box(11, 5, 0, 1, 1, 6, Iron).Box(8, 11, 0, 1, 1, 6, Iron).Box(6, 6, 5, 5, 5, 1, Iron).Box(8, 8, 5, 1, 1, 14, Iron)
                 .Cyl(8.5, 8.5, 18, 5, 5, 7, new Mat(X("b89a6a"), Pat.Rattan));
                break;
            case 3: o.Box(5, 5, 0, 1, 1, 8, Wd(t)).Box(11, 5, 0, 1, 1, 8, Wd(t)).Box(8, 11, 0, 1, 1, 8, Wd(t)).Box(8, 8, 8, 1, 1, 10, Wd(t)).Cyl(8.5, 8.5, 17, 5, 5, 8, new Mat(t.fab, Pat.Folds)); break;
            default: o.Cyl(8, 8, 0, 3.5, 3.5, 1, Pl(t.light)).Box(8, 8, 1, 1, 1, 19, Pl(t.light)).Cone(8.5, 8.5, 18, 6, 4, 7, Rattan); break;
        }
    }

    // ================================================================ BOOKS, BEDROOM
    static void Books(Obj o, int x0, int x1, int z, int y, int depth, int maxH, Color[] pal, int seed)
    {
        int x = x0;
        while (x < x1)
        {
            int bw = 1 + (int)(Rnd(x, z, seed, 31) * 2.2); if (x + bw > x1) bw = x1 - x;
            if (Rnd(x, z, seed, 32) < 0.12 && x + 3 < x1) { x += 2; continue; }
            int bh = maxH - (int)(Rnd(x, z, seed, 33) * 3);
            Color c = pal[(int)(Rnd(x, z, seed, 34) * pal.Length) % pal.Length];
            o.Box(x, y, z, bw, depth, bh, Pl(c)); x += bw;
        }
    }

    static void Bookshelf(Th t)
    {
        Obj o = New(t, "Bookshelf", "bookshelf",
            new[] { "Oak Bookshelf", "Black Grid Shelf", "Crate Bookshelf", "Walnut Bookcase", "White Basket Shelf" },
            new[] { 260, 300, 180, 320, 280 },
            new[] { "Three full shelves of well-loved books.", "Open squares you can see through.", "Old fruit crates stacked up.", "Sliding teal and orange doors below.", "Woven baskets hide the clutter." }, 2, 1);
        Color[] pal;
        switch (t.i)
        {
            case 0:
                pal = new[] { t.acc, t.fab, t.fab2, X("6d8fb0"), X("8a5a3a"), X("c9a25a") };
                o.Box(1, 6, 0, 30, 10, 30, Wd(t)).Cut(3, 6, 2, 26, 8, 7).Cut(3, 6, 11, 26, 8, 7).Cut(3, 6, 20, 26, 8, 7)
                 .Box(3, 14, 2, 26, 1, 7, WdD(t)).Box(3, 14, 11, 26, 1, 7, WdD(t)).Box(3, 14, 20, 26, 1, 7, WdD(t));
                Books(o, 3, 29, 2, 8, 6, 6, pal, 1); Books(o, 3, 22, 11, 8, 6, 6, pal, 2); o.Cyl(25.5, 11, 11, 2, 2, 4, Pl("c46a3d")).Ell(25.5, 11, 17, 2.5, 2.5, 2, Leaves, 0.3); Books(o, 3, 29, 20, 8, 6, 6, pal, 3);
                break;
            case 1:
                pal = new[] { X("eef0f2"), X("9aa1ab"), X("2a2c31"), t.acc };
                o.Box(1, 8, 0, 30, 8, 30, Pl(t.dark)).Cut(2, 8, 1, 13, 8, 9).Cut(17, 8, 1, 13, 8, 9).Cut(2, 8, 11, 13, 8, 9).Cut(17, 8, 11, 13, 8, 9).Cut(2, 8, 21, 13, 8, 8).Cut(17, 8, 21, 13, 8, 8);
                Books(o, 3, 11, 1, 10, 5, 7, pal, 4); o.Cyl(23, 12, 1, 2, 2, 6, Pl(t.acc)).Box(19, 9, 11, 7, 5, 4, Pl(t.light)); Books(o, 3, 14, 11, 10, 5, 7, pal, 5);
                o.Cyl(8, 12, 21, 2, 2, 3, Pl(t.light)).Ell(8, 12, 26, 2.5, 2.5, 2, Leaves, 0.3); Books(o, 18, 29, 21, 10, 5, 6, pal, 6);
                break;
            case 2:
                pal = new[] { X("6b3a2a"), X("8a4f2e"), X("a8323a"), X("c9b48e"), X("4a5a3a") };
                for (int r = 0; r < 3; r++) for (int c = 0; c < 2; c++)
                {
                    int x = 1 + c * 15, z = r * 10;
                    o.Box(x, 6, z, 15, 10, 10, Wd(t)).Cut(x + 1, 6, z + 1, 13, 8, 8);
                    if ((r + c) % 3 == 1) { o.Cyl(x + 4, 10, z + 1, 2, 2, 5, new Mat(X("9fc8a0"), Pat.Glass)).Cyl(x + 9, 10, z + 1, 2, 2, 4, new Mat(X("d8b070"), Pat.Glass)); }
                    else Books(o, x + 1, x + 14, z + 1, 8, 6, 7, pal, r * 2 + c);
                }
                break;
            case 3:
                pal = new[] { t.fab, t.fab2, t.acc, X("f2e6c8"), X("6a4a32") };
                o.Box(1, 6, 0, 30, 10, 30, Wd(t)).Cut(3, 6, 2, 26, 8, 7).Cut(3, 6, 11, 26, 8, 7).Cut(3, 6, 20, 26, 8, 7)
                 .Box(3, 14, 11, 26, 1, 7, WdD(t)).Box(3, 14, 20, 26, 1, 7, WdD(t)).Box(3, 6, 2, 13, 1, 7, Pl(t.fab2)).Box(16, 6, 2, 13, 1, 7, Pl(t.fab))
                 .Box(14, 5, 5, 1, 1, 2, Chrome).Box(17, 5, 5, 1, 1, 2, Chrome);
                Books(o, 3, 20, 11, 8, 6, 6, pal, 7); o.Cone(25, 11, 11, 2.5, 1, 6, Pl(t.fab)); Books(o, 3, 29, 20, 8, 6, 6, pal, 8);
                break;
            default:
                pal = new[] { t.acc, t.fab, X("f4f1ea"), X("c9a46a") };
                o.Box(1, 6, 0, 30, 10, 30, Wd(t)).Cut(3, 6, 2, 26, 8, 7).Cut(3, 6, 11, 26, 8, 7).Cut(3, 6, 20, 26, 8, 7)
                 .Box(3, 14, 2, 26, 1, 7, Pl(t.cab)).Box(3, 14, 11, 26, 1, 7, Pl(t.cab)).Box(3, 14, 20, 26, 1, 7, Pl(t.cab))
                 .Box(4, 7, 2, 11, 7, 6, Rattan, 1).Box(17, 7, 2, 11, 7, 6, Rattan, 1);
                Books(o, 3, 18, 11, 8, 6, 6, pal, 9); o.Ell(23, 10, 12, 2.5, 2, 1.5, Pl("f0c8b8")); Books(o, 10, 29, 20, 8, 6, 6, pal, 10); o.Cyl(6, 10, 20, 2, 2, 4, new Mat(X("7fb0c8"), Pat.Glass));
                break;
        }
    }

    static void Bed(Th t)
    {
        Obj o = New(t, "Bed", "bed",
            new[] { "Patchwork Bed", "Platform Bed", "Log Bed", "Walnut Bed", "Seaside Bed" },
            new[] { 500, 620, 560, 540, 520 },
            new[] { "A hand-sewn patchwork quilt.", "Low and wide, with a mustard throw.", "Built from whole logs, with a plaid quilt.", "Striped orange and mustard bedding.", "Navy stripes and a white slatted headboard." }, 2, 3);
        Mat sheet = Pl("f3f3ee"), pil = Pl("fbfbf8");
        switch (t.i)
        {
            case 0:
                o.Legs(1, 2, 30, 44, 3, 2, Wd(t)).Box(1, 2, 3, 30, 43, 5, Wd(t)).Box(2, 3, 8, 28, 41, 4, sheet).Box(1, 2, 11, 30, 30, 2, new Mat(t.acc, Pat.Checker, t.fab2))
                 .Box(1, 31, 11, 30, 3, 3, sheet).Box(4, 36, 12, 11, 6, 3, pil, 1).Box(17, 36, 12, 11, 6, 3, pil, 1)
                 .Box(0, 44, 0, 32, 4, 22, Wd(t), 1).Ell(16, 46, 22, 15, 2, 4, Wd(t)).Box(0, 0, 0, 32, 2, 13, Wd(t));
                break;
            case 1:
                o.Box(0, 2, 0, 32, 44, 7, WdD(t)).Box(2, 3, 7, 28, 41, 4, sheet).Box(1, 2, 10, 30, 30, 2, Fab(t.fab)).Box(1, 31, 10, 30, 3, 3, sheet)
                 .Box(1, 5, 12, 30, 6, 1, Fab(t.acc)).Box(4, 36, 11, 11, 6, 3, pil, 1).Box(17, 36, 11, 11, 6, 3, Fab(t.fab2), 1).Box(1, 44, 0, 30, 4, 18, Fab(t.fab), 1);
                break;
            case 2:
            {
                Mat log = new Mat(X("8a5a32"), Pat.Wood);
                o.Cyl(2.5, 45.5, 0, 2.5, 2.5, 26, log).Cyl(29.5, 45.5, 0, 2.5, 2.5, 26, log).Cyl(2.5, 2.5, 0, 2.5, 2.5, 14, log).Cyl(29.5, 2.5, 0, 2.5, 2.5, 14, log)
                 .Box(3, 44, 8, 26, 3, 3, log).Box(3, 44, 14, 26, 3, 3, log).Box(3, 44, 20, 26, 3, 3, log).Box(3, 1, 8, 26, 3, 3, log).Box(2, 4, 3, 28, 40, 5, log)
                 .Box(3, 4, 8, 26, 39, 4, sheet).Box(2, 5, 11, 28, 28, 2, new Mat(t.acc, Pat.Plaid, X("2e2a30"))).Box(2, 32, 11, 28, 3, 3, sheet)
                 .Box(5, 36, 12, 10, 6, 3, pil, 1).Box(17, 36, 12, 10, 6, 3, pil, 1);
                break;
            }
            case 3:
                o.Legs(2, 3, 28, 42, 4, 1, Wd(t)).Box(1, 2, 4, 30, 43, 4, Wd(t)).Box(2, 3, 8, 28, 41, 4, sheet).Box(1, 2, 11, 30, 30, 2, new Mat(t.fab, Pat.Stripes, t.acc))
                 .Box(1, 31, 11, 30, 3, 3, sheet).Box(4, 36, 12, 11, 6, 3, Fab(t.fab2), 1).Box(17, 36, 12, 11, 6, 3, Fab(t.fab2), 1).Box(0, 44, 4, 32, 4, 16, Wd(t), 1);
                break;
            default:
                o.Box(1, 2, 0, 30, 43, 8, Wd(t)).Box(2, 3, 8, 28, 41, 4, sheet).Box(1, 2, 11, 30, 30, 2, new Mat(t.acc, Pat.Stripes, t.fab2))
                 .Box(1, 31, 11, 30, 3, 3, sheet).Box(4, 36, 12, 11, 6, 3, Fab(t.fab), 1).Box(17, 36, 12, 11, 6, 3, pil, 1).Box(0, 44, 0, 32, 4, 22, Wd(t), 1);
                for (int x = 4; x < 29; x += 4) o.Box(x, 44, 9, 1, 1, 11, Groove(t.wood));
                break;
        }
    }

    static void Dresser(Th t)
    {
        Obj o = New(t, "Dresser", "dresser",
            new[] { "Oak Dresser", "White Lacquer Dresser", "Iron-Pull Dresser", "Walnut Lowboy", "Seaside Dresser" },
            new[] { 280, 320, 260, 300, 290 },
            new[] { "Brass knobs, a little lamp and a photo.", "No handles, just smooth white fronts.", "Dark oak with black iron pulls.", "On peg legs, with a lava lamp.", "White drawers with navy knobs and a lantern." }, 2, 1);
        Mat W = Wd(t), G = Groove(t.wood);
        Mat knob = t.i == 2 ? Iron : (t.i == 4 ? Pl(t.acc) : (t.i == 3 ? Pl(t.acc) : Brass));
        switch (t.i)
        {
            case 1: W = Pl(t.light); G = Pl(X("c9ccd1")); o.Box(2, 5, 0, 28, 10, 2, Pl(t.dark)).Box(1, 4, 2, 30, 12, 16, W).Top(18); break;
            case 3: o.Legs(2, 5, 28, 9, 5, 1, W).Box(1, 4, 5, 30, 12, 13, W).Top(18); break;
            default: o.Legs(2, 5, 28, 10, 2, 2, W).Box(1, 4, 2, 30, 12, 18, W).Top(20); break;
        }
        int z0 = t.i == 3 ? 5 : 2, zt = o.top;
        int z1 = z0 + (zt - z0) / 3, z2 = z0 + 2 * (zt - z0) / 3;
        o.Box(1, 4, z1, 30, 1, 1, G).Box(1, 4, z2, 30, 1, 1, G).Box(16, 4, z0, 1, 1, z2 - z0, G);
        if (t.i != 1)
        {
            int kw = t.i == 2 ? 3 : 1;
            foreach (int zz in new[] { (z0 + z1) / 2, (z1 + z2) / 2 }) { o.Box(8, 3, zz, kw, 1, 1, knob).Box(23, 3, zz, kw, 1, 1, knob); }
            o.Box(15, 3, (z2 + zt) / 2, 2, 1, 1, knob);
        }
        switch (t.i)
        {
            case 0: o.Cyl(6, 10, zt, 2, 2, 1, Brass).Box(6, 10, zt + 1, 1, 1, 4, Brass).Cone(6.5, 10.5, zt + 5, 3, 2, 3, new Mat(X("f6e7c4"), Pat.Folds)).Box(22, 11, zt, 4, 1, 4, W).Box(23, 10, zt + 1, 2, 1, 2, Pl("9cc8e0")); break;
            case 1: o.Cyl(24, 10, zt, 1.5, 1.5, 6, Pl(t.acc)).Box(5, 8, zt, 7, 5, 1, Pl("9aa1ab")).Box(5, 8, zt + 1, 7, 5, 1, Pl(t.dark)); break;
            case 2: o.Box(22, 9, zt, 4, 4, 5, Iron).Box(23, 8, zt + 1, 2, 1, 3, Fire).Box(5, 8, zt, 7, 5, 1, Pl(t.acc)); break;
            case 3: o.Cone(25, 10, zt, 2.5, 1, 7, new Mat(X("e07b39"), Pat.Lit, X("ffb060"))).Box(6, 9, zt, 6, 4, 1, Pl(t.fab2)); break;
            default: o.Box(22, 9, zt, 4, 4, 5, Pl(t.light)).Box(23, 8, zt + 1, 2, 1, 3, Fire).Ell(8, 10, zt + 0.5, 2, 1.5, 1, Pl("f0c8b8")); break;
        }
    }

    // ================================================================ KITCHEN
    static Mat CabMat(Th t) { return t.i == 2 || t.i == 4 ? new Mat(t.cab, Pat.Wood) : Pl(t.cab); }
    static Mat TopMat(Th t)
    {
        switch (t.i)
        {
            case 0: return new Mat(t.top, Pat.Wood);
            case 2: return new Mat(t.top, Pat.Stone, Mul(t.top, 0.8));
            default: return Pl(t.top);
        }
    }
    static Mat Handle(Th t) { return t.i == 0 ? Brass : (t.i == 2 ? Iron : (t.i == 4 ? Pl(t.acc) : Chrome)); }

    static void CounterBody(Obj o, Th t)
    {
        Mat cab = CabMat(t), top = TopMat(t), dk = Pl(Mul(t.cab, 0.45)), g = Groove(t.cab), h = Handle(t);
        o.Box(0, 3, 1, 32, 12, 12, cab).Box(1, 4, 0, 30, 11, 1, dk).Box(0, 2, 13, 32, 14, 2, top).Box(0, 15, 15, 32, 1, 3, top)
         .Box(16, 3, 1, 1, 1, 8, g).Box(0, 3, 9, 32, 1, 1, g).Box(0, 3, 1, 1, 1, 12, g).Box(31, 3, 1, 1, 1, 12, g)
         .Box(13, 2, 5, 1, 1, 3, h).Box(19, 2, 5, 1, 1, 3, h).Box(6, 2, 11, 3, 1, 1, h).Box(23, 2, 11, 3, 1, 1, h);
        o.Top(15);
    }

    static void Counter(Th t)
    {
        Obj o = New(t, "KitchenCounter", "counter",
            new[] { "Cream Cabinets, Butcher Block", "Charcoal Cabinets, Quartz", "Oak Cabinets, Stone Top", "Mint Cabinets, Formica", "Sky Cabinets, White Top" },
            new[] { 220, 260, 240, 220, 230 },
            new[] { "Line them up along the wall.", "Line them up along the wall.", "Line them up along the wall.", "Line them up along the wall.", "Line them up along the wall." }, 2, 1);
        CounterBody(o, t);
    }

    static void Sink(Th t)
    {
        Obj o = New(t, "Sink", "sink",
            new[] { "Farmhouse Apron Sink", "Steel Sink", "Copper Basin Sink", "Mint Enamel Sink", "White Seaside Sink" },
            new[] { 300, 320, 360, 280, 300 },
            new[] { "A deep white sink with a brass tap.", "Brushed steel and a tall chrome tap.", "Hammered copper in a stone top.", "White enamel in a mint cabinet.", "Matches the sky-blue cabinets." }, 2, 1);
        CounterBody(o, t);
        Mat bas = t.i == 1 ? Met("b8bec8") : (t.i == 2 ? Met("b87333") : Porc);
        Mat tap = t.i == 0 || t.i == 2 ? Brass : Chrome;
        o.Cut(8, 4, 11, 16, 9, 4).Box(8, 4, 10, 16, 9, 1, bas).Box(8, 13, 11, 16, 1, 4, bas);
        if (t.i == 0) o.Box(8, 2, 4, 16, 1, 11, Porc);
        o.Box(15, 13, 15, 2, 2, 5, tap).Box(15, 10, 19, 2, 3, 1, tap).Box(12, 14, 15, 1, 1, 1, tap).Box(19, 14, 15, 1, 1, 1, tap);
    }

    static void Oven(Th t)
    {
        Obj o = New(t, "Oven", "oven",
            new[] { "Cream Range Cooker", "Steel Induction Range", "Wood Cookstove", "Turquoise Retro Range", "White Gas Range" },
            new[] { 600, 750, 650, 550, 600 },
            new[] { "Two ovens, two warm hotplate lids.", "A black glass top that cooks with magnets.", "Feed it logs and it bakes all day.", "Chrome trim and a clock on the back.", "Black grates, navy knobs, brass handle." }, 2, 1);
        Mat burn = Pl("4a4a50");
        switch (t.i)
        {
            case 0:
            {
                Mat body = Pl("efe2c4"), door = Pl("e2d3b0");
                o.Box(0, 3, 0, 32, 12, 15, body).Box(0, 3, 15, 32, 12, 1, Pl("3a3d44")).Cyl(9, 8, 16, 4.5, 3.5, 2, Chrome).Cyl(23, 8, 16, 4.5, 3.5, 2, Chrome)
                 .Box(3, 2, 2, 12, 1, 10, door).Box(17, 2, 2, 12, 1, 10, door).Box(5, 1, 10, 8, 1, 1, Brass).Box(19, 1, 10, 8, 1, 1, Brass).Box(0, 1, 13, 32, 1, 1, Brass);
                break;
            }
            case 1:
            {
                Mat body = Met("c2c7cf");
                o.Box(0, 3, 0, 32, 12, 15, body).Box(0, 3, 15, 32, 12, 1, Pl("1b1c20")).Box(0, 14, 16, 32, 1, 2, body)
                 .Cyl(8, 7, 16, 3.5, 2.5, 1, Pl("34363d")).Cyl(24, 7, 16, 3.5, 2.5, 1, Pl("34363d")).Cyl(8, 11.5, 16, 3, 2, 1, Pl("34363d")).Cyl(24, 11.5, 16, 3, 2, 1, Pl("34363d"))
                 .Box(4, 2, 3, 24, 1, 7, Scr).Box(3, 1, 11, 26, 1, 1, Chrome);
                for (int x = 5; x < 28; x += 6) o.Box(x, 2, 13, 1, 1, 1, Black);
                break;
            }
            case 2:
            {
                Mat lite = Met("55555c");
                o.Legs(1, 4, 30, 10, 3, 2, Iron).Box(0, 3, 3, 32, 12, 12, Iron).Box(0, 3, 15, 32, 12, 1, Met("2e2e33"))
                 .Box(3, 2, 6, 9, 1, 7, lite).Box(5, 1, 8, 5, 1, 1, Fire).Box(15, 2, 5, 14, 1, 8, lite).Box(17, 1, 11, 10, 1, 1, Brass)
                 .Cyl(8, 8, 16, 3, 3, 1, burn).Cyl(17, 8, 16, 3, 3, 1, burn).Cyl(27, 12.5, 16, 2, 2, 14, Iron);
                break;
            }
            case 3:
            {
                Mat body = Pl("5fb8ae");
                o.Box(0, 3, 0, 32, 12, 15, body).Box(0, 3, 15, 32, 12, 1, Pl(t.light)).Box(0, 2, 14, 32, 1, 1, Chrome).Box(0, 14, 16, 32, 2, 7, body).Box(14, 13, 19, 4, 1, 3, Pl(t.light))
                 .Cyl(8, 7, 16, 3, 2.5, 1, Black).Cyl(24, 7, 16, 3, 2.5, 1, Black).Cyl(8, 11.5, 16, 2.5, 1.5, 1, Black).Cyl(24, 11.5, 16, 2.5, 1.5, 1, Black)
                 .Box(3, 2, 2, 26, 1, 10, Pl("6cc6bb")).Box(9, 2, 5, 14, 1, 4, Scr).Box(5, 1, 10, 22, 1, 1, Chrome);
                foreach (int x in new[] { 4, 9, 22, 27 }) o.Box(x, 13, 17, 1, 1, 1, Chrome);
                break;
            }
            default:
            {
                Mat body = Pl(t.light);
                o.Box(0, 3, 0, 32, 12, 15, body).Box(0, 3, 15, 32, 12, 1, Black).Box(0, 14, 16, 32, 2, 4, body)
                 .Cyl(8, 7, 16, 3, 2.5, 1, burn).Cyl(24, 7, 16, 3, 2.5, 1, burn).Cyl(8, 11.5, 16, 2.5, 1.5, 1, burn).Cyl(24, 11.5, 16, 2.5, 1.5, 1, burn)
                 .Box(3, 2, 2, 26, 1, 10, Pl("eceae4")).Box(8, 2, 4, 16, 1, 5, Scr).Box(5, 1, 10, 22, 1, 1, Brass);
                foreach (int x in new[] { 4, 9, 22, 27 }) o.Box(x, 13, 17, 1, 1, 1, Pl(t.acc));
                break;
            }
        }
    }

    static void Fridge(Th t)
    {
        Obj o = New(t, "Fridge", "fridge",
            new[] { "Cream Fridge", "Steel French-Door Fridge", "Wood-Panel Fridge", "Mint Retro Fridge", "Sky Blue Fridge" },
            new[] { 500, 800, 560, 520, 520 },
            new[] { "Covered in notes and magnets.", "Two doors, a drawer and an ice maker.", "Hidden behind oak panels.", "Rounded top and a big chrome lever.", "A child's drawing stuck on the door." }, 1, 1);
        switch (t.i)
        {
            case 0:
            {
                Mat b = Pl("efe2c4");
                o.Box(1, 3, 0, 14, 12, 30, b, 1).Box(1, 3, 21, 14, 1, 1, Groove(b.c)).Box(12, 2, 13, 1, 1, 6, Chrome).Box(12, 2, 23, 1, 1, 4, Chrome)
                 .Box(3, 2, 15, 3, 1, 3, Pl("fbfbf8")).Box(4, 2, 18, 1, 1, 1, Pl(t.acc)).Box(7, 2, 11, 1, 1, 1, Pl("6d8fb0"));
                break;
            }
            case 1:
            {
                Mat b = Met("c2c7cf"); Mat g = Pl("8d939c");
                o.Box(1, 3, 0, 14, 12, 30, b).Box(8, 3, 8, 1, 1, 22, g).Box(1, 3, 8, 14, 1, 1, g).Box(6, 2, 12, 1, 1, 10, Black).Box(9, 2, 12, 1, 1, 10, Black)
                 .Box(4, 2, 4, 8, 1, 1, Black).Box(2, 2, 15, 3, 1, 5, Pl("2b2d33"));
                break;
            }
            case 2: o.Box(1, 3, 0, 14, 12, 30, Wd(t)).Box(1, 3, 21, 14, 1, 1, Groove(t.wood)).Box(12, 2, 13, 1, 1, 6, Iron).Box(12, 2, 23, 1, 1, 4, Iron); break;
            case 3: { Mat b = Pl("9fd8c8"); o.Box(1, 3, 0, 14, 12, 24, b, 2).Ell(8, 9, 24, 7, 6, 5, b).Box(11, 2, 14, 2, 1, 3, Chrome).Box(5, 2, 25, 6, 1, 1, Chrome); break; }
            default:
            {
                Mat b = Pl(t.cab);
                o.Box(1, 3, 0, 14, 12, 30, b, 1).Box(1, 3, 21, 14, 1, 1, Groove(b.c)).Box(12, 2, 13, 1, 1, 6, Pl(t.acc)).Box(12, 2, 23, 1, 1, 4, Pl(t.acc))
                 .Box(3, 2, 12, 4, 1, 4, Pl("fbfbf8")).Box(4, 2, 13, 2, 1, 1, Pl("e8a030")).Box(4, 2, 15, 1, 1, 1, Pl("d04040"));
                break;
            }
        }
    }

    static Mat ApplianceBody(Th t)
    {
        switch (t.i) { case 0: return Pl("f1ead8"); case 1: return Pl("4a4e57"); case 2: return Pl("e3d6b8"); case 3: return Pl("a8b45a"); default: return Pl("f6f7f6"); }
    }

    static void Washer(Th t)
    {
        Obj o = New(t, "Washer", "washer",
            new[] { "Cream Washer", "Graphite Washer", "Almond Top-Loader", "Avocado Top-Loader", "White and Blue Washer" },
            new[] { 400, 480, 350, 360, 420 },
            new[] { "Round window, clothes going round.", "Quiet, dark grey and very fast.", "Lift the lid, drop the clothes in.", "That 1970s green, with chrome dials.", "Sea-blue ring around the door." }, 1, 1);
        Mat b = ApplianceBody(t);
        if (t.i == 2 || t.i == 3)
        {
            o.Box(1, 3, 0, 14, 12, 16, b, 1).Box(2, 4, 16, 12, 8, 1, Pl(Mul(b.c, 1.06))).Box(1, 12, 16, 14, 3, 4, b).Box(3, 11, 18, 2, 1, 2, Chrome).Box(11, 11, 18, 2, 1, 2, Chrome)
             .Box(6, 11, 18, 4, 1, 1, Pl(t.dark)).Box(3, 2, 12, 10, 1, 1, Chrome);
        }
        else
        {
            Mat ring = t.i == 4 ? Pl(t.fab) : (t.i == 1 ? Pl("6d737d") : Chrome);
            o.Box(1, 3, 0, 14, 12, 16, b, 1).Box(1, 13, 16, 14, 2, 3, b).Box(10, 12, 17, 2, 1, 1, Chrome).Box(3, 12, 17, 4, 1, 1, new Mat(X("5ac87a"), Pat.Lit, X("9af0b0")))
             .Box(2, 2, 13, 4, 1, 2, Pl(Mul(b.c, 0.85))).Ell(8, 2.5, 7.5, 5, 0.9, 5, ring).Ell(8, 2.5, 7.5, 3.8, 0.9, 3.8, new Mat(X("6fa0c8"), Pat.Water, X("4a7aa8"), X("e8f0f8")));
        }
    }

    static void Dryer(Th t)
    {
        Obj o = New(t, "Dryer", "dryer",
            new[] { "Cream Dryer", "Graphite Dryer", "Almond Dryer", "Avocado Dryer", "White and Blue Dryer" },
            new[] { 380, 460, 330, 340, 400 },
            new[] { "Warm towels in twenty minutes.", "Matches the graphite washer.", "Matches the almond washer.", "Matches the avocado washer.", "Matches the white and blue washer." }, 1, 1);
        Mat b = ApplianceBody(t), dk = Pl(Mul(b.c, 0.82));
        o.Box(1, 3, 0, 14, 12, 16, b, 1).Box(1, 12, 16, 14, 3, 3, b).Box(4, 11, 17, 2, 1, 1, Chrome).Box(10, 11, 17, 2, 1, 1, Chrome)
         .Box(3, 2, 3, 10, 1, 9, dk).Box(5, 2, 5, 6, 1, 5, Pl("3a4250")).Box(12, 1, 7, 1, 1, 3, t.i == 4 ? Pl(t.fab) : Chrome);
        if (t.i == 2 || t.i == 3) o.Box(1, 12, 16, 14, 3, 5, b).Box(3, 11, 18, 2, 1, 2, Chrome).Box(11, 11, 18, 2, 1, 2, Chrome);
    }

    static void Blender(Th t)
    {
        Obj o = New(t, "Blender", "blender",
            new[] { "Strawberry Blender", "Green Smoothie Blender", "Copper Blender", "Orange Juice Blender", "Blueberry Blender" },
            new[] { 60, 80, 90, 65, 70 },
            new[] { "Half full of strawberry smoothie.", "Spinach, apple and something green.", "A shiny copper base.", "Fresh orange juice, pulp and all.", "Blueberry and yoghurt." }, 1, 1);
        o.mount = "Surface";
        Color[] baseC = { X("efe2c4"), X("2b2d33"), X("b87333"), X("9fd8c8"), X("8fbcd6") };
        Color[] drink = { X("e86a7a"), X("7ac050"), X("8a3a6a"), X("f0a030"), X("5a6ac8") };
        o.Box(5, 5, 0, 6, 6, 4, t.i == 2 ? Met(baseC[2]) : Pl(baseC[t.i]), 1).Box(7, 4, 1, 2, 1, 1, Chrome)
         .Box(6, 6, 4, 4, 4, 4, Pl(drink[t.i])).Box(6, 6, 8, 4, 4, 3, GlassM).Box(6, 6, 11, 4, 4, 1, Black).Box(10, 7, 6, 1, 2, 4, Pl("8aa4b0"));
    }

    static void SmallAppliances(Th[] T)
    {
        Obj k = NewAny("SmallAppliance", "kettle_cottage", "cottage", "Enamel Kettle", 1, 1, 45, "A rose enamel kettle for the stove top.");
        k.mount = "Surface";
        k.Cyl(8, 8, 0, 3.5, 3, 6, Pl(T[0].acc)).Cone(8, 8, 6, 3, 1.5, 2, Pl(T[0].acc)).Box(8, 8, 8, 1, 1, 1, Pl(T[0].dark)).Box(3, 8, 3, 2, 1, 1, Pl(T[0].acc)).Box(2, 8, 4, 1, 1, 1, Pl(T[0].acc))
         .Box(11, 8, 2, 2, 1, 1, Pl(T[0].dark)).Box(12, 8, 3, 1, 1, 3, Pl(T[0].dark)).Box(11, 8, 6, 1, 1, 1, Pl(T[0].dark));

        Obj c = NewAny("SmallAppliance", "coffeemaker_modern", "modern", "Coffee Machine", 1, 1, 120, "Fresh coffee, one cup at a time.");
        c.mount = "Surface";
        c.Box(5, 7, 0, 7, 6, 10, Black).Cut(5, 7, 1, 7, 3, 5).Cyl(8.5, 8.5, 1, 2, 1.5, 4, new Mat(X("6a4a32"), Pat.Glass, X("4a3020"), X("a08060"))).Box(6, 6, 8, 2, 1, 1, LitBlue);

        Obj b = NewAny("SmallAppliance", "breadbox_rustic", "rustic", "Bread Box", 1, 1, 40, "Keeps the loaf fresh.");
        b.mount = "Surface";
        b.Box(3, 5, 0, 10, 7, 6, Wd(T[2]), 1).Box(3, 4, 3, 10, 1, 1, WdD(T[2])).Box(7, 4, 4, 2, 1, 1, Brass);

        Obj ts = NewAny("SmallAppliance", "toaster_retro", "retro", "Chrome Toaster", 1, 1, 50, "Two slices, golden brown.");
        ts.mount = "Surface";
        ts.Box(4, 6, 0, 8, 5, 6, Chrome, 1).Box(5, 7, 5, 6, 1, 1, Black).Box(5, 9, 5, 6, 1, 1, Black).Box(6, 7, 6, 4, 1, 1, Pl("d8a050")).Box(12, 7, 2, 1, 1, 2, Black);

        Obj m = NewAny("SmallAppliance", "mixer_coastal", "coastal", "Stand Mixer", 1, 1, 150, "For cakes, cookies and bread dough.");
        m.mount = "Surface";
        m.Box(4, 5, 0, 8, 7, 1, Pl(T[4].fab)).Box(9, 8, 1, 3, 3, 9, Pl(T[4].fab)).Ell(8, 8, 11, 4.5, 2.5, 2.2, Pl(T[4].fab)).Cyl(7, 7.5, 1, 2.6, 2.6, 4, Chrome).Box(6, 7, 5, 1, 1, 3, Chrome);

        Obj mw = NewAny("SmallAppliance", "microwave_modern", "modern", "Microwave", 1, 1, 110, "Leftovers in two minutes.");
        mw.mount = "Surface";
        mw.Box(1, 4, 0, 14, 10, 9, Black).Box(2, 3, 1, 9, 1, 7, Scr).Box(12, 3, 1, 2, 1, 7, Met("c2c7cf")).Box(12, 3, 2, 1, 1, 1, LitBlue);
    }

    static void KitchenTable(Th t)
    {
        Obj o = New(t, "KitchenTable", "kitchentable",
            new[] { "Farmhouse Table", "White Oval Table", "Trestle Table", "Diner Table", "Round Pedestal Table" },
            new[] { 320, 380, 340, 300, 330 },
            new[] { "Painted legs, oak top and a fruit bowl.", "One black stem, a vase of branches.", "Thick planks and candles.", "Red top, chrome rim, ketchup and mustard.", "A blue vase of white hydrangeas." }, 2, 2);
        switch (t.i)
        {
            case 0:
                o.Legs(2, 4, 28, 24, 12, 2, Pl(t.cab)).Box(2, 4, 9, 28, 24, 3, Pl(t.cab)).Box(1, 3, 12, 30, 26, 2, Wd(t)).Cyl(16, 16, 14, 4, 3.5, 1, Porc)
                 .Ell(15, 16, 16, 1.6, 1.6, 1.6, Pl("c8323a")).Ell(17.5, 15.5, 16, 1.6, 1.6, 1.6, Pl("e98a2a")).Ell(16, 17.5, 16.5, 1.6, 1.6, 1.6, Pl("8cc850")).Top(14);
                break;
            case 1: o.Cyl(16, 16, 0, 6, 4.5, 1, Pl(t.dark)).Cyl(16, 16, 1, 1.5, 1.5, 11, Pl(t.dark)).Cyl(16, 16, 12, 14, 11, 1, Pl(t.light)).Cyl(16, 16, 13, 1.5, 1.5, 5, GlassM).Ell(16, 16, 19, 3, 2.5, 2, Leaves, 0.4).Top(13); break;
            case 2:
                o.Box(4, 8, 2, 3, 16, 10, WdD(t)).Box(25, 8, 2, 3, 16, 10, WdD(t)).Box(3, 6, 0, 5, 20, 2, WdD(t)).Box(24, 6, 0, 5, 20, 2, WdD(t)).Box(7, 15, 6, 18, 2, 2, WdD(t))
                 .Box(1, 3, 12, 30, 9, 3, Wd(t)).Box(1, 12, 12, 30, 9, 3, Wd(t)).Box(1, 21, 12, 30, 8, 3, Wd(t))
                 .Box(12, 15, 15, 1, 1, 4, Pl(t.light)).Box(18, 15, 15, 1, 1, 3, Pl(t.light)).Box(12, 15, 19, 1, 1, 1, Fire).Box(18, 15, 18, 1, 1, 1, Fire).Top(15);
                break;
            case 3:
                o.Cyl(16, 16, 0, 6, 6, 1, Chrome).Cyl(16, 16, 1, 1.5, 1.5, 10, Chrome).Cyl(16, 16, 11, 13.5, 13.5, 1, Chrome).Cyl(16, 16, 12, 13, 13, 1, Pl("c84a3a"))
                 .Box(15, 17, 13, 3, 1, 3, Chrome).Cyl(19.5, 16.5, 13, 1, 1, 3, Pl("c8323a")).Cyl(12.5, 16.5, 13, 1, 1, 3, Pl("e2b23a")).Top(13);
                break;
            default: o.Cyl(16, 16, 0, 6, 5, 2, Wd(t)).Cyl(16, 16, 2, 2.5, 2.5, 10, Wd(t)).Cyl(16, 16, 12, 13, 12, 2, Wd(t)).Cyl(16, 16, 14, 2, 2, 4, new Mat(t.fab, Pat.Glass)).Ell(16, 16, 19.5, 3, 2.5, 2, Pl("fbfaf6"), 0.3).Top(14); break;
        }
    }

    // ================================================================ BATHROOM (not themed: they suit every room)
    static void Bathroom()
    {
        Obj toilet = NewAny("Bathroom", "toilet", "any", "Toilet", 1, 1, 200, "Every house needs one.");
        toilet.Cyl(8, 7.5, 0, 3, 3.5, 5, Porc).Cyl(8, 6.5, 5, 4, 4.5, 3, Porc).Cyl(8, 6.5, 6, 2.5, 3, 1, Water).CutCyl(8, 6.5, 7, 2.5, 3, 1)
              .Cyl(8, 6.5, 8, 4, 4.5, 1, Pl("ffffff")).CutCyl(8, 6.5, 8, 2.5, 3, 1).Box(3, 11, 5, 10, 4, 10, Porc, 1).Box(4, 11, 15, 8, 4, 1, Pl("fbfbf8")).Box(11, 10, 12, 2, 1, 1, Chrome);

        Obj claw = NewAny("Bathroom", "clawfoot_tub", "any", "Clawfoot Tub", 3, 1, 700, "Navy outside, white inside, brass feet.");
        claw.Box(3, 2, 0, 2, 2, 3, Brass).Box(43, 2, 0, 2, 2, 3, Brass).Box(3, 12, 0, 2, 2, 3, Brass).Box(43, 12, 0, 2, 2, 3, Brass)
            .Box(1, 1, 3, 46, 14, 8, Pl("3c5a7a"), 6).Box(1, 1, 11, 46, 14, 1, Porc, 6).Cut(3, 3, 6, 42, 10, 6, 4).Box(4, 4, 8, 40, 8, 1, Water, 3)
            .Box(42, 7, 12, 2, 2, 3, Brass).Box(40, 7, 14, 3, 2, 1, Brass);

        Obj tub = NewAny("Bathroom", "builtin_tub", "any", "Tiled Bathtub", 3, 1, 600, "Built in and tiled all round.");
        tub.Box(0, 2, 0, 48, 13, 10, new Mat(X("eef0f2"), Pat.Tile, X("c9d0d4"))).Box(0, 2, 10, 48, 13, 1, Pl("f6f7f8")).Cut(3, 4, 4, 42, 9, 7, 3).Box(4, 5, 7, 40, 7, 1, Water, 2)
           .Box(45, 8, 11, 2, 2, 3, Chrome).Box(43, 8, 13, 2, 2, 1, Chrome);

        Obj shower = NewAny("Bathroom", "shower", "any", "Shower with Curtain", 2, 2, 650, "Tiled walls and a striped curtain.");
        Mat sub = new Mat(X("f2f4f2"), Pat.Tile, X("c9d0d0"));
        shower.Box(1, 1, 0, 30, 30, 1, Pl("e8ecee")).Box(1, 28, 0, 30, 3, 30, sub).Box(1, 1, 0, 2, 27, 30, sub).Cyl(16, 16, 1, 1, 1, 1, Pl("8d939c"))
              .Box(15, 27, 22, 2, 1, 2, Chrome).Box(15, 25, 21, 2, 2, 1, Chrome).Box(3, 1, 26, 28, 1, 1, Chrome).Box(30, 1, 0, 1, 1, 26, Chrome)
              .Box(3, 1, 2, 12, 1, 24, new Mat(X("8fbcd6"), Pat.Folds, X("f4f1ea")));

        Obj van = NewAny("Bathroom", "vanity", "any", "Vanity Sink", 2, 1, 380, "Navy cupboard, marble top.");
        van.Box(1, 4, 0, 30, 11, 13, Pl("3c5a7a")).Box(16, 4, 1, 1, 1, 11, Pl("2c4058")).Box(13, 3, 6, 1, 1, 3, Brass).Box(19, 3, 6, 1, 1, 3, Brass)
           .Box(0, 3, 13, 32, 13, 2, new Mat(X("eceae6"), Pat.Stone, X("d8d4cc"))).CutCyl(16, 9, 12, 6, 3.5, 3).Cyl(16, 9, 11, 6, 3.5, 1, Porc)
           .Box(15, 13, 15, 2, 2, 4, Chrome).Box(15, 10, 18, 2, 3, 1, Chrome).Box(24, 10, 15, 2, 2, 3, Pl("f0c8b8"));
        van.Top(15);

        Obj ped = NewAny("Bathroom", "pedestal_sink", "any", "Pedestal Sink", 1, 1, 220, "Small and classic.");
        ped.Cyl(8, 10, 0, 2, 2, 11, Porc).Cyl(8, 8, 11, 6, 5, 3, Porc).CutCyl(8, 8, 13, 4.5, 3.5, 1).Box(7, 12, 14, 2, 2, 3, Chrome).Box(7, 10, 16, 2, 2, 1, Chrome);

        Obj mat = NewAny("Bathroom", "bath_mat", "any", "Fluffy Bath Mat", 2, 1, 40, "Soft on wet feet.");
        mat.mount = "Flat";
        mat.Box(2, 3, 0, 28, 10, 1, new Mat(X("8fbcd6"), Pat.Shag), 3);
    }

    // ================================================================ DRAWING THE MODELS
    class Vox { public int W, D, H; public short[] g; public Prim[] p; public int At(int x, int y, int z) { return x < 0 || y < 0 || z < 0 || x >= W || y >= D || z >= H ? 0 : g[(z * D + y) * W + x]; } }

    static Vox Build(Obj o)
    {
        var v = new Vox { W = o.W, D = o.D, H = 1, p = o.P.ToArray() };
        foreach (Prim p in o.P) if (!p.carve) v.H = Math.Max(v.H, p.z1);
        v.g = new short[v.W * v.D * v.H];
        for (int i = 0; i < v.p.Length; i++)
        {
            Prim p = v.p[i];
            for (int z = Math.Max(0, p.z0); z < Math.Min(v.H, p.z1); z++)
                for (int y = Math.Max(0, p.y0); y < Math.Min(v.D, p.y1); y++)
                    for (int x = Math.Max(0, p.x0); x < Math.Min(v.W, p.x1); x++)
                        if (p.In(x, y, z)) v.g[(z * v.D + y) * v.W + x] = p.carve ? (short)0 : (short)(i + 1);
        }
        return v;
    }

    // Where a point of the turned picture comes from in the model. f: 0 front, 1 right, 2 back, 3 left.
    static void Inv(int f, int X, int Y, int W, int D, out int lx, out int ly)
    {
        switch (f)
        {
            case 0: lx = X; ly = Y; break;
            case 1: lx = Y; ly = D - 1 - X; break;
            case 2: lx = W - 1 - X; ly = D - 1 - Y; break;
            default: lx = W - 1 - Y; ly = X; break;
        }
    }

    static Color Shade(Mat m, bool top, bool rim, int x, int y, int z, Vox v)
    {
        Color c = m.c; double k = 1.0;
        switch (m.p)
        {
            case Pat.Glow: { double r = Rnd(x, y, z, 5); return r < 0.4 ? m.c : (r < 0.8 ? m.c2 : m.c3); }
            case Pat.Lit: return (Mod(x - z, 7) == 0 || Mod(x - z - y, 9) == 0) ? m.c2 : m.c;
            case Pat.Wood:
                if (top) { if (Mod(y + (int)(Rnd(x / 6, 0, 0, 3) * 4), 5) == 0) k *= 0.9; }
                else { if (Mod(z + (int)(Rnd(x / 8, 0, 0, 5) * 3), 5) == 0) k *= 0.93; }
                k *= 0.97 + 0.06 * Rnd(x / 3, y, z, 1);
                break;
            case Pat.Fabric: if (((x + y + z) & 1) == 0) k *= 0.965; break;
            case Pat.Folds: if (Mod(x, 3) == 0) k *= 0.82; else if (Mod(x, 3) == 1) k *= 1.04; if (m.c2 != Mul(m.c, 0.75) && Mod(top ? y : z, 6) < 3) c = m.c2; break;
            case Pat.Leather: k *= 0.93 + 0.12 * Rnd(x / 2, y / 2, z / 2, 7); break;
            case Pat.Tufted:
                if (((x + y + z) & 1) == 0) k *= 0.97;
                if (top ? (Mod(x, 6) == 3 && Mod(y, 5) == 2) : (Mod(x, 6) == 3 && Mod(z, 5) == 2)) k *= 0.7;
                break;
            case Pat.Metal: if (!top) k *= 0.9 + 0.2 * (Mod(z, 8) / 8.0); else if (Mod(x + y, 7) == 0) k *= 1.15; break;
            case Pat.Glass: if (Mod(x - (top ? y : z), 6) == 0) return m.c3; break;
            case Pat.Screen: if (Mod(x + z, 8) <= 1) c = m.c2; break;
            case Pat.Brick:
                if (!top) { int row = z / 3; bool mort = Mod(z, 3) == 2 || Mod(x + (row & 1) * 3, 6) == 0; if (mort) c = m.c2; else k *= 0.88 + 0.22 * Rnd((x + (row & 1) * 3) / 6, row, 0, 9); }
                break;
            case Pat.Stone:
            {
                int row = top ? y / 4 : z / 4, sh = (row & 1) * 3;
                bool mort = top ? (Mod(y, 4) == 3 || Mod(x + sh, 5) == 0) : (Mod(z, 4) == 3 || Mod(x + sh, 5) == 0);
                if (mort) c = m.c2; else k *= 0.82 + 0.3 * Rnd((x + sh) / 5, row, top ? 1 : 0, 4);
                break;
            }
            case Pat.Rattan: { int q = Mod(x + (top ? y : z), 3); k *= q == 0 ? 0.8 : (q == 1 ? 1.06 : 0.95); break; }
            case Pat.Plaid: { bool a = Mod(x, 8) < 2, b = Mod(top ? y : z, 8) < 2; if (a && b) c = m.c2; else if (a || b) k *= 0.78; break; }
            case Pat.Stripes: if (Mod(top ? y : z, 6) < 3) c = m.c2; break;
            case Pat.Leaves: { k *= 0.78 + 0.44 * Rnd(x / 2, y / 2, z / 2, 11); double r = Rnd(x, y, z, 12); if (r > 0.9) c = m.c3; else if (r < 0.12) c = m.c2; break; }
            case Pat.Rings:
            {
                double dx = (x + 0.5 - v.W / 2.0) / (v.W / 2.0), dy = (y + 0.5 - v.D / 2.0) / (v.D / 2.0);
                int ring = (int)(Math.Sqrt(dx * dx + dy * dy) * 8);
                c = ring % 3 == 0 ? m.c : (ring % 3 == 1 ? m.c2 : m.c3); if (((x + y) & 1) == 0) k *= 0.94;
                break;
            }
            case Pat.Shag: k *= 0.8 + 0.35 * Rnd(x, y, z, 13); break;
            case Pat.Checker: if ((x / 4 + (top ? y / 4 : z / 4)) % 2 == 0) c = m.c2; break;
            case Pat.Diag: if (Mod(x + y, 8) == 0 || Mod(x - y, 8) == 0) c = m.c2; break;
            case Pat.Water: k *= 0.95 + 0.1 * Rnd(x, y, z, 21); if (Rnd(x, y, z, 22) > 0.9) c = m.c3; break;
            case Pat.Tile: if (Mod(x, 4) == 0 || Mod(top ? y : z, 4) == 0) c = m.c2; break;
        }
        if (top) k *= 1.1; else { k *= 0.85; if (z <= 1) k *= 0.88; }
        if (rim) k *= 1.1;
        return Mul(c, k);
    }

    // Draws a model from one side. The pivot (the corner of the floor space it takes) is 2 pixels in and 3 pixels up from the bottom-left.
    public const int PadL = 2, PadR = 3, PadT = 2, PadB = 3;
    static Bitmap Render(Obj o, Vox v, int f, bool shadow)
    {
        bool side = f == 1 || f == 3;
        int WW = side ? v.D : v.W, DD = side ? v.W : v.D, H = v.H;
        int cw = WW + PadL + PadR, ch = DD + H + 1 + PadT + PadB;
        var col = new Color[cw * ch]; var key = new int[cw * ch]; var pid = new int[cw * ch]; var has = new bool[cw * ch];
        for (int i = 0; i < key.Length; i++) key[i] = int.MaxValue;

        Func<int, int, int, int> A = (X, Y, z) =>
        {
            if (X < 0 || Y < 0 || X >= WW || Y >= DD || z < 0 || z >= H) return 0;
            int lx, ly; Inv(f, X, Y, v.W, v.D, out lx, out ly); return v.At(lx, ly, z);
        };
        Action<int, int, int, Color, int> plot = (sx, sy, k, c, id) =>
        {
            int px = PadL + sx, py = ch - 1 - (PadB + sy);
            if (px < 0 || py < 0 || px >= cw || py >= ch) return;
            int i = py * cw + px;
            if (k < key[i]) { key[i] = k; col[i] = c; pid[i] = id; has[i] = true; }
        };

        for (int z = 0; z < H; z++)
            for (int Y = 0; Y < DD; Y++)
                for (int X = 0; X < WW; X++)
                {
                    int id = A(X, Y, z); if (id == 0) continue;
                    Prim p = v.p[id - 1]; int lx, ly; Inv(f, X, Y, v.W, v.D, out lx, out ly);
                    bool topOpen = A(X, Y, z + 1) == 0, frontOpen = A(X, Y - 1, z) == 0;
                    int k = 2 * (Y - z);
                    if (topOpen) plot(X, Y + z + 1, k, Shade(p.m, true, frontOpen, lx, ly, z, v), id);
                    if (frontOpen) plot(X, Y + z, k, Shade(p.m, false, false, lx, ly, z, v), id);
                }

        // lines where one part sits in front of another (so cushions, doors and drawers read as separate pieces)
        var dark = new double[cw * ch];
        for (int i = 0; i < dark.Length; i++) dark[i] = 1;
        int[] ox = { 1, -1, 0, 0 }, oy = { 0, 0, 1, -1 };
        for (int py = 0; py < ch; py++) for (int px = 0; px < cw; px++)
        {
            int i = py * cw + px; if (!has[i]) continue;
            for (int n = 0; n < 4; n++)
            {
                int qx = px + ox[n], qy = py + oy[n]; if (qx < 0 || qy < 0 || qx >= cw || qy >= ch) continue;
                int j = qy * cw + qx; if (!has[j]) continue;
                int dk = key[i] - key[j]; bool diff = pid[i] != pid[j];
                if (dk > 3 || (diff && dk > 0)) dark[i] = Math.Min(dark[i], 0.74);
                else if (diff && dk == 0 && (n == 0 || n == 2) && v.p[pid[i] - 1].m == v.p[pid[j] - 1].m) dark[i] = Math.Min(dark[i], 0.84);
            }
        }
        for (int i = 0; i < col.Length; i++) if (has[i] && dark[i] < 1) col[i] = Mul(col[i], dark[i]);

        // outline around the whole shape
        var outline = new bool[cw * ch];
        for (int py = 0; py < ch; py++) for (int px = 0; px < cw; px++)
        {
            int i = py * cw + px; if (has[i]) continue;
            for (int n = 0; n < 4; n++)
            {
                int qx = px + ox[n], qy = py + oy[n]; if (qx < 0 || qy < 0 || qx >= cw || qy >= ch) continue;
                int j = qy * cw + qx; if (has[j]) { col[i] = Ink(col[j]); outline[i] = true; break; }
            }
        }
        for (int i = 0; i < col.Length; i++) if (outline[i]) has[i] = true;

        // a soft shadow on the floor under and just below-right of the piece
        if (shadow)
        {
            Func<int, int, bool> S = (X, Y) =>
            {
                if (X < 0 || Y < 0 || X >= WW || Y >= DD) return false;
                for (int z = 0; z < H; z++) if (A(X, Y, z) != 0) return true;
                return false;
            };
            for (int sy = -2; sy < DD; sy++) for (int sx = 0; sx < WW + 2; sx++)
            {
                int px = PadL + sx, py = ch - 1 - (PadB + sy); if (px >= cw || py >= ch || py < 0) continue;
                int i = py * cw + px; if (has[i]) continue;
                int a = S(sx, sy) ? 78 : ((S(sx - 1, sy) || S(sx, sy + 1) || S(sx - 1, sy + 1) || S(sx, sy + 2)) ? 46 : 0);
                if (a > 0) { col[i] = Color.FromArgb(a, 30, 18, 24); has[i] = true; }
            }
        }

        var bmp = new Bitmap(cw, ch, PixelFormat.Format32bppArgb);
        for (int py = 0; py < ch; py++) for (int px = 0; px < cw; px++) { int i = py * cw + px; bmp.SetPixel(px, py, has[i] ? col[i] : Color.Transparent); }
        return bmp;
    }

    // ================================================================ FLAT THINGS ON THE BACK WALL (front only)
    static void Save(Bitmap b, string folder, string name)
    {
        string dir = Path.Combine(outDir, folder); Directory.CreateDirectory(dir);
        b.Save(Path.Combine(dir, name + ".png"), ImageFormat.Png);
    }

    static void WallPiece(string cat, string id, string theme, string name, int wTiles, int hTiles, int price, string desc, Cv cv)
    {
        man.AppendLine(string.Join("\t", new[] { "piece", id, name, cat, theme, "Wall", wTiles.ToString(), hTiles.ToString(), price.ToString(), "0", "0", "0", "0", desc }));
        Bitmap b = cv.Bmp(); Save(b, cat, id + "_front");
        previews.Add(new KeyValuePair<string, List<Bitmap>>(id, new List<Bitmap> { b }));
    }

    // A framed picture: frame colour, then 'paint' fills the inside (x0, y0, w, h).
    static Cv Framed(int wTiles, int hTiles, int artW, int artH, Color frame, Color mat, Action<Cv, int, int, int, int> paint)
    {
        var cv = new Cv(wTiles * 16, hTiles * 16);
        int x = (cv.w - artW) / 2, y = (cv.h - artH) / 2;
        cv.R(x, y, artW, artH, frame);
        cv.R(x, y, artW, 1, Mul(frame, 1.25)); cv.R(x, y, 1, artH, Mul(frame, 1.15)); cv.R(x, y + artH - 1, artW, 1, Mul(frame, 0.7)); cv.R(x + artW - 1, y, 1, artH, Mul(frame, 0.8));
        int ix = x + 2, iy = y + 2, iw = artW - 4, ih = artH - 4;
        if (mat.A > 0) { cv.R(ix, iy, iw, ih, mat); ix++; iy++; iw -= 2; ih -= 2; }
        paint(cv, ix, iy, iw, ih);
        cv.Outline(); cv.WallShadow();
        return cv;
    }

    static void Sky(Cv cv, int x, int y, int w, int h, Color top, Color bottom)
    {
        for (int j = 0; j < h; j++) { Color c = Mix(top, bottom, h <= 1 ? 0 : j / (double)(h - 1)); cv.R(x, y + j, w, 1, c); }
    }

    static void Pictures(Th[] T)
    {
        WallPiece("Picture", "picture_cottage", "cottage", "Rolling Hills Painting", 2, 1, 120, "Green hills under a summer sun.",
            Framed(2, 1, 24, 16, T[0].wood, Color.Transparent, (cv, x, y, w, h) =>
            {
                Sky(cv, x, y, w, h, X("9ccbe6"), X("d8eef6")); cv.Disc(x + w - 5, y + 3, 2, X("ffd84a"));
                for (int i = 0; i < w; i++)
                {
                    int a = (int)(h * 0.55 + Math.Sin(i * 0.5) * 1.5), b = (int)(h * 0.72 + Math.Sin(i * 0.33 + 2) * 1.5);
                    for (int j = a; j < h; j++) cv.P(x + i, y + j, X("8cc850")); for (int j = b; j < h; j++) cv.P(x + i, y + j, X("5fa040"));
                }
            }));
        WallPiece("Picture", "picture_modern", "modern", "Abstract Print", 1, 2, 140, "Shapes in mustard, teal and grey.",
            Framed(1, 2, 14, 22, T[1].dark, X("f3f3ee"), (cv, x, y, w, h) =>
            {
                cv.Disc(x + w / 2.0, y + h * 0.35, 3.5, X("e0a030")); cv.R(x + 1, y + h - 8, w - 4, 4, X("3f9a96")); cv.R(x + 2, y + h - 3, w - 3, 1, X("2a2c31"));
            }));
        WallPiece("Picture", "picture_rustic", "rustic", "Mountain Lake Painting", 2, 1, 130, "Snowy peaks, dark pines, still water.",
            Framed(2, 1, 26, 16, T[2].woodDk, Color.Transparent, (cv, x, y, w, h) =>
            {
                Sky(cv, x, y, w, h, X("8fbcd8"), X("cfe4ee"));
                for (int i = 0; i < w; i++)
                {
                    int peak = (int)(h * 0.2 + Math.Abs(((i * 1.3) % 10) - 5) * 0.9);
                    for (int j = peak; j < h; j++) cv.P(x + i, y + j, j - peak < 2 ? X("f4f6f8") : X("8d96a3"));
                }
                cv.R(x, y + h - 4, w, 4, X("5f8fb0"));
                for (int i = 1; i < w; i += 3) { int top = y + h - 9 + (i % 2); for (int j = 0; j < 5; j++) cv.R(x + i - j / 2, top + j, 1 + j, 1, X("2f5e2a")); }
            }));
        WallPiece("Picture", "picture_retro", "retro", "Atomic Starburst", 1, 1, 110, "Orange and teal, very 1958.",
            Framed(1, 1, 16, 16, T[3].fab, T[3].light, (cv, x, y, w, h) =>
            {
                double cx = x + w / 2.0, cy = y + h / 2.0;
                for (int a = 0; a < 8; a++) { double ang = a * Math.PI / 4; for (int r = 1; r < 5; r++) cv.P((int)(cx + Math.Cos(ang) * r), (int)(cy + Math.Sin(ang) * r), a % 2 == 0 ? X("e07b39") : X("3f9a96")); }
                cv.Disc(cx, cy, 1.2, X("e2b23a"));
            }));
        WallPiece("Picture", "picture_coastal", "coastal", "Sailboat Painting", 2, 1, 120, "A little boat on a blue sea.",
            Framed(2, 1, 24, 16, T[4].light, Color.Transparent, (cv, x, y, w, h) =>
            {
                Sky(cv, x, y, w, h, X("a9d4ec"), X("e4f2f8")); cv.R(x, y + h - 5, w, 5, X("3f7aa8"));
                for (int i = 0; i < w; i += 3) cv.P(x + i + (i / 3) % 2, y + h - 3, X("e8f6fc"));
                int bx = x + w / 2 - 3, by = y + h - 6;
                cv.R(bx, by, 7, 1, X("c84a3a")); cv.R(bx + 1, by + 1, 5, 1, X("a83a2a"));
                for (int j = 0; j < 6; j++) cv.R(bx + 3, by - 1 - j, Math.Max(1, (6 - j) / 2), 1, X("fbfaf6"));
                cv.R(bx + 3, by - 7, 1, 7, X("6a4a32"));
            }));
        WallPiece("Picture", "picture_photos", "any", "Family Photos", 2, 1, 90, "Three frames of happy faces.", PhotoCluster());
        WallPiece("Picture", "picture_botanical", "cottage", "Botanical Print", 1, 2, 100, "A pressed fern in a brass frame.",
            Framed(1, 2, 14, 22, T[0].metal, X("f3ead6"), (cv, x, y, w, h) =>
            {
                int mx = x + w / 2; cv.R(mx, y + 2, 1, h - 3, X("4f7a3a"));
                for (int j = 3; j < h - 2; j += 2) { int len = Math.Max(1, (int)((h - j) * 0.35)); cv.R(mx - len, y + j, len, 1, X("6fa04a")); cv.R(mx + 1, y + j + 1, len, 1, X("6fa04a")); }
            }));
    }

    static Cv PhotoCluster()
    {
        var cv = new Cv(32, 16);
        Action<int, int, int, int, Color, Color, Color> photo = (x, y, w, h, frame, bg, shirt) =>
        {
            cv.R(x, y, w, h, frame); cv.R(x + 1, y + 1, w - 2, h - 2, bg);
            cv.Disc(x + w / 2.0, y + h * 0.42, 1.6, X("e8c0a0")); cv.R(x + w / 2 - 2, y + h - 4, 4, 2, shirt);
        };
        photo(2, 4, 8, 10, X("2a2c31"), X("cfe4ee"), X("d9877f"));
        photo(12, 2, 9, 11, X("b07a48"), X("f2e6c8"), X("3f9a96"));
        photo(23, 5, 7, 9, X("fbfaf6"), X("dfe8d0"), X("e2b23a"));
        cv.Outline(); cv.WallShadow();
        return cv;
    }

    static void GlassPane(Cv cv, int x, int y, int w, int h)
    {
        Sky(cv, x, y, w, h, X("8ec4e6"), X("d6eef8"));
        cv.Disc(x + w * 0.3, y + h * 0.3, 2, X("f4fafc")); cv.Disc(x + w * 0.3 + 2, y + h * 0.3 + 0.5, 1.6, X("f4fafc"));
        for (int i = 0; i < Math.Min(w, h); i++) { cv.P(x + w - 2 - i, y + 1 + i, Mix(cv.G(x + w - 2 - i, y + 1 + i), Color.White, 0.45)); }
    }

    static void Windows(Th[] T)
    {
        // cottage: four panes, lace cafe curtain, a flower box
        {
            var cv = new Cv(32, 32); Color fr = X("f4f1ea");
            cv.R(3, 2, 26, 24, fr); GlassPane(cv, 5, 4, 22, 20); cv.R(15, 4, 2, 20, fr); cv.R(5, 13, 22, 2, fr);
            for (int x = 5; x < 27; x++) for (int y = 16; y < 24; y++) if ((x + y) % 3 != 0) cv.P(x, y, Mix(cv.G(x, y), X("fbfbf8"), 0.75));
            for (int x = 5; x < 27; x += 2) cv.P(x, 24, X("fbfbf8"));
            cv.R(2, 26, 28, 4, X("8a5a32")); cv.R(2, 26, 28, 1, X("a87a48"));
            for (int x = 3; x < 29; x += 3) { cv.P(x, 25, X("4f8f3a")); cv.P(x + 1, 24, Rnd(x, 0, 0, 3) > 0.5 ? X("e86a7a") : X("f0c040")); cv.P(x + 1, 25, X("4f8f3a")); }
            cv.Outline(); cv.WallShadow();
            WallPiece("Window", "window_cottage", "cottage", "Cottage Window", 2, 2, 160, "Lace curtain and a box of flowers.", cv);
        }
        // modern: one big pane, thin black frame, roller blind
        {
            var cv = new Cv(48, 32); Color fr = X("2a2c31");
            cv.R(2, 3, 44, 24, fr); GlassPane(cv, 3, 4, 42, 22); cv.R(24, 4, 1, 22, fr);
            cv.R(3, 4, 42, 7, X("d8dadd")); cv.R(3, 10, 42, 1, X("b8bbc0")); cv.R(23, 11, 2, 2, X("9aa1ab"));
            cv.R(1, 27, 46, 2, X("dfe3e8"));
            cv.Outline(); cv.WallShadow();
            WallPiece("Window", "window_modern", "modern", "Picture Window", 3, 2, 260, "One wide pane with a roller blind.", cv);
        }
        // rustic: six panes, plaid curtains tied back, wooden valance
        {
            var cv = new Cv(32, 32); Color fr = T[2].woodDk;
            cv.R(5, 4, 22, 22, fr); GlassPane(cv, 7, 6, 18, 18); cv.R(13, 6, 1, 18, fr); cv.R(19, 6, 1, 18, fr); cv.R(7, 14, 18, 1, fr);
            for (int s = 0; s < 2; s++)
                for (int y = 4; y < 27; y++)
                {
                    int wdt = y < 15 ? 5 : (y < 18 ? 3 : 4 + (y - 18) / 3);
                    for (int i = 0; i < wdt; i++)
                    {
                        int x = s == 0 ? 2 + i : 29 - i; bool a = (x % 4) < 1, b = (y % 4) < 1;
                        cv.P(x, y, a && b ? X("2e2a30") : (a || b ? X("80262c") : X("a8323a")));
                    }
                }
            cv.R(3, 15, 4, 1, X("c9a25a")); cv.R(25, 15, 4, 1, X("c9a25a"));
            cv.R(1, 1, 30, 4, T[2].wood); cv.R(1, 4, 30, 1, T[2].woodDk);
            cv.Outline(); cv.WallShadow();
            WallPiece("Window", "window_rustic", "rustic", "Cabin Window", 2, 2, 150, "Red plaid curtains and a log valance.", cv);
        }
        // retro: wide window, orange drapes with mustard dots and a valance
        {
            var cv = new Cv(48, 32); Color fr = T[3].wood;
            cv.R(6, 4, 36, 22, fr); GlassPane(cv, 8, 6, 32, 18); cv.R(23, 6, 2, 18, fr);
            for (int s = 0; s < 2; s++)
                for (int y = 3; y < 28; y++) for (int i = 0; i < 7; i++)
                {
                    int x = s == 0 ? 2 + i : 45 - i;
                    Color c = (i % 3 == 0) ? Mul(T[3].fab, 0.85) : T[3].fab;
                    if ((x * 3 + y * 5) % 11 == 0) c = T[3].acc;
                    cv.P(x, y, c);
                }
            cv.R(1, 1, 46, 4, T[3].fab2); for (int x = 1; x < 47; x += 4) cv.R(x, 5, 2, 1, T[3].fab2);
            cv.Outline(); cv.WallShadow();
            WallPiece("Window", "window_retro", "retro", "Drape Window", 3, 2, 200, "Orange drapes with mustard dots.", cv);
        }
        // coastal: sea view and sheer blue curtains
        {
            var cv = new Cv(32, 32); Color fr = X("fbfaf6");
            cv.R(5, 2, 22, 25, fr); Sky(cv, 7, 4, 18, 13, X("9fd0ee"), X("e0f2fa")); cv.R(7, 17, 18, 8, X("3f7aa8"));
            for (int x = 7; x < 25; x += 3) cv.P(x + (x / 3) % 2, 19 + (x % 2), X("e8f6fc"));
            cv.R(15, 4, 2, 21, fr); cv.R(7, 14, 18, 1, fr);
            for (int s = 0; s < 2; s++) for (int y = 1; y < 29; y++) for (int i = 0; i < 6; i++)
            {
                int x = s == 0 ? 2 + i : 29 - i; Color under = cv.G(x, y);
                Color sheer = X("b8d8ec"); cv.P(x, y, under.A > 0 ? Mix(under, sheer, 0.6) : (i % 2 == 0 ? sheer : X("a8cce4")));
            }
            cv.R(1, 0, 30, 1, X("c9a46a"));
            cv.Outline(); cv.WallShadow();
            WallPiece("Window", "window_coastal", "coastal", "Sea View Window", 2, 2, 180, "Sheer blue curtains and the sea.", cv);
        }
        // arched: a white arch-topped window
        {
            var cv = new Cv(32, 32); Color fr = X("f4f1ea");
            for (int y = 2; y < 28; y++) for (int x = 6; x < 26; x++)
            {
                double dx = x + 0.5 - 16, dy = y + 0.5 - 12;
                if (y >= 12 || dx * dx + dy * dy <= 100) cv.P(x, y, fr);
            }
            var glass = new Cv(32, 32); GlassPane(glass, 8, 4, 16, 22);
            for (int y = 4; y < 26; y++) for (int x = 8; x < 24; x++)
            {
                double dx = x + 0.5 - 16, dy = y + 0.5 - 12;
                if (y >= 12 || dx * dx + dy * dy <= 64) cv.P(x, y, glass.G(x, y));
            }
            cv.R(15, 4, 2, 22, fr); cv.R(8, 15, 16, 1, fr); cv.R(4, 27, 24, 2, fr);
            cv.Outline(); cv.WallShadow();
            WallPiece("Window", "window_arched", "any", "Arched Window", 2, 2, 220, "A tall window with a round top.", cv);
        }
    }

    static void WallDecor(Th[] T)
    {
        {
            var cv = new Cv(16, 16);
            cv.Disc(8, 8, 6.5, T[0].woodDk); cv.Disc(8, 8, 5.5, X("f6efdc"));
            for (int a = 0; a < 12; a++) { double an = a * Math.PI / 6; cv.P((int)(8 + Math.Cos(an) * 4.6), (int)(8 + Math.Sin(an) * 4.6), X("4a3a30")); }
            cv.R(8, 4, 1, 4, X("2a1e2a")); cv.R(8, 8, 3, 1, X("2a1e2a")); cv.P(8, 8, X("c84a3a"));
            cv.Outline(); cv.WallShadow();
            WallPiece("WallDecor", "clock", "any", "Wall Clock", 1, 1, 70, "Tick, tock.", cv);
        }
        {
            var cv = new Cv(16, 32);
            for (int y = 0; y < 32; y++) for (int x = 0; x < 16; x++)
            {
                double dx = (x + 0.5 - 8) / 6.5, dy = (y + 0.5 - 15) / 11.5, d = dx * dx + dy * dy;
                if (d <= 1) cv.P(x, y, d > 0.72 ? (((x + y) % 3 == 0) ? X("e2c070") : X("c9a25a")) : Mix(X("bcdbe8"), X("e6f2f6"), (y - 4) / 22.0));
            }
            for (int i = 0; i < 6; i++) cv.P(5 + i / 2, 9 + i, X("f8fcfd"));
            cv.Outline(); cv.WallShadow();
            WallPiece("WallDecor", "mirror", "any", "Gilded Mirror", 1, 2, 150, "An oval mirror in a gold frame.", cv);
        }
        {
            var cv = new Cv(32, 16);
            cv.R(2, 11, 28, 2, T[0].wood); cv.R(2, 11, 28, 1, Mul(T[0].wood, 1.2)); cv.R(5, 13, 1, 2, T[0].woodDk); cv.R(26, 13, 1, 2, T[0].woodDk);
            cv.R(4, 6, 3, 5, X("c8e0d8")); cv.R(4, 5, 3, 1, X("8a5a32")); cv.R(5, 8, 1, 3, X("e86a7a"));
            cv.R(8, 7, 3, 4, X("c8e0d8")); cv.R(8, 6, 3, 1, X("8a5a32")); cv.R(9, 8, 1, 3, X("f0c040"));
            cv.R(13, 8, 4, 3, X("c46a3d")); cv.Disc(15, 6, 2.5, X("4f8f3a")); cv.P(14, 5, X("8cc850"));
            cv.R(20, 4, 2, 7, X("6d8fb0")); cv.R(22, 5, 2, 6, X("d9877f")); cv.R(24, 3, 2, 8, X("8fae7e")); cv.R(26, 6, 3, 5, X("efe2c4"));
            cv.Outline(); cv.WallShadow();
            WallPiece("WallDecor", "wall_shelf", "any", "Wall Shelf", 2, 1, 80, "Jam jars, a plant and a few books.", cv);
        }
        {
            var cv = new Cv(16, 16);
            for (int y = 0; y < 16; y++) for (int x = 0; x < 16; x++)
            {
                double dx = x + 0.5 - 8, dy = y + 0.5 - 8, d = Math.Sqrt(dx * dx + dy * dy);
                if (d <= 6.5 && d >= 3.5) cv.P(x, y, Rnd(x, y, 0, 41) > 0.85 ? X("8cc850") : (Rnd(x, y, 0, 42) > 0.5 ? X("3f7a3a") : X("4f8f3a")));
                if (d <= 6.5 && d >= 3.5 && Rnd(x, y, 0, 43) > 0.9) cv.P(x, y, X("c8323a"));
            }
            cv.R(6, 12, 4, 2, X("c8323a")); cv.P(5, 14, X("c8323a")); cv.P(10, 14, X("c8323a")); cv.P(8, 13, X("e85050"));
            cv.Outline(); cv.WallShadow();
            WallPiece("WallDecor", "wreath", "any", "Berry Wreath", 1, 1, 60, "Evergreen with red berries and a bow.", cv);
        }
        {
            var cv = new Cv(32, 32);
            cv.R(3, 3, 26, 1, T[2].woodDk); cv.P(2, 3, T[2].woodDk); cv.P(29, 3, T[2].woodDk);
            Color[] q = { X("d9877f"), X("efe2c4"), X("8fae7e"), X("6d8fb0"), X("e2b23a"), X("f3ead6") };
            for (int y = 4; y < 28; y++) for (int x = 5; x < 27; x++)
            {
                int cx = (x - 5) / 4, cy = (y - 4) / 4; Color c = q[(int)(Rnd(cx, cy, 0, 51) * q.Length) % q.Length];
                if ((x - 5) % 4 == 0 || (y - 4) % 4 == 0) c = Mul(c, 0.88);
                cv.P(x, y, c);
            }
            cv.Outline(); cv.WallShadow();
            WallPiece("WallDecor", "quilt_hanging", "cottage", "Quilt Hanging", 2, 2, 140, "A patchwork quilt hung on a dowel.", cv);
        }
        {
            var cv = new Cv(32, 16);
            cv.R(2, 2, 28, 1, X("c2c7cf")); cv.P(2, 3, X("8d939c")); cv.P(29, 3, X("8d939c"));
            cv.R(5, 3, 1, 2, X("8d939c")); cv.Disc(5.5, 9, 3.5, X("3a3a40")); cv.Disc(5.5, 9, 2.5, X("4a4a50"));
            cv.R(12, 3, 1, 9, X("b87333")); cv.Disc(12.5, 12, 1.8, X("b87333"));
            cv.R(17, 3, 1, 6, X("c2c7cf")); for (int j = 0; j < 4; j++) cv.R(16, 9 + j, 3, 1, (j % 2 == 0) ? X("c2c7cf") : X("e8ecef"));
            cv.R(22, 3, 1, 3, X("8d939c")); cv.Disc(23, 9, 3, X("b87333")); cv.Disc(23, 9, 2, X("d08848")); cv.R(26, 8, 3, 1, X("6b4a32"));
            cv.Outline(); cv.WallShadow();
            WallPiece("WallDecor", "utensil_rail", "any", "Pot Rail", 2, 1, 90, "Pans and ladles ready by the stove.", cv);
        }
    }

    // ================================================================ FLOORS AND WALL COVERINGS (16 x 16 tiles that repeat)
    static Cv Tile(Func<int, int, Color> f) { var cv = new Cv(16, 16); for (int y = 0; y < 16; y++) for (int x = 0; x < 16; x++) cv.P(x, y, f(x, y)); return cv; }

    static void Floor(string id, string theme, string name, int price, string desc, Func<int, int, Color> f)
    {
        man.AppendLine(string.Join("\t", new[] { "floor", id, name, theme, price.ToString(), desc }));
        Bitmap b = Tile(f).Bmp(); Save(b, "Flooring", id);
        var big = new Bitmap(32, 32, PixelFormat.Format32bppArgb);
        for (int y = 0; y < 32; y++) for (int x = 0; x < 32; x++) big.SetPixel(x, y, b.GetPixel(x % 16, y % 16));
        previews.Add(new KeyValuePair<string, List<Bitmap>>(id, new List<Bitmap> { big }));
    }

    static Color Planks(int x, int y, int rowH, Color a, Color b, int seed)
    {
        int row = y / rowH; int joint = Mod(row * 7 + 3, 16);
        Color c = Rnd(row, x > joint ? 1 : 0, 0, seed) > 0.5 ? a : b;
        double k = 0.95 + 0.08 * Rnd(x / 4, y, 0, seed + 1);
        if (Mod(y, rowH) == rowH - 1) k = 0.68; else if (x == joint) k = 0.75; else if (Mod(y, rowH) == 0) k *= 1.06;
        if (Rnd(x / 5, row, 0, seed + 2) > 0.85 && Mod(y, rowH) == rowH / 2) k *= 0.9;
        return Mul(c, k);
    }

    static void Floors()
    {
        Floor("floor_oak", "cottage", "Honey Oak Boards", 120, "Warm, narrow oak boards.", (x, y) => Planks(x, y, 4, X("c08a55"), X("b07a48"), 1));
        Floor("floor_concrete", "modern", "Polished Concrete", 160, "Big smooth grey tiles.", (x, y) =>
        {
            if (x == 0 || y == 0) return X("8f8f8a");
            return Mul(X("b2b1ac"), 0.97 + 0.06 * Rnd(x / 2, y / 2, 0, 7) + (x + y < 6 ? 0.04 : 0));
        });
        Floor("floor_barnwood", "rustic", "Barn Wood", 130, "Wide old boards with nail heads.", (x, y) =>
        {
            Color c = Planks(x, y, 8, X("7a5232"), X("6a4428"), 11);
            if (Mod(y, 8) == 3 && (x == Mod((y / 8) * 7 + 3, 16) + 1 || x == Mod((y / 8) * 7 + 3, 16) - 1)) c = X("3a3030");
            return c;
        });
        Floor("floor_checker", "retro", "Diner Checker", 140, "Cream and teal squares.", (x, y) =>
        {
            Color c = ((x / 8 + y / 8) % 2 == 0) ? X("f2e6c8") : X("3f9a96");
            return Mul(c, 0.97 + 0.05 * Rnd(x, y, 0, 3));
        });
        Floor("floor_whitewash", "coastal", "Whitewashed Boards", 120, "Pale boards like a beach house.", (x, y) => Planks(x, y, 4, X("e2ded4"), X("d6d1c4"), 21));
        Floor("floor_parquet", "any", "Parquet", 180, "Little oak blocks laid in squares.", (x, y) =>
        {
            bool horiz = ((x / 8) + (y / 8)) % 2 == 0; int along = horiz ? y % 8 : x % 8;
            Color c = (along / 4) % 2 == 0 ? X("b07a48") : X("c08a55");
            if (along % 4 == 3) c = Mul(c, 0.72); if (x % 8 == 7 || y % 8 == 7) c = Mul(c, 0.8);
            return Mul(c, 0.96 + 0.06 * Rnd(x, y, 0, 31));
        });
        Floor("floor_terracotta", "any", "Terracotta Tiles", 150, "Warm clay tiles for kitchens.", (x, y) =>
        {
            if (x % 8 == 0 || y % 8 == 0) return X("d8c3a8");
            return Mul(X("c46a3d"), 0.9 + 0.15 * Rnd(x / 8, y / 8, 0, 41) + 0.04 * Rnd(x, y, 0, 42));
        });
        Floor("floor_carpet", "any", "Oatmeal Carpet", 110, "Soft beige carpet for bedrooms.", (x, y) => Mul(X("cbb99c"), 0.92 + 0.12 * Rnd(x, y, 0, 51) - (((x + y) & 1) == 0 ? 0.02 : 0)));
        Floor("floor_mosaic", "any", "White Mosaic", 140, "Tiny white tiles for bathrooms.", (x, y) =>
        {
            if (x % 4 == 0 || y % 4 == 0) return X("bfc6ca");
            return Rnd(x / 4, y / 4, 0, 61) > 0.8 ? X("d8e4ea") : X("eef2f3");
        });
    }

    // A wall covering is three tiles: the top row (with the crown moulding), the middle rows and the bottom row (skirting / wainscot).
    static void Wall(string id, string theme, string name, int price, string desc, Func<int, int, Color> paper, Func<int, int, Color> top, Func<int, int, Color> bottom)
    {
        man.AppendLine(string.Join("\t", new[] { "wall", id, name, theme, price.ToString(), desc }));
        Bitmap t = Tile(top).Bmp(), u = Tile(paper).Bmp(), b = Tile(bottom).Bmp();
        Save(t, "WallCovering", id + "_top"); Save(u, "WallCovering", id + "_upper"); Save(b, "WallCovering", id + "_base");
        var big = new Bitmap(32, 48, PixelFormat.Format32bppArgb);
        for (int y = 0; y < 48; y++) for (int x = 0; x < 32; x++) big.SetPixel(x, y, (y < 16 ? t : (y < 32 ? u : b)).GetPixel(x % 16, y % 16));
        previews.Add(new KeyValuePair<string, List<Bitmap>>(id, new List<Bitmap> { big }));
    }

    static Func<int, int, Color> Crown(Func<int, int, Color> paper, Color trim)
    {
        return (x, y) =>
        {
            if (y == 0) return Mul(trim, 0.7);
            if (y == 1) return Mul(trim, 1.05);
            if (y == 2) return trim;
            if (y == 3) return Mul(trim, 0.85);
            if (y == 4) return Mul(paper(x, y), 0.8);
            if (y == 5) return Mul(paper(x, y), 0.92);
            return paper(x, y);
        };
    }
    static Func<int, int, Color> Skirting(Func<int, int, Color> paper, Color trim)
    {
        return (x, y) =>
        {
            if (y < 12) return paper(x, y);
            if (y == 12) return Mul(trim, 1.12);
            if (y == 15) return Mul(trim, 0.62);
            return Mul(trim, y == 13 ? 1.0 : 0.9);
        };
    }
    static Func<int, int, Color> Wainscot(Color panel, Color rail)
    {
        return (x, y) =>
        {
            if (y == 0) return Mul(rail, 1.15);
            if (y == 1) return rail;
            if (y == 2) return Mul(rail, 0.75);
            if (y >= 13) return Mul(rail, y == 15 ? 0.62 : (y == 13 ? 1.08 : 0.92));
            double k = (x % 4 == 0) ? 0.86 : (x % 4 == 1 ? 1.05 : 1.0);
            return Mul(panel, k);
        };
    }

    static void Walls()
    {
        Func<int, int, Color> floral = (x, y) =>
        {
            int ox = (y / 8) % 2 == 0 ? 0 : 4, lx = Mod(x - ox, 8), ly = y % 8;
            Color c = X("efe6cf");
            if (lx == 3 && ly == 3) return X("e8c050");
            if ((lx == 2 && ly == 3) || (lx == 4 && ly == 3) || (lx == 3 && ly == 2) || (lx == 3 && ly == 4)) return X("d9877f");
            if (lx == 5 && ly == 5) return X("8fae7e");
            return c;
        };
        Wall("wall_cottage", "cottage", "Rosebud Wallpaper", 120, "Little roses above a white beadboard.", floral, Crown(floral, X("f4f1ea")), Wainscot(X("ece8dc"), X("f4f1ea")));

        Func<int, int, Color> plain = (x, y) => Mul(X("b9b4ad"), 0.98 + 0.03 * Rnd(x, y, 0, 71));
        Wall("wall_modern", "modern", "Warm Grey Paint", 80, "Calm grey walls, charcoal skirting.", plain,
            (x, y) => y == 0 ? X("8e8a84") : (y == 1 ? X("a7a29b") : plain(x, y)), Skirting(plain, X("3c3f46")));

        Func<int, int, Color> logs = (x, y) =>
        {
            int ly = y % 8; if (ly == 7) return X("3a2414");
            double k = 0.72 + 0.42 * Math.Sin(Math.PI * (ly + 0.5) / 7.0);
            Color c = Mul(X("8a5a34"), k * (0.95 + 0.08 * Rnd(x / 4, y / 8, 0, 72)));
            if (Rnd(x, y / 8, 0, 73) > 0.94 && ly > 1 && ly < 6) c = Mul(c, 0.8);
            return c;
        };
        Wall("wall_rustic", "rustic", "Log Wall", 140, "Round logs, like a mountain cabin.", logs,
            (x, y) => y < 4 ? Mul(X("4e301b"), y == 0 ? 0.7 : (y == 3 ? 0.85 : 1)) : logs(x, y), Skirting(logs, X("4e301b")));

        Func<int, int, Color> atomic = (x, y) =>
        {
            Color bg = X("f2e6c8");
            double d1 = Math.Sqrt(Math.Pow(Mod(x, 16) + 0.5 - 4, 2) + Math.Pow(Mod(y, 16) + 0.5 - 4, 2));
            double d2 = Math.Sqrt(Math.Pow(Mod(x, 16) + 0.5 - 12, 2) + Math.Pow(Mod(y, 16) + 0.5 - 12, 2));
            if (d1 <= 3 && d1 >= 2) return X("e07b39"); if (d1 < 1.2) return X("e2b23a");
            if (d2 <= 3 && d2 >= 2) return X("3f9a96"); if (d2 < 1.2) return X("e07b39");
            return bg;
        };
        Func<int, int, Color> walnut = (x, y) => Mul(X("8b5a32"), (x % 5 == 0 ? 0.88 : 1.0) * (0.95 + 0.08 * Rnd(x, y / 3, 0, 74)));
        Wall("wall_retro", "retro", "Atomic Wallpaper", 130, "Orange and teal rings over walnut panels.", atomic,
            (x, y) => y < 3 ? Mul(X("5e3b20"), y == 0 ? 0.7 : 1) : atomic(x, y),
            (x, y) => y == 0 ? X("5e3b20") : (y == 15 ? Mul(X("5e3b20"), 0.7) : walnut(x, y)));

        Func<int, int, Color> shiplap = (x, y) =>
        {
            if (y % 4 == 3) return X("c9cdcc");
            if (y % 4 == 0) return X("f8f9f8");
            return X("eef0ee");
        };
        Wall("wall_coastal", "coastal", "White Shiplap", 120, "Painted boards above a sky-blue wainscot.", shiplap, Crown(shiplap, X("fbfaf6")), Wainscot(X("9cc3d8"), X("f6f7f6")));

        Func<int, int, Color> brick = (x, y) =>
        {
            int row = y / 4; int sx = Mod(x + (row % 2) * 4, 8);
            if (y % 4 == 3 || sx == 7) return X("d8c6b0");
            return Mul(X("a14a32"), 0.85 + 0.25 * Rnd((x + (row % 2) * 4) / 8, row, 0, 75));
        };
        Wall("wall_brick", "any", "Exposed Brick", 160, "Red brick with pale mortar.", brick,
            (x, y) => y < 4 ? Mul(X("4e301b"), y == 0 ? 0.7 : 1) : brick(x, y), Skirting(brick, X("4e301b")));

        Func<int, int, Color> stripes = (x, y) => (x % 8) < 4 ? X("a8c095") : X("efe6cf");
        Wall("wall_stripes", "any", "Sage Stripe Wallpaper", 110, "Wide sage and cream stripes.", stripes, Crown(stripes, X("f4f1ea")), Wainscot(X("ece8dc"), X("f4f1ea")));

        Func<int, int, Color> subway = (x, y) =>
        {
            int row = y / 4; int sx = Mod(x + (row % 2) * 4, 8);
            if (y % 4 == 3 || sx == 7) return X("c9d0d0");
            return y % 4 == 0 ? X("fbfcfb") : X("f0f3f2");
        };
        Wall("wall_subway", "any", "Subway Tile", 150, "White tiles for kitchens and bathrooms.", subway,
            (x, y) => y < 2 ? X(y == 0 ? "9aa4a8" : "d8dee0") : subway(x, y), Skirting(subway, X("8d939c")));
    }

    // ================================================================ ROOM FRAME AND SHADOWS
    static void RoomPieces()
    {
        // the top of the room's outer wall, seen from above (5 pixels thick). The inner edge is at the top of cap_h and the right of cap_v.
        Color cap = X("6a5850"), capLight = X("8c786c"), capDark = X("4a3a34");
        var h = new Cv(16, 6);
        for (int x = 0; x < 16; x++) { h.P(x, 0, capLight); h.P(x, 1, cap); h.P(x, 2, cap); h.P(x, 3, Mul(cap, 0.92)); h.P(x, 4, capDark); h.P(x, 5, INK); }
        Save(h.Bmp(), "Room", "room_cap_h");
        var v = new Cv(6, 16);
        for (int y = 0; y < 16; y++) { v.P(5, y, capLight); v.P(4, y, cap); v.P(3, y, cap); v.P(2, y, Mul(cap, 0.92)); v.P(1, y, capDark); v.P(0, y, INK); }
        Save(v.Bmp(), "Room", "room_cap_v");
        var corner = new Cv(6, 6);
        for (int y = 0; y < 6; y++) for (int x = 0; x < 6; x++) corner.P(x, y, (x == 0 || y == 5) ? INK : ((x == 1 || y == 4) ? capDark : cap));
        Save(corner.Bmp(), "Room", "room_cap_corner");
        // shadow the back wall throws onto the floor (darkest at the top)
        var s = new Cv(16, 16); int[] al = { 120, 92, 68, 48, 32, 20, 11, 5 };
        for (int y = 0; y < al.Length; y++) for (int x = 0; x < 16; x++) s.P(x, y, Color.FromArgb(al[y], 24, 14, 20));
        Save(s.Bmp(), "Room", "room_wall_shadow");
        // shadow along a side wall (darkest on the left)
        var ss = new Cv(16, 16); int[] sa = { 80, 52, 32, 18, 8 };
        for (int x = 0; x < sa.Length; x++) for (int y = 0; y < 16; y++) ss.P(x, y, Color.FromArgb(sa[x], 24, 14, 20));
        Save(ss.Bmp(), "Room", "room_side_shadow");
    }

    // ================================================================ MAIN
    public static string Run(string dir, string previewPath)
    {
        outDir = dir; all.Clear(); man.Clear(); previews.Clear();
        man.AppendLine("# kind\tid\tname\tcategory\ttheme\tmount\tw\td\tprice\tsurfaceHeight\tpivotX\tpivotY\trotates\tdescription   (written by Tools/make_interior_art.ps1 - do not edit by hand)");
        Th[] T = Themes();
        foreach (Th t in T)
        {
            Couch(t); Armchair(t); Ottoman(t); Fireplace(t); TV(t); ComputerDesk(t); SideTable(t); CoffeeTable(t); Chair(t); Rug(t); Plant(t); FloorLamp(t);
            Bookshelf(t); Bed(t); Dresser(t); Counter(t); Sink(t); Oven(t); Fridge(t); Washer(t); Dryer(t); Blender(t); KitchenTable(t);
        }
        SmallAppliances(T); Bathroom();

        int pngs = 0;
        foreach (Obj o in all)
        {
            Vox v = Build(o);
            var shots = new List<Bitmap>();
            bool shadow = o.mount != "Flat";
            for (int f = 0; f < 4; f++)
            {
                Bitmap b = Render(o, v, f, shadow);
                Save(b, o.cat, o.id + "_" + FaceName[f]); shots.Add(b); pngs++;
            }
            previews.Add(new KeyValuePair<string, List<Bitmap>>(o.id, shots));
            man.AppendLine(string.Join("\t", new[] { "piece", o.id, o.name, o.cat, o.theme, o.mount, o.w.ToString(), o.d.ToString(), o.price.ToString(), o.top.ToString(), PadL.ToString(), PadB.ToString(), "1", o.desc }));
        }
        int before = previews.Count;
        Pictures(T); Windows(T); WallDecor(T); Floors(); Walls(); RoomPieces();
        pngs += previews.Count - before;

        File.WriteAllText(Path.Combine(dir, "decor_manifest.txt"), man.ToString());
        if (!string.IsNullOrEmpty(previewPath)) SavePreview(previewPath);
        return "Drew " + all.Count + " turnable pieces and the flat pieces (" + pngs + " pictures) into " + dir;
    }

    static void SavePreview(string path)
    {
        const int S = 2, maxW = 1900, gap = 10;
        int x = gap, y = gap + 12, rowH = 0, totalH = 0;
        var spots = new List<int[]>();
        foreach (var kv in previews)
        {
            int w = 0, h = 0; foreach (Bitmap b in kv.Value) { w += b.Width * S + 4; h = Math.Max(h, b.Height * S); }
            if (x + w > maxW) { x = gap; y += rowH + 22; rowH = 0; }
            spots.Add(new[] { x, y }); x += w + gap; rowH = Math.Max(rowH, h); totalH = y + rowH + gap;
        }
        using (var bmp = new Bitmap(maxW, totalH, PixelFormat.Format32bppArgb))
        using (var g = Graphics.FromImage(bmp))
        using (var font = new Font("Arial", 8))
        {
            g.Clear(X("c8b8a0")); g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.NearestNeighbor; g.PixelOffsetMode = System.Drawing.Drawing2D.PixelOffsetMode.Half;
            for (int i = 0; i < previews.Count; i++)
            {
                int px = spots[i][0], py = spots[i][1];
                g.DrawString(previews[i].Key, font, Brushes.Black, px, py - 12);
                foreach (Bitmap b in previews[i].Value) { g.DrawImage(b, px, py, b.Width * S, b.Height * S); px += b.Width * S + 4; }
            }
            bmp.Save(path, ImageFormat.Png);
        }
    }
}
'@

Add-Type -TypeDefinition $code -Language CSharp -ReferencedAssemblies System.Drawing
[InteriorArt]::Run($out, $preview)
Write-Host "Catalogue picture: $preview"
