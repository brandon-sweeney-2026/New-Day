using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Farm > Forest > Set Up Sick Fairy and Fairy Door
/// Imports the sick-fairy art (fairy_sick.png, fairy_sick_portraits.png, fairy_door_locked.png, fairy_door_open.png - copy them into the same folder as your other
/// forest art) and makes two prefabs next to it: SickFairy and FairyDoor. Both are labelled so they show up in the Level Editor's Objects palette.
/// The Maze Generator calls this for you, so you usually don't need to run it yourself.
/// Must live in a folder named "Editor".
/// </summary>
public static class ForestSickFairyMenu
{
    [MenuItem("Farm/Forest/Set Up Sick Fairy and Fairy Door")]
    public static void Run()
    {
        GameObject sick, door;
        if (Ensure(out sick, out door))
            EditorUtility.DisplayDialog("Done", "Made the SickFairy and FairyDoor prefabs (next to your forest art, in a 'Prefabs' folder).\n\nThe Maze Generator places them for you. To place them by hand, use the Forest Level Editor's Objects palette.", "OK");
    }

    public static bool Ensure(out GameObject sickPrefab, out GameObject doorPrefab)
    {
        sickPrefab = null; doorPrefab = null;
        string art = ForestSetupMenu.FindFolder("fairy_sick");
        if (art == null)
        {
            EditorUtility.DisplayDialog("Sick fairy art not found",
                "Couldn't find fairy_sick.png in your project.\nCopy the four new pictures (fairy_sick, fairy_sick_portraits, fairy_door_locked, fairy_door_open) into your forest art folder and try again.", "OK");
            return false;
        }
        string prefabDir = art + "/Prefabs";
        ForestSetupMenu.EnsureFolder(prefabDir);

        // ---- import the art ----
        SliceSheet(art + "/fairy_sick.png", 24, 24);
        SliceSheet(art + "/fairy_sick_portraits.png", 64, 64);
        ImportSingle(art + "/fairy_door_locked.png");
        ImportSingle(art + "/fairy_door_open.png");
        AssetDatabase.Refresh();

        // ---- the sick fairy ----
        string sickPath = prefabDir + "/SickFairy.prefab";
        sickPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(sickPath);
        if (sickPrefab == null)
        {
            var root = new GameObject("SickFairy");
            var pic = new GameObject("Picture");
            pic.transform.SetParent(root.transform, false);
            var sr = pic.AddComponent<SpriteRenderer>();
            sr.sortingOrder = 12;
            var col = root.AddComponent<CircleCollider2D>();
            col.radius = 1.3f; col.isTrigger = true; col.offset = new Vector2(0f, 0.7f);

            var f = root.AddComponent<SickFairyNpc>();
            f.flapFrames = new[] { Cell(art, "fairy_sick", 0), Cell(art, "fairy_sick", 1) };
            f.fallFrame = Cell(art, "fairy_sick", 2);
            f.lieFrames = new[] { Cell(art, "fairy_sick", 3), Cell(art, "fairy_sick", 4) };
            f.sitFrame = Cell(art, "fairy_sick", 5);
            f.portraitIdle = Cell(art, "fairy_sick_portraits", 0); f.portraitTalk = Cell(art, "fairy_sick_portraits", 1);
            f.portraitHappy = Cell(art, "fairy_sick_portraits", 2); f.portraitBlink = Cell(art, "fairy_sick_portraits", 3);
            f.dialoguePanel = FindSprite("ui_dialog_panel"); f.portraitFrame = FindSprite("ui_dialog_frame");
            if (f.lieFrames[0] != null) sr.sprite = f.lieFrames[0];
            sickPrefab = PrefabUtility.SaveAsPrefabAsset(root, sickPath);
            Object.DestroyImmediate(root);
            AssetDatabase.SetLabels(sickPrefab, new[] { FarmEditorLabels.Objects });
        }

        // ---- the fairy door ----
        string doorPath = prefabDir + "/FairyDoor.prefab";
        doorPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(doorPath);
        if (doorPrefab == null)
        {
            var d = new GameObject("FairyDoor");
            var sr = d.AddComponent<SpriteRenderer>();
            sr.sortingOrder = 7;
            var box = d.AddComponent<BoxCollider2D>();
            box.size = new Vector2(2.2f, 3.1f); box.offset = new Vector2(0f, 0.35f); box.isTrigger = true;
            var s = d.AddComponent<ForestShortcutDoor>();
            s.lockedSprite = Single(art, "fairy_door_locked"); s.openSprite = Single(art, "fairy_door_open");
            sr.sprite = s.lockedSprite;
            doorPrefab = PrefabUtility.SaveAsPrefabAsset(d, doorPath);
            Object.DestroyImmediate(d);
            AssetDatabase.SetLabels(doorPrefab, new[] { FarmEditorLabels.Objects });
        }
        AssetDatabase.SaveAssets();
        return sickPrefab != null && doorPrefab != null;
    }

    // ------------------------------------------------------------------ helpers
    private static Sprite Single(string art, string name)
    {
        return AssetDatabase.LoadAssetAtPath<Sprite>(art + "/" + name + ".png");
    }

    private static Sprite Cell(string art, string sheet, int col)
    {
        foreach (Object o in AssetDatabase.LoadAllAssetsAtPath(art + "/" + sheet + ".png"))
        {
            var s = o as Sprite;
            if (s != null && s.name == sheet + "_" + col + "_0") return s;
        }
        return null;
    }

    private static Sprite FindSprite(string fileName)
    {
        foreach (string guid in AssetDatabase.FindAssets(fileName + " t:Sprite"))
        {
            string p = AssetDatabase.GUIDToAssetPath(guid);
            if (Path.GetFileNameWithoutExtension(p) == fileName) return AssetDatabase.LoadAssetAtPath<Sprite>(p);
        }
        return null;
    }

    private static void ImportSingle(string path)
    {
        var imp = AssetImporter.GetAtPath(path) as TextureImporter;
        if (imp == null) return;
        imp.textureType = TextureImporterType.Sprite;
        imp.spriteImportMode = SpriteImportMode.Single;
        imp.spritePixelsPerUnit = 16f;
        imp.filterMode = FilterMode.Point;
        imp.textureCompression = TextureImporterCompression.Uncompressed;
        imp.mipmapEnabled = false;
        imp.alphaIsTransparency = true;
        var settings = new TextureImporterSettings();
        imp.ReadTextureSettings(settings);
        settings.spriteAlignment = (int)SpriteAlignment.BottomCenter;
        settings.spriteMeshType = SpriteMeshType.FullRect;
        imp.SetTextureSettings(settings);
        imp.SaveAndReimport();
    }

    private static void SliceSheet(string path, int cellW, int cellH)
    {
        var imp = AssetImporter.GetAtPath(path) as TextureImporter;
        if (imp == null) return;
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
        imp.SaveAndReimport();
    }
}
