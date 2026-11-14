using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Repairs the Bat and Bug art when their sprites have gone missing or blank.
/// Menu: Farm > Mountain > Repair Enemy Art.   Must live in a folder named "Editor".
///
/// Why it breaks: the mountain setup cut bat_sheet.png and bug_sheet.png with RANDOM sprite IDs every time it ran, so anything that
/// pointed at the old sprites (prefab frame lists, copies in a scene) lost them. This tool
///   1. re-cuts both sheets with STABLE ids (same sprite = same id forever, so this cannot happen again),
///   2. puts the frames back on the Bug and Bat prefabs (and the sprite shown at rest),
///   3. does the same for any Bug / Bat already placed in the open scene.
/// Cell sizes: bug 16x16 (feet pivot), bat 24x16 (centre pivot). Sprites are named like  bat_sheet_0_0  (column_row, row from the top).
/// </summary>
public static class EnemyArtRepair
{
    [MenuItem("Farm/Mountain/Repair Enemy Art")]
    public static void Repair()
    {
        string batPath = FindTexture("bat_sheet"), bugPath = FindTexture("bug_sheet");
        if (batPath == null && bugPath == null)
        {
            EditorUtility.DisplayDialog("Repair enemy art", "Could not find bat_sheet.png or bug_sheet.png anywhere in Assets. Copy the Mountain art folder in and try again.", "OK");
            return;
        }

        Dictionary<string, Sprite> bat = batPath != null ? SliceStable(batPath, 24, 16, SpriteAlignment.Center) : null;
        Dictionary<string, Sprite> bug = bugPath != null ? SliceStable(bugPath, 16, 16, SpriteAlignment.BottomCenter) : null;

        int prefabs = 0, scene = 0;
        foreach (string guid in AssetDatabase.FindAssets("t:Prefab"))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            string n = Path.GetFileNameWithoutExtension(path);
            if (n != "Bat" && n != "Bug") continue;
            GameObject root = PrefabUtility.LoadPrefabContents(path);
            if (Fix(root, bat, bug)) { PrefabUtility.SaveAsPrefabAsset(root, path); prefabs++; }
            PrefabUtility.UnloadPrefabContents(root);
        }

        // enemies already sitting in the open scene (a scene copy keeps its own frame lists if it was changed there)
        foreach (BatEnemy b in Object.FindObjectsByType<BatEnemy>(FindObjectsInactive.Include))
        {
            Undo.RecordObject(b, "Repair enemy art");
            if (Fix(b.gameObject, bat, bug)) { scene++; EditorUtility.SetDirty(b); }
        }
        foreach (BugEnemy b in Object.FindObjectsByType<BugEnemy>(FindObjectsInactive.Include))
        {
            Undo.RecordObject(b, "Repair enemy art");
            if (Fix(b.gameObject, bat, bug)) { scene++; EditorUtility.SetDirty(b); }
        }
        if (scene > 0) UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(UnityEngine.SceneManagement.SceneManager.GetActiveScene());
        AssetDatabase.SaveAssets();

        EditorUtility.DisplayDialog("Repair enemy art",
            "Bat sheet: " + (bat == null ? "not found" : bat.Count + " sprites") + ", Bug sheet: " + (bug == null ? "not found" : bug.Count + " sprites") +
            "\nPrefabs repaired: " + prefabs + "\nEnemies repaired in this scene: " + scene +
            "\n\nIf you see 0 prefabs, the Bat/Bug prefabs are missing: run Farm > Mountain > Set Up Mountain Trail." +
            "\n\nNot compiled in Unity - tell me about any red errors.", "OK");
    }

    /// <summary>Gives the enemy its frames again. Returns true if anything was changed.</summary>
    private static bool Fix(GameObject go, Dictionary<string, Sprite> bat, Dictionary<string, Sprite> bug)
    {
        bool changed = false;
        var batEnemy = go.GetComponent<BatEnemy>();
        if (batEnemy != null && bat != null)
        {
            batEnemy.flapFrames = Cells(bat, "bat_sheet", 0, 0, 4);       // columns 0-3, top row
            batEnemy.diveFrames = Cells(bat, "bat_sheet", 4, 0, 2);       // columns 4-5, top row
            SpriteRenderer body = batEnemy.body != null ? batEnemy.body.GetComponent<SpriteRenderer>() : go.GetComponentInChildren<SpriteRenderer>();
            if (body != null && batEnemy.flapFrames.Length > 0) body.sprite = batEnemy.flapFrames[0];
            changed = true;
        }
        var bugEnemy = go.GetComponent<BugEnemy>();
        if (bugEnemy != null && bug != null)
        {
            bugEnemy.sideFrames = Cells(bug, "bug_sheet", 0, 0, 4);       // row 0 = side
            bugEnemy.downFrames = Cells(bug, "bug_sheet", 0, 1, 4);       // row 1 = down
            bugEnemy.upFrames = Cells(bug, "bug_sheet", 0, 2, 4);         // row 2 = up
            var sr = go.GetComponent<SpriteRenderer>();
            if (sr != null && bugEnemy.sideFrames.Length > 0) sr.sprite = bugEnemy.sideFrames[0];
            changed = true;
        }
        return changed;
    }

    private static Sprite[] Cells(Dictionary<string, Sprite> sheet, string baseName, int firstCol, int row, int count)
    {
        var list = new List<Sprite>();
        for (int i = 0; i < count; i++)
        {
            Sprite s;
            if (sheet.TryGetValue(baseName + "_" + (firstCol + i) + "_" + row, out s)) list.Add(s);
        }
        return list.ToArray();
    }

    private static string FindTexture(string fileName)
    {
        foreach (string guid in AssetDatabase.FindAssets(fileName + " t:Texture2D"))
        {
            string p = AssetDatabase.GUIDToAssetPath(guid);
            if (Path.GetFileNameWithoutExtension(p) == fileName) return p;
        }
        return null;
    }

    /// <summary>The same id for the same sprite name, every time.</summary>
    public static GUID StableId(string textureName, string spriteName)
    {
        using (var md5 = System.Security.Cryptography.MD5.Create())
        {
            byte[] hash = md5.ComputeHash(System.Text.Encoding.UTF8.GetBytes("mountain:" + textureName + ":" + spriteName));
            var sb = new System.Text.StringBuilder();
            foreach (byte b in hash) sb.Append(b.ToString("x2"));
            return new GUID(sb.ToString());
        }
    }

    /// <summary>Cuts a sheet into equal cells with stable sprite ids. Returns "bat_sheet_column_row" -> sprite.</summary>
    public static Dictionary<string, Sprite> SliceStable(string path, int cellW, int cellH, SpriteAlignment align)
    {
        var imp = (TextureImporter)AssetImporter.GetAtPath(path);
        imp.textureType = TextureImporterType.Sprite;
        imp.spriteImportMode = SpriteImportMode.Multiple;
        imp.spritePixelsPerUnit = 16f;
        imp.filterMode = FilterMode.Point;
        imp.textureCompression = TextureImporterCompression.Uncompressed;
        imp.mipmapEnabled = false;
        imp.alphaIsTransparency = true;
        imp.SaveAndReimport();

        var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        int cols = tex.width / cellW, rows = tex.height / cellH;
        string baseName = Path.GetFileNameWithoutExtension(path);

        var factory = new UnityEditor.U2D.Sprites.SpriteDataProviderFactories();
        factory.Init();
        UnityEditor.U2D.Sprites.ISpriteEditorDataProvider provider = factory.GetSpriteEditorDataProviderFromObject(imp);
        provider.InitSpriteEditorDataProvider();

        var rects = new List<SpriteRect>();
        for (int r = 0; r < rows; r++)
            for (int c = 0; c < cols; c++)
            {
                var rect = new SpriteRect();
                rect.name = baseName + "_" + c + "_" + r;
                rect.rect = new Rect(c * cellW, tex.height - (r + 1) * cellH, cellW, cellH);
                rect.alignment = align;
                rect.pivot = align == SpriteAlignment.BottomCenter ? new Vector2(0.5f, 0f) : new Vector2(0.5f, 0.5f);
                rect.spriteID = StableId(baseName, rect.name);
                rects.Add(rect);
            }
        provider.SetSpriteRects(rects.ToArray());
        var idProvider = provider.GetDataProvider<UnityEditor.U2D.Sprites.ISpriteNameFileIdDataProvider>();
        var pairs = new List<SpriteNameFileIdPair>();
        foreach (SpriteRect sr in rects) pairs.Add(new SpriteNameFileIdPair(sr.name, sr.spriteID));
        idProvider.SetNameFileIdPairs(pairs);
        provider.Apply();
        imp.SaveAndReimport();

        var result = new Dictionary<string, Sprite>();
        foreach (Object o in AssetDatabase.LoadAllAssetsAtPath(path))
        {
            var s = o as Sprite;
            if (s != null) result[s.name] = s;
        }
        return result;
    }
}
