using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

/// <summary>
/// Two fixes for the objects you place in the editor. Must live in a folder named "Editor".
///
/// Farm > Fix Draw Order (Y-sort)
///     Makes objects lower on the map draw in front of objects higher up, in the Scene view and the Game view:
///     Project Graphics sort mode = Custom Axis (0,1,0), the 2D Renderer asset(s) the same, and every Objects prefab and
///     every object under "Objects" in the open scene sorts by its pivot (the feet) instead of its centre.
///
/// Farm > Mountain > Shrink Ore Hitboxes
///     Shrinks the solid collider of every Ore_ prefab so it is easy to walk around them.
/// </summary>
public static class DrawOrderTools
{
    // ---- ore hitbox (in tiles). The old size was 0.85 x 0.5 at offset y 0.35. Change these and run the menu again.
    // The box sits on the very bottom of the art, as wide as WidthFraction of the art and BoxHeight tiles tall.
    private const float OffsetY = -0.1f;          // the box's centre height, set exactly (not worked out from the art)
    private const float WidthFraction = 0.6f;
    private const float BoxHeight = 0.3f;

    [MenuItem("Farm/Fix Draw Order (Y-sort)")]
    public static void FixDrawOrder()
    {
        // 1. project-wide sort mode
        GraphicsSettings.transparencySortMode = TransparencySortMode.CustomAxis;
        GraphicsSettings.transparencySortAxis = new Vector3(0f, 1f, 0f);

        // 2. the URP 2D Renderer asset(s): they keep their own copy of the setting (looked up by name, skipped if this Unity version has none)
        int renderers = 0;
        foreach (string guid in AssetDatabase.FindAssets("t:ScriptableObject"))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            if (!path.StartsWith("Assets/")) continue;
            var obj = AssetDatabase.LoadMainAssetAtPath(path);
            if (obj == null || obj.GetType().Name != "Renderer2DData") continue;
            var so = new SerializedObject(obj);
            SerializedProperty mode = so.FindProperty("m_TransparencySortMode");
            SerializedProperty axis = so.FindProperty("m_TransparencySortAxis");
            if (mode != null) mode.enumValueIndex = (int)TransparencySortMode.CustomAxis;
            if (axis != null) axis.vector3Value = new Vector3(0f, 1f, 0f);
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(obj);
            renderers++;
        }

        // 3. prefabs labelled Objects: sort by pivot
        int prefabs = 0;
        foreach (string guid in AssetDatabase.FindAssets("l:Objects t:Prefab"))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            GameObject root = PrefabUtility.LoadPrefabContents(path);
            bool changed = false;
            foreach (SpriteRenderer sr in root.GetComponentsInChildren<SpriteRenderer>(true))
                if (sr.spriteSortPoint != SpriteSortPoint.Pivot) { sr.spriteSortPoint = SpriteSortPoint.Pivot; changed = true; }
            if (changed) { PrefabUtility.SaveAsPrefabAsset(root, path); prefabs++; }
            PrefabUtility.UnloadPrefabContents(root);
        }

        // 4. what is already placed in the open scene
        int placed = 0;
        GameObject objects = GameObject.Find("Objects");
        if (objects != null)
            foreach (SpriteRenderer sr in objects.GetComponentsInChildren<SpriteRenderer>(true))
                if (sr.spriteSortPoint != SpriteSortPoint.Pivot)
                {
                    Undo.RecordObject(sr, "Sort by pivot");
                    sr.spriteSortPoint = SpriteSortPoint.Pivot;
                    placed++;
                }
        if (placed > 0) UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());

        AssetDatabase.SaveAssets();
        EditorUtility.DisplayDialog("Draw order",
            "Project sort mode: Custom Axis (0,1,0)\n2D Renderer assets updated: " + renderers +
            "\nObjects prefabs changed: " + prefabs + "\nPlaced objects changed in this scene: " + placed +
            "\n\nObjects lower on the map now draw in front. The game also sets this itself when a scene loads (DrawOrderSetup.cs)." +
            (renderers == 0 ? "\n\nNo 2D Renderer asset was changed. If order is still wrong, select your 2D Renderer asset and set Transparency Sort Mode to Custom Axis, Y = 1." : "") +
            "\n\nNot compiled in Unity - tell me about any red errors.", "OK");
    }

    /// <summary>Ore_Coal, Rock_Brown_Large, Rock_Grey_Small... Add more start-of-name words here if you have other mineable things.</summary>
    private static bool IsRockName(string n) { return n.StartsWith("Ore_") || n.StartsWith("Rock_"); }

    [MenuItem("Farm/Mountain/Shrink Ore Hitboxes")]
    public static void ShrinkOres()
    {
        int n = 0;
        foreach (string guid in AssetDatabase.FindAssets("t:Prefab"))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            if (!IsRockName(System.IO.Path.GetFileNameWithoutExtension(path))) continue;
            GameObject root = PrefabUtility.LoadPrefabContents(path);
            foreach (BoxCollider2D box in root.GetComponents<BoxCollider2D>())
                if (!box.isTrigger)
                {
                    var sr = root.GetComponent<SpriteRenderer>();
                    Bounds b = sr != null && sr.sprite != null ? sr.sprite.bounds : new Bounds(new Vector3(0f, 0.4f, 0f), new Vector3(1f, 0.8f, 0f));
                    box.size = new Vector2(Mathf.Max(0.2f, b.size.x * WidthFraction), BoxHeight);
                    box.offset = new Vector2(b.center.x, OffsetY);
                    n++;
                }
            PrefabUtility.SaveAsPrefabAsset(root, path);
            PrefabUtility.UnloadPrefabContents(root);
        }
        // ores already placed in the open scene: set them too, so a collider changed by hand in the scene (an "override") cannot keep the old offset
        int inScene = 0;
        foreach (BoxCollider2D box in Object.FindObjectsByType<BoxCollider2D>(FindObjectsInactive.Include))
        {
            if (box.isTrigger || !IsRockName(box.gameObject.name)) continue;
            var sr = box.GetComponent<SpriteRenderer>();
            Bounds b = sr != null && sr.sprite != null ? sr.sprite.bounds : new Bounds(new Vector3(0f, 0.4f, 0f), new Vector3(1f, 0.8f, 0f));
            Undo.RecordObject(box, "Ore hitbox");
            box.size = new Vector2(Mathf.Max(0.2f, b.size.x * WidthFraction), BoxHeight);
            box.offset = new Vector2(b.center.x, OffsetY);
            inScene++;
        }
        if (inScene > 0) UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());

        AssetDatabase.SaveAssets();
        EditorUtility.DisplayDialog("Ore hitboxes", "Resized the collider on " + n + " ore/rock prefab(s) and " + inScene + " ore/rock(s) in the open scene: box centre at y " + OffsetY + ", " + (WidthFraction * 100f) + "% of the art's width, " + BoxHeight + " tiles tall.\n\n" +
            "Ores already placed in a scene follow the prefab, unless you changed their collider by hand there. To change the size, edit OffsetY, WidthFraction and BoxHeight at the top of DrawOrderTools.cs.\n" +
            "If you run Mountain Setup again it rebuilds the ore prefabs at the old size, so run this again afterwards.", "OK");
    }
}
