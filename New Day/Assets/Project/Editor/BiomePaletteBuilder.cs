using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Tilemaps;

/// <summary>
/// Builds the 3 area palettes (Old Growth Forest, Dense City, Mountain Range). Menu: Farm > Biomes > Build Tile Palettes.
///
/// Before running it, copy the "Biomes" folder (forest_tiles.png, city_tiles.png, mountain_tiles.png) into your project,
/// for example Assets/Farm/Biomes. It then:
///   - cuts each sheet into 16x16 pixel-art sprites (Point filter, 16 pixels per unit),
///   - makes a Tile asset for every picture, with a clear name (Forest_Moss_1, City_Road_TopLeft, Mountain_Boulder...),
///   - makes one Tile Palette per area in Assets/Farm/Biomes/Palettes (open it in Window > 2D > Tile Palette).
///
/// Palette layout (the same in all three):
///   top 2 rows   : 4 ground types x 4 variants (paint a mix of variants so the ground doesn't look flat)
///   left 3x3     : PATH   (trail / road). Paint it on a layer ABOVE the ground. Edges are see-through.
///   middle 3x3   : FEATURE (stream / rooftop / rocky ridge). Also paint above the ground.
///   right 2x3    : decoration overlays (flowers, road markings, snow patches...)
///   bottom       : props (rocks, bushes, lamps, cars, pines...) and one big 2x2 prop in the corner.
///
/// Safe to run again. This file must live in a folder named "Editor".
/// </summary>
public static class BiomePaletteBuilder
{
    private const int Cell = 16;
    private const float PixelsPerUnit = 16f;
    private const string OutRoot = "Assets/Farm/Biomes";

    private class Biome
    {
        public string file, title;
        public string[] ground;     // 4 names
        public string path, feature;
        public string[] decor;      // 6 names
        public string[] props;      // names for the single-tile props, in sheet order
        public string big;
    }

    private static readonly Biome[] Biomes =
    {
        new Biome {
            file = "forest_tiles", title = "Forest",
            ground = new[] { "Moss", "Leaves", "Ferns", "Roots" },
            path = "Trail", feature = "Stream",
            decor = new[] { "WhiteFlowers", "PurpleFlowers", "MossyStones", "FallenLeaves", "Mushrooms", "FernTuft" },
            props = new[] { "Boulder", "SmallBoulder", "Bush", "BerryBush", "BlueberryBush", "Canopy1", "Canopy2", "Canopy3",
                            "Stump", "Log", "HollowLog", "Signpost", "Sapling", "BigMushroom", "BigFern", "Rock" },
            big = "GiantCanopy" },
        new Biome {
            file = "city_tiles", title = "City",
            ground = new[] { "Asphalt", "Sidewalk", "Brick", "ParkGrass" },
            path = "Road", feature = "Rooftop",
            decor = new[] { "LaneHorizontal", "LaneVertical", "CrosswalkHorizontal", "CrosswalkVertical", "Manhole", "Drain" },
            props = new[] { "AirCon", "Lamp", "Hydrant", "Bench", "Bin", "Planter", "Mailbox", "TrafficCone", "StreetSign", "Dumpster",
                            "NewsStand", "CarBlueUp", "CarRedUp", "CarYellowUp", "CarBlueSide", "CarRedSide", "Vent", "WaterTank" },
            big = "Fountain" },
        new Biome {
            file = "mountain_tiles", title = "Mountain",
            ground = new[] { "Rock", "Gravel", "Snow", "AlpineGrass" },
            path = "Trail", feature = "Ridge",
            decor = new[] { "SnowPatch", "IcePatch", "SmallRocks", "Pebbles", "Crack", "AlpineFlowers" },
            props = new[] { "Boulder", "SmallBoulder", "TanBoulder", "Pebble", "SnowBoulder", "Pine", "SnowPine", "Pine2", "DeadTree",
                            "Cairn", "CaveEntrance", "IceCrystal", "Tent", "Signpost", "Shrub", "BerryShrub" },
            big = "BigBoulder" },
    };

    private static readonly string[] Edge9 =
        { "TopLeft", "Top", "TopRight", "Left", "Middle", "Right", "BottomLeft", "Bottom", "BottomRight" };

    [MenuItem("Farm/Biomes/Build Tile Palettes")]
    public static void Build()
    {
        var log = new List<string>();
        foreach (Biome b in Biomes)
        {
            string[] found = AssetDatabase.FindAssets(b.file + " t:Texture2D");
            string sheetPath = null;
            foreach (string g in found)
            {
                string p = AssetDatabase.GUIDToAssetPath(g);
                if (Path.GetFileNameWithoutExtension(p) == b.file) { sheetPath = p; break; }
            }
            if (sheetPath == null) { log.Add(b.title + ": " + b.file + ".png not found (skipped)"); continue; }

            string folder = OutRoot;
            EnsureFolder(folder + "/Tiles/" + b.title);
            EnsureFolder(folder + "/Palettes");

            Slice(sheetPath);
            var sprites = LoadSprites(sheetPath);

            // name each cell
            var names = new Dictionary<string, string>();
            for (int g = 0; g < 4; g++)
                for (int v = 0; v < 4; v++)
                    names[Key((g % 2) * 4 + v, g / 2)] = b.title + "_" + b.ground[g] + "_" + (v + 1);
            for (int i = 0; i < 9; i++)
            {
                names[Key(i % 3, 2 + i / 3)] = b.title + "_" + b.path + "_" + Edge9[i];
                names[Key(3 + i % 3, 2 + i / 3)] = b.title + "_" + b.feature + "_" + Edge9[i];
            }
            for (int i = 0; i < 6; i++) names[Key(6 + i % 2, 2 + i / 2)] = b.title + "_" + b.decor[i];
            int pi = 0;
            for (int r = 5; r < 8; r++)
                for (int c = 0; c < 8; c++)
                {
                    if (c < 2 && r < 7) continue;                       // the big 2x2 prop's area
                    if (pi < b.props.Length) names[Key(c, r)] = b.title + "_" + b.props[pi];
                    pi++;
                }
            string[] quad = { "TL", "TR", "BL", "BR" };
            for (int q = 0; q < 4; q++) names[Key(q % 2, 5 + q / 2)] = b.title + "_" + b.big + "_" + quad[q];

            // make tiles, remembering the cell position of each
            var tilesByCell = new Dictionary<string, Tile>();
            foreach (KeyValuePair<string, Sprite> kv in sprites)
            {
                string cellKey = kv.Key;                                // "c_r"
                string tileName;
                if (!names.TryGetValue(cellKey, out tileName)) continue;
                string tilePath = folder + "/Tiles/" + b.title + "/" + tileName + ".asset";
                var tile = AssetDatabase.LoadAssetAtPath<Tile>(tilePath);
                if (tile == null)
                {
                    tile = ScriptableObject.CreateInstance<Tile>();
                    AssetDatabase.CreateAsset(tile, tilePath);
                }
                tile.sprite = kv.Value;
                tile.colliderType = Tile.ColliderType.None;
                EditorUtility.SetDirty(tile);
                tilesByCell[cellKey] = tile;
            }

            MakePalette(folder + "/Palettes/" + b.title + "_Palette.prefab", tilesByCell);
            log.Add(b.title + ": " + tilesByCell.Count + " tiles + palette");
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        string msg = string.Join("\n", log.ToArray()) +
            "\n\nOpen Window > 2D > Tile Palette and pick Forest_Palette, City_Palette or Mountain_Palette.";
        Debug.Log(msg);
        EditorUtility.DisplayDialog("Palettes built", msg, "OK");
    }

    private static string Key(int c, int r) { return c + "_" + r; }

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

    /// <summary>Returns cell key "c_r" -> sprite, skipping completely empty cells.</summary>
    private static Dictionary<string, Sprite> LoadSprites(string path)
    {
        var result = new Dictionary<string, Sprite>();
        string baseName = Path.GetFileNameWithoutExtension(path);
        foreach (Object o in AssetDatabase.LoadAllAssetsAtPath(path))
        {
            var s = o as Sprite;
            if (s == null || !s.name.StartsWith(baseName + "_")) continue;
            string cellKey = s.name.Substring(baseName.Length + 1);
            if (IsEmpty(s)) continue;
            result[cellKey] = s;
        }
        return result;
    }

    private static bool IsEmpty(Sprite s)
    {
        // Read pixels through a temporary readable copy so the import settings stay untouched.
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

    /// <summary>Makes a palette prefab laid out exactly like the sheet (cell column, row from the top).</summary>
    private static void MakePalette(string prefabPath, Dictionary<string, Tile> tilesByCell)
    {
        var root = new GameObject(Path.GetFileNameWithoutExtension(prefabPath));
        root.AddComponent<Grid>();
        var layer = new GameObject("Layer1");
        layer.transform.SetParent(root.transform, false);
        var map = layer.AddComponent<Tilemap>();
        layer.AddComponent<TilemapRenderer>();

        foreach (KeyValuePair<string, Tile> kv in tilesByCell)
        {
            string[] parts = kv.Key.Split('_');
            int c = int.Parse(parts[0]);
            int r = int.Parse(parts[1]);
            map.SetTile(new Vector3Int(c, 7 - r, 0), kv.Value);          // sheet row 0 is the top
        }

        if (File.Exists(prefabPath)) AssetDatabase.DeleteAsset(prefabPath);
        GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
        Object.DestroyImmediate(root);

        // This small settings asset is what makes Unity list the prefab in the Tile Palette window.
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
