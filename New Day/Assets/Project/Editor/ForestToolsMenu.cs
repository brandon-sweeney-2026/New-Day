using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Tilemaps;

/// <summary>
/// Forest helpers (Farm > Forest > ...). Must live in a folder named "Editor".
///   Make Level Editor Layers   Gives the open scene the 'Ground' / 'Paths' / 'Fences' tilemaps and the 'Objects' parent the Farm Level Editor needs.
///                              (The forest rooms draw their floors with sprites, so a forest scene has none of these until you run this.)
///   Make Sign Prefab           Creates the ForestSign prefab (label FarmLayer_Objects) so signs show up in the Level Editor's Objects palette.
/// The Maze Generator runs both for you.
/// </summary>
public static class ForestToolsMenu
{
    [MenuItem("Farm/Level Editors/Make Forest Editor Layers", false, 30)]
    public static void MakeLayersMenu()
    {
        bool made = EnsureEditLayers();
        EditorUtility.DisplayDialog("Level Editor layers",
            made ? "Made 'ForestEditGrid' (tilemaps Ground, Paths, Fences) and an 'Objects' parent.\n\nNow open Farm > Level Editors > Farm Level Editor and press 'Auto-find in scene (by name)'."
                 : "This scene already has them. Open the Level Editor and press 'Auto-find in scene (by name)'.", "OK");
    }

    /// <summary>Returns true if anything was created.</summary>
    public static bool EnsureEditLayers()
    {
        bool made = false;
        GameObject gridGo = GameObject.Find("ForestEditGrid");
        if (gridGo == null)
        {
            gridGo = new GameObject("ForestEditGrid");
            Undo.RegisterCreatedObjectUndo(gridGo, "Forest edit layers");
            var grid = gridGo.AddComponent<Grid>();
            grid.cellSize = new Vector3(1f, 1f, 0f);
            made = true;
        }
        // order: above the room floors (-20), below walls (6) and objects
        MakeMap(gridGo, "Ground", -10, ref made);
        MakeMap(gridGo, "Paths", -9, ref made);
        MakeMap(gridGo, "Fences", 7, ref made);
        if (GameObject.Find("Objects") == null)
        {
            var o = new GameObject("Objects");
            Undo.RegisterCreatedObjectUndo(o, "Objects parent");
            made = true;
        }
        if (made) UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(UnityEngine.SceneManagement.SceneManager.GetActiveScene());
        return made;
    }

    private static void MakeMap(GameObject gridGo, string name, int order, ref bool made)
    {
        if (gridGo.transform.Find(name) != null) return;
        var go = new GameObject(name);
        go.transform.SetParent(gridGo.transform, false);
        go.AddComponent<Tilemap>();
        go.AddComponent<TilemapRenderer>().sortingOrder = order;
        made = true;
    }

    // ------------------------------------------------------------------ the sign prefab
    [MenuItem("Farm/Forest/Make Sign Prefab")]
    public static void MakeSignMenu()
    {
        GameObject p = EnsureSignPrefab();
        EditorUtility.DisplayDialog("Sign prefab", p != null ? "Made " + AssetDatabase.GetAssetPath(p) + ".\nIn the Level Editor's Objects palette press 'Reload this palette from labelled assets' to place signs." : "Couldn't make it.", "OK");
    }

    public static GameObject EnsureSignPrefab()
    {
        string dir = ForestSetupMenu.DataFolder;
        string prefabPath = dir + "/ForestSign.prefab";
        var existing = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
        if (existing != null) return existing;

        if (!AssetDatabase.IsValidFolder("Assets/Farm")) AssetDatabase.CreateFolder("Assets", "Farm");
        if (!AssetDatabase.IsValidFolder("Assets/Farm/Data")) AssetDatabase.CreateFolder("Assets/Farm", "Data");
        if (!AssetDatabase.IsValidFolder(dir)) AssetDatabase.CreateFolder("Assets/Farm/Data", "Forest");

        string png = dir + "/forest_sign.png";
        if (!File.Exists(png))
        {
            File.WriteAllBytes(png, DrawSign().EncodeToPNG());
            AssetDatabase.ImportAsset(png);
            var imp = (TextureImporter)AssetImporter.GetAtPath(png);
            imp.textureType = TextureImporterType.Sprite;
            imp.spritePixelsPerUnit = 16f;
            imp.filterMode = FilterMode.Point;
            imp.mipmapEnabled = false;
            imp.textureCompression = TextureImporterCompression.Uncompressed;
            var settings = new TextureImporterSettings();
            imp.ReadTextureSettings(settings);
            settings.spriteAlignment = (int)SpriteAlignment.BottomCenter;
            imp.SetTextureSettings(settings);
            imp.SaveAndReimport();
        }

        var go = new GameObject("ForestSign");
        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = AssetDatabase.LoadAssetAtPath<Sprite>(png);
        sr.sortingOrder = 9;
        var cc = go.AddComponent<CircleCollider2D>(); cc.radius = 0.8f; cc.isTrigger = true; cc.offset = new Vector2(0f, 0.4f);
        go.AddComponent<ForestSign>();
        go.AddComponent<BehindPlayerSorter>().thresholdOffset = 0f;
        GameObject prefab = PrefabUtility.SaveAsPrefabAsset(go, prefabPath);
        Object.DestroyImmediate(go);
        AssetDatabase.SetLabels(prefab, new[] { FarmEditorLabels.Objects });
        return prefab;
    }

    private static Texture2D DrawSign()
    {
        const int W = 16, H = 24;
        var t = new Texture2D(W, H, TextureFormat.RGBA32, false);
        var clear = new Color32(0, 0, 0, 0);
        var px = new Color32[W * H];
        for (int i = 0; i < px.Length; i++) px[i] = clear;
        Color32 post = new Color32(110, 76, 46, 255), postD = new Color32(80, 54, 32, 255);
        Color32 board = new Color32(206, 160, 96, 255), boardL = new Color32(232, 190, 124, 255), boardD = new Color32(150, 108, 60, 255), ink = new Color32(70, 48, 28, 255);
        System.Action<int, int, int, int, Color32> rect = (x0, y0, x1, y1, c) => { for (int y = y0; y <= y1; y++) for (int x = x0; x <= x1; x++) px[y * W + x] = c; };
        rect(7, 0, 8, 12, post); rect(8, 0, 8, 12, postD);                    // post
        rect(1, 10, 14, 21, boardD); rect(2, 11, 13, 20, board); rect(2, 20, 13, 20, boardL);   // board
        for (int x = 4; x <= 11; x += 1) { px[17 * W + x] = ink; if (x % 3 != 0) px[14 * W + x] = ink; }   // scribbled text
        rect(4, 15, 9, 15, ink);
        t.SetPixels32(px);
        t.Apply();
        return t;
    }
}
