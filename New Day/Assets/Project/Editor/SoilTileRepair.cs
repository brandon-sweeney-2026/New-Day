using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Tilemaps;

/// <summary>
/// Farm > Fix and Diagnose > Fix Soil Tiles
///
/// Use this when the hoe "does nothing": the FarmManager in the scene has lost its Tilled Soil Tile and/or Watered Soil Tile,
/// so the ground IS tilled but no picture is drawn for it.
/// For every FarmManager in the open scene(s) this:
///   1. fills an EMPTY Tilled Soil Tile with the "Tilled" tile and an EMPTY Watered Soil Tile with the "Wet" tile
///      (found by their Level Editor labels, FarmLayer_Soil and FarmLayer_Wet);
///   2. removes empty slots from the Tillable Ground Tiles list (a slot goes empty when its tile asset is broken or deleted).
/// It never replaces a tile you already chose. Ctrl+Z undoes it. Save the scene afterwards.
/// Must live in a folder named "Editor".
/// </summary>
public static class SoilTileRepair
{
    [MenuItem("Farm/Fix and Diagnose/Fix Soil Tiles", false, 423)]
    public static void Fix()
    {
        TileBase tilled = FindTile(FarmEditorLabels.Soil, "Tilled");
        TileBase wet = FindTile(FarmEditorLabels.Wet, "Wet");

        FarmManager[] managers = Object.FindObjectsByType<FarmManager>(FindObjectsInactive.Include);
        if (managers.Length == 0)
        {
            EditorUtility.DisplayDialog("Fix soil tiles", "There is no FarmManager in the open scene. Open your farm scene and run this again.", "OK");
            return;
        }

        var report = new List<string>();
        foreach (FarmManager manager in managers)
        {
            var so = new SerializedObject(manager);
            bool changed = false;

            changed |= FillIfEmpty(so, "tilledSoilTile", tilled, "Tilled Soil Tile", report);
            changed |= FillIfEmpty(so, "wateredSoilTile", wet, "Watered Soil Tile", report);

            // Empty slots in the "where can I till" list do nothing, but they hide the fact that a tile was lost.
            SerializedProperty list = so.FindProperty("tillableGroundTiles");
            int removed = 0;
            if (list != null)
                for (int i = list.arraySize - 1; i >= 0; i--)
                    if (list.GetArrayElementAtIndex(i).objectReferenceValue == null)
                    {
                        list.DeleteArrayElementAtIndex(i);
                        removed++;
                    }
            if (removed > 0)
            {
                report.Add("Removed " + removed + " empty slot(s) from Tillable Ground Tiles (the tile that was there is broken or deleted).");
                changed = true;
            }

            if (changed)
            {
                so.ApplyModifiedProperties();              // also records the change for Ctrl+Z
                EditorSceneManager.MarkSceneDirty(manager.gameObject.scene);
            }
        }

        EditorUtility.DisplayDialog("Fix soil tiles",
            report.Count == 0
                ? "Nothing needed fixing: the FarmManager already has both soil tiles."
                : string.Join("\n", report.ToArray()) + "\n\nSave the scene (Ctrl+S), then press Play and try the hoe.",
            "OK");
    }

    /// <summary>Sets a tile field only when it is empty. Returns true if it changed something.</summary>
    private static bool FillIfEmpty(SerializedObject so, string property, TileBase tile, string niceName, List<string> report)
    {
        SerializedProperty p = so.FindProperty(property);
        if (p == null || p.objectReferenceValue != null) return false;   // already set: leave it alone
        if (tile == null)
        {
            report.Add(niceName + " is empty, and no tile was found to put there. Run Farm > Build Art > Kiln Harvest > 1. Import Pack Assets, then run this again.");
            return false;
        }
        p.objectReferenceValue = tile;
        report.Add("Set " + niceName + " to '" + tile.name + "'.");
        return true;
    }

    /// <summary>Finds a tile by its Level Editor label (preferring the usual name), or by name if nothing has the label.</summary>
    private static TileBase FindTile(string label, string usualName)
    {
        TileBase first = null;
        foreach (string guid in AssetDatabase.FindAssets("l:" + label))
        {
            var tile = AssetDatabase.LoadAssetAtPath<TileBase>(AssetDatabase.GUIDToAssetPath(guid));
            if (tile == null) continue;
            if (tile.name == usualName) return tile;
            if (first == null) first = tile;
        }
        if (first != null) return first;

        foreach (string guid in AssetDatabase.FindAssets(usualName))
        {
            var tile = AssetDatabase.LoadAssetAtPath<TileBase>(AssetDatabase.GUIDToAssetPath(guid));
            if (tile != null && tile.name == usualName) return tile;
        }
        return null;
    }
}
