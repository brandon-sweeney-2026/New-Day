using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Makes the invisible-barrier prefabs for every level editor: Barrier_1x1, Barrier_4x1 (wide), Barrier_1x4 (tall), Barrier_4x4.
/// They are made automatically the first time Unity compiles this, and you can remake them with Farm > Level Editors > Make Barrier Prefabs.
/// They carry the label that puts them in the Objects palette of the Farm Level Editor (also used for the mountain) and the Forest Level Editor.
/// In the Farm Level Editor press "Reload this palette from labelled assets" on the Objects layer once to see them.
/// Must live in a folder named "Editor".
/// </summary>
public static class BarrierPrefabMenu
{
    private const string Folder = "Assets/Farm/Data/Barriers";

    [InitializeOnLoadMethod]
    private static void AutoMake()
    {
        EditorApplication.delayCall += () =>
        {
            if (!AssetDatabase.IsValidFolder(Folder) || AssetDatabase.LoadAssetAtPath<GameObject>(Folder + "/Barrier_1x1.prefab") == null)
                Make();
        };
    }

    [MenuItem("Farm/Level Editors/Make Barrier Prefabs", false, 32)]
    public static void MakeMenu()
    {
        Make();
        EditorUtility.DisplayDialog("Barriers", "Made Barrier_1x1, Barrier_4x1, Barrier_1x4 and Barrier_4x4 in " + Folder +
            ".\n\nIn the Farm Level Editor: Objects layer > 'Edit palette list' > 'Reload this palette from labelled assets'. The Forest Level Editor finds them when you press 'Reload palettes'.", "OK");
    }

    /// <summary>Called by the level editors: makes the barrier prefabs only if they are missing.</summary>
    public static void EnsureBarriers()
    {
        if (AssetDatabase.LoadAssetAtPath<GameObject>(Folder + "/Barrier_1x1.prefab") == null) Make();
    }

    private static void Make()
    {
        EnsureFolder(Folder);
        string png = Folder + "/barrier_marker.png";
        if (!File.Exists(png))
        {
            File.WriteAllBytes(png, DrawMarker().EncodeToPNG());
            AssetDatabase.ImportAsset(png);
        }
        var imp = AssetImporter.GetAtPath(png) as TextureImporter;
        if (imp != null)
        {
            imp.textureType = TextureImporterType.Sprite;
            imp.spriteImportMode = SpriteImportMode.Single;
            imp.spritePixelsPerUnit = 16f;
            imp.filterMode = FilterMode.Point;
            imp.mipmapEnabled = false;
            imp.textureCompression = TextureImporterCompression.Uncompressed;
            imp.alphaIsTransparency = true;
            var settings = new TextureImporterSettings();
            imp.ReadTextureSettings(settings);
            settings.spriteAlignment = (int)SpriteAlignment.BottomCenter;
            settings.spriteMeshType = SpriteMeshType.FullRect;
            imp.SetTextureSettings(settings);
            imp.SaveAndReimport();
        }
        var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(png);

        MakePrefab("Barrier_1x1", new Vector2(1f, 1f), sprite);
        MakePrefab("Barrier_4x1", new Vector2(4f, 1f), sprite);
        MakePrefab("Barrier_1x4", new Vector2(1f, 4f), sprite);
        MakePrefab("Barrier_4x4", new Vector2(4f, 4f), sprite);
        AssetDatabase.SaveAssets();
    }

    private static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path)) return;
        string parent = Path.GetDirectoryName(path).Replace('\\', '/');
        EnsureFolder(parent);
        AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
    }

    private static void MakePrefab(string name, Vector2 size, Sprite sprite)
    {
        string path = Folder + "/" + name + ".prefab";
        if (AssetDatabase.LoadAssetAtPath<GameObject>(path) != null) return;
        var go = new GameObject(name);
        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = sprite;
        sr.drawMode = SpriteDrawMode.Tiled;
        sr.size = size;
        sr.sortingOrder = 500;
        var col = go.AddComponent<BoxCollider2D>();
        col.size = size; col.offset = new Vector2(0f, size.y * 0.5f);
        var b = go.AddComponent<InvisibleBarrier>();
        b.size = size;
        GameObject prefab = PrefabUtility.SaveAsPrefabAsset(go, path);
        Object.DestroyImmediate(go);
        AssetDatabase.SetLabels(prefab, new[] { FarmEditorLabels.Objects });
    }

    /// <summary>16x16: a translucent red box with diagonal hatching and a white edge.</summary>
    private static Texture2D DrawMarker()
    {
        const int N = 16;
        var t = new Texture2D(N, N, TextureFormat.RGBA32, false);
        var px = new Color32[N * N];
        for (int y = 0; y < N; y++)
            for (int x = 0; x < N; x++)
            {
                bool edge = x == 0 || y == 0 || x == N - 1 || y == N - 1;
                bool hatch = (x + y) % 6 < 2;
                Color32 c = edge ? new Color32(255, 255, 255, 230) : hatch ? new Color32(255, 70, 70, 190) : new Color32(255, 70, 70, 70);
                px[y * N + x] = c;
            }
        t.SetPixels32(px);
        t.Apply();
        return t;
    }
}
