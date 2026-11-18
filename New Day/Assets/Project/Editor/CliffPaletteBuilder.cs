using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.U2D.Sprites;
using UnityEngine;
using UnityEngine.Tilemaps;

/// <summary>
/// Builds the CLIFF tile palette from cliff_tiles.png (a 16-column sheet of 16x16 tiles).
/// Menu: Farm > Build Art > Cliff Palette.   Must live in a folder named "Editor".
///
/// - Slices the sheet, makes one Tile asset per piece in Assets/Farm/Paths/Tiles/Cliff (named Cliff_Fill_1, Cliff_Lip_T ...).
/// - The ROCK pieces (fills, lips, inner corners, wall) get a GRID COLLIDER, so the player cannot walk on them.
///   The decor pieces (moss, snow, cracks, rubble...) have no collider.
/// - Then makes the "Mountain Cliff" geography so it shows up in the Geography Brush.
/// Safe to run again: existing tile assets are updated in place, so scene references stay valid.
/// The order of NAMES must match the sheet made by Generators/gen_cliff.py.
/// </summary>
public static class CliffPaletteBuilder
{
    private const string SheetPath = "Assets/Farm/Mountain/Cliff/cliff_tiles.png";
    private const string TileFolder = "Assets/Farm/Paths/Tiles/Cliff";
    private const int Cell = 16, Columns = 16, Rows = 3;

    // null = empty slot. Index = column + row * 16, row 0 is the TOP of the picture (this matches gen_cliff.py exactly).
    private static readonly string[] Names =
    {
        "Fill_1", "Fill_2", "Fill_3", "Fill_4", "Lip_T", "Lip_L", "Lip_R", "Lip_TL", "Lip_TR", "Inner_TL", "Inner_TR", null, null, null, null, null,
        "FaceUpper_1", "FaceUpper_2", "FaceUpper_3", "FaceUpper_4", "FaceUpper_L", "FaceUpper_R",
        "FaceLower_1", "FaceLower_2", "FaceLower_3", "FaceLower_4", "FaceLower_L", "FaceLower_R",
        "Moss", "Snow", "Crack", "Rubble", "Pebbles", "Ice", "Vines", "SmallRock"
    };

    private static bool IsDecor(string n)
    {
        return n == "Moss" || n == "Snow" || n == "Crack" || n == "Rubble" || n == "Pebbles" || n == "Ice" || n == "Vines" || n == "SmallRock";
    }

    [MenuItem("Farm/Build Art/Cliff Palette", false, 205)]
    public static void Build()
    {
        string png = FindSheet();
        if (png == null)
        {
            EditorUtility.DisplayDialog("Cliff palette", "Could not find cliff_tiles.png. Put the Cliff folder from the zip into " + Path.GetDirectoryName(SheetPath) + " (or anywhere in Assets) and run this again.", "OK");
            return;
        }
        Slice(png);
        EnsureFolder(TileFolder);

        // the sprites come back named "cliff_<index>"
        var sprites = new Dictionary<string, Sprite>();
        foreach (Object o in AssetDatabase.LoadAllAssetsAtPath(png)) { var sp = o as Sprite; if (sp != null) sprites[sp.name] = sp; }

        int made = 0, updated = 0;
        for (int i = 0; i < Names.Length; i++)
        {
            string n = Names[i];
            if (n == null) continue;
            Sprite sprite;
            if (!sprites.TryGetValue("cliff_" + i, out sprite)) continue;
            string path = TileFolder + "/Cliff_" + n + ".asset";
            var tile = AssetDatabase.LoadAssetAtPath<Tile>(path);
            if (tile == null) { tile = ScriptableObject.CreateInstance<Tile>(); AssetDatabase.CreateAsset(tile, path); made++; }
            else updated++;
            tile.sprite = sprite;
            tile.colliderType = IsDecor(n) ? Tile.ColliderType.None : Tile.ColliderType.Grid;   // rock is solid, decor is not
            EditorUtility.SetDirty(tile);
        }
        AssetDatabase.SaveAssets();

        GeographyPresetMaker.EnsureAll();       // adds the Mountain Cliff geography if it is missing

        EditorUtility.DisplayDialog("Cliff palette", "Tiles made: " + made + ", updated: " + updated + "\nFolder: " + TileFolder +
            "\n\nOpen Farm > Level Editors > Geography Brush and pick 'Mountain Cliff'. If it was already there from before, open Farm > Build Art > Geography Presets and choose Reset all once so it picks up the tiles." +
            "\n\nNot compiled in Unity - tell me about any red errors.", "OK");
    }

    private static string FindSheet()
    {
        if (File.Exists(SheetPath)) return SheetPath;
        foreach (string guid in AssetDatabase.FindAssets("cliff_tiles t:Texture2D"))
        {
            string p = AssetDatabase.GUIDToAssetPath(guid);
            if (Path.GetFileNameWithoutExtension(p) == "cliff_tiles") return p;
        }
        return null;
    }

    /// <summary>
    /// Cuts the sheet into 16x16 sprites named "cliff_&lt;index&gt;".
    /// Every sprite keeps the ID it already had (StableSpriteId.For), so the Cliff tiles and the Mine scene's
    /// tilemaps, which point at these sprites by ID, never lose their pictures when this menu is run again.
    /// </summary>
    private static void Slice(string pngPath)
    {
        // 1. pixel-art import settings
        var imp = (TextureImporter)AssetImporter.GetAtPath(pngPath);
        imp.textureType = TextureImporterType.Sprite;
        imp.spriteImportMode = SpriteImportMode.Multiple;
        imp.spritePixelsPerUnit = 16f;
        imp.filterMode = FilterMode.Point;
        imp.mipmapEnabled = false;
        imp.textureCompression = TextureImporterCompression.Uncompressed;
        imp.alphaIsTransparency = true;
        imp.SaveAndReimport();

        // 2. open the sheet's sprite list (the same data the Sprite Editor window edits)
        string baseName = Path.GetFileNameWithoutExtension(pngPath);
        var factory = new SpriteDataProviderFactories();
        factory.Init();
        ISpriteEditorDataProvider provider = factory.GetSpriteEditorDataProviderFromObject(imp);
        provider.InitSpriteEditorDataProvider();

        // 3. one rectangle per named slot; empty slots (null) are skipped
        var rects = new List<SpriteRect>();
        for (int i = 0; i < Names.Length; i++)
        {
            if (Names[i] == null) continue;
            int col = i % Columns, row = i / Columns;
            var rect = new SpriteRect();
            rect.name = "cliff_" + i;
            rect.rect = new Rect(col * Cell, (Rows - 1 - row) * Cell, Cell, Cell);   // row 0 is the TOP of the picture
            rect.alignment = SpriteAlignment.Center;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.spriteID = StableSpriteId.For(provider, baseName, rect.name);   // keeps the same ID every run, so nothing loses its art
            rects.Add(rect);
        }
        provider.SetSpriteRects(rects.ToArray());

        // 4. the name -> ID table Unity uses to find each sprite again after a reimport
        var idProvider = provider.GetDataProvider<ISpriteNameFileIdDataProvider>();
        var pairs = new List<SpriteNameFileIdPair>();
        foreach (SpriteRect r in rects) pairs.Add(new SpriteNameFileIdPair(r.name, r.spriteID));
        idProvider.SetNameFileIdPairs(pairs);

        provider.Apply();
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
