using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Small helpers shared by the suburb setup menus (Kim family, Frank's store, the houses, the residents): art import, finding assets,
/// setting Inspector fields from code. Must live in a folder named "Editor".
/// </summary>
public static class HardwareEditorUtil
{
    public static string FindFolder(string fileName)
    {
        foreach (string guid in AssetDatabase.FindAssets(fileName + " t:Texture2D"))
        {
            string p = AssetDatabase.GUIDToAssetPath(guid);
            if (Path.GetFileNameWithoutExtension(p) == fileName) return Path.GetDirectoryName(p).Replace('\\', '/');
        }
        return null;
    }

    public static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path)) return;
        string parent = Path.GetDirectoryName(path).Replace('\\', '/');
        EnsureFolder(parent);
        AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
    }

    public static T LoadOrCreate<T>(string path) where T : ScriptableObject
    {
        var a = AssetDatabase.LoadAssetAtPath<T>(path);
        if (a != null) return a;
        a = ScriptableObject.CreateInstance<T>();
        AssetDatabase.CreateAsset(a, path);
        return a;
    }

    public static Sprite Spr(string folder, string name)
    {
        return AssetDatabase.LoadAssetAtPath<Sprite>(folder + "/" + name + ".png");
    }

    public static Sprite Frame(string folder, string sheet, int col, int row)
    {
        foreach (Object o in AssetDatabase.LoadAllAssetsAtPath(folder + "/" + sheet + ".png"))
        {
            var s = o as Sprite;
            if (s != null && s.name == sheet + "_" + col + "_" + row) return s;
        }
        return null;
    }

    public static void ImportSingle(string path, SpriteAlignment align)
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
        settings.spriteAlignment = (int)align;
        settings.spriteMeshType = SpriteMeshType.FullRect;
        imp.SetTextureSettings(settings);
        imp.SaveAndReimport();
    }

    public static void SliceSheet(string path, int cellW, int cellH, SpriteAlignment align)
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
                rect.pivot = align == SpriteAlignment.Center ? new Vector2(0.5f, 0.5f) : new Vector2(0.5f, 0f);
                rect.spriteID = StableSpriteId.For(provider, baseName, rect.name);   // keeps the same ID every run, so nothing loses its art
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

    /// <summary>Finds a building material item (WoodPlank, PaintBucket, FabricRoll, StoneBrick, GlassPane) made by the farmhouse setup.</summary>
    public static ItemData FindItem(string assetName)
    {
        // the usual place first, then a look through every item in the project (searching by name AND type at once proved unreliable)
        var direct = AssetDatabase.LoadAssetAtPath<ItemData>("Assets/Farm/Data/Items/" + assetName + ".asset");
        if (direct != null) return direct;
        foreach (string guid in AssetDatabase.FindAssets("t:ItemData"))
        {
            string p = AssetDatabase.GUIDToAssetPath(guid);
            if (Path.GetFileNameWithoutExtension(p) == assetName) return AssetDatabase.LoadAssetAtPath<ItemData>(p);
        }
        return null;
    }

    /// <summary>Finds a sprite by its picture's file name anywhere in the project (used for the dialogue box art).</summary>
    public static Sprite FindSprite(string fileName)
    {
        foreach (string guid in AssetDatabase.FindAssets(fileName + " t:Sprite"))
        {
            string p = AssetDatabase.GUIDToAssetPath(guid);
            if (Path.GetFileNameWithoutExtension(p) == fileName) return AssetDatabase.LoadAssetAtPath<Sprite>(p);
        }
        return null;
    }

    /// <summary>One row of an NPC walk sheet (4 frames; row 0 = facing down, 1 = up, 2 = sideways).</summary>
    public static Sprite[] Row(string folder, string sheet, int row)
    {
        var list = new List<Sprite>();
        for (int c = 0; c < 4; c++) { Sprite s = Frame(folder, sheet, c, row); if (s != null) list.Add(s); }
        return list.ToArray();
    }

    /// <summary>
    /// Fills in the pictures every ScheduledNpc needs: the walk sheet (16 x 28 cells) and the portrait sheet (four 64 x 64 faces:
    /// idle, talking, happy, blinking). Also gives the sprite the lit material, so NPCs get dark at night like everything else.
    /// </summary>
    public static void GiveNpcArt(ScheduledNpc npc, string folder, string walkSheet, string portraitSheet)
    {
        npc.down = Row(folder, walkSheet, 0); npc.up = Row(folder, walkSheet, 1); npc.side = Row(folder, walkSheet, 2);
        npc.portraitIdle = Frame(folder, portraitSheet, 0, 0); npc.portraitTalk = Frame(folder, portraitSheet, 1, 0);
        npc.portraitHappy = Frame(folder, portraitSheet, 2, 0); npc.portraitBlink = Frame(folder, portraitSheet, 3, 0);
        npc.dialoguePanel = FindSprite("ui_dialog_panel"); npc.portraitFrame = FindSprite("ui_dialog_frame");

        var sr = npc.GetComponent<SpriteRenderer>();
        if (sr == null) return;
        if (npc.down != null && npc.down.Length > 0) sr.sprite = npc.down[0];
        var lit = AssetDatabase.LoadAssetAtPath<Material>("Assets/Resources/Lighting/SpriteLit.mat");
        if (lit != null) sr.sharedMaterial = lit;
    }

    /// <summary>Looks an NpcSpot up by its id in the open scene. (NpcSpot.Find only works while the game is playing.)</summary>
    public static NpcSpot FindSpot(string id)
    {
        foreach (NpcSpot s in Object.FindObjectsByType<NpcSpot>()) if (s.spotId == id) return s;
        return null;
    }

    /// <summary>Looks a PathNode up by its id in the open scene. (PathNode.Find only works while the game is playing.)</summary>
    public static PathNode FindNode(string id)
    {
        foreach (PathNode n in Object.FindObjectsByType<PathNode>()) if (n.Id == id) return n;
        return null;
    }

    public static void SetRef(Object target, string field, Object value)
    {
        var so = new SerializedObject(target);
        SerializedProperty p = so.FindProperty(field);
        if (p == null) { Debug.LogWarning("Hardware upgrades: field '" + field + "' not found on " + target.GetType().Name); return; }
        p.objectReferenceValue = value;
        so.ApplyModifiedProperties();
    }

    public static void SetString(Object target, string field, string value)
    {
        var so = new SerializedObject(target);
        SerializedProperty p = so.FindProperty(field);
        if (p == null) return;
        p.stringValue = value;
        so.ApplyModifiedProperties();
    }

    public static void SetObjects(Object target, string field, IList<Object> values)
    {
        var so = new SerializedObject(target);
        SerializedProperty p = so.FindProperty(field);
        if (p == null) return;
        p.arraySize = values.Count;
        for (int i = 0; i < values.Count; i++) p.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
        so.ApplyModifiedProperties();
    }
}
