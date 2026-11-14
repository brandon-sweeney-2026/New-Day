using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// The MASTER level editor: Farm > Level Editors > Open Level Editor (This Scene)   (shortcut Ctrl+Shift+L)
///
/// It looks at the scene you have open and opens the right painting editor for it:
///   - a forest scene (has ForestRoom / ForestGrove / ForestEditGrid, or "forest" in the scene name) -> Forest Level Editor
///   - anything else (the farm, house, mountain, town...)                                          -> Farm Level Editor
/// Switching scenes and pressing the shortcut again swaps to the right one. The other editor window is closed so you never paint
/// with the wrong one. The individual editors are still listed in the same menu if you want to force one.
/// </summary>
public static class LevelEditorHub
{
    [MenuItem("Farm/Level Editors/Open Level Editor (This Scene) %#l", false, 0)]
    public static void OpenForThisScene()
    {
        Scene();
        bool forest = IsForestScene();

        // close the editor that does not belong to this scene
        if (forest) CloseIfOpen<FarmLevelEditorWindow>(); else CloseIfOpen<ForestLevelEditorWindow>();

        if (forest) ForestLevelEditorWindow.Open(); else FarmLevelEditorWindow.Open();
        SceneView.lastActiveSceneView?.Focus();
        Debug.Log("[Level Editor] Opened the " + (forest ? "Forest" : "Farm") + " Level Editor for scene '" + Scene().name + "'.");
    }

    private static UnityEngine.SceneManagement.Scene Scene() { return EditorSceneManager.GetActiveScene(); }

    private static bool IsForestScene()
    {
        if (Scene().name.ToLowerInvariant().Contains("forest")) return true;
        if (Object.FindAnyObjectByType<ForestRoom>(FindObjectsInactive.Include) != null) return true;
        if (Object.FindAnyObjectByType<ForestGrove>(FindObjectsInactive.Include) != null) return true;
        return GameObject.Find("ForestEditGrid") != null;
    }

    private static void CloseIfOpen<T>() where T : EditorWindow
    {
        T[] open = Resources.FindObjectsOfTypeAll<T>();
        foreach (T w in open) if (w != null) w.Close();
    }
}
