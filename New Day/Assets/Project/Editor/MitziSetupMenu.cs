using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Farm > NPCs > Set Up Mitzi
/// Imports Mitzi's art (copy the "Cat" folder into your Assets first), then puts a cat tree, a Douglas fir, a sunny spot and Mitzi
/// into the open scene near your house and wires everything up. Safe to run again: it reuses what is already in the scene.
/// </summary>
public static class MitziSetupMenu
{
    [MenuItem("Farm/NPCs/Set Up Mitzi")]
    public static void Run()
    {
        string dir = FindFolder("mitzi_sheet");
        if (dir == null)
        {
            EditorUtility.DisplayDialog("Mitzi art not found",
                "Couldn't find mitzi_sheet.png in your project.\nCopy the 'Cat' folder into Assets (for example Assets/Art/Cat) and run this again.", "OK");
            return;
        }

        // ---- 1. import ----
        Dictionary<string, Sprite> sheet = SliceSheet(dir + "/mitzi_sheet.png", 24, 24, 16f, SpriteAlignment.BottomCenter);
        Dictionary<string, Sprite> faces = SliceSheet(dir + "/mitzi_portraits.png", 64, 64, 50f, SpriteAlignment.Center);
        ImportSingle(dir + "/douglas_fir.png", 16f, Vector4.zero, SpriteAlignment.BottomCenter);
        ImportSingle(dir + "/cat_tree.png", 16f, Vector4.zero, SpriteAlignment.BottomCenter);
        ImportSingle(dir + "/sun_patch.png", 16f, Vector4.zero, SpriteAlignment.Center);
        ImportSingle(dir + "/toy_mouse.png", 16f, Vector4.zero, SpriteAlignment.Center);
        ImportSingle(dir + "/toy_ball.png", 16f, Vector4.zero, SpriteAlignment.Center);
        AssetDatabase.Refresh();

        // the dialogue box art comes with the Meatball pack; Mitzi works without it (plain box)
        Sprite panel = FindSprite("ui_dialog_panel");
        Sprite frame = FindSprite("ui_dialog_frame");

        // ---- 2. where to put things: near the house ----
        Vector3 basePos = new Vector3(0f, 0f, 0f);
        HouseDoor door = null;
        foreach (HouseDoor d in Object.FindObjectsByType<HouseDoor>())
        {
            // the OUTSIDE door is the one that leads into an interior room
            var dso = new SerializedObject(d);
            SerializedProperty into = dso.FindProperty("enterInterior");
            if (into != null && into.objectReferenceValue != null) { door = d; break; }
        }
        var player = Object.FindAnyObjectByType<PlayerController>();
        if (door != null) basePos = door.transform.position + new Vector3(0f, -3f, 0f);
        else if (player != null) basePos = player.transform.position + new Vector3(0f, -3f, 0f);

        // ---- 3. the cat tree ----
        var treeComp = Object.FindAnyObjectByType<CatTree>();
        GameObject treeGo;
        if (treeComp == null)
        {
            treeGo = new GameObject("CatTree");
            Undo.RegisterCreatedObjectUndo(treeGo, "Create Cat Tree");
            treeGo.transform.position = basePos + new Vector3(4f, 0f, 0f);
            treeComp = treeGo.AddComponent<CatTree>();
        }
        else treeGo = treeComp.gameObject;
        SetupProp(treeGo, Load(dir + "/cat_tree.png"), new Vector2(1.2f, 0.4f), new Vector2(0f, 0.25f), 0.3f);

        Transform ground = Child(treeGo.transform, "GroundPoint", new Vector3(0.2f, -0.45f, 0f));
        Transform perchTop = Child(treeGo.transform, "Perch_Top", new Vector3(0.12f, 2.45f, 0f));
        Transform perchMid = Child(treeGo.transform, "Perch_Mid", new Vector3(-0.15f, 1.15f, 0f));
        var tso = new SerializedObject(treeComp);
        SetObj(tso, "groundPoint", ground);
        SerializedProperty perches = tso.FindProperty("perchPoints");
        perches.arraySize = 2;
        perches.GetArrayElementAtIndex(0).objectReferenceValue = perchTop;
        perches.GetArrayElementAtIndex(1).objectReferenceValue = perchMid;
        tso.ApplyModifiedProperties();

        // ---- 4. the Douglas fir ----
        GameObject firGo = GameObject.Find("DouglasFir");
        if (firGo == null)
        {
            firGo = new GameObject("DouglasFir");
            Undo.RegisterCreatedObjectUndo(firGo, "Create Douglas Fir");
            firGo.transform.position = basePos + new Vector3(-4f, 0.5f, 0f);
        }
        SetupProp(firGo, Load(dir + "/douglas_fir.png"), new Vector2(0.5f, 0.4f), new Vector2(0f, 0.25f), 0.3f);
        Transform shade = Child(firGo.transform, "ShadePoint", new Vector3(1.1f, -0.15f, 0f));

        // ---- 5. the sunny spot ----
        GameObject sunGo = GameObject.Find("MitziSunSpot");
        if (sunGo == null)
        {
            sunGo = new GameObject("MitziSunSpot");
            Undo.RegisterCreatedObjectUndo(sunGo, "Create Sun Spot");
            sunGo.transform.position = basePos + new Vector3(0f, -2.2f, 0f);
        }
        var patch = sunGo.GetComponent<SpriteRenderer>();
        if (patch == null) patch = sunGo.AddComponent<SpriteRenderer>();
        patch.sprite = Load(dir + "/sun_patch.png");
        patch.sortingOrder = 2;

        // ---- 6. Mitzi ----
        var cat = Object.FindAnyObjectByType<MitziCat>();
        GameObject catGo;
        if (cat == null)
        {
            catGo = new GameObject("Mitzi");
            Undo.RegisterCreatedObjectUndo(catGo, "Create Mitzi");
            catGo.transform.position = shade.position;
            var sr0 = catGo.AddComponent<SpriteRenderer>();
            sr0.sortingOrder = 10;
            var body = catGo.AddComponent<Rigidbody2D>();
            body.gravityScale = 0f;
            body.freezeRotation = true;
            body.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
            cat = catGo.AddComponent<MitziCat>();
        }
        else catGo = cat.gameObject;

        foreach (var c in catGo.GetComponents<Collider2D>()) Object.DestroyImmediate(c);
        var feet = catGo.AddComponent<CircleCollider2D>();
        feet.radius = 0.16f;
        feet.offset = new Vector2(0f, 0.16f);
        var talk = catGo.AddComponent<BoxCollider2D>();
        talk.isTrigger = true;
        talk.size = new Vector2(1.0f, 0.8f);
        talk.offset = new Vector2(0f, 0.4f);

        var csr = catGo.GetComponent<SpriteRenderer>();
        csr.sprite = sheet.ContainsKey("4_1") ? sheet["4_1"] : null;

        var so = new SerializedObject(cat);
        SetArray(so, "walkSide", Cells(sheet, 0, 0, 4));
        SetArray(so, "walkDown", Cells(sheet, 4, 0, 4));
        SetArray(so, "walkUp", Cells(sheet, 0, 1, 4));
        SetArray(so, "sitIdle", Cells(sheet, 4, 1, 4));
        SetArray(so, "lickPaw", Cells(sheet, 0, 2, 4));
        SetArray(so, "loaf", Cells(sheet, 4, 2, 2));
        SetArray(so, "jump", Cells(sheet, 6, 2, 2));
        SetArray(so, "rollOver", Cells(sheet, 0, 3, 6));
        SetArray(so, "hairball", Cells(sheet, 0, 4, 6));
        SetArray(so, "batToy", Cells(sheet, 0, 5, 4));
        SetObj(so, "toyMouse", Load(dir + "/toy_mouse.png"));
        SetObj(so, "toyBall", Load(dir + "/toy_ball.png"));
        SetObj(so, "portraitIdle", faces.ContainsKey("0_0") ? faces["0_0"] : null);
        SetObj(so, "portraitTalk", faces.ContainsKey("1_0") ? faces["1_0"] : null);
        SetObj(so, "portraitHappy", faces.ContainsKey("2_0") ? faces["2_0"] : null);
        SetObj(so, "portraitBlink", faces.ContainsKey("3_0") ? faces["3_0"] : null);
        SetObj(so, "dialoguePanel", panel);
        SetObj(so, "portraitFrame", frame);
        SetObj(so, "catTree", treeComp);
        SetObj(so, "shadeSpot", shade);
        SerializedProperty suns = so.FindProperty("sunSpots");
        suns.arraySize = 1;
        suns.GetArrayElementAtIndex(0).objectReferenceValue = sunGo.transform;
        SerializedProperty patches = so.FindProperty("sunPatches");
        patches.arraySize = 1;
        patches.GetArrayElementAtIndex(0).objectReferenceValue = patch;
        so.ApplyModifiedProperties();

        EditorUtility.SetDirty(catGo);
        Selection.activeGameObject = catGo;
        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(catGo.scene);

        EditorUtility.DisplayDialog("Mitzi is ready",
            "Added Mitzi, a cat tree, a Douglas fir and a sunny spot near your house. Move any of them anywhere you like " +
            "(the fir's ShadePoint and the cat tree's Perch points are child objects you can nudge).\n\n" +
            "Tip: if Meatball isn't installed, the dialogue box uses a plain background. Add the Meatball pack for the wooden box art.", "OK");
    }

    // ------------------------------------------------------------------
    // Helpers
    // ------------------------------------------------------------------

    private static void SetupProp(GameObject go, Sprite sprite, Vector2 colSize, Vector2 colOffset, float sorterOffset)
    {
        var sr = go.GetComponent<SpriteRenderer>();
        if (sr == null) sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = sprite;
        sr.sortingOrder = 10;
        foreach (var c in go.GetComponents<Collider2D>()) Object.DestroyImmediate(c);
        var box = go.AddComponent<BoxCollider2D>();
        box.size = colSize;
        box.offset = colOffset;
        var sorter = go.GetComponent<BehindPlayerSorter>();
        if (sorter == null) sorter = go.AddComponent<BehindPlayerSorter>();
        sorter.thresholdOffset = sorterOffset;
    }

    private static Transform Child(Transform parent, string name, Vector3 localPos)
    {
        Transform t = parent.Find(name);
        if (t == null)
        {
            var g = new GameObject(name);
            g.transform.SetParent(parent, false);
            g.transform.localPosition = localPos;
            t = g.transform;
        }
        return t;
    }

    private static Sprite FindSprite(string name)
    {
        foreach (string guid in AssetDatabase.FindAssets(name + " t:Sprite"))
        {
            string p = AssetDatabase.GUIDToAssetPath(guid);
            if (Path.GetFileNameWithoutExtension(p) == name) return AssetDatabase.LoadAssetAtPath<Sprite>(p);
        }
        return null;
    }

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
        if (p == null) { Debug.LogWarning("The script has no field called " + field); return; }
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
