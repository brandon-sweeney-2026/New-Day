using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Applies the HUD artwork. Menu: Farm > UI > Apply HUD Art.
///
/// Before running it, copy the "UI" folder (the ui_*.png / orn_*.png files) into your project, e.g. Assets/Farm/UI.
/// It then imports each picture as crisp pixel art (2 screen pixels per art pixel), sets up the 9-slice borders so frames
/// stretch without blurring, and fills in the art slots on the GameHUD object in your open scene.
///
///   Bottom inventory   : dark forest (bark frame, leaves, mushrooms)
///   Time / calendar    : city (steel frame, skyline, street lamp)
///   Health / magic     : magic mountain (peaks, crystals) plus the player's face in a ring
///
/// Safe to run again. This file must live in a folder named "Editor".
/// </summary>
public static class HudArtMenu
{
    private const float PixelsPerUnit = 50f;     // 100 / 50 = every art pixel is 2 screen pixels at the HUD's reference size

    // file name -> (GameHUD field, 9-slice border in art pixels; 0 = not sliced)
    private static readonly (string file, string field, int border)[] Map =
    {
        ("ui_forest_panel",     "forestPanel",     12),
        ("ui_forest_slot",      "forestSlot",      3),
        ("ui_forest_select",    "forestSelected",  4),
        ("orn_forest_leaves",   "leavesOrnament",  0),
        ("orn_forest_mushrooms","mushroomOrnament",0),
        ("ui_city_panel",       "cityPanel",       10),
        ("ui_city_skyline",     "citySkyline",     0),
        ("orn_city_lamp",       "cityLamp",        0),
        ("ui_mountain_panel",   "mountainPanel",   10),
        ("orn_mountain_peaks",  "mountainPeaks",   0),
        ("orn_mountain_crystals","crystalOrnament",0),
        ("ui_mountain_slot",    "mountainSlot",    3),
        ("ui_bar_back",         "barBack",         2),
        ("ui_bar_fill",         "barFill",         0),
        ("ui_lcd_window",       "lcdWindow",       3),
        ("orn_alarm_bell",      "alarmBell",       0),
        ("ui_face",             "portraitFace",    0),
        ("ui_portrait_frame",   "portraitFrame",   0),
    };

    [MenuItem("Farm/UI/Apply HUD Art")]
    public static void Apply()
    {
        var hud = Object.FindAnyObjectByType<GameHUD>();
        var so = hud != null ? new SerializedObject(hud) : null;

        int done = 0;

        // The two letter strips are read as plain textures (the HUD cuts them up itself).
        foreach (var t in new[] { ("ui_lcd_digits", "lcdDigits"), ("ui_lcd_font", "lcdFont") })
        {
            string tp = Find(t.Item1);
            if (tp == null) continue;
            var ti = (TextureImporter)AssetImporter.GetAtPath(tp);
            ti.textureType = TextureImporterType.Default;
            ti.filterMode = FilterMode.Point;
            ti.mipmapEnabled = false;
            ti.npotScale = TextureImporterNPOTScale.None;
            ti.alphaIsTransparency = true;
            ti.textureCompression = TextureImporterCompression.Uncompressed;
            ti.SaveAndReimport();
            if (so != null)
            {
                SerializedProperty tprop = so.FindProperty(t.Item2);
                if (tprop != null) tprop.objectReferenceValue = AssetDatabase.LoadAssetAtPath<Texture2D>(tp);
            }
            done++;
        }
        var missing = new System.Collections.Generic.List<string>();

        foreach (var m in Map)
        {
            string path = Find(m.file);
            if (path == null) { missing.Add(m.file + ".png"); continue; }

            var imp = (TextureImporter)AssetImporter.GetAtPath(path);
            imp.textureType = TextureImporterType.Sprite;
            imp.spriteImportMode = SpriteImportMode.Single;
            var ts = new TextureImporterSettings();
            imp.ReadTextureSettings(ts);
            ts.textureType = TextureImporterType.Sprite;
            ts.spriteMode = (int)SpriteImportMode.Single;
            ts.spriteAlignment = (int)SpriteAlignment.Center;
            ts.spritePixelsPerUnit = PixelsPerUnit;
            ts.filterMode = FilterMode.Point;
            ts.mipmapEnabled = false;
            ts.spriteMeshType = SpriteMeshType.FullRect;
            imp.SetTextureSettings(ts);
            imp.spriteBorder = m.border > 0 ? new Vector4(m.border, m.border, m.border, m.border) : Vector4.zero;
            imp.textureCompression = TextureImporterCompression.Uncompressed;
            imp.SaveAndReimport();
            done++;

            if (so != null)
            {
                SerializedProperty p = so.FindProperty(m.field);
                if (p != null) p.objectReferenceValue = AssetDatabase.LoadAssetAtPath<Sprite>(path);
            }
        }

        if (so != null) { so.ApplyModifiedProperties(); EditorUtility.SetDirty(hud); }

        string msg = done + " pictures imported.";
        if (hud == null) msg += "\n\nNo GameHUD found in the open scene. Open your farm scene and run this again to assign them.";
        else msg += "\nAssigned to the GameHUD. Press Play to see them.";
        if (missing.Count > 0) msg += "\n\nNot found (copy them into your project): " + string.Join(", ", missing.ToArray());
        Debug.Log(msg);
        EditorUtility.DisplayDialog("HUD art", msg, "OK");
    }

    private static string Find(string name)
    {
        foreach (string g in AssetDatabase.FindAssets(name + " t:Texture2D"))
        {
            string p = AssetDatabase.GUIDToAssetPath(g);
            if (Path.GetFileNameWithoutExtension(p) == name) return p;
        }
        return null;
    }
}
