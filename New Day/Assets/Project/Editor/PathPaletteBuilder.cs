using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Tilemaps;

/// <summary>
/// Builds 7 palettes for roads and paths: Asphalt Road, Asphalt Path, Sidewalk, Forest Path, Mountain Path, Dirt Path, Dock.
/// Menu: Farm > Biomes > Build Path Palettes.
///
/// First copy the "Paths" art folder (the six *_tiles.png files) anywhere into your Assets.
/// Each palette is laid out the same way (the picture of the sheet IS the palette):
///   top row          : 8 ground variants (paint a mix so it doesn't look flat)
///   left 3x3         : WIDE block (roads, plazas). Edges are see-through, so paint it on a layer ABOVE the ground.
///   next to it 2x2   : 4 inner corners for L-shaped wide areas
///   right side       : overlays (cracks, puddles, lane lines, crosswalks, footprints...)
///   bottom left 4x4  : props (cones, lamps, signs, bins, stumps, cairns...)
///   bottom right 4x4 : 1-TILE-WIDE pieces (straight, corners, T, cross, ends), picked by hand if you want
///   one row BELOW    : the "Auto" tile. Paint with it and the pieces connect by themselves.
/// Safe to run again. This file must live in a folder named "Editor".
/// </summary>
public static class PathPaletteBuilder
{
    private const int Cell = 16;
    private const float PixelsPerUnit = 16f;
    private const string OutRoot = "Assets/Farm/Paths";

    private class Surface
    {
        public string file, title;
        public string[] decor;      // up to 11, in DecorCells order
        public string[] props;      // up to 16, reading order in the 4x4 block
    }

    private static readonly Surface[] Surfaces =
    {
        new Surface { file = "asphalt_road_tiles", title = "AsphaltRoad",
            decor = new[] { "LaneDashedH", "LaneDashedV", "LaneSolidH", "LaneSolidV", "CrosswalkH", "CrosswalkV", "Manhole", "Drain", "Pothole", "StopLineH", "StopLineV" },
            props = new[] { "TrafficCone", "Barricade", "Bollard", "StopSign", "SpeedBump", "ReflectorPost", "LampPost" } },
        new Surface { file = "asphalt_path_tiles", title = "AsphaltPath",
            decor = new[] { "Crack", "Puddle", "Leaves", "Pebbles", "Weeds", "OilStain", "Chalk", "Manhole", "Drain", "Pothole" },
            props = new[] { "Bollard", "LampPost", "Bin", "Bench", "Signpost", "TrafficCone", "BikeRack" } },
        new Surface { file = "sidewalk_tiles", title = "Sidewalk",
            decor = new[] { "Crack", "Gum", "Puddle", "Leaves", "Weeds", "Chalk", "Stain", "Drain", "Manhole", "TactileH", "TactileV" },
            props = new[] { "Hydrant", "Bin", "Bench", "LampPost", "Planter", "BikeRack", "Bollard" } },
        new Surface { file = "forest_path_tiles", title = "ForestPath",
            decor = new[] { "Leaves", "Needles", "Roots", "Moss", "Mushrooms", "Puddle", "Pebbles", "FernTuft", "Flowers", "Twigs" },
            props = new[] { "Signpost", "Stump", "Log", "Lantern", "MushroomCluster", "StoneMarker", "TrailMarker" } },
        new Surface { file = "mountain_path_tiles", title = "MountainPath",
            decor = new[] { "Snow", "Ice", "Pebbles", "Scree", "Crack", "Puddle", "AlpineFlowers", "Moss", "Footprints" },
            props = new[] { "Cairn", "TrailMarker", "Boulder", "Signpost", "Pine", "RopePost", "StoneMarker" } },
        new Surface { file = "dock_tiles", title = "Dock",
            decor = new[] { "Puddle", "Seaweed", "FishBones", "Starfish", "Stain", "Rope", "WetPrints", "Feather", "Shells", "Splinters", "Knot" },
            props = new[] { "Bollard", "Crate", "Barrel", "RopeCoil", "LampPost", "Lifebuoy", "Bucket", "LobsterTrap", "FishCrate", "Cleat", "Ladder", "Signpost", "Buoy", "Anchor", "NetPile" } },
        new Surface { file = "dirt_path_tiles", title = "DirtPath",
            decor = new[] { "Footprints", "RutsH", "RutsV", "Puddle", "GrassTuft", "Pebbles", "Cracks", "Leaves", "Weeds", "Flowers" },
            props = new[] { "FencePost", "Signpost", "HayBale", "Barrel", "StoneMarker", "Stump", "Lantern" } },
    };

    // (column, row from the top) for each decor name, in order
    private static readonly int[,] DecorCells =
        { {3,3},{4,3},{5,1},{6,1},{7,1},{5,2},{6,2},{7,2},{5,3},{6,3},{7,3} };

    private static readonly string[] Edge9 =
        { "TopLeft", "Top", "TopRight", "Left", "Middle", "Right", "BottomLeft", "Bottom", "BottomRight" };

    private static readonly string[] Narrow16 =
        { "Dot", "N", "E", "NE", "S", "NS", "ES", "NES", "W", "NW", "EW", "NEW", "SW", "NSW", "ESW", "NESW" };

    [MenuItem("Farm/Biomes/Build Path Palettes")]
    public static void Build() { BuildSurfaces(null); }

    /// <summary>Builds only the dock palette (needs dock_tiles.png somewhere in your Assets).</summary>
    [MenuItem("Farm/Biomes/Build Dock Palette")]
    public static void BuildDock() { BuildSurfaces("Dock"); }

    private static void BuildSurfaces(string onlyTitle)
    {
        var log = new List<string>();
        foreach (Surface s in Surfaces)
        {
            if (onlyTitle != null && s.title != onlyTitle) continue;
            string sheetPath = FindSheet(s.file);
            if (sheetPath == null) { log.Add(s.title + ": " + s.file + ".png not found (skipped)"); continue; }

            EnsureFolder(OutRoot + "/Tiles/" + s.title);
            EnsureFolder(OutRoot + "/Palettes");

            Slice(sheetPath);
            Dictionary<string, Sprite> sprites = LoadSprites(sheetPath);

            // names for each cell
            var names = new Dictionary<string, string>();
            for (int v = 0; v < 8; v++) names[Key(v, 0)] = s.title + "_Fill_" + (v + 1);
            for (int i = 0; i < 9; i++) names[Key(i % 3, 1 + i / 3)] = s.title + "_Wide_" + Edge9[i];
            names[Key(3, 1)] = s.title + "_Inner_TL"; names[Key(4, 1)] = s.title + "_Inner_TR";
            names[Key(3, 2)] = s.title + "_Inner_BL"; names[Key(4, 2)] = s.title + "_Inner_BR";
            for (int i = 0; i < s.decor.Length && i < 11; i++) names[Key(DecorCells[i, 0], DecorCells[i, 1])] = s.title + "_" + s.decor[i];
            for (int i = 0; i < s.props.Length && i < 16; i++) names[Key(i % 4, 4 + i / 4)] = s.title + "_" + s.props[i];
            for (int m = 0; m < 16; m++) names[Key(4 + m % 4, 4 + m / 4)] = s.title + "_Narrow_" + Narrow16[m];

            var tilesByCell = new Dictionary<string, TileBase>();
            var narrowSprites = new Sprite[16];
            foreach (KeyValuePair<string, Sprite> kv in sprites)
            {
                string tileName;
                if (!names.TryGetValue(kv.Key, out tileName)) continue;
                string tilePath = OutRoot + "/Tiles/" + s.title + "/" + tileName + ".asset";
                var tile = AssetDatabase.LoadAssetAtPath<Tile>(tilePath);
                if (tile == null)
                {
                    tile = ScriptableObject.CreateInstance<Tile>();
                    AssetDatabase.CreateAsset(tile, tilePath);
                }
                tile.sprite = kv.Value;
                tile.colliderType = Tile.ColliderType.None;
                EditorUtility.SetDirty(tile);
                tilesByCell[kv.Key] = tile;

                // Label the tile so the Farm Level Editor lists it: ground variants on the Ground layer, everything else on the Paths layer.
                bool isFill = kv.Key.EndsWith("_0") && kv.Key.Length == 3;
                AssetDatabase.SetLabels(tile, new[] { isFill ? FarmEditorLabels.Ground : FarmEditorLabels.Paths });
            }
            for (int m = 0; m < 16; m++)
            {
                Sprite sp;
                if (sprites.TryGetValue(Key(4 + m % 4, 4 + m / 4), out sp)) narrowSprites[m] = sp;
            }

            // the self-connecting tile
            string autoPath = OutRoot + "/Tiles/" + s.title + "/" + s.title + "_Auto.asset";
            var auto = AssetDatabase.LoadAssetAtPath<AutoPathTile>(autoPath);
            if (auto == null)
            {
                auto = ScriptableObject.CreateInstance<AutoPathTile>();
                AssetDatabase.CreateAsset(auto, autoPath);
            }
            auto.sprites = narrowSprites;
            auto.group = s.title;
            EditorUtility.SetDirty(auto);
            AssetDatabase.SetLabels(auto, new[] { FarmEditorLabels.Paths });

            MakePalette(OutRoot + "/Palettes/" + s.title + "_Palette.prefab", tilesByCell, auto);
            log.Add(s.title + ": " + tilesByCell.Count + " tiles + Auto tile + palette");
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        string msg = string.Join("\n", log.ToArray()) +
            "\n\nOpen Window > 2D > Tile Palette and pick e.g. AsphaltRoad_Palette. The Auto tile is in the row below the sheet." +
            "\nIn the Farm Level Editor the new tiles appear by themselves (Ground and Paths palettes, group names like Dock).";
        Debug.Log(msg);
        EditorUtility.DisplayDialog("Path palettes built", msg, "OK");
    }

    private static string Key(int c, int r) { return c + "_" + r; }

    private static string FindSheet(string file)
    {
        foreach (string g in AssetDatabase.FindAssets(file + " t:Texture2D"))
        {
            string p = AssetDatabase.GUIDToAssetPath(g);
            if (Path.GetFileNameWithoutExtension(p) == file) return p;
        }
        return null;
    }

    private static void Slice(string path)
    {
        var importer = (TextureImporter)AssetImporter.GetAtPath(path);
        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Multiple;
        importer.spritePixelsPerUnit = PixelsPerUnit;
        importer.filterMode = FilterMode.Point;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        importer.mipmapEnabled = false;
        importer.SaveAndReimport();

        var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        int cols = tex.width / Cell, rows = tex.height / Cell;
        string baseName = Path.GetFileNameWithoutExtension(path);

        var factory = new UnityEditor.U2D.Sprites.SpriteDataProviderFactories();
        factory.Init();
        UnityEditor.U2D.Sprites.ISpriteEditorDataProvider provider = factory.GetSpriteEditorDataProviderFromObject(importer);
        provider.InitSpriteEditorDataProvider();

        var rects = new List<SpriteRect>();
        for (int r = 0; r < rows; r++)
            for (int c = 0; c < cols; c++)
            {
                var rect = new SpriteRect();
                rect.name = baseName + "_" + c + "_" + r;               // row counted from the top
                rect.rect = new Rect(c * Cell, tex.height - (r + 1) * Cell, Cell, Cell);
                rect.alignment = SpriteAlignment.Center;
                rect.pivot = new Vector2(0.5f, 0.5f);
                rect.spriteID = GUID.Generate();
                rects.Add(rect);
            }

        provider.SetSpriteRects(rects.ToArray());
        var idProvider = provider.GetDataProvider<UnityEditor.U2D.Sprites.ISpriteNameFileIdDataProvider>();
        var pairs = new List<SpriteNameFileIdPair>();
        foreach (SpriteRect sr in rects) pairs.Add(new SpriteNameFileIdPair(sr.name, sr.spriteID));
        idProvider.SetNameFileIdPairs(pairs);
        provider.Apply();
        importer.SaveAndReimport();
    }

    private static Dictionary<string, Sprite> LoadSprites(string path)
    {
        var result = new Dictionary<string, Sprite>();
        string baseName = Path.GetFileNameWithoutExtension(path);
        foreach (Object o in AssetDatabase.LoadAllAssetsAtPath(path))
        {
            var s = o as Sprite;
            if (s == null || !s.name.StartsWith(baseName + "_")) continue;
            if (IsEmpty(s)) continue;
            result[s.name.Substring(baseName.Length + 1)] = s;
        }
        return result;
    }

    private static bool IsEmpty(Sprite s)
    {
        Texture2D src = s.texture;
        RenderTexture rt = RenderTexture.GetTemporary(src.width, src.height, 0);
        Graphics.Blit(src, rt);
        RenderTexture prev = RenderTexture.active;
        RenderTexture.active = rt;
        Rect r = s.textureRect;
        var copy = new Texture2D((int)r.width, (int)r.height, TextureFormat.RGBA32, false);
        copy.ReadPixels(new Rect(r.x, r.y, r.width, r.height), 0, 0);
        copy.Apply();
        RenderTexture.active = prev;
        RenderTexture.ReleaseTemporary(rt);

        bool empty = true;
        foreach (Color32 c in copy.GetPixels32()) if (c.a > 0) { empty = false; break; }
        Object.DestroyImmediate(copy);
        return empty;
    }

    private static void MakePalette(string prefabPath, Dictionary<string, TileBase> tilesByCell, TileBase auto)
    {
        var root = new GameObject(Path.GetFileNameWithoutExtension(prefabPath));
        root.AddComponent<Grid>();
        var layer = new GameObject("Layer1");
        layer.transform.SetParent(root.transform, false);
        var map = layer.AddComponent<Tilemap>();
        layer.AddComponent<TilemapRenderer>();

        foreach (KeyValuePair<string, TileBase> kv in tilesByCell)
        {
            string[] parts = kv.Key.Split('_');
            map.SetTile(new Vector3Int(int.Parse(parts[0]), 7 - int.Parse(parts[1]), 0), kv.Value);
        }
        map.SetTile(new Vector3Int(0, -1, 0), auto);                   // the Auto tile sits just under the sheet

        if (File.Exists(prefabPath)) AssetDatabase.DeleteAsset(prefabPath);
        GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
        Object.DestroyImmediate(root);

        var settings = ScriptableObject.CreateInstance<GridPalette>();
        settings.name = "Palette Settings";
        AssetDatabase.AddObjectToAsset(settings, prefab);
        AssetDatabase.SaveAssets();
    }

    private static void EnsureFolder(string path)
    {
        string[] parts = path.Split('/');
        string current = parts[0];
        for (int i = 1; i < parts.Length; i++)
        {
            string next = current + "/" + parts[i];
            if (!AssetDatabase.IsValidFolder(next)) AssetDatabase.CreateFolder(current, parts[i]);
            current = next;
        }
    }
}
