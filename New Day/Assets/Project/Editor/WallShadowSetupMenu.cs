using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Tilemaps;

/// <summary>
/// Farm > Set Up > Wall Shadows
///
/// Makes the mountain trail cliffs and any painted forest walls throw shadows:
///   - a DAYTIME sun shadow on the ground that leans with the clock, like the tree shadows (WallSunShadows draws it)
///   - a LAMP shadow: torches, lanterns, mushrooms and crystals cannot shine through them (Unity's Shadow Caster 2D)
///
/// It opens the Forest and Mine scenes one after the other, adds a WallShadowBlock (plus a Shadow Caster 2D shaped
/// like the collider) to every solid tilemap called Cliffs, Walls or Fences (only if it has a Tilemap Collider 2D),
/// saves them, and opens the scene you had open again.
///
/// Safe to run again: anything that already has a WallShadowBlock is left as it is, so your Inspector changes stay.
/// New walls get shadows by themselves (the Forest Level Editor and the Geography Brush add them).
/// The numbers below are the starting heights for walls that don't have a WallShadowBlock yet.
/// </summary>
public static class WallShadowSetupMenu
{
    /// <summary>How tall cliffs look, in tiles (longer sun shadow). Change it on the Cliffs object's WallShadowBlock afterwards.</summary>
    public const float CliffHeight = 2f;
    /// <summary>How tall the forest's painted wall tiles look, in tiles.</summary>
    public const float ForestWallHeight = 1.5f;

    private static readonly string[] Scenes = { "Assets/Project/Scenes/Forest.unity", "Assets/Project/Scenes/Mine.unity" };
    private static readonly string[] SolidTilemapNames = { "Cliffs", "Walls", "Fences" };

    [MenuItem("Farm/Set Up/Wall Shadows", false, 109)]
    public static void Run()
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;   // you pressed Cancel
        string result = Build();
        EditorUtility.DisplayDialog("Wall Shadows", result +
            "\n\nPress Play in the Forest or on the mountain trail. Daytime: walls and cliffs throw a sun shadow that leans with the clock. " +
            "Night: lamps, torches, mushrooms and crystals stop at the walls.\n\nTo test quickly, select LightingManager while playing, tick 'Use Preview Hour' and drag 'Preview Hour'." +
            "\nSettings: LightingManager (master switches), each light's 'Shadows from walls and cliffs', each wall's WallShadowBlock, and 'WallSunShadows' while playing.", "OK");
    }

    /// <summary>Does the work without pop-ups. Returns a sentence starting with "Added" on success.</summary>
    public static string Build()
    {
        string startScene = SceneManager.GetActiveScene().path;
        var lines = new List<string>();
        foreach (string path in Scenes)
        {
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(path) == null) { lines.Add(path + ": scene not found, skipped."); continue; }
            Scene scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
            int maps, kept;
            ApplyToScene(out maps, out kept);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            lines.Add(System.IO.Path.GetFileNameWithoutExtension(path) + ": " + maps + " tilemaps given shadows, " + kept + " already had them.");
        }
        if (!string.IsNullOrEmpty(startScene) && AssetDatabase.LoadAssetAtPath<SceneAsset>(startScene) != null)
            EditorSceneManager.OpenScene(startScene, OpenSceneMode.Single);
        return "Added wall shadows.\n" + string.Join("\n", lines);
    }

    /// <summary>Adds shadows to the walls and cliffs of the scene that is open now.</summary>
    public static void ApplyToScene(out int maps, out int kept)
    {
        maps = 0; kept = 0;

        // solid tilemaps (cliffs on the mountain, painted walls in the forest)
        foreach (TilemapCollider2D tc in Object.FindObjectsByType<TilemapCollider2D>(FindObjectsInactive.Include))
        {
            if (System.Array.IndexOf(SolidTilemapNames, tc.gameObject.name) < 0) continue;
            float height = tc.gameObject.name == "Cliffs" ? CliffHeight : ForestWallHeight;
            if (AddTo(tc.gameObject, height)) maps++; else kept++;
        }
    }

    /// <summary>
    /// Gives one wall or solid tilemap its shadows: a WallShadowBlock (daytime sun shadow) and a Shadow Caster 2D
    /// shaped like its collider (blocks lamp light). Returns false if it already had a WallShadowBlock (then nothing
    /// on it is changed, except that a missing Shadow Caster 2D is added again).
    /// </summary>
    public static bool AddTo(GameObject go, float height)
    {
        if (go == null) return false;
        var block = go.GetComponent<WallShadowBlock>();
        bool isNew = block == null;
        if (isNew)
        {
            block = Undo.AddComponent<WallShadowBlock>(go);
            block.height = height;
        }
        if (block.blockLamps)
        {
            Collider2D col = block.SolidCollider();
            bool hadCaster = go.GetComponent<UnityEngine.Rendering.Universal.ShadowCaster2D>() != null;
            if (col != null && (isNew || !hadCaster))
            {
                var caster = LampShadowHelper.CastFromCollider(col);
                if (caster != null) EditorUtility.SetDirty(caster);     // so the scene saves the caster's new shape setting
            }
        }
        EditorUtility.SetDirty(go);
        return isNew;
    }
}
