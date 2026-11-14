using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Makes the 54 mountain boulders ready for the Level Editor. Menu: Farm > Mountain > Build Boulder Prefabs.
///
/// Put the Boulders folder (the PNGs named Boulder_3x2_Granite.png etc.) anywhere in Assets, then run this. It
///   - sets each picture up as a sprite (16 pixels per tile, crisp, standing on its bottom edge),
///   - makes a prefab next to it with a solid base (so the player walks around it and behind the top), and
///   - labels the prefab so it shows up in the Level Editor's Objects palette, grouped Boulder > 3x2 > ...
/// Sizes are width x height in tiles: 1x1 2x1 2x2 2x3 3x3 4x3 4x4 5x4 5x5, each in Granite, Sandstone, Slate, Mossy, Rust and Frost.
/// Run it again any time: prefabs you have edited are left alone. This file must live in a folder named "Editor".
/// </summary>
public static class BoulderPrefabBuilder
{
    private static readonly Regex NameRule = new Regex("^Boulder_([1-9])x([1-9])_([A-Za-z]+)$");

    [MenuItem("Farm/Mountain/Build Boulder Prefabs")]
    public static void Build()
    {
        var pngs = new List<string>();
        foreach (string guid in AssetDatabase.FindAssets("Boulder_ t:Texture2D"))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            if (NameRule.IsMatch(Path.GetFileNameWithoutExtension(path))) pngs.Add(path);
        }
        if (pngs.Count == 0)
        {
            EditorUtility.DisplayDialog("Boulders", "No Boulder_<W>x<H>_<Name>.png pictures were found. Copy the Boulders folder into your Assets folder first.", "OK");
            return;
        }

        int made = 0, kept = 0;
        try
        {
            for (int i = 0; i < pngs.Count; i++)
            {
                EditorUtility.DisplayProgressBar("Boulders", pngs[i], (float)i / pngs.Count);
                ConfigureSprite(pngs[i]);
            }
            AssetDatabase.Refresh();
            for (int i = 0; i < pngs.Count; i++)
            {
                EditorUtility.DisplayProgressBar("Boulders", "Prefab " + Path.GetFileNameWithoutExtension(pngs[i]), (float)i / pngs.Count);
                if (MakePrefab(pngs[i])) made++; else kept++;
            }
        }
        finally { EditorUtility.ClearProgressBar(); }

        AssetDatabase.SaveAssets();
        EditorUtility.DisplayDialog("Boulders",
            "Prefabs made: " + made + (kept > 0 ? "\nAlready there (left as they were): " + kept : "") +
            "\n\nOpen the Level Editor, pick the Objects layer and look for the 'Boulder' group (search 'boulder' or a size like '3x2').\nPress R to turn the boulder before you place it.\n\nNot compiled in Unity - tell me about any red errors.", "OK");
    }

    private static void ConfigureSprite(string path)
    {
        var imp = AssetImporter.GetAtPath(path) as TextureImporter;
        if (imp == null) return;
        imp.textureType = TextureImporterType.Sprite;
        imp.spriteImportMode = SpriteImportMode.Single;
        imp.spritePixelsPerUnit = 16f;
        imp.filterMode = FilterMode.Point;
        imp.mipmapEnabled = false;
        imp.textureCompression = TextureImporterCompression.Uncompressed;
        imp.alphaIsTransparency = true;
        var s = new TextureImporterSettings();
        imp.ReadTextureSettings(s);
        s.spriteAlignment = (int)SpriteAlignment.BottomCenter;
        s.spriteMeshType = SpriteMeshType.FullRect;
        imp.SetTextureSettings(s);
        imp.SaveAndReimport();
    }

    private static bool MakePrefab(string pngPath)
    {
        string name = Path.GetFileNameWithoutExtension(pngPath);
        Match m = NameRule.Match(name);
        int w = int.Parse(m.Groups[1].Value), h = int.Parse(m.Groups[2].Value);

        string folder = Path.GetDirectoryName(pngPath).Replace('\\', '/') + "/Prefabs";
        EnsureFolder(folder);
        string prefabPath = folder + "/" + name + ".prefab";
        if (AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath) != null) return false;

        var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(pngPath);
        if (sprite == null) return false;

        var go = new GameObject(name);
        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = sprite;
        sr.sortingOrder = 10;                                  // same layer as the player and other objects

        var box = go.AddComponent<BoxCollider2D>();            // solid base: the lower part of the boulder
        float height = Mathf.Max(0.5f, h * 0.5f);
        box.size = new Vector2(Mathf.Max(0.8f, w - 0.25f), height);
        box.offset = new Vector2(0f, height * 0.5f);

        GameObject prefab = PrefabUtility.SaveAsPrefabAsset(go, prefabPath);
        Object.DestroyImmediate(go);
        if (prefab == null) return false;
        AssetDatabase.SetLabels(prefab, new[] { FarmEditorLabels.Objects });
        return true;
    }

    private static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path)) return;
        string parent = Path.GetDirectoryName(path).Replace('\\', '/');
        EnsureFolder(parent);
        AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
    }
}
