using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Turning objects in the editor. Used by the Farm Level Editor and the Forest Level Editor (R / T keys and the Rotate buttons),
/// and by the menu items here for anything you have selected in the Scene:
///   Farm > Level Editors > Rotate Selected Objects Clockwise          (Ctrl+Shift+R)
///   Farm > Level Editors > Rotate Selected Objects Counter-Clockwise  (Ctrl+Alt+R)
/// An object turns around the middle of its picture (not its feet), so a boulder stays where it is. It turns in 90 degree steps.
/// This file must live in a folder named "Editor".
/// </summary>
public static class ObjectRotation
{
    /// <summary>The middle of the object's main picture, in the world.</summary>
    public static Vector3 VisualCentre(GameObject go)
    {
        SpriteRenderer best = null;
        float bestArea = -1f;
        foreach (SpriteRenderer sr in go.GetComponentsInChildren<SpriteRenderer>())
        {
            if (sr.sprite == null || sr.gameObject.name.Contains("Shadow")) continue;
            Vector3 s = sr.sprite.bounds.size;
            float area = s.x * s.y;
            if (area > bestArea) { bestArea = area; best = sr; }
        }
        if (best == null) return go.transform.position;
        return best.transform.TransformPoint(best.sprite.bounds.center);
    }

    /// <summary>Turns an object by 'degrees' (positive = counter-clockwise) around the middle of its picture. Undoable.</summary>
    public static void Rotate(GameObject go, float degrees)
    {
        if (go == null || Mathf.Approximately(degrees, 0f)) return;
        Undo.RecordObject(go.transform, "Rotate Object");
        go.transform.RotateAround(VisualCentre(go), Vector3.forward, degrees);
        go.transform.position = new Vector3(go.transform.position.x, go.transform.position.y, 0f);
        EditorUtility.SetDirty(go);
    }

    /// <summary>For a brand-new object that has no undo step of its own (it was just created).</summary>
    public static void RotateNew(GameObject go, float degrees)
    {
        if (go == null || Mathf.Approximately(degrees, 0f)) return;
        go.transform.RotateAround(VisualCentre(go), Vector3.forward, degrees);
        go.transform.position = new Vector3(go.transform.position.x, go.transform.position.y, 0f);
    }

    /// <summary>
    /// The objects under a cell: the ones standing in it, and any bigger object whose picture covers it
    /// (so you can click anywhere on a boulder, not just its foot).
    /// </summary>
    public static List<Transform> At(Transform parent, Grid grid, Vector3Int cell, bool bottomPivot)
    {
        var found = new List<Transform>();
        if (parent == null || grid == null) return found;
        Vector3 centre = grid.GetCellCenterWorld(cell);
        foreach (Transform child in parent)
        {
            Vector3 p = child.position;
            if (bottomPivot) p.y += grid.cellSize.y * 0.5f;
            Vector3Int own = grid.WorldToCell(p);
            bool hit = own.x == cell.x && own.y == cell.y;
            if (!hit)
                foreach (SpriteRenderer sr in child.GetComponentsInChildren<SpriteRenderer>())
                {
                    if (sr.sprite == null || sr.gameObject.name.Contains("Shadow")) continue;
                    Bounds b = sr.bounds;
                    if (centre.x >= b.min.x && centre.x <= b.max.x && centre.y >= b.min.y && centre.y <= b.max.y) { hit = true; break; }
                }
            if (hit) found.Add(child);
        }
        return found;
    }

    [MenuItem("Farm/Level Editors/Rotate Selected Objects Clockwise %#r", false, 40)]
    public static void RotateSelectedClockwise() { RotateSelected(-90f); }

    [MenuItem("Farm/Level Editors/Rotate Selected Objects Counter-Clockwise %&r", false, 41)]
    public static void RotateSelectedCounterClockwise() { RotateSelected(90f); }

    public static void RotateSelected(float degrees)
    {
        GameObject[] objects = Selection.gameObjects;
        if (objects == null || objects.Length == 0)
        {
            Debug.Log("[Rotate] Select one or more objects in the Scene or the Hierarchy first.");
            return;
        }
        Undo.IncrementCurrentGroup();
        Undo.SetCurrentGroupName("Rotate Objects");
        int group = Undo.GetCurrentGroup();
        foreach (GameObject go in objects)
        {
            if (go == null || EditorUtility.IsPersistent(go)) continue;      // not prefab assets in the Project window
            Rotate(go, degrees);
        }
        Undo.CollapseUndoOperations(group);
    }
}
