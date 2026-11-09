using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Farm > NPCs > Set Up Meatball
/// Imports Meatball's art (copy the "Dog" folder into your Assets first), then puts a dog house and Meatball into the open scene
/// and wires everything up. Safe to run again: it reuses what is already in the scene.
/// </summary>
public static class MeatballSetupMenu
{
    [MenuItem("Farm/NPCs/Set Up Meatball")]
    public static void Run()
    {
        string dir = FindFolder("meatball_sheet");
        if (dir == null)
        {
            EditorUtility.DisplayDialog("Meatball art not found",
                "Couldn't find meatball_sheet.png in your project.\nCopy the 'Dog' folder into Assets (for example Assets/Art/Dog) and run this again.", "OK");
            return;
        }

        // ---- 1. import ----
        Dictionary<string, Sprite> sheet = SliceSheet(dir + "/meatball_sheet.png", 24, 24, 16f, SpriteAlignment.BottomCenter);
        Dictionary<string, Sprite> faces = SliceSheet(dir + "/meatball_portraits.png", 64, 64, 50f, SpriteAlignment.Center);
        ImportSingle(dir + "/doghouse.png", 16f, Vector4.zero, SpriteAlignment.BottomCenter);
        ImportSingle(dir + "/dog_bowl.png", 16f, Vector4.zero, SpriteAlignment.BottomCenter);
        ImportSingle(dir + "/ui_dialog_panel.png", 50f, new Vector4(8, 8, 8, 8), SpriteAlignment.Center);
        ImportSingle(dir + "/ui_dialog_frame.png", 50f, new Vector4(6, 6, 6, 6), SpriteAlignment.Center);
        AssetDatabase.Refresh();

        Sprite house = Load(dir + "/doghouse.png");
        Sprite bowl = Load(dir + "/dog_bowl.png");

        // ---- 2. where to put things ----
        Vector3 basePos = new Vector3(6f, 2f, 0f);
        var player = Object.FindAnyObjectByType<PlayerController>();
        if (player != null) basePos = player.transform.position + new Vector3(5f, 2f, 0f);

        // ---- 3. the dog house ----
        var houseComp = Object.FindAnyObjectByType<DogHouse>();
        GameObject houseGo;
        if (houseComp == null)
        {
            houseGo = new GameObject("DogHouse");
            Undo.RegisterCreatedObjectUndo(houseGo, "Create Dog House");
            houseGo.transform.position = basePos;
            houseComp = houseGo.AddComponent<DogHouse>();
        }
        else houseGo = houseComp.gameObject;

        var hsr = houseGo.GetComponent<SpriteRenderer>();
        if (hsr == null) hsr = houseGo.AddComponent<SpriteRenderer>();
        hsr.sprite = house;
        hsr.sortingOrder = 10;

        foreach (var c in houseGo.GetComponents<Collider2D>()) Object.DestroyImmediate(c);
        var solid = houseGo.AddComponent<BoxCollider2D>();
        solid.size = new Vector2(1.5f, 0.7f);
        solid.offset = new Vector2(0f, 0.45f);

        var sorter = houseGo.GetComponent<BehindPlayerSorter>();
        if (sorter == null) sorter = houseGo.AddComponent<BehindPlayerSorter>();
        sorter.thresholdOffset = 0.5f;

        Transform sit = houseGo.transform.Find("SitPoint");
        if (sit == null)
        {
            var sp = new GameObject("SitPoint");
            sp.transform.SetParent(houseGo.transform, false);
            sp.transform.localPosition = new Vector3(1.6f, -0.1f, 0f);
            sit = sp.transform;
        }
        Transform bowlT = houseGo.transform.Find("Bowl");
        if (bowlT == null)
        {
            var b = new GameObject("Bowl");
            b.transform.SetParent(houseGo.transform, false);
            b.transform.localPosition = new Vector3(-1.3f, -0.1f, 0f);
            b.AddComponent<SpriteRenderer>();
            bowlT = b.transform;
        }
        var bsr = bowlT.GetComponent<SpriteRenderer>();
        bsr.sprite = bowl;
        bsr.sortingOrder = 9;

        var hso = new SerializedObject(houseComp);
        SetObj(hso, "sitPoint", sit);
        hso.ApplyModifiedProperties();

        // ---- 4. Meatball ----
        var dog = Object.FindAnyObjectByType<MeatballDog>();
        GameObject dogGo;
        if (dog == null)
        {
            dogGo = new GameObject("Meatball");
            Undo.RegisterCreatedObjectUndo(dogGo, "Create Meatball");
            dogGo.transform.position = sit.position;
            var sr0 = dogGo.AddComponent<SpriteRenderer>();
            sr0.sortingOrder = 10;
            var body = dogGo.AddComponent<Rigidbody2D>();
            body.gravityScale = 0f;
            body.freezeRotation = true;
            body.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
            dog = dogGo.AddComponent<MeatballDog>();
        }
        else dogGo = dog.gameObject;

        foreach (var c in dogGo.GetComponents<Collider2D>()) Object.DestroyImmediate(c);
        var feet = dogGo.AddComponent<CircleCollider2D>();           // bumps into trees and walls
        feet.radius = 0.2f;
        feet.offset = new Vector2(0f, 0.2f);
        var talk = dogGo.AddComponent<BoxCollider2D>();              // lets the player "see" him to talk
        talk.isTrigger = true;
        talk.size = new Vector2(1.2f, 0.9f);
        talk.offset = new Vector2(0f, 0.45f);

        var dsr = dogGo.GetComponent<SpriteRenderer>();
        dsr.sprite = sheet.ContainsKey("4_0") ? sheet["4_0"] : null;

        var so = new SerializedObject(dog);
        SetArray(so, "walkSide", Cells(sheet, 0, 0, 4));
        SetArray(so, "walkDown", Cells(sheet, 4, 0, 4));
        SetArray(so, "walkUp", Cells(sheet, 0, 1, 4));
        SetArray(so, "sitIdle", Cells(sheet, 4, 1, 4));
        SetArray(so, "rollOver", Cells(sheet, 0, 2, 7));
        SetArray(so, "scratchEar", Cells(sheet, 0, 3, 4));
        SetObj(so, "portraitIdle", faces.ContainsKey("0_0") ? faces["0_0"] : null);
        SetObj(so, "portraitTalk", faces.ContainsKey("1_0") ? faces["1_0"] : null);
        SetObj(so, "portraitHappy", faces.ContainsKey("2_0") ? faces["2_0"] : null);
        SetObj(so, "portraitBlink", faces.ContainsKey("3_0") ? faces["3_0"] : null);
        SetObj(so, "dialoguePanel", Load(dir + "/ui_dialog_panel.png"));
        SetObj(so, "portraitFrame", Load(dir + "/ui_dialog_frame.png"));
        so.ApplyModifiedProperties();

        EditorUtility.SetDirty(dogGo);
        EditorUtility.SetDirty(houseGo);
        Selection.activeGameObject = dogGo;
        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(dogGo.scene);

        EditorUtility.DisplayDialog("Meatball is ready",
            "Added a DogHouse and Meatball to the scene. Move them anywhere you like.\n\n" +
            "To make Meatball tell the player what villagers like: create NPC Profile assets (Project window > Create > Farm > NPC Profile) " +
            "and drag them into the 'Known Npcs' list on Meatball.", "OK");
    }

    // ------------------------------------------------------------------
    // Helpers
    // ------------------------------------------------------------------

    private static string FindFolder(string fileName)
    {
        foreach (string guid in AssetDatabase.FindAssets(fileName + " t:Texture2D"))
        {
            string p = AssetDatabase.GUIDToAssetPath(guid);
            if (Path.GetFileNameWithoutExtension(p) == fileName) return Path.GetDirectoryName(p).Replace('\\', '/');
        }
        return null;
    }

    private static Sprite Load(string path) { return AssetDatabase.LoadAssetAtPath<Sprite>(path); }

    private static void ImportSingle(string path, float ppu, Vector4 border, SpriteAlignment align)
    {
        var imp = AssetImporter.GetAtPath(path) as TextureImporter;
        if (imp == null) return;
        imp.textureType = TextureImporterType.Sprite;
        imp.spriteImportMode = SpriteImportMode.Single;
        imp.spritePixelsPerUnit = ppu;
        imp.filterMode = FilterMode.Point;
        imp.textureCompression = TextureImporterCompression.Uncompressed;
        imp.mipmapEnabled = false;
        imp.alphaIsTransparency = true;
        imp.spriteBorder = border;
        var settings = new TextureImporterSettings();
        imp.ReadTextureSettings(settings);
        settings.spriteAlignment = (int)align;
        settings.spriteMeshType = SpriteMeshType.FullRect;
        imp.SetTextureSettings(settings);
        imp.SaveAndReimport();
    }

    /// <summary>Cuts a sheet into equal cells. Returns "column_row" (row from the top) -> sprite, skipping empty cells.</summary>
    private static Dictionary<string, Sprite> SliceSheet(string path, int cellW, int cellH, float ppu, SpriteAlignment align)
    {
        var imp = (TextureImporter)AssetImporter.GetAtPath(path);
        imp.textureType = TextureImporterType.Sprite;
        imp.spriteImportMode = SpriteImportMode.Multiple;
        imp.spritePixelsPerUnit = ppu;
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

        var result = new Dictionary<string, Sprite>();
        foreach (Object o in AssetDatabase.LoadAllAssetsAtPath(path))
        {
            var s = o as Sprite;
            if (s != null && s.name.StartsWith(baseName + "_")) result[s.name.Substring(baseName.Length + 1)] = s;
        }
        return result;
    }

    private static Sprite[] Cells(Dictionary<string, Sprite> sheet, int firstCol, int row, int count)
    {
        var list = new List<Sprite>();
        for (int i = 0; i < count; i++)
        {
            Sprite s;
            if (sheet.TryGetValue((firstCol + i) + "_" + row, out s)) list.Add(s);
        }
        return list.ToArray();
    }

    private static void SetArray(SerializedObject so, string field, Sprite[] sprites)
    {
        SerializedProperty p = so.FindProperty(field);
        if (p == null) { Debug.LogWarning("MeatballDog has no field called " + field); return; }
        p.arraySize = sprites.Length;
        for (int i = 0; i < sprites.Length; i++) p.GetArrayElementAtIndex(i).objectReferenceValue = sprites[i];
    }

    private static void SetObj(SerializedObject so, string field, Object value)
    {
        SerializedProperty p = so.FindProperty(field);
        if (p != null) p.objectReferenceValue = value;
        else Debug.LogWarning("No field called " + field);
    }
}
