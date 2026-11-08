using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.U2D.Sprites;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Tilemaps;

/// <summary>
/// One-click setup for the "Kiln Harvest Free Sampler" art pack (16x16 cozy farming).
///
///   Farm > Kiln Harvest > 1. Import Pack Assets
///       Fixes every texture's import settings (Sprite, 16 Pixels Per Unit, Point filter, correct pivots),
///       slices the sprite sheets, and generates:
///         - Tile assets: random grass, flat dirt/sand, and AUTO-TILES (edges and corners join up by themselves):
///           dirt patch, sand patch, animated water, tilled soil, wet soil, cobblestone, gravel, cliff, wooden fence
///         - ItemData assets (seeds, produce, tools...) and CropData assets (8 crops with growth stages)
///         - Prefabs for trees, the farmhouse and props, ready to place with the Level Editor
///
///   Farm > Kiln Harvest > 2. Create Farm Scene Layout
///       Builds a correctly configured Grid with all the tilemaps and a FarmManager that is already wired up.
///
/// Everything generated goes into a "_UnityAssets" folder inside the pack folder. Running the importer again is safe:
/// it will not overwrite items, crops or prefabs you have edited.
/// </summary>
public static class KilnHarvestImporter
{
    private const int Ppu = 16;
    private const int TileSize = 16;
    private const string TerrainFile = "Tilesets/Terrain/terrain_spring.png";
    private const string OutputFolderName = "_UnityAssets";

    // ------------------------------------------------------------------
    // Menu entry 1: import everything
    // ------------------------------------------------------------------

    [MenuItem("Farm/Kiln Harvest/1. Import Pack Assets")]
    public static void ImportPack()
    {
        string root = FindPackRoot();
        if (root == null)
        {
            EditorUtility.DisplayDialog("Kiln Harvest",
                "Could not find the pack in your project.\n\n" +
                "Copy the unzipped KILN_HARVEST_Free_Sampler folder somewhere inside your Assets folder " +
                "(for example Assets/Art/), wait for Unity to import it, then run this again.", "OK");
            return;
        }

        string outRoot = root + "/" + OutputFolderName;
        int tileCount = 0, itemCount = 0, cropCount = 0, prefabCount = 0;

        try
        {
            EnsureFolder(outRoot);

            // ---- Step 1: textures ----
            var terrainFiles = new List<string>();
            string[] pngs = Directory.GetFiles(AbsolutePath(root), "*.png", SearchOption.AllDirectories);
            for (int i = 0; i < pngs.Length; i++)
            {
                string rel = Path.GetRelativePath(AbsolutePath(root), pngs[i]).Replace('\\', '/');
                string assetPath = root + "/" + rel;

                EditorUtility.DisplayProgressBar("Kiln Harvest", "Importing textures: " + rel, 0.45f * i / pngs.Length);

                TexPlan plan = PlanFor(rel);
                switch (plan.kind)
                {
                    case TexKind.Skip:
                        break;
                    case TexKind.Single:
                        ImportSingle(assetPath, plan.pivot);
                        break;
                    case TexKind.Grid:
                        SliceGrid(assetPath, plan.cellW, plan.cellH, Path.GetFileNameWithoutExtension(rel), plan.pivot);
                        if (rel.StartsWith("Tilesets/Terrain/terrain_")) terrainFiles.Add(assetPath);
                        break;
                }
            }

            // ---- Step 2: tiles ----
            EditorUtility.DisplayProgressBar("Kiln Harvest", "Creating tiles", 0.5f);
            foreach (string terrainPath in terrainFiles)
                tileCount += BuildSeasonTiles(terrainPath, outRoot);

            tileCount += BuildFenceTiles(root + "/Tilesets/Fences/fence_wood.png", outRoot);

            // ---- Step 3: items and crops ----
            EditorUtility.DisplayProgressBar("Kiln Harvest", "Creating items and crops", 0.7f);
            var itemsByFile = BuildItems(root, outRoot, out itemCount);
            cropCount = BuildCrops(root, outRoot, itemsByFile);

            // ---- Step 4: prefabs ----
            EditorUtility.DisplayProgressBar("Kiln Harvest", "Creating prefabs", 0.85f);
            prefabCount = BuildPrefabs(root, outRoot);
        }
        finally
        {
            EditorUtility.ClearProgressBar();
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        EditorUtility.DisplayDialog("Kiln Harvest",
            "Import finished.\n\n" +
            "Tiles: " + tileCount + "\nItems: " + itemCount + "\nCrops: " + cropCount + "\nPrefabs: " + prefabCount +
            "\n\nEverything is in:\n" + outRoot +
            "\n\nNext: Farm > Kiln Harvest > 2. Create Farm Scene Layout.", "OK");
    }

    // ------------------------------------------------------------------
    // Menu entry 2: scene layout
    // ------------------------------------------------------------------

    [MenuItem("Farm/Kiln Harvest/2. Create Farm Scene Layout")]
    public static void CreateSceneLayout()
    {
        string root = FindPackRoot();
        if (root == null)
        {
            EditorUtility.DisplayDialog("Kiln Harvest", "Pack not found. Run \"1. Import Pack Assets\" first.", "OK");
            return;
        }

        string tilesFolder = root + "/" + OutputFolderName + "/Tiles/Spring";
        var grass = AssetDatabase.LoadAssetAtPath<TileBase>(tilesFolder + "/Grass.asset");
        var dirtFlat = AssetDatabase.LoadAssetAtPath<TileBase>(tilesFolder + "/DirtFlat.asset");
        var dirtPatch = AssetDatabase.LoadAssetAtPath<TileBase>(tilesFolder + "/DirtPatch.asset");
        var tilled = AssetDatabase.LoadAssetAtPath<TileBase>(tilesFolder + "/Tilled.asset");
        var wet = AssetDatabase.LoadAssetAtPath<TileBase>(tilesFolder + "/Wet.asset");
        if (grass == null || tilled == null || wet == null)
        {
            EditorUtility.DisplayDialog("Kiln Harvest", "The generated tiles were not found. Run \"1. Import Pack Assets\" first.", "OK");
            return;
        }

        if (Object.FindAnyObjectByType<Grid>() != null)
        {
            bool go = EditorUtility.DisplayDialog("Kiln Harvest",
                "This scene already contains a Grid. Create another one anyway?", "Create another", "Cancel");
            if (!go) return;
        }

        // Grid: cell size 1 unit = one 16 px tile at 16 Pixels Per Unit.
        var gridObject = new GameObject("Grid", typeof(Grid));
        Undo.RegisterCreatedObjectUndo(gridObject, "Create Farm Layout");
        gridObject.GetComponent<Grid>().cellSize = new Vector3(1f, 1f, 0f);

        Tilemap ground = MakeTilemap(gridObject.transform, "Ground", 0, true);
        Tilemap paths = MakeTilemap(gridObject.transform, "Paths", 1, false);
        Tilemap soil = MakeTilemap(gridObject.transform, "Soil", 2, false);
        Tilemap wetSoil = MakeTilemap(gridObject.transform, "WetSoil", 3, false);
        Tilemap fences = MakeTilemap(gridObject.transform, "Fences", 4, true);

        var objects = new GameObject("Objects");
        Undo.RegisterCreatedObjectUndo(objects, "Create Farm Layout");

        // Start with a field of grass so there is something to paint on.
        for (int x = -15; x < 15; x++)
            for (int y = -10; y < 10; y++)
                ground.SetTile(new Vector3Int(x, y, 0), grass);

        // FarmManager, fully wired up.
        var managerObject = new GameObject("FarmManager");
        Undo.RegisterCreatedObjectUndo(managerObject, "Create Farm Layout");
        var manager = managerObject.AddComponent<FarmManager>();

        var so = new SerializedObject(manager);
        SetRef(so, "groundTilemap", ground);
        SetRef(so, "soilTilemap", soil);
        SetRef(so, "waterTilemap", wetSoil);
        SetRef(so, "tilledSoilTile", tilled);
        SetRef(so, "wateredSoilTile", wet);

        var tillable = new List<TileBase>();
        if (grass != null) tillable.Add(grass);
        if (dirtFlat != null) tillable.Add(dirtFlat);
        if (dirtPatch != null) tillable.Add(dirtPatch);
        SerializedProperty tillableProp = so.FindProperty("tillableGroundTiles");
        if (tillableProp != null)
        {
            tillableProp.arraySize = tillable.Count;
            for (int i = 0; i < tillable.Count; i++)
                tillableProp.GetArrayElementAtIndex(i).objectReferenceValue = tillable[i];
        }
        so.ApplyModifiedProperties();

        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        Selection.activeGameObject = gridObject;

        FarmLevelEditorWindow.Open();

        EditorUtility.DisplayDialog("Kiln Harvest",
            "Farm layout created:\n\n" +
            "Grid > Ground, Paths, Soil, WetSoil, Fences\nObjects\nFarmManager (already connected)\n\n" +
            "The Farm Level Editor is open. Pick a layer, pick something from the palette, and paint in the Scene view.\n\n" +
            "Water tiles and fences are solid. Remember to give your Player a Collider2D.", "OK");
    }

    private static Tilemap MakeTilemap(Transform parent, string name, int sortingOrder, bool solid)
    {
        var go = new GameObject(name, typeof(Tilemap), typeof(TilemapRenderer));
        go.transform.SetParent(parent, false);
        go.GetComponent<TilemapRenderer>().sortingOrder = sortingOrder;
        if (solid) go.AddComponent<TilemapCollider2D>();
        Undo.RegisterCreatedObjectUndo(go, "Create Farm Layout");
        return go.GetComponent<Tilemap>();
    }

    private static void SetRef(SerializedObject so, string property, Object value)
    {
        SerializedProperty p = so.FindProperty(property);
        if (p != null) p.objectReferenceValue = value;
    }

    // ------------------------------------------------------------------
    // Texture plans: what each file in the pack should become
    // ------------------------------------------------------------------

    private enum TexKind { Skip, Single, Grid }

    private struct TexPlan
    {
        public TexKind kind;
        public Vector2 pivot;
        public int cellW, cellH;
    }

    private static TexPlan SinglePlan(float px, float py)
    {
        return new TexPlan { kind = TexKind.Single, pivot = new Vector2(px, py) };
    }

    private static TexPlan GridPlan(int w, int h, float px, float py)
    {
        return new TexPlan { kind = TexKind.Grid, cellW = w, cellH = h, pivot = new Vector2(px, py) };
    }

    /// <summary>rel = path relative to the pack folder, with forward slashes.</summary>
    private static TexPlan PlanFor(string rel)
    {
        if (rel.StartsWith("Godot/") || rel.StartsWith("Tiled/") || rel.StartsWith(OutputFolderName + "/"))
            return new TexPlan { kind = TexKind.Skip };

        if (rel.StartsWith("Tilesets/")) return GridPlan(TileSize, TileSize, 0.5f, 0.5f);

        // Props stand on the ground: pivot at the bottom centre of the sprite.
        if (rel == "Props/field/scarecrow_classic_2f.png") return GridPlan(16, 32, 0.5f, 0f);
        if (rel.StartsWith("Props/")) return SinglePlan(0.5f, 0f);

        // Crops are 16x32; the lower 16 px cover the soil tile, so the pivot is the centre of the lower half.
        if (rel.StartsWith("Crops/")) return SinglePlan(0.5f, 0.25f);

        if (rel.StartsWith("Nature/")) return SinglePlan(0.5f, 4f / 64f);        // tree base is 4 px above the bottom
        if (rel.StartsWith("Buildings/")) return SinglePlan(0.5f, 4f / 80f);

        if (rel.StartsWith("Characters/Farmer/")) return GridPlan(48, 48, 0.5f, 6f / 48f);   // feet at y=42 of 48
        if (rel.StartsWith("Animals/chicken/")) return GridPlan(16, 16, 0.5f, 3f / 16f);
        if (rel.StartsWith("Animals/cow/")) return GridPlan(32, 32, 0.5f, 3f / 32f);

        return SinglePlan(0.5f, 0.5f);
    }

    // ------------------------------------------------------------------
    // Texture import helpers
    // ------------------------------------------------------------------

    private static TextureImporter ConfigureCommon(string path)
    {
        var importer = AssetImporter.GetAtPath(path) as TextureImporter;
        if (importer == null) return null;

        importer.textureType = TextureImporterType.Sprite;
        importer.spritePixelsPerUnit = Ppu;
        importer.filterMode = FilterMode.Point;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        importer.mipmapEnabled = false;
        importer.alphaIsTransparency = true;
        importer.wrapMode = TextureWrapMode.Clamp;
        return importer;
    }

    private static void ImportSingle(string path, Vector2 pivot)
    {
        TextureImporter importer = ConfigureCommon(path);
        if (importer == null) return;

        importer.spriteImportMode = SpriteImportMode.Single;

        var settings = new TextureImporterSettings();
        importer.ReadTextureSettings(settings);
        settings.spriteAlignment = (int)SpriteAlignment.Custom;
        settings.spritePivot = pivot;
        settings.spriteMeshType = SpriteMeshType.FullRect;
        importer.SetTextureSettings(settings);

        importer.SaveAndReimport();
    }

    private static int SpriteCount(string path)
    {
        return AssetDatabase.LoadAllAssetRepresentationsAtPath(path).OfType<Sprite>().Count();
    }

    private static void SliceGrid(string path, int cellW, int cellH, string namePrefix, Vector2 pivot)
    {
        TextureImporter importer = ConfigureCommon(path);
        if (importer == null) return;

        int texW, texH;
        if (!ReadPngSize(path, out texW, out texH)) return;

        int cols = texW / cellW;
        int rows = texH / cellH;

        importer.spriteImportMode = SpriteImportMode.Multiple;
        importer.SaveAndReimport();

        // Already sliced the right way (e.g. you are running the importer a second time): leave it alone.
        if (SpriteCount(path) == cols * rows) return;

        var factory = new SpriteDataProviderFactories();
        factory.Init();
        ISpriteEditorDataProvider provider = factory.GetSpriteEditorDataProviderFromObject(importer);
        provider.InitSpriteEditorDataProvider();

        var rects = new List<SpriteRect>();
        for (int r = 0; r < rows; r++)
        {
            for (int c = 0; c < cols; c++)
            {
                var rect = new SpriteRect();
                rect.name = namePrefix + "_" + c + "_" + r;                       // column_row, counted from the TOP-left
                rect.rect = new Rect(c * cellW, texH - (r + 1) * cellH, cellW, cellH);
                rect.alignment = SpriteAlignment.Custom;
                rect.pivot = pivot;
                rect.spriteID = UnityEngine.GUID.Generate();
                rects.Add(rect);
            }
        }

        provider.SetSpriteRects(rects.ToArray());

        var idProvider = provider.GetDataProvider<ISpriteNameFileIdDataProvider>();
        idProvider.SetNameFileIdPairs(rects.Select(x => new SpriteNameFileIdPair(x.name, x.spriteID)).ToList());

        provider.Apply();
        importer.SaveAndReimport();
    }

    /// <summary>Reads the width and height straight from the PNG header (no need to load the image).</summary>
    private static bool ReadPngSize(string assetPath, out int width, out int height)
    {
        width = 0;
        height = 0;
        try
        {
            using (var stream = new FileStream(AbsolutePath(assetPath), FileMode.Open, FileAccess.Read))
            {
                var header = new byte[24];
                if (stream.Read(header, 0, 24) < 24) return false;
                width = (header[16] << 24) | (header[17] << 16) | (header[18] << 8) | header[19];
                height = (header[20] << 24) | (header[21] << 16) | (header[22] << 8) | header[23];
                return width > 0 && height > 0;
            }
        }
        catch (System.Exception)
        {
            return false;
        }
    }

    private static Dictionary<string, Sprite> LoadSprites(string path)
    {
        var map = new Dictionary<string, Sprite>();
        foreach (Object o in AssetDatabase.LoadAllAssetRepresentationsAtPath(path))
        {
            var sprite = o as Sprite;
            if (sprite != null) map[sprite.name] = sprite;
        }
        return map;
    }

    // ------------------------------------------------------------------
    // Tiles
    // ------------------------------------------------------------------

    private static int BuildSeasonTiles(string terrainPath, string outRoot)
    {
        string texName = Path.GetFileNameWithoutExtension(terrainPath);          // terrain_spring
        string season = texName.Replace("terrain_", "");
        string seasonName = char.ToUpper(season[0]) + season.Substring(1);        // Spring
        string folder = outRoot + "/Tiles/" + seasonName;
        EnsureFolder(folder);

        Dictionary<string, Sprite> sprites = LoadSprites(terrainPath);
        if (sprites.Count == 0) return 0;

        int made = 0;

        // ---- Random flat fills ----
        var grassVariants = new List<RandomGroundTile.Variant>();
        for (int c = 0; c < 8; c++) AddVariant(grassVariants, sprites, texName, c, 0, 1f);
        for (int c = 8; c < 16; c++) AddVariant(grassVariants, sprites, texName, c, 0, 0.06f);   // flowers, tufts: rare
        MakeRandomTile(folder + "/Grass.asset", grassVariants, FarmEditorLabels.Ground);
        made++;

        var dirtVariants = new List<RandomGroundTile.Variant>();
        for (int c = 0; c < 4; c++) AddVariant(dirtVariants, sprites, texName, c, 1, 1f);
        for (int c = 4; c < 12; c++) AddVariant(dirtVariants, sprites, texName, c, 1, 0.05f);
        MakeRandomTile(folder + "/DirtFlat.asset", dirtVariants, FarmEditorLabels.Ground);
        made++;

        var sandVariants = new List<RandomGroundTile.Variant>();
        for (int c = 12; c < 16; c++) AddVariant(sandVariants, sprites, texName, c, 1, 1f);
        MakeRandomTile(folder + "/SandFlat.asset", sandVariants, FarmEditorLabels.Ground);
        made++;

        // ---- Autotile terrains ----
        foreach (KilnTerrainData.TerrainDef def in KilnTerrainData.Terrains)
        {
            string assetName;
            string label;
            Tile.ColliderType collider = Tile.ColliderType.None;

            switch (def.name)
            {
                case "dirt_patch": assetName = "DirtPatch"; label = FarmEditorLabels.Ground; break;
                case "sand_patch": assetName = "SandPatch"; label = FarmEditorLabels.Ground; break;
                case "water": assetName = "Water"; label = FarmEditorLabels.Ground; collider = Tile.ColliderType.Grid; break;
                case "tilled": assetName = "Tilled"; label = FarmEditorLabels.Soil; break;
                case "wet": assetName = "Wet"; label = FarmEditorLabels.Wet; break;
                case "cobble": assetName = "Cobble"; label = FarmEditorLabels.Paths; break;
                case "gravel": assetName = "Gravel"; label = FarmEditorLabels.Paths; break;
                case "cliff": assetName = "Cliff"; label = FarmEditorLabels.Paths; break;
                default: continue;
            }

            // The sampler's "north-west corner" piece (mask 28) is a copy of the "inner corner" piece (mask 20) for
            // dirt and sand, which leaves a small grass notch. Build a corrected copy for those two.
            Dictionary<int, Sprite> overrides = null;
            if (def.name == "dirt_patch" || def.name == "sand_patch")
                overrides = BuildCornerFix(terrainPath, def, folder + "/Fixes", season + "_" + def.name);

            MakeBlobTile(folder + "/" + assetName + ".asset", def, sprites, texName, overrides, collider, label);
            made++;
        }

        return made;
    }

    private static void AddVariant(List<RandomGroundTile.Variant> list, Dictionary<string, Sprite> sprites,
                                   string texName, int col, int row, float weight)
    {
        Sprite s;
        if (!sprites.TryGetValue(texName + "_" + col + "_" + row, out s)) return;
        list.Add(new RandomGroundTile.Variant { sprite = s, weight = weight });
    }

    private static void MakeRandomTile(string path, List<RandomGroundTile.Variant> variants, string label)
    {
        var tile = LoadOrCreate<RandomGroundTile>(path);
        tile.variants = variants.ToArray();
        tile.previewSprite = variants.Count > 0 ? variants[0].sprite : null;
        EditorUtility.SetDirty(tile);
        AssetDatabase.SetLabels(tile, new[] { label });
    }

    private static void MakeBlobTile(string path, KilnTerrainData.TerrainDef def, Dictionary<string, Sprite> sprites,
                                     string texName, Dictionary<int, Sprite> overrides, Tile.ColliderType collider, string label)
    {
        var tile = LoadOrCreate<AutoTile>(path);
        tile.mode = AutoTile.Mode.Blob47;
        tile.pieces = new AutoTile.Piece[256];
        tile.colliderType = collider;
        tile.animationFramesPerSecond = (def.frames > 1 && def.frameMs > 0) ? 1000f / def.frameMs : 4f;

        for (int i = 0; i < KilnTerrainData.BlobMasks.Length; i++)
        {
            int mask = KilnTerrainData.BlobMasks[i];
            var frames = new List<Sprite>();

            for (int f = 0; f < def.frames; f++)
            {
                Sprite s;
                if (sprites.TryGetValue(texName + "_" + (def.cols[i] + f) + "_" + def.rows[i], out s)) frames.Add(s);
            }

            Sprite fix;
            if (overrides != null && overrides.TryGetValue(mask, out fix) && fix != null)
            {
                frames.Clear();
                frames.Add(fix);
            }

            tile.pieces[mask] = new AutoTile.Piece { frames = frames.ToArray() };

            if (mask == 255 && frames.Count > 0) tile.previewSprite = frames[0];
        }

        EditorUtility.SetDirty(tile);
        AssetDatabase.SetLabels(tile, new[] { label });
    }

    private static int BuildFenceTiles(string fencePath, string outRoot)
    {
        Dictionary<string, Sprite> sprites = LoadSprites(fencePath);
        if (sprites.Count == 0) return 0;

        string texName = Path.GetFileNameWithoutExtension(fencePath);            // fence_wood
        string folder = outRoot + "/Tiles/Fences";
        EnsureFolder(folder);

        var fence = LoadOrCreate<AutoTile>(folder + "/FenceWood.asset");
        fence.mode = AutoTile.Mode.Cardinal4;
        fence.pieces = new AutoTile.Piece[16];
        fence.colliderType = Tile.ColliderType.Grid;

        for (int mask = 0; mask < 16; mask++)
        {
            Sprite s;
            if (!sprites.TryGetValue(texName + "_" + KilnTerrainData.FenceCols[mask] + "_" + KilnTerrainData.FenceRows[mask], out s)) continue;
            fence.pieces[mask] = new AutoTile.Piece { frames = new[] { s } };
            if (mask == 10) fence.previewSprite = s;   // 10 = east + west = a horizontal run of fence
        }
        EditorUtility.SetDirty(fence);
        AssetDatabase.SetLabels(fence, new[] { FarmEditorLabels.Fences });

        int made = 1;
        made += MakeGate(folder + "/GateHorizontalClosed.asset", sprites, texName, KilnTerrainData.GateHClosed, Tile.ColliderType.Grid);
        made += MakeGate(folder + "/GateHorizontalOpen.asset", sprites, texName, KilnTerrainData.GateHOpen, Tile.ColliderType.None);
        made += MakeGate(folder + "/GateVerticalClosed.asset", sprites, texName, KilnTerrainData.GateVClosed, Tile.ColliderType.Grid);
        made += MakeGate(folder + "/GateVerticalOpen.asset", sprites, texName, KilnTerrainData.GateVOpen, Tile.ColliderType.None);
        return made;
    }

    private static int MakeGate(string path, Dictionary<string, Sprite> sprites, string texName, int[] cell, Tile.ColliderType collider)
    {
        Sprite s;
        if (!sprites.TryGetValue(texName + "_" + cell[0] + "_" + cell[1], out s)) return 0;

        var tile = LoadOrCreate<Tile>(path);
        tile.sprite = s;
        tile.colliderType = collider;
        EditorUtility.SetDirty(tile);
        AssetDatabase.SetLabels(tile, new[] { FarmEditorLabels.Fences });
        return 1;
    }

    /// <summary>
    /// Returns {28 -> corrected sprite} if the pack's mask-28 piece is a duplicate of mask 20 (the known flaw), else null.
    /// The correction copies the fully-surrounded piece's pixels over the stray notch.
    /// </summary>
    private static Dictionary<int, Sprite> BuildCornerFix(string atlasPath, KilnTerrainData.TerrainDef def, string fixFolder, string fixName)
    {
        int i20 = System.Array.IndexOf(KilnTerrainData.BlobMasks, 20);
        int i28 = System.Array.IndexOf(KilnTerrainData.BlobMasks, 28);
        int i255 = System.Array.IndexOf(KilnTerrainData.BlobMasks, 255);
        if (i20 < 0 || i28 < 0 || i255 < 0) return null;

        string fixPath = fixFolder + "/" + fixName + "_corner28.png";

        var tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
        try
        {
            if (!ImageConversion.LoadImage(tex, File.ReadAllBytes(AbsolutePath(atlasPath)))) return null;

            Color[] c20 = tex.GetPixels(def.cols[i20] * TileSize, tex.height - (def.rows[i20] + 1) * TileSize, TileSize, TileSize);
            Color[] c28 = tex.GetPixels(def.cols[i28] * TileSize, tex.height - (def.rows[i28] + 1) * TileSize, TileSize, TileSize);
            Color[] c255 = tex.GetPixels(def.cols[i255] * TileSize, tex.height - (def.rows[i255] + 1) * TileSize, TileSize, TileSize);

            bool identical = true;
            for (int p = 0; p < c20.Length; p++)
            {
                if (c20[p] != c28[p]) { identical = false; break; }
            }
            if (!identical) return null;      // the pack has been fixed, nothing to do

            // Pixel rows count from the BOTTOM here. The notch sits in the bottom-right: x 7..15, y 0..12.
            var patched = (Color[])c28.Clone();
            for (int y = 0; y <= 12; y++)
                for (int x = 7; x <= 15; x++)
                    patched[y * TileSize + x] = c255[y * TileSize + x];

            var output = new Texture2D(TileSize, TileSize, TextureFormat.RGBA32, false);
            output.SetPixels(patched);
            output.Apply();
            byte[] png = ImageConversion.EncodeToPNG(output);
            Object.DestroyImmediate(output);

            EnsureFolder(fixFolder);
            File.WriteAllBytes(AbsolutePath(fixPath), png);
            AssetDatabase.ImportAsset(fixPath);
            ImportSingle(fixPath, new Vector2(0.5f, 0.5f));

            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(fixPath);
            if (sprite == null) return null;
            return new Dictionary<int, Sprite> { { 28, sprite } };
        }
        finally
        {
            Object.DestroyImmediate(tex);
        }
    }

    // ------------------------------------------------------------------
    // Items and crops (uses the ItemData / CropData scripts from the earlier steps)
    // ------------------------------------------------------------------

    private static Dictionary<string, ItemData> BuildItems(string root, string outRoot, out int count)
    {
        var byFile = new Dictionary<string, ItemData>();
        count = 0;

        string itemsFolder = outRoot + "/Data/Items";
        EnsureFolder(itemsFolder);

        string itemsRoot = AbsolutePath(root + "/Items");
        if (!Directory.Exists(itemsRoot)) return byFile;

        foreach (string file in Directory.GetFiles(itemsRoot, "*.png", SearchOption.AllDirectories))
        {
            string rel = Path.GetRelativePath(AbsolutePath(root), file).Replace('\\', '/');
            string name = Path.GetFileNameWithoutExtension(file);
            if (name == "items_sampler") continue;                                  // the combined sheet, not an item

            string group = Path.GetFileName(Path.GetDirectoryName(file));
            var icon = AssetDatabase.LoadAssetAtPath<Sprite>(root + "/" + rel);
            if (icon == null) continue;

            string assetPath = itemsFolder + "/" + Pretty(name).Replace(" ", "") + ".asset";
            bool isNew = AssetDatabase.LoadAssetAtPath<ItemData>(assetPath) == null;
            var item = LoadOrCreate<ItemData>(assetPath);
            item.icon = icon;

            if (isNew)
            {
                if (group == "seeds" && name.StartsWith("seeds_")) item.itemName = Pretty(name.Substring(6)) + " Seeds";
                else item.itemName = Pretty(name);

                switch (group)
                {
                    case "tools": item.type = ItemData.ItemType.Tool; item.toolAction = GuessToolAction(name); break;
                    case "seeds": item.type = ItemData.ItemType.Seed; item.buyPrice = 20; break;     // placeholder price
                    case "produce": item.type = ItemData.ItemType.Crop; item.sellPrice = 50; break;  // placeholder price
                    default: item.type = ItemData.ItemType.Resource; break;
                }
            }

            EditorUtility.SetDirty(item);
            byFile[name] = item;
            count++;
        }
        return byFile;
    }

    /// <summary>Picks what a tool does from its file name (e.g. "tool_hoe" becomes Hoe). Change it on the item afterwards if needed.</summary>
    private static ItemData.ToolAction GuessToolAction(string fileName)
    {
        string n = fileName.ToLowerInvariant();
        if (n.Contains("hoe")) return ItemData.ToolAction.Hoe;
        if (n.Contains("water")) return ItemData.ToolAction.WateringCan;
        if (n.Contains("sickle") || n.Contains("scythe")) return ItemData.ToolAction.Sickle;
        if (n.Contains("pickaxe") || n.Contains("pick")) return ItemData.ToolAction.Pickaxe;
        if (n.Contains("axe")) return ItemData.ToolAction.Axe;
        if (n.Contains("shovel") || n.Contains("spade")) return ItemData.ToolAction.Shovel;
        if (n.Contains("rod") || n.Contains("fish")) return ItemData.ToolAction.FishingRod;
        return ItemData.ToolAction.None;
    }

    private static int BuildCrops(string root, string outRoot, Dictionary<string, ItemData> items)
    {
        string[] cropNames = { "turnip", "potato", "carrot", "strawberry", "tomato", "corn", "wheat", "pumpkin" };
        string[] stages = { "seed", "sprout", "small", "medium", "large", "ripe" };

        string folder = outRoot + "/Data/Crops";
        EnsureFolder(folder);

        int made = 0;
        foreach (string crop in cropNames)
        {
            var sprites = new List<Sprite>();
            foreach (string stage in stages)
            {
                var s = AssetDatabase.LoadAssetAtPath<Sprite>(root + "/Crops/" + crop + "/" + crop + "_" + stage + ".png");
                if (s != null) sprites.Add(s);
            }
            if (sprites.Count == 0) continue;

            string assetPath = folder + "/" + Pretty(crop) + ".asset";
            bool isNew = AssetDatabase.LoadAssetAtPath<CropData>(assetPath) == null;
            var data = LoadOrCreate<CropData>(assetPath);

            data.growthStageSprites = sprites.ToArray();
            if (isNew)
            {
                data.cropName = Pretty(crop);
                data.daysPerStage = 1;
                data.needsWater = true;
                data.harvestAmount = 1;
            }

            ItemData seed, produce;
            if (items.TryGetValue("seeds_" + crop, out seed)) data.seedItem = seed;
            if (items.TryGetValue(crop, out produce)) data.harvestItem = produce;

            EditorUtility.SetDirty(data);
            made++;
        }
        return made;
    }

    // ------------------------------------------------------------------
    // Prefabs for the Level Editor's Objects layer
    // ------------------------------------------------------------------

    private static int BuildPrefabs(string root, string outRoot)
    {
        int made = 0;

        // Trees: the trunk blocks the player, the leaves don't.
        foreach (string file in FilesIn(root + "/Nature/forest_trees"))
        {
            string name = Path.GetFileNameWithoutExtension(file);
            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(root + "/Nature/forest_trees/" + name + ".png");
            made += MakePrefab(outRoot + "/Prefabs/Trees/" + Pretty(name).Replace(" ", "") + ".prefab",
                               Pretty(name), sprite, new Vector2(0.75f, 0.5f), new Vector2(0f, 0.25f), null);
        }

        // Farmhouse: 4x3 tiles of solid wall, taken from the pack's buildings.json.
        var house = AssetDatabase.LoadAssetAtPath<Sprite>(root + "/Buildings/farm/farmhouse_1.png");
        made += MakePrefab(outRoot + "/Prefabs/Buildings/Farmhouse1.prefab", "Farmhouse 1", house,
                           new Vector2(4f, 3f), new Vector2(0f, 1.5f), null);

        // Props.
        foreach (string file in FilesIn(root + "/Props"))
        {
            string name = Path.GetFileNameWithoutExtension(file);
            string rel = Path.GetRelativePath(AbsolutePath(root), file).Replace('\\', '/');
            string prefabPath = outRoot + "/Prefabs/Props/" + Pretty(name.Replace("_2f", "")).Replace(" ", "") + ".prefab";

            if (name == "scarecrow_classic_2f")
            {
                made += MakeScarecrowPrefab(prefabPath, root + "/" + rel);
                continue;
            }

            // Anything you can walk over (a sprinkler) gets no collider; everything else blocks a half-tile base.
            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(root + "/" + rel);
            Vector2 size = Vector2.zero, offset = Vector2.zero;
            if (sprite != null && name != "sprinkler_basic")
            {
                size = new Vector2(sprite.rect.width / Ppu * 0.9f, 0.5f);
                offset = new Vector2(0f, 0.25f);
            }
            made += MakePrefab(prefabPath, Pretty(name), sprite, size, offset, null);
        }

        return made;
    }

    private static int MakePrefab(string prefabPath, string objectName, Sprite sprite,
                                  Vector2 colliderSize, Vector2 colliderOffset, System.Action<GameObject> extra)
    {
        if (sprite == null) return 0;
        if (AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath) != null) return 0;     // keep the user's edits

        EnsureFolder(Path.GetDirectoryName(prefabPath).Replace('\\', '/'));

        var go = new GameObject(objectName);
        var renderer = go.AddComponent<SpriteRenderer>();
        renderer.sprite = sprite;
        renderer.sortingOrder = 10;      // above the tilemaps, same as the player

        if (colliderSize.x > 0f && colliderSize.y > 0f)
        {
            var box = go.AddComponent<BoxCollider2D>();
            box.size = colliderSize;
            box.offset = colliderOffset;
        }

        if (extra != null) extra(go);

        GameObject prefab = PrefabUtility.SaveAsPrefabAsset(go, prefabPath);
        Object.DestroyImmediate(go);
        if (prefab != null) AssetDatabase.SetLabels(prefab, new[] { FarmEditorLabels.Objects });
        return prefab != null ? 1 : 0;
    }

    private static int MakeScarecrowPrefab(string prefabPath, string sheetPath)
    {
        // The scarecrow file is a 2-frame strip, sliced into two 16x32 sprites.
        Dictionary<string, Sprite> frames = LoadSprites(sheetPath);
        string prefix = Path.GetFileNameWithoutExtension(sheetPath);
        Sprite f0, f1;
        if (!frames.TryGetValue(prefix + "_0_0", out f0)) return 0;
        frames.TryGetValue(prefix + "_1_0", out f1);

        return MakePrefab(prefabPath, "Scarecrow", f0,
                          new Vector2(0.9f, 0.5f), new Vector2(0f, 0.25f),
                          go =>
                          {
                              var animator = go.AddComponent<SimpleSpriteAnimator>();
                              animator.frames = f1 != null ? new[] { f0, f1 } : new[] { f0 };
                              animator.framesPerSecond = 1000f / 420f;
                          });
    }

    private static IEnumerable<string> FilesIn(string assetFolder)
    {
        string abs = AbsolutePath(assetFolder);
        if (!Directory.Exists(abs)) return new string[0];
        return Directory.GetFiles(abs, "*.png", SearchOption.AllDirectories);
    }

    // ------------------------------------------------------------------
    // Small helpers
    // ------------------------------------------------------------------

    private static string FindPackRoot()
    {
        foreach (string guid in AssetDatabase.FindAssets("terrain_spring t:Texture2D"))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            if (path.EndsWith("/" + TerrainFile))
                return path.Substring(0, path.Length - TerrainFile.Length - 1);
        }
        return null;
    }

    private static string ProjectRoot()
    {
        return Path.GetDirectoryName(Application.dataPath);
    }

    private static string AbsolutePath(string assetPath)
    {
        return Path.Combine(ProjectRoot(), assetPath);
    }

    private static void EnsureFolder(string folder)
    {
        if (AssetDatabase.IsValidFolder(folder)) return;
        string parent = Path.GetDirectoryName(folder).Replace('\\', '/');
        if (!string.IsNullOrEmpty(parent)) EnsureFolder(parent);
        AssetDatabase.CreateFolder(parent, Path.GetFileName(folder));
    }

    private static T LoadOrCreate<T>(string path) where T : ScriptableObject
    {
        var existing = AssetDatabase.LoadAssetAtPath<T>(path);
        if (existing != null) return existing;

        var created = ScriptableObject.CreateInstance<T>();
        AssetDatabase.CreateAsset(created, path);
        return created;
    }

    /// <summary>"seeds_turnip" -> "Seeds Turnip".</summary>
    private static string Pretty(string fileName)
    {
        string[] parts = fileName.Split('_');
        for (int i = 0; i < parts.Length; i++)
        {
            if (parts[i].Length > 0) parts[i] = char.ToUpper(parts[i][0]) + parts[i].Substring(1);
        }
        return string.Join(" ", parts);
    }
}
