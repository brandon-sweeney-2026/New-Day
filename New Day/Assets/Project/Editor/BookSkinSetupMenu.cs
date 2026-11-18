using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Sets up the storybook look of the game's windows. Menu: Farm > Set Up > Book UI.
///
///  1. Imports every picture in Assets/Project/Art/UI/Book as crisp pixel art (Point filter, no compression) and gives
///     it its Border (the part at each edge that is never stretched).
///  2. Makes Assets/Resources/BookSkin.asset (the Book Skin) and fills any EMPTY picture slot with the matching file.
///     Slots you filled yourself, and every colour and number on the asset, are kept. Safe to run again.
///
/// It also runs by itself the first time the pictures are imported (BookArtImporter), so normally there is nothing to do.
/// To change the art, paint over the PNGs (same file names). The default pictures are drawn by Tools/make_book_ui_art.ps1.
/// This file must live in a folder named "Editor".
/// </summary>
public static class BookSkinSetupMenu
{
    public const string ArtFolder = "Assets/Project/Art/UI/Book";
    public const string SkinPath = "Assets/Resources/BookSkin.asset";

    /// <summary>50 pixels per unit = 2 screen pixels per art pixel at the 1280 x 720 reference size (the same as the HUD art).</summary>
    public const float PixelsPerUnit = 50f;

    /// <summary>
    /// File name -> the Book Skin slot it fills, and its border in art pixels (left, bottom, right, top).
    /// The border is only set when a picture is imported for the first time (or by this menu when it has none yet),
    /// so a border you changed in the Sprite Editor is not undone.
    /// </summary>
    public static readonly Dictionary<string, (string field, Vector4 border)> Pictures = new Dictionary<string, (string, Vector4)>
    {
        { "book_cover",     ("cover",         new Vector4(22, 22, 22, 22)) },
        { "page_single",    ("pageSingle",    new Vector4(6, 10, 10, 6)) },
        { "page_left",      ("pageLeft",      new Vector4(10, 10, 16, 6)) },
        { "page_right",     ("pageRight",     new Vector4(16, 10, 10, 6)) },
        { "gutter",         ("gutter",        Vector4.zero) },
        { "tab",            ("tab",           new Vector4(6, 4, 6, 7)) },
        { "tab_selected",   ("tabSelected",   new Vector4(6, 4, 6, 7)) },
        { "highlight",      ("highlight",     new Vector4(10, 4, 10, 4)) },
        { "rule",           ("rule",          Vector4.zero) },
        { "banner",         ("banner",        new Vector4(16, 2, 16, 2)) },
        { "divider",        ("divider",       new Vector4(12, 0, 12, 0)) },
        { "slot",           ("slot",          new Vector4(4, 4, 4, 4)) },
        { "slot_selected",  ("slotSelected",  new Vector4(5, 5, 5, 5)) },
        { "label_box",      ("labelBox",      new Vector4(6, 6, 6, 6)) },
        { "bar_back",       ("barBack",       new Vector4(2, 2, 2, 2)) },
        { "portrait_frame", ("portraitFrame", new Vector4(9, 9, 9, 9)) },
    };

    [MenuItem("Farm/Set Up/Book UI", false, 111)]
    public static void RunFromMenu()
    {
        string msg = Run(true);
        Debug.Log(msg);
        EditorUtility.DisplayDialog("Book UI", msg, "OK");
    }

    /// <summary>Does the work without pop-ups. Returns a sentence saying what happened.</summary>
    public static string Run(bool reimport)
    {
        int imported = 0;
        if (reimport)
        {
            foreach (var pair in Pictures)
            {
                string path = ArtFolder + "/" + pair.Key + ".png";
                var imp = AssetImporter.GetAtPath(path) as TextureImporter;
                if (imp == null) continue;
                if (ApplyImportSettings(imp, pair.Key, false)) { imp.SaveAndReimport(); imported++; }
            }
        }

        // Make the asset if it isn't there yet.
        BookSkin skin = AssetDatabase.LoadAssetAtPath<BookSkin>(SkinPath);
        bool made = false;
        if (skin == null)
        {
            if (!AssetDatabase.IsValidFolder("Assets/Resources")) AssetDatabase.CreateFolder("Assets", "Resources");
            skin = ScriptableObject.CreateInstance<BookSkin>();
            AssetDatabase.CreateAsset(skin, SkinPath);
            made = true;
        }

        // Fill the empty picture slots.
        var so = new SerializedObject(skin);
        int filled = 0;
        var missing = new List<string>();
        foreach (var pair in Pictures)
        {
            SerializedProperty p = so.FindProperty(pair.Value.field);
            if (p == null || p.objectReferenceValue != null) continue;
            Sprite s = AssetDatabase.LoadAssetAtPath<Sprite>(ArtFolder + "/" + pair.Key + ".png");
            if (s == null) { missing.Add(pair.Key + ".png"); continue; }
            p.objectReferenceValue = s;
            filled++;
        }
        so.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(skin);
        AssetDatabase.SaveAssets();

        string msg = (made ? "Made the Book Skin (" + SkinPath + ")" : "Updated the Book Skin") + ": " + filled + " picture slots filled";
        if (reimport) msg += ", " + imported + " pictures re-imported";
        msg += ".";
        if (missing.Count > 0) msg += "\nNot found in " + ArtFolder + ": " + string.Join(", ", missing) + ". Run Tools/make_book_ui_art.ps1 to draw them.";
        return msg;
    }

    /// <summary>
    /// Pixel-art sprite settings for one book picture. The border is set only when the sprite has none yet
    /// (or when 'firstImport' is true), so your own Sprite Editor changes stay. Returns true when something changed.
    /// </summary>
    public static bool ApplyImportSettings(TextureImporter imp, string fileName, bool firstImport)
    {
        if (!Pictures.TryGetValue(fileName, out var info)) return false;

        var before = new TextureImporterSettings();
        imp.ReadTextureSettings(before);
        Vector4 oldBorder = imp.spriteBorder;

        var ts = new TextureImporterSettings();
        imp.ReadTextureSettings(ts);
        ts.textureType = TextureImporterType.Sprite;
        ts.spriteMode = (int)SpriteImportMode.Single;
        ts.spritePixelsPerUnit = PixelsPerUnit;
        ts.filterMode = FilterMode.Point;
        ts.mipmapEnabled = false;
        ts.spriteMeshType = SpriteMeshType.FullRect;       // needed for sliced and tiled drawing
        ts.wrapMode = fileName == "rule" ? TextureWrapMode.Repeat : TextureWrapMode.Clamp;
        ts.alphaIsTransparency = true;
        imp.SetTextureSettings(ts);
        imp.textureType = TextureImporterType.Sprite;
        imp.spriteImportMode = SpriteImportMode.Single;
        imp.textureCompression = TextureImporterCompression.Uncompressed;
        imp.npotScale = TextureImporterNPOTScale.None;

        if (firstImport || imp.spriteBorder == Vector4.zero) imp.spriteBorder = info.border;

        return firstImport
            || before.textureType != TextureImporterType.Sprite
            || !Mathf.Approximately(before.spritePixelsPerUnit, PixelsPerUnit)
            || before.filterMode != FilterMode.Point
            || before.mipmapEnabled
            || before.spriteMeshType != SpriteMeshType.FullRect
            || oldBorder != imp.spriteBorder;
    }

    /// <summary>The file name without folder or ".png" when 'path' is one of the book pictures, otherwise null.</summary>
    public static string BookPictureName(string path)
    {
        if (string.IsNullOrEmpty(path)) return null;
        path = path.Replace('\\', '/');
        if (!path.StartsWith(ArtFolder + "/") || !path.EndsWith(".png", System.StringComparison.OrdinalIgnoreCase)) return null;
        string name = Path.GetFileNameWithoutExtension(path);
        return Pictures.ContainsKey(name) ? name : null;
    }
}
