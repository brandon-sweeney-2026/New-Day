using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Tilemaps;

/// <summary>
/// Makes (or resets) the four ready-to-use geographies from the tiles that Farm > Biomes > Build Path Palettes created:
/// Mountain Pathway, Road, Sidewalk, Forest Pathway, Dock. Menu: Farm > Level Editors > Make Geography Presets.
/// Run Build Path Palettes first, otherwise the presets come out empty (the Geography Brush window has a Reset button for that).
/// Must live in a folder named "Editor".
/// </summary>
public static class GeographyPresetMaker
{
    public const string Folder = "Assets/Farm/Data/Geography";
    private const string TileRoot = "Assets/Farm/Paths/Tiles";
    private static readonly string[] Edge9 = { "TopLeft", "Top", "TopRight", "Left", "Middle", "Right", "BottomLeft", "Bottom", "BottomRight" };
    private static readonly string[] Inner4 = { "TL", "TR", "BL", "BR" };

    private class R
    {
        public string label; public string[] names; public GeographyPreset.Zone zone; public float chance; public int spacing, minWidth;
        public R(string label, string[] names, GeographyPreset.Zone zone, float chance, int spacing = 0, int minWidth = 1)
        { this.label = label; this.names = names; this.zone = zone; this.chance = chance; this.spacing = spacing; this.minWidth = minWidth; }
    }

    private class Def
    {
        public string group, title;
        public GeographyPreset.BrushShape shape;
        public int width, wobble, shoulder, centerMin;
        public bool onGround, cliff;
        public float rough;
        public string centerH, centerV;
        public R[] rules;
    }

    private const GeographyPreset.Zone Core = GeographyPreset.Zone.PathCore;
    private const GeographyPreset.Zone Edge = GeographyPreset.Zone.PathEdge;
    private const GeographyPreset.Zone Shoulder = GeographyPreset.Zone.Shoulder;

    private static readonly Def[] Defs =
    {
        new Def { group = "MountainPath", title = "Mountain Pathway", shape = GeographyPreset.BrushShape.Round, width = 3, wobble = 1, shoulder = 3, centerMin = 11,
            rules = new[] {
                new R("Pebbles and scree", new[] { "Pebbles", "Scree", "Crack", "Footprints" }, Core, 0.10f),
                new R("Puddles and moss", new[] { "Puddle", "Moss" }, Core, 0.03f),
                new R("Snow and ice patches", new[] { "Snow", "Ice" }, Edge, 0.07f),
                new R("Alpine flowers", new[] { "AlpineFlowers" }, Shoulder, 0.08f),
                new R("Boulders and pines", new[] { "Boulder", "Pine" }, Shoulder, 0.07f, 2),
                new R("Trail markers", new[] { "Cairn", "TrailMarker", "StoneMarker", "RopePost" }, Edge, 0.04f, 5),
                new R("Signpost", new[] { "Signpost" }, Edge, 0.01f, 14, 2) } },
        new Def { group = "AsphaltRoad", title = "Road", shape = GeographyPreset.BrushShape.Square, width = 4, wobble = 0, shoulder = 1, centerMin = 4,
            centerH = "LaneDashedH", centerV = "LaneDashedV",
            rules = new[] {
                new R("Potholes and drains", new[] { "Pothole", "Drain" }, Core, 0.012f, 2),
                new R("Manholes", new[] { "Manhole" }, Core, 0.01f, 5),
                new R("Lamp posts", new[] { "LampPost" }, Edge, 0.04f, 8),
                new R("Bollards and reflectors", new[] { "Bollard", "ReflectorPost" }, Edge, 0.03f, 4),
                new R("Cones and barricades", new[] { "TrafficCone", "Barricade" }, Edge, 0.008f, 6),
                new R("Stop sign", new[] { "StopSign" }, Shoulder, 0.01f, 12) } },
        new Def { group = "Sidewalk", title = "Sidewalk", shape = GeographyPreset.BrushShape.Square, width = 2, wobble = 0, shoulder = 1, centerMin = 11,
            rules = new[] {
                new R("Cracks, gum, leaves, stains", new[] { "Crack", "Gum", "Stain", "Leaves", "Weeds", "Chalk", "Puddle" }, Core, 0.07f),
                new R("Drains and manholes", new[] { "Drain", "Manhole" }, Core, 0.012f, 5),
                new R("Street furniture", new[] { "Hydrant", "Bin", "Bench", "LampPost", "Planter", "BikeRack", "Bollard" }, Edge, 0.05f, 6) } },
        new Def { group = "Dock", title = "Dock", shape = GeographyPreset.BrushShape.Square, width = 3, wobble = 0, shoulder = 1, centerMin = 11, onGround = true,
            rules = new[] {
                new R("Wet planks, rope, shells", new[] { "Puddle", "WetPrints", "Stain", "Rope", "Shells", "Feather", "Splinters", "Knot", "FishBones", "Starfish" }, Core, 0.08f),
                new R("Posts, cleats and lamps", new[] { "Bollard", "Cleat", "LampPost" }, Edge, 0.05f, 6),
                new R("Crates, barrels, nets", new[] { "Crate", "Barrel", "FishCrate", "LobsterTrap", "NetPile", "RopeCoil", "Bucket" }, Edge, 0.05f, 3),
                new R("Lifebuoys and ladders", new[] { "Lifebuoy", "Ladder" }, Edge, 0.012f, 10),
                new R("Signpost", new[] { "Signpost" }, Shoulder, 0.01f, 12) } },
        new Def { group = "Cliff", title = "Mountain Cliff", shape = GeographyPreset.BrushShape.Square, width = 4, wobble = 0, shoulder = 2, centerMin = 11, cliff = true, rough = 0.2f,
            rules = new[] {
                new R("Moss, snow, cracks on top", new[] { "Moss", "Snow", "Crack", "Pebbles" }, Core, 0.08f),
                new R("Ice on the rim", new[] { "Ice", "Snow" }, Edge, 0.06f),
                new R("Vines on the wall", new[] { "Vines" }, Edge, 0.05f),
                new R("Rubble at the foot", new[] { "Rubble", "Pebbles", "SmallRock" }, Shoulder, 0.12f) } },
        new Def { group = "ForestPath", title = "Forest Pathway", shape = GeographyPreset.BrushShape.Round, width = 3, wobble = 1, shoulder = 3, centerMin = 11,
            rules = new[] {
                new R("Leaves, needles, twigs", new[] { "Leaves", "Needles", "Twigs", "Pebbles" }, Core, 0.14f),
                new R("Puddles", new[] { "Puddle" }, Core, 0.02f),
                new R("Roots and moss", new[] { "Roots", "Moss" }, Edge, 0.12f),
                new R("Mushrooms, ferns, flowers", new[] { "Mushrooms", "FernTuft", "Flowers" }, Shoulder, 0.10f),
                new R("Stumps, logs, lanterns", new[] { "Stump", "Log", "MushroomCluster", "StoneMarker", "TrailMarker", "Lantern" }, Edge, 0.03f, 5),
                new R("Signpost", new[] { "Signpost" }, Edge, 0.008f, 14, 2) } },
    };

    public static bool HasDef(string group) { return FindDef(group) != null; }

    private static Def FindDef(string group)
    {
        foreach (Def d in Defs) if (d.group == group) return d;
        return null;
    }

    [MenuItem("Farm/Level Editors/Make Geography Presets", false, 33)]
    public static void MakeMenu()
    {
        bool anyExist = false;
        foreach (Def d in Defs) if (AssetDatabase.LoadAssetAtPath<GeographyPreset>(PathOf(d)) != null) anyExist = true;

        bool reset = false;
        if (anyExist)
        {
            int choice = EditorUtility.DisplayDialogComplex("Geography presets",
                "Some geography presets already exist.\n\nAdd missing = make only the ones that are missing and keep your edits.\nReset all = put every default preset back to the original tiles and chances (your changes to them are lost).",
                "Add missing only", "Reset all", "Cancel");
            if (choice == 2) return;
            reset = choice == 1;
        }

        int made = 0, filled = 0;
        foreach (Def d in Defs)
        {
            bool isNew;
            GeographyPreset p = LoadOrCreate(d, out isNew);
            if (isNew || reset) { Apply(p, d); made++; filled += p.fillTiles != null ? p.fillTiles.Length : 0; }
        }
        AssetDatabase.SaveAssets();
        string warn = filled == 0 ? "\n\nNo path tiles were found. Run Farm > Biomes > Build Path Palettes first, then use 'Reset' again (here or in the Geography Brush window)." : "";
        EditorUtility.DisplayDialog("Geography presets", "Made or reset " + made + " preset(s) in " + Folder + "." + warn + "\n\nOpen Farm > Level Editors > Geography Brush to paint with them.\nNot compiled in Unity - tell me about any red errors.", "OK");
    }

    /// <summary>Makes sure the four presets exist (used when the brush window opens and finds none).</summary>
    public static void EnsureAll()
    {
        foreach (Def d in Defs)
        {
            bool isNew;
            GeographyPreset p = LoadOrCreate(d, out isNew);
            if (isNew) Apply(p, d);
        }
        AssetDatabase.SaveAssets();
    }

    /// <summary>Puts one preset back to its original tiles and chances. Does nothing for custom presets.</summary>
    public static bool ResetPreset(GeographyPreset p)
    {
        Def d = FindDef(p.paletteGroup);
        if (d == null) return false;
        Apply(p, d);
        AssetDatabase.SaveAssets();
        return true;
    }

    private static string PathOf(Def d) { return Folder + "/" + d.title.Replace(" ", "") + ".asset"; }

    private static GeographyPreset LoadOrCreate(Def d, out bool isNew)
    {
        string path = PathOf(d);
        var p = AssetDatabase.LoadAssetAtPath<GeographyPreset>(path);
        isNew = p == null;
        if (p == null)
        {
            EnsureFolder(Folder);
            p = ScriptableObject.CreateInstance<GeographyPreset>();
            AssetDatabase.CreateAsset(p, path);
        }
        return p;
    }

    private static void Apply(GeographyPreset p, Def d)
    {
        p.title = d.title;
        p.paletteGroup = d.group;
        p.shape = d.shape;
        p.defaultWidth = d.width;
        p.widthWobble = d.wobble;
        p.shoulderWidth = d.shoulder;
        p.mergeWithExisting = true;
        p.paintOnGroundLayer = d.onGround;
        p.useEdgeTiles = !d.onGround;
        p.swapInnerCorners = false;
        p.centerMinWidth = d.centerMin;
        p.dashOn = 2; p.dashOff = 2;
        p.centerHorizontal = d.centerH != null ? Tile(d.group, d.centerH) : null;
        p.centerVertical = d.centerV != null ? Tile(d.group, d.centerV) : null;

        var fills = new List<TileBase>();
        for (int i = 1; i <= 8; i++) { TileBase t = Tile(d.group, "Fill_" + i); if (t != null) fills.Add(t); }
        p.fillTiles = fills.ToArray();

        p.wideTiles = new TileBase[9];
        for (int i = 0; i < 9; i++) p.wideTiles[i] = Tile(d.group, "Wide_" + Edge9[i]);
        p.innerTiles = new TileBase[4];
        for (int i = 0; i < 4; i++) p.innerTiles[i] = Tile(d.group, "Inner_" + Inner4[i]);
        p.autoTile = Tile(d.group, "Auto");

        p.style = d.cliff ? GeographyPreset.Style.Cliff : GeographyPreset.Style.Path;
        p.roughEdges = d.rough;
        if (d.cliff)
        {
            p.useEdgeTiles = true; p.paintOnGroundLayer = false;
            p.fillTiles = Variants(d.group, "Fill_", 4);
            p.cliffFaceUpper = Variants(d.group, "FaceUpper_", 4);
            p.cliffFaceLower = Variants(d.group, "FaceLower_", 4);
            p.cliffLip = new[] { Tile(d.group, "Lip_T"), Tile(d.group, "Lip_L"), Tile(d.group, "Lip_R"), Tile(d.group, "Lip_TL"), Tile(d.group, "Lip_TR") };
            p.cliffInner = new[] { Tile(d.group, "Inner_TL"), Tile(d.group, "Inner_TR") };
            p.cliffFaceEdges = new[] { Tile(d.group, "FaceUpper_L"), Tile(d.group, "FaceUpper_R"), Tile(d.group, "FaceLower_L"), Tile(d.group, "FaceLower_R") };
            p.wideTiles = new TileBase[9]; p.innerTiles = new TileBase[4]; p.autoTile = null;
        }

        var rules = new List<GeographyPreset.ScatterRule>();
        foreach (R r in d.rules)
        {
            var assets = new List<Object>();
            foreach (string n in r.names) { TileBase t = Tile(d.group, n); if (t != null) assets.Add(t); }
            var rule = new GeographyPreset.ScatterRule
            {
                label = r.label, assets = assets.ToArray(), zone = r.zone, chance = r.chance, minSpacing = r.spacing, minBrushWidth = r.minWidth
            };
            rules.Add(rule);
        }
        p.scatter = rules.ToArray();
        EditorUtility.SetDirty(p);
    }

    private static TileBase[] Variants(string group, string prefix, int count)
    {
        var list = new List<TileBase>();
        for (int i = 1; i <= count; i++) { TileBase t = Tile(group, prefix + i); if (t != null) list.Add(t); }
        return list.ToArray();
    }

    private static TileBase Tile(string group, string name)
    {
        string full = group + "_" + name;
        var t = AssetDatabase.LoadAssetAtPath<TileBase>(TileRoot + "/" + group + "/" + full + ".asset");
        if (t != null) return t;
        foreach (string guid in AssetDatabase.FindAssets(full + " t:TileBase"))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            if (Path.GetFileNameWithoutExtension(path) == full) return AssetDatabase.LoadAssetAtPath<TileBase>(path);
        }
        return null;
    }

    private static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path)) return;
        string parent = Path.GetDirectoryName(path).Replace('\\', '/');
        EnsureFolder(parent);
        AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
    }
}
