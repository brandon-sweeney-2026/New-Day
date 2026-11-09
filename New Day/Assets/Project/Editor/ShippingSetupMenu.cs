using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Farm > Shipping > Set Up Mailbox
/// Imports the mailbox art, then turns your ShippingBin (or makes one) into the blue mailbox and wires up the shipping screen art.
/// Safe to run again.
/// </summary>
public static class ShippingSetupMenu
{
    [MenuItem("Farm/Shipping/Set Up Mailbox")]
    public static void Run()
    {
        string dir = FindMailFolder();
        if (dir == null)
        {
            EditorUtility.DisplayDialog("Mailbox art not found",
                "Couldn't find mailbox_empty.png in your project.\nCopy the 'Mail' folder into Assets (for example Assets/Art/Mail) and run this again.", "OK");
            return;
        }

        // 1. import settings
        Import(dir + "/mailbox_empty.png", 16, new Vector4(0, 0, 0, 0), true);
        Import(dir + "/mailbox_full.png", 16, new Vector4(0, 0, 0, 0), true);
        Import(dir + "/ui_ship_panel.png", 50, new Vector4(10, 10, 10, 10), false);
        Import(dir + "/ui_ship_slot.png", 50, new Vector4(3, 3, 3, 3), false);
        Import(dir + "/ui_ship_select.png", 50, new Vector4(4, 4, 4, 4), false);
        Import(dir + "/orn_ship_envelope.png", 50, Vector4.zero, false);
        AssetDatabase.Refresh();

        // 2. find or create the bin
        var bin = Object.FindAnyObjectByType<ShippingBin>();
        GameObject go;
        if (bin == null)
        {
            go = new GameObject("ShippingBin");
            Undo.RegisterCreatedObjectUndo(go, "Create Mailbox");
            go.transform.position = new Vector3(4f, -2f, 0f);
            bin = go.AddComponent<ShippingBin>();
        }
        else go = bin.gameObject;
        Undo.RecordObject(go, "Set Up Mailbox");

        // 3. renderer
        var sr = go.GetComponent<SpriteRenderer>();
        if (sr == null) sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = Load(dir + "/mailbox_empty.png");
        sr.color = Color.white;
        sr.sortingOrder = 10;
        go.transform.localScale = Vector3.one;

        // 4. colliders: a big trigger so the player can interact from nearby, and a small solid base
        foreach (var c in go.GetComponents<Collider2D>()) Object.DestroyImmediate(c);
        var trigger = go.AddComponent<BoxCollider2D>();
        trigger.isTrigger = true;
        trigger.size = new Vector2(1.6f, 1.8f);
        trigger.offset = new Vector2(0f, 0.8f);
        var solid = go.AddComponent<BoxCollider2D>();
        solid.isTrigger = false;
        solid.size = new Vector2(0.5f, 0.3f);
        solid.offset = new Vector2(0f, 0.15f);

        // 5. walk behind / in front
        var sorter = go.GetComponent<BehindPlayerSorter>();
        if (sorter == null) sorter = go.AddComponent<BehindPlayerSorter>();
        sorter.thresholdOffset = 0.2f;

        // 6. wire the sprites
        var so = new SerializedObject(bin);
        Set(so, "mailboxRenderer", sr);
        Set(so, "emptySprite", Load(dir + "/mailbox_empty.png"));
        Set(so, "fullSprite", Load(dir + "/mailbox_full.png"));
        Set(so, "panelSprite", Load(dir + "/ui_ship_panel.png"));
        Set(so, "slotSprite", Load(dir + "/ui_ship_slot.png"));
        Set(so, "selectSprite", Load(dir + "/ui_ship_select.png"));
        Set(so, "envelopeSprite", Load(dir + "/orn_ship_envelope.png"));

        // the black glass box for the total: reuse the HUD's if it exists
        string[] lcd = AssetDatabase.FindAssets("ui_lcd_window t:Sprite");
        if (lcd.Length > 0)
            Set(so, "totalBoxSprite", AssetDatabase.LoadAssetAtPath<Sprite>(AssetDatabase.GUIDToAssetPath(lcd[0])));
        so.ApplyModifiedProperties();

        EditorUtility.SetDirty(go);
        Selection.activeGameObject = go;
        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(go.scene);
        Debug.Log("Mailbox set up. Walk up to it and press the action button to open the shipping screen. Move the object wherever you like.");
    }

    // ---- helpers ----

    private static string FindMailFolder()
    {
        foreach (string guid in AssetDatabase.FindAssets("mailbox_empty t:Texture2D"))
        {
            string p = AssetDatabase.GUIDToAssetPath(guid);
            if (Path.GetFileName(p) == "mailbox_empty.png") return Path.GetDirectoryName(p).Replace('\\', '/');
        }
        return null;
    }

    private static void Import(string path, int ppu, Vector4 border, bool bottomPivot)
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
        settings.spriteAlignment = (int)(bottomPivot ? SpriteAlignment.BottomCenter : SpriteAlignment.Center);
        settings.spriteMeshType = SpriteMeshType.FullRect;
        imp.SetTextureSettings(settings);
        imp.SaveAndReimport();
    }

    private static Sprite Load(string path)
    {
        return AssetDatabase.LoadAssetAtPath<Sprite>(path);
    }

    private static void Set(SerializedObject so, string field, Object value)
    {
        var p = so.FindProperty(field);
        if (p != null) p.objectReferenceValue = value;
        else Debug.LogWarning("ShippingBin has no field called " + field);
    }
}
