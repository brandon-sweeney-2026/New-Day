using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// Farm > Level Editors > Update Forest Maze
/// Brings a maze you ALREADY built up to date without rebuilding it:
///   - redoes only the scenery in every forest room: at most 'Max Scenery' items (7), dead trees twice as big, scenery casting shadows;
///   - upgrades the Forest_DeadTreeA / B prefabs (twice as big, with a shadow) so ones you painted by hand match;
///   - leaves walls, gates, stumps, door links, the fairy and everything you moved by hand exactly as they are.
/// The tutorial room keeps having no scenery. Save the scene afterwards (Ctrl+S).
/// Must live in a folder named "Editor".
/// </summary>
public static class ForestUpdateMenu
{
    [MenuItem("Farm/Level Editors/Update Forest Maze", false, 4)]
    public static void UpdateMaze()
    {
        // 1. the style assets: apply the cap
        int styles = 0;
        foreach (string guid in AssetDatabase.FindAssets("t:ForestStyle"))
        {
            var st = AssetDatabase.LoadAssetAtPath<ForestStyle>(AssetDatabase.GUIDToAssetPath(guid));
            if (st == null) continue;
            if (st.maxScenery > 7 || st.maxScenery <= 0) st.maxScenery = 7;
            if (st.deadTreeScale < 0.5f) st.deadTreeScale = 2f;
            EditorUtility.SetDirty(st);
            styles++;
        }

        // 2. the hand-placing prefabs
        int trees = 0;
        foreach (string n in new[] { "Forest_DeadTreeA", "Forest_DeadTreeB" })
            foreach (string guid in AssetDatabase.FindAssets(n + " t:Prefab"))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (System.IO.Path.GetFileNameWithoutExtension(path) != n) continue;
                GameObject root = PrefabUtility.LoadPrefabContents(path);
                UpgradeDeadTree(root, 2f);
                PrefabUtility.SaveAsPrefabAsset(root, path);
                PrefabUtility.UnloadPrefabContents(root);
                trees++;
            }
        AssetDatabase.SaveAssets();

        // 3. every room in the open scene
        int rooms = 0, tidied = 0;
        foreach (ForestRoom r in Object.FindObjectsByType<ForestRoom>(FindObjectsInactive.Include))
        {
            if (r.style == null) continue;
            if (r.name.Contains("Tutorial"))
            {
                Transform scen = r.transform.Find("Built/Scenery");
                if (scen != null) { Object.DestroyImmediate(scen.gameObject); tidied++; }
                continue;
            }
            r.RebuildScenery();
            EditorUtility.SetDirty(r);
            rooms++;
        }
        if (rooms > 0 || tidied > 0)
            EditorSceneManager.MarkSceneDirty(UnityEngine.SceneManagement.SceneManager.GetActiveScene());

        EditorUtility.DisplayDialog("Update Maze",
            "Redid the scenery in " + rooms + " room(s) (max 7 each, dead trees 2x with shadows)." +
            (tidied > 0 ? "\nKept the tutorial room clear." : "") +
            "\nUpdated " + styles + " style asset(s) and " + trees + " dead tree prefab(s)." +
            (rooms == 0 ? "\n\nNo forest rooms found in the OPEN scene - open the forest scene and run this again." : "\n\nWalls, gates and stumps were not touched. Save the scene (Ctrl+S).") +
            "\n\nNot compiled in Unity - tell me about any red errors.", "OK");
    }

    /// <summary>Makes a dead-tree prefab 'scale' times bigger, adds a shadow, and keeps its trunk collider the same size in the world.</summary>
    public static void UpgradeDeadTree(GameObject go, float scale)
    {
        go.transform.localScale = new Vector3(scale, scale, 1f);
        var col = go.GetComponent<BoxCollider2D>();
        if (col != null)
        {
            col.size = new Vector2(1f, 0.5f) / scale;
            col.offset = new Vector2(0f, 0.25f / scale);
        }
        if (go.GetComponent<ShadowCaster>() == null)
            go.AddComponent<ShadowCaster>().lengthMultiplier = 1.3f;
    }
}
