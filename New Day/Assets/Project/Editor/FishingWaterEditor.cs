using UnityEditor;
using UnityEditor.IMGUI.Controls;
using UnityEngine;

/// <summary>Drag the white handles in the Scene view to fit a FishingWater box over the water. Must live in an Editor folder.</summary>
[CustomEditor(typeof(FishingWater))]
public class FishingWaterEditor : Editor
{
    private readonly BoxBoundsHandle handle = new BoxBoundsHandle();

    private void OnSceneGUI()
    {
        var w = (FishingWater)target;
        handle.axes = PrimitiveBoundsHandle.Axes.X | PrimitiveBoundsHandle.Axes.Y;
        handle.center = w.transform.position;
        handle.size = new Vector3(w.boxSize.x, w.boxSize.y, 0f);
        handle.handleColor = Color.white;
        handle.wireframeColor = Color.clear;

        EditorGUI.BeginChangeCheck();
        handle.DrawHandle();
        if (EditorGUI.EndChangeCheck())
        {
            Undo.RecordObject(w, "Resize water");
            Undo.RecordObject(w.transform, "Resize water");
            w.boxSize = new Vector2(Mathf.Max(1f, Mathf.Round(handle.size.x)), Mathf.Max(1f, Mathf.Round(handle.size.y)));
            w.transform.position = new Vector3(handle.center.x, handle.center.y, w.transform.position.z);
        }
        Handles.Label(w.transform.position + new Vector3(-w.boxSize.x * 0.5f, w.boxSize.y * 0.5f + 0.6f, 0f), w.areaName + "  " + Mathf.RoundToInt(w.boxSize.x) + " x " + Mathf.RoundToInt(w.boxSize.y));
    }
}
