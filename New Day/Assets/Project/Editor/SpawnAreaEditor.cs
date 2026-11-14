using UnityEditor;
using UnityEditor.IMGUI.Controls;
using UnityEngine;

/// <summary>
/// Lets you reshape a rock or enemy zone by dragging its box handles in the Scene view (when "Limit To Box" is ticked).
/// Select the zone (click it in the Hierarchy, or click its little picture) and drag the square handles on the box.
/// Must live in an Editor folder.
/// </summary>
[CustomEditor(typeof(SpawnAreaBase), true)]
public class SpawnAreaEditor : Editor
{
    private readonly BoxBoundsHandle handle = new BoxBoundsHandle();

    private void OnSceneGUI()
    {
        var area = (SpawnAreaBase)target;
        if (!area.limitToBox) return;

        handle.axes = PrimitiveBoundsHandle.Axes.X | PrimitiveBoundsHandle.Axes.Y;
        handle.center = area.transform.position;
        handle.size = new Vector3(area.boxSize.x, area.boxSize.y, 0f);
        handle.handleColor = new Color(1f, 1f, 1f, 0.9f);
        handle.wireframeColor = Color.clear;

        EditorGUI.BeginChangeCheck();
        handle.DrawHandle();
        if (EditorGUI.EndChangeCheck())
        {
            Undo.RecordObject(area, "Resize zone");
            Undo.RecordObject(area.transform, "Resize zone");
            area.boxSize = new Vector2(Mathf.Max(2f, Mathf.Round(handle.size.x)), Mathf.Max(2f, Mathf.Round(handle.size.y)));
            area.transform.position = new Vector3(handle.center.x, handle.center.y, area.transform.position.z);
            area.RefreshArea();
        }

        Handles.Label(area.transform.position + new Vector3(-area.boxSize.x * 0.5f, area.boxSize.y * 0.5f + 0.6f, 0f),
            area.GetType().Name + "  " + Mathf.RoundToInt(area.boxSize.x) + " x " + Mathf.RoundToInt(area.boxSize.y));
    }
}
