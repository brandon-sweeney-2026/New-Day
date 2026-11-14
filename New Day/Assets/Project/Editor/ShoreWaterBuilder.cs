using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Tilemaps;

/// <summary>
/// Makes the water's shore match the ground it touches. Menu: Farm > Biomes > Build Shore-Matching Water.
///
/// The Kiln water auto-tile has grass painted into its edge pieces, so a pond next to sand or dirt still had a grass rim.
/// This builds a copy of every shore piece for each ground it can find - Dirt, Sand, and every biome ground
/// (Moss, Leaves, Ferns, Roots, Asphalt, Sidewalk, Brick, ParkGrass, Rock, Gravel, Snow, AlpineGrass) - by recolouring only the
/// LAND pixels of the edge (the water, foam and animation stay untouched), and stores them inside the Water tile itself.
/// The Water tile then looks at the tiles around each water cell and picks the matching shore. Water you already painted updates
/// by itself, no repainting. Grass and unknown ground keep the original pieces.
///
/// Run Farm > Kiln Harvest > 1. Import Pack Assets (and Farm > Biomes > Build Tile Palettes for the biome grounds) first.
/// Safe to run again. This file must live in a folder named "Editor".
/// </summary>
public static class ShoreWaterBuilder
{
    private class Source
    {
        public string name;
        public List<TileBase> matchTiles = new List<TileBase>();
        public List<string> keywords = new List<string>();
        public List<Sprite> sprites = new List<Sprite>();
        public bool stilts;          // docks: the shore is the underside of the deck, with posts standing in the water
    }

    private static readonly Dictionary<string, Texture2D> loaded = new Dictionary<string, Texture2D>();

    [MenuItem("Farm/Biomes/Build Shore-Matching Water")]
    public static void Build()
    {
        loaded.Clear();
        var waters = new List<AutoTile>();
        foreach (string guid in AssetDatabase.FindAssets("Water t:AutoTile"))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            if (Path.GetFileNameWithoutExtension(path) != "Water") continue;
            var w = AssetDatabase.LoadAssetAtPath<AutoTile>(path);
            if (w != null) waters.Add(w);
        }
        if (waters.Count == 0)
        {
            EditorUtility.DisplayDialog("Shore-matching water", "No 'Water' auto-tile was found. Run Farm > Kiln Harvest > 1. Import Pack Assets first.", "OK");
            return;
        }

        var log = new System.Text.StringBuilder();
        int totalKinds = 0;
        foreach (AutoTile water in waters)
        {
            try { totalKinds += BuildFor(water, log); }
            catch (System.Exception ex) { log.AppendLine("Failed on " + AssetDatabase.GetAssetPath(water) + ": " + ex.Message); }
        }
        AssetDatabase.SaveAssets();
        RefreshOpenScene();
        EditorUtility.DisplayDialog("Shore-matching water",
            "Done: " + totalKinds + " shore looks added to " + waters.Count + " water tile(s).\n\n" + log +
            "\nWater next to sand, dirt and the biome grounds now gets a matching edge. If a pond didn't change, use Farm > Level Editors > Refresh Tile Edges.\n\nNot compiled in Unity - tell me about any red errors.", "OK");
    }

    [MenuItem("Farm/Level Editors/Refresh Tile Edges (Water etc.)", false, 34)]
    public static void RefreshOpenScene()
    {
        foreach (Tilemap map in Object.FindObjectsByType<Tilemap>(FindObjectsInactive.Include))
        {
            map.RefreshAllTiles();
            EditorUtility.SetDirty(map);
        }
        SceneView.RepaintAll();
    }

    // ------------------------------------------------------------------ one water tile
    private static int BuildFor(AutoTile water, System.Text.StringBuilder log)
    {
        string waterPath = AssetDatabase.GetAssetPath(water);
        string folder = Path.GetDirectoryName(waterPath).Replace('\\', '/');
        string outFolder = folder + "/Shore";
        EnsureFolder(outFolder);

        List<Source> sources = FindSources(folder);
        if (sources.Count == 0) { log.AppendLine(folder + ": no ground tiles found."); return 0; }

        // the 47 pieces and their frames
        var masks = new List<int>();
        var frameSprites = new List<Sprite[]>();
        int maxFrames = 1;
        for (int m = 0; m < water.pieces.Length; m++)
        {
            AutoTile.Piece p = water.pieces[m];
            if (p == null || p.frames == null || p.frames.Length == 0) continue;
            masks.Add(m);
            frameSprites.Add(p.frames);
            if (p.frames.Length > maxFrames) maxFrames = p.frames.Length;
        }
        if (masks.Count == 0) { log.AppendLine(folder + ": the Water tile has no pieces."); return 0; }

        // read every water frame once
        var original = new Color32[masks.Count][][];
        Color32 fill = default(Color32);
        var counts = new Dictionary<int, int>();
        for (int i = 0; i < masks.Count; i++)
        {
            original[i] = new Color32[frameSprites[i].Length][];
            for (int f = 0; f < frameSprites[i].Length; f++)
            {
                original[i][f] = Pixels(frameSprites[i][f]);
                if (original[i][f] == null) throw new System.Exception("could not read the water picture");
                foreach (Color32 c in original[i][f])
                    if (c.a > 0 && !IsWater(c))
                    {
                        int n; int key = Key(c); counts.TryGetValue(key, out n); counts[key] = n + 1;
                    }
            }
        }
        if (counts.Count == 0) { log.AppendLine(folder + ": no land pixels in the water edges."); return 0; }
        int best = -1;
        foreach (KeyValuePair<int, int> kv in counts) if (kv.Value > best) { best = kv.Value; fill = FromKey(kv.Key); }
        float fillLum = Lum(fill);

        var kinds = new List<AutoTile.ShoreKind>();
        foreach (Source src in sources)
        {
            // the target ground: its commonest colour, and its pixels to sample for the flat part of the shore
            var srcPixels = new List<Color32[]>();
            var srcCounts = new Dictionary<int, int>();
            foreach (Sprite s in src.sprites)
            {
                Color32[] px = Pixels(s);
                if (px == null || px.Length != 256) continue;       // only 16x16 grounds
                srcPixels.Add(px);
                foreach (Color32 c in px) if (c.a > 0) { int n; int key = Key(c); srcCounts.TryGetValue(key, out n); srcCounts[key] = n + 1; }
            }
            if (srcPixels.Count == 0) continue;
            Color32 baseColor = default(Color32); int top = -1;
            foreach (KeyValuePair<int, int> kv in srcCounts) if (kv.Value > top) { top = kv.Value; baseColor = FromKey(kv.Key); }

            // one sheet: frames across, pieces down (piece 0 at the top)
            int cell = 16;
            int sheetW = maxFrames * cell, sheetH = masks.Count * cell;
            var tex = new Texture2D(sheetW, sheetH, TextureFormat.RGBA32, false);
            var clear = new Color32[sheetW * sheetH];
            tex.SetPixels32(clear);
            for (int i = 0; i < masks.Count; i++)
            {
                Color32[] sample = srcPixels[i % srcPixels.Count];
                for (int f = 0; f < original[i].Length; f++)
                {
                    Color32[] o = original[i][f];
                    var outPx = new Color32[256];
                    for (int k = 0; k < 256; k++)
                    {
                        Color32 c = o[k];
                        if (c.a == 0 || IsWater(c)) { outPx[k] = c; continue; }
                        if (c.r == fill.r && c.g == fill.g && c.b == fill.b) { Color32 sp = sample[k]; sp.a = 255; outPx[k] = sp; continue; }
                        float factor = Mathf.Clamp(Lum(c) / fillLum, 0.35f, 1.1f);
                        outPx[k] = new Color32(Scale(baseColor.r, factor), Scale(baseColor.g, factor), Scale(baseColor.b, factor), 255);
                    }
                    if (src.stilts) AddStilts(outPx, o, masks[i]);
                    tex.SetPixels32(f * cell, (masks.Count - 1 - i) * cell, cell, cell, outPx);
                }
            }
            tex.Apply();

            string safe = Regex.Replace(src.name, "[^A-Za-z0-9_]", "");
            string pngPath = outFolder + "/Shore_" + safe + ".png";
            File.WriteAllBytes(pngPath, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
            AssetDatabase.ImportAsset(pngPath, ImportAssetOptions.ForceUpdate);
            SliceSheet(pngPath, masks.Count, maxFrames, cell);

            var byName = new Dictionary<string, Sprite>();
            foreach (Object o in AssetDatabase.LoadAllAssetRepresentationsAtPath(pngPath))
            {
                var sp = o as Sprite;
                if (sp != null) byName[sp.name] = sp;
            }

            var kind = new AutoTile.ShoreKind { name = src.name, pieces = new AutoTile.Piece[256], matchTiles = src.matchTiles.ToArray(), keywords = src.keywords.ToArray() };
            for (int i = 0; i < masks.Count; i++)
            {
                var frames = new List<Sprite>();
                for (int f = 0; f < original[i].Length; f++)
                {
                    Sprite sp;
                    if (byName.TryGetValue("shore_" + i + "_" + f, out sp)) frames.Add(sp);
                }
                kind.pieces[masks[i]] = new AutoTile.Piece { frames = frames.ToArray() };
            }
            kinds.Add(kind);
        }

        water.shoreKinds = kinds.ToArray();
        EditorUtility.SetDirty(water);
        log.AppendLine(folder + ": " + kinds.Count + " looks (" + string.Join(", ", kinds.ConvertAll(k => k.name).ToArray()) + ")");
        return kinds.Count;
    }

    // ------------------------------------------------------------------ which grounds exist
    private static List<Source> FindSources(string seasonFolder)
    {
        var list = new List<Source>();

        // Kiln: dirt and sand, from the same season folder as the Water tile
        AddKiln(list, seasonFolder, "Dirt", "DirtFlat", "DirtPatch", "dirt");
        AddKiln(list, seasonFolder, "Sand", "SandFlat", "SandPatch", "sand");

        // Dock planks (Dock_Fill_1..8 made by Build Path Palettes): the water gets stilts instead of a grass bank
        var dock = new Source { name = "Dock", stilts = true };
        dock.keywords.Add("dock");
        var dockRule = new Regex("^Dock_Fill_[1-8]$");
        foreach (string guid in AssetDatabase.FindAssets("Dock_Fill t:Tile"))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            if (!dockRule.IsMatch(Path.GetFileNameWithoutExtension(path))) continue;
            var tile = AssetDatabase.LoadAssetAtPath<Tile>(path);
            if (tile == null || tile.sprite == null) continue;
            dock.matchTiles.Add(tile);
            dock.sprites.Add(tile.sprite);
        }
        if (dock.sprites.Count > 0) list.Add(dock);

        // Biome grounds: Biome_Ground_1..4 (made by Build Tile Palettes)
        var groups = new Dictionary<string, Source>();
        var reg = new Regex("^(Forest|City|Mountain)_(Moss|Leaves|Ferns|Roots|Asphalt|Sidewalk|Brick|ParkGrass|Rock|Gravel|Snow|AlpineGrass)_([1-4])$");
        foreach (string guid in AssetDatabase.FindAssets("t:Tile"))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            Match m = reg.Match(Path.GetFileNameWithoutExtension(path));
            if (!m.Success) continue;
            var tile = AssetDatabase.LoadAssetAtPath<Tile>(path);
            if (tile == null || tile.sprite == null) continue;
            string key = m.Groups[1].Value + m.Groups[2].Value;
            Source s;
            if (!groups.TryGetValue(key, out s)) { s = new Source { name = key }; groups[key] = s; }
            s.matchTiles.Add(tile);
            s.sprites.Add(tile.sprite);
        }
        foreach (Source s in groups.Values) list.Add(s);
        return list;
    }

    private static void AddKiln(List<Source> list, string folder, string name, string flatName, string patchName, string keyword)
    {
        var s = new Source { name = name };
        s.keywords.Add(keyword);
        var flat = AssetDatabase.LoadAssetAtPath<RandomGroundTile>(folder + "/" + flatName + ".asset");
        if (flat != null)
        {
            s.matchTiles.Add(flat);
            if (flat.variants != null)
                foreach (RandomGroundTile.Variant v in flat.variants)
                    if (v != null && v.sprite != null && v.weight >= 0.5f) s.sprites.Add(v.sprite);
        }
        var patch = AssetDatabase.LoadAssetAtPath<AutoTile>(folder + "/" + patchName + ".asset");
        if (patch != null)
        {
            s.matchTiles.Add(patch);
            if (s.sprites.Count == 0 && patch.previewSprite != null) s.sprites.Add(patch.previewSprite);
        }
        if (s.sprites.Count > 0) list.Add(s);
    }

    // ------------------------------------------------------------------ stilts
    private static readonly Color32 PostLight = new Color32(150, 108, 66, 255);
    private static readonly Color32 PostDark = new Color32(88, 60, 40, 255);
    private static readonly Color32 PostFoot = new Color32(207, 240, 244, 255);

    /// <summary>
    /// Draws wooden posts standing in the water along every side where the dock is. 'px' is the recoloured piece, 'orig' the original
    /// (to know which pixels were land), 'mask' says which neighbours are water (N=1, E=4, S=16, W=64). Rows count from the bottom.
    /// </summary>
    private static void AddStilts(Color32[] px, Color32[] orig, int mask)
    {
        int[] along = { 3, 4, 11, 12 };                 // two posts, each 2 pixels wide
        const int length = 5;
        System.Func<int, int, bool> land = (x, y) => orig[y * 16 + x].a > 0 && !IsWater(orig[y * 16 + x]);
        System.Action<int, int, int> put = (x, y, k) =>
        {
            if (x < 0 || x > 15 || y < 0 || y > 15 || land(x, y)) return;      // only ever over water
            px[y * 16 + x] = (k == 0) ? PostLight : (k == 1) ? PostDark : PostFoot;
        };

        foreach (int a in along)
        {
            int k = (a == 3 || a == 11) ? 0 : 1;
            if ((mask & 1) == 0 && land(a, 15))            // dock to the north: posts go down
            {
                int y = 15; while (y > 0 && land(a, y)) y--;
                for (int n = 0; n < length; n++) put(a, y - n, k);
                put(a, y - length, 2);
            }
            if ((mask & 16) == 0 && land(a, 0))            // dock to the south: posts go up
            {
                int y = 0; while (y < 15 && land(a, y)) y++;
                for (int n = 0; n < length; n++) put(a, y + n, k);
                put(a, y + length, 2);
            }
            if ((mask & 4) == 0 && land(15, a))            // dock to the east: posts go left
            {
                int x = 15; while (x > 0 && land(x, a)) x--;
                for (int n = 0; n < length; n++) put(x - n, a, k);
                put(x - length, a, 2);
            }
            if ((mask & 64) == 0 && land(0, a))            // dock to the west: posts go right
            {
                int x = 0; while (x < 15 && land(x, a)) x++;
                for (int n = 0; n < length; n++) put(x + n, a, k);
                put(x + length, a, 2);
            }
        }
    }

    // ------------------------------------------------------------------ pixels
    private static bool IsWater(Color32 c)
    {
        return c.b >= c.r + 25 || (c.r > 190 && c.g > 190 && c.b > 190);
    }

    private static int Key(Color32 c) { return (c.r << 16) | (c.g << 8) | c.b; }
    private static Color32 FromKey(int k) { return new Color32((byte)((k >> 16) & 255), (byte)((k >> 8) & 255), (byte)(k & 255), 255); }
    private static float Lum(Color32 c) { return 0.3f * c.r + 0.59f * c.g + 0.11f * c.b; }
    private static byte Scale(byte v, float f) { return (byte)Mathf.Clamp(Mathf.RoundToInt(v * f), 0, 255); }

    /// <summary>The exact pixels of a sprite (read from the PNG file, so the texture does not need Read/Write).</summary>
    private static Color32[] Pixels(Sprite sprite)
    {
        if (sprite == null || sprite.texture == null) return null;
        string path = AssetDatabase.GetAssetPath(sprite.texture);
        if (string.IsNullOrEmpty(path)) return null;
        Texture2D tex;
        if (!loaded.TryGetValue(path, out tex))
        {
            tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            if (!tex.LoadImage(File.ReadAllBytes(path))) return null;
            loaded[path] = tex;
        }
        if (tex.width != sprite.texture.width || tex.height != sprite.texture.height) return null;
        Rect r = sprite.textureRect;
        Color32[] all = tex.GetPixels32();
        return Crop(all, tex.width, (int)r.x, (int)r.y, (int)r.width, (int)r.height);
    }

    private static Color32[] Crop(Color32[] all, int texWidth, int x, int y, int w, int h)
    {
        var res = new Color32[w * h];
        for (int j = 0; j < h; j++)
            for (int i = 0; i < w; i++)
                res[j * w + i] = all[(y + j) * texWidth + (x + i)];
        return res;
    }

    private static void SliceSheet(string pngPath, int rows, int cols, int cell)
    {
        var imp = (TextureImporter)AssetImporter.GetAtPath(pngPath);
        imp.textureType = TextureImporterType.Sprite;
        imp.spriteImportMode = SpriteImportMode.Multiple;
        imp.spritePixelsPerUnit = 16f;
        imp.filterMode = FilterMode.Point;
        imp.mipmapEnabled = false;
        imp.textureCompression = TextureImporterCompression.Uncompressed;
        imp.alphaIsTransparency = true;
        var data = new List<SpriteMetaData>();
        for (int i = 0; i < rows; i++)
            for (int f = 0; f < cols; f++)
            {
                var m = new SpriteMetaData();
                m.name = "shore_" + i + "_" + f;
                m.rect = new Rect(f * cell, (rows - 1 - i) * cell, cell, cell);
                m.alignment = (int)SpriteAlignment.Center;
                m.pivot = new Vector2(0.5f, 0.5f);
                data.Add(m);
            }
#pragma warning disable 618
        imp.spritesheet = data.ToArray();
#pragma warning restore 618
        imp.SaveAndReimport();
    }

    private static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path)) return;
        string parent = Path.GetDirectoryName(path).Replace('\\', '/');
        EnsureFolder(parent);
        AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
    }
}
