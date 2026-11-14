using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Tilemaps;

/// <summary>
/// Builds the CLIFF tile palette from cliff_tiles.png (a 16-column sheet of 16x16 tiles).
/// Menu: Farm > Mountain > Build Cliff Palette.   Must live in a folder named "Editor".
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

    [MenuItem("Farm/Mountain/Build Cliff Palette", false, 120)]
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
            "\n\nOpen Farm > Level Editors > Geography Brush and pick 'Mountain Cliff'. If it was already there from before, open the Make Geography Presets menu and choose Reset all once so it picks up the tiles." +
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

    private static void Slice(string pngPath)
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
        for (int i = 0; i < Names.Length; i++)
        {
            if (Names[i] == null) continue;
            int col = i % Columns, row = i / Columns;
            var m = new SpriteMetaData();
            m.name = "cliff_" + i;
            m.rect = new Rect(col * Cell, (Rows - 1 - row) * Cell, Cell, Cell);
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
