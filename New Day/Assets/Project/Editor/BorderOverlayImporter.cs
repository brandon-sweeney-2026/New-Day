using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Sets up the 3 map-edge overlays. Menu: Farm > Biomes > Build Edge Overlay Prefabs.
///
/// Before running it, copy the "Borders" folder (border_forest.png, border_city.png, border_mountain.png) into your project,
/// e.g. Assets/Farm/Biomes/Borders. It imports them as pixel-art sprites that repeat sideways (top-centre pivot)
/// and makes a ready-to-use prefab for each (Forest, City, Mountain) with a MapEdgeOverlay component.
/// Safe to run again. This file must live in a folder named "Editor".
/// </summary>
public static class BorderOverlayImporter
{
    private static readonly string[] Names = { "border_forest", "border_city", "border_mountain" };
    private static readonly string[] Titles = { "ForestBorder", "CityBorder", "MountainBorder" };

    [MenuItem("Farm/Biomes/Build Edge Overlay Prefabs")]
    public static void Build()
    {
        var made = new System.Text.StringBuilder();
        for (int i = 0; i < Names.Length; i++)
        {
            string path = Find(Names[i]);
            if (path == null) { made.AppendLine(Names[i] + ".png not found (skipped)"); continue; }

            var imp = (TextureImporter)AssetImporter.GetAtPath(path);
            imp.textureType = TextureImporterType.Sprite;
            imp.spriteImportMode = SpriteImportMode.Single;
            var ts = new TextureImporterSettings();
            imp.ReadTextureSettings(ts);
            ts.textureType = TextureImporterType.Sprite;
            ts.spriteMode = (int)SpriteImportMode.Single;
            ts.spriteAlignment = (int)SpriteAlignment.TopCenter;     // hangs down from the map edge
            ts.spritePixelsPerUnit = 16f;
            ts.spriteMeshType = SpriteMeshType.FullRect;             // needed so it can repeat sideways
            ts.filterMode = FilterMode.Point;
            ts.wrapMode = TextureWrapMode.Repeat;
            ts.mipmapEnabled = false;
            imp.SetTextureSettings(ts);
            imp.textureCompression = TextureImporterCompression.Uncompressed;
            imp.SaveAndReimport();

            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
            string folder = Path.GetDirectoryName(path).Replace('\\', '/');
            string prefabPath = folder + "/" + Titles[i] + ".prefab";

            var go = new GameObject(Titles[i]);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = sprite;
            sr.drawMode = SpriteDrawMode.Tiled;
            sr.tileMode = SpriteTileMode.Continuous;
            sr.size = new Vector2(16f, 5f);
            sr.sortingOrder = 50;
            go.AddComponent<MapEdgeOverlay>();

            PrefabUtility.SaveAsPrefabAsset(go, prefabPath);
            Object.DestroyImmediate(go);
            made.AppendLine(Titles[i] + " prefab created in " + folder);
        }

        AssetDatabase.SaveAssets();
        string msg = made + "\nDrag a prefab into your scene, choose its Side, drag your ground Tilemap into 'Map To Fit',\nthen use the component's three-dot menu > Place Along Map Edge.";
        Debug.Log(msg);
        EditorUtility.DisplayDialog("Edge overlays ready", msg, "OK");
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
